using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

[System.Serializable]
public class NoteData
{
    public float time;       // 音符應該被擊中的時間點 (秒)
    public int trackIndex;   // 軌道編號 (0, 1, 2...)
}

public class AutoBeatmapGenerator : MonoBehaviour
{
    [Header("UI Reference")]
    public GameObject spawnPositionsList;
    [Header("Audio Settings")]
    public AudioSource bgmSource;
    public AudioSource seSource;
    [Header("Generator Settings")]
    [Tooltip("分析時每區塊的 Sample 數量 (須為 2 的次方)")]
    int sampleChunkSize = 1024;
    [Tooltip("判定為音符的能量倍率門檻 (越高音符越少，越低音符越多)")]
    float thresholdMultiplier = 1.5f;
    [Tooltip("兩個音符之間的最小時間間隔 (秒)，防止音符疊在一起")]
    float minNoteInterval = 0.15f;
    [Tooltip("絕對音量保底門檻，低於此音量的靜音段落絕對不生成音符")]
    float minEnergyThreshold = 0.05f;

    [Header("Game Play Settings")]
    public GameObject notePrefab;       // 音符的 Prefab

    public AudioClip clickSE;
    public AudioClip missSE;
    [SerializeField] Proxy _proxy;
    Transform[] spawnPositions;  // 各軌道的生成點
    Transform[] hitPositions;    // 各軌道的判定點 (終點)
    GameObject[] lightBars;

    float notePreSpawnTime = 3f; // 音符需要提前多久生成 (讓玩家反應)

    List<NoteData> beatmap = new List<NoteData>();
    int currentNoteIndex = 0;
    double songStartTime;

    // 存放「畫面上已經生成、但還沒被打擊」的音符物件
    List<NoteController>[] activeNotesPerTrack;

    // ==================== 物件池相關變數 ====================
    [Header("Object Pool Settings")]
    [Tooltip("預先生成的音符池初始數量")]
    [SerializeField] private int initialPoolSize = 20;
    private Queue<NoteController> notePool = new Queue<NoteController>();

    // ==================== Addressables 資源管理 Handle ====================
    private AsyncOperationHandle<AudioClip> currentBgmHandle;

    // 定義判定時間區間 (秒)
    float perfectWindow = 0.05f; // ±50ms
    float greatWindow = 0.1f;   // ±100ms
    float missWindow = 0.15f;    // ±150ms

    public static AutoBeatmapGenerator Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        int childCount = spawnPositionsList.transform.childCount;
        activeNotesPerTrack = new List<NoteController>[childCount];
        spawnPositions = new Transform[childCount];
        hitPositions = new Transform[childCount];
        lightBars = new GameObject[childCount];

        for (int i = 0; i < childCount; i++)
        {
            activeNotesPerTrack[i] = new List<NoteController>();
            spawnPositions[i] = spawnPositionsList.transform.GetChild(i);
            hitPositions[i] = spawnPositions[i].Find("hitPositions");
            lightBars[i] = spawnPositions[i].Find("LightBar").gameObject;
        }

        // 初始化物件池
        InitializePool();

        initData();
        _proxy.OnIsPlayingChanged += OnStartButtonClicked;

        // 如果 Inspector 填有預設 BGM 名稱，啟動時自動下載載入
        if (!string.IsNullOrEmpty(_proxy.DefaultBgmAddress))
        {
            LoadBGMAndInit(_proxy.DefaultBgmAddress);
        }
    }

    public void LoadBGMAndInit(string bgmAddress)
    {
        // 載入新音檔前先釋放舊音檔
        UnloadCurrentBGM();

        Debug.Log($"[AutoBeatmap] 開始從 Addressables 載入 BGM: {bgmAddress}");

        currentBgmHandle = Addressables.LoadAssetAsync<AudioClip>(bgmAddress);
        currentBgmHandle.Completed += handle =>
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"[AutoBeatmap] BGM 載入成功: {bgmAddress}");
                bgmSource.clip = handle.Result;
                StartCoroutine(InitBeatmapRoutine());
            }
            else
            {
                Debug.LogError($"[AutoBeatmap] BGM 載入失敗: {bgmAddress}");
            }
        };
    }

    /// <summary>
    /// 卸載當前載入的 BGM 資源以釋放記憶體
    /// </summary>
    public void UnloadCurrentBGM()
    {
        if (currentBgmHandle.IsValid())
        {
            if (bgmSource != null)
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }
            Addressables.Release(currentBgmHandle);
            Debug.Log("[AutoBeatmap] 已卸載舊的 BGM 資源");
        }
    }

    public void PalySE(AudioClip audioClip)
    {
        seSource.PlayOneShot(audioClip);
    }

    private void OnDestroy()
    {
        UnloadCurrentBGM();
    }

    // ==================== 物件池邏輯 ====================
    private void InitializePool()
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            NoteController note = CreateNewNoteInstance();
            note.gameObject.SetActive(false);
            notePool.Enqueue(note);
        }
    }

    private NoteController CreateNewNoteInstance()
    {
        GameObject noteObj = Instantiate(notePrefab, transform, false);
        if (noteObj.TryGetComponent<NoteController>(out var noteController))
        {
            return noteController;
        }
        Debug.LogError("notePrefab 缺少 NoteController 組件！");
        return null;
    }

    private NoteController GetNoteFromPool()
    {
        if (notePool.Count > 0)
        {
            NoteController note = notePool.Dequeue();
            note.gameObject.SetActive(true);
            return note;
        }
        else
        {
            return CreateNewNoteInstance();
        }
    }

    private void ReturnNoteToPool(NoteController note)
    {
        note.gameObject.SetActive(false);
        notePool.Enqueue(note);
    }

    IEnumerator InitBeatmapRoutine()
    {
        if (bgmSource.clip == null)
        {
            Debug.LogError("[AutoBeatmap] 無法生成譜面，AudioSource 的 clip 為空！");
            yield break;
        }

        AudioClip clip = bgmSource.clip;
        clip.LoadAudioData();

        while (clip.loadState == AudioDataLoadState.Loading)
        {
            yield return null;
        }
        yield return null;

        GenerateBeatmap();
        _proxy.OnAudioReady?.Invoke();
    }

    void OnStartButtonClicked()
    {
        StartGame();
    }

    void initData()
    {
        _proxy.IsPlaying = false;
        _proxy.PerfectCount = 0;
        _proxy.GreatCount = 0;
        _proxy.BadCount = 0;
        _proxy.MissCount = 0;
        _proxy.Energy = 40;
    }

    void GenerateBeatmap()
    {
        beatmap.Clear();
        AudioClip clip = bgmSource.clip;
        int channels = clip.channels;
        float[] rawSamples = new float[clip.samples * channels];

        bool success = clip.GetData(rawSamples, 0);
        if (!success)
        {
            Debug.LogError("[AutoBeatmap] GetData 失敗！");
            return;
        }

        float sampleRate = clip.frequency;
        float dt = 1f / sampleRate;

        float lowPassCutoff = 250f;
        float lowPassRC = 1f / (2f * Mathf.PI * lowPassCutoff);
        float alphaLow = dt / (lowPassRC + dt);

        float highPassCutoff = 60f;
        float highPassRC = 1f / (2f * Mathf.PI * highPassCutoff);
        float alphaHigh = highPassRC / (highPassRC + dt);

        float[] filteredSamples = new float[rawSamples.Length];
        float lastLowPass = 0f;
        float lastRawSample = 0f;
        float lastHighPass = 0f;

        for (int i = 0; i < rawSamples.Length; i++)
        {
            float currentSample = rawSamples[i];
            lastLowPass = lastLowPass + alphaLow * (currentSample - lastLowPass);
            float currentHighPass = alphaHigh * (lastHighPass + lastLowPass - lastRawSample);

            lastRawSample = lastLowPass;
            lastHighPass = currentHighPass;

            filteredSamples[i] = currentHighPass;
        }

        int totalChunks = filteredSamples.Length / sampleChunkSize;
        float[] chunkEnergies = new float[totalChunks];

        for (int i = 0; i < totalChunks; i++)
        {
            float sum = 0;
            for (int j = 0; j < sampleChunkSize; j++)
            {
                float sample = filteredSamples[i * sampleChunkSize + j];
                sum += sample * sample;
            }
            chunkEnergies[i] = Mathf.Sqrt(sum / sampleChunkSize);
        }

        int historyWindow = 10;
        float lastNoteTime = -minNoteInterval;

        for (int i = historyWindow; i < totalChunks - historyWindow; i++)
        {
            float localAverageEnergy = 0;
            for (int j = i - historyWindow; j <= i + historyWindow; j++)
            {
                localAverageEnergy += chunkEnergies[j];
            }
            localAverageEnergy /= (historyWindow * 2 + 1);

            if (chunkEnergies[i] > localAverageEnergy * thresholdMultiplier &&
                chunkEnergies[i] > minEnergyThreshold)
            {
                float currentTime = (float)i * sampleChunkSize / (clip.frequency * channels);

                if (currentTime - lastNoteTime >= minNoteInterval)
                {
                    beatmap.Add(new NoteData
                    {
                        time = currentTime,
                        trackIndex = UnityEngine.Random.Range(0, spawnPositions.Length)
                    });
                    lastNoteTime = currentTime;
                }
            }
        }
    }

    public void OnTrackPressed(int trackIndex)
    {
        lightBars[trackIndex].GetComponent<LightBar>().shoot();
        if (!_proxy.IsPlaying) return;
        PalySE(clickSE);
        var trackNotes = activeNotesPerTrack[trackIndex];
        if (trackNotes.Count == 0) return;

        double elapsedSongTime = AudioSettings.dspTime - songStartTime;

        NoteController closestNote = null;
        double minTimeDiff = double.MaxValue;

        for (int i = 0; i < trackNotes.Count; i++)
        {
            double diff = System.Math.Abs(elapsedSongTime - trackNotes[i].targetHitTime);

            if (diff < minTimeDiff)
            {
                minTimeDiff = diff;
                closestNote = trackNotes[i];
            }
            else
            {
                break;
            }
        }

        if (closestNote == null || minTimeDiff > missWindow + 1f) return;
        string txt = "";

        if (minTimeDiff <= perfectWindow)
        {
            _proxy.PerfectCount++;
            txt = "Perfect";
        }
        else if (minTimeDiff <= greatWindow)
        {
            _proxy.GreatCount++;
            txt = "Great";
        }
        else if (minTimeDiff <= missWindow)
        {
            _proxy.BadCount++;
            txt = "Bad";
        }

        if (txt != "")
        {
            RemoveNote(trackIndex, closestNote);
        }
    }

    public void OnTrackReleased(int trackIndex)
    {
        // HOLD 音符邏輯預留
    }

    private void RemoveNote(int trackIndex, NoteController note)
    {
        activeNotesPerTrack[trackIndex].Remove(note);
        ReturnNoteToPool(note);
    }

    void StartGame()
    {
        currentNoteIndex = 0;
        songStartTime = AudioSettings.dspTime + notePreSpawnTime;
        bgmSource.PlayScheduled(songStartTime);
    }

    void Update()
    {
        if (!_proxy.IsPlaying) return;

        double elapsedSongTime = AudioSettings.dspTime - songStartTime;

        while (currentNoteIndex < beatmap.Count &&
               beatmap[currentNoteIndex].time - elapsedSongTime <= notePreSpawnTime)
        {
            SpawnNote(beatmap[currentNoteIndex]);
            currentNoteIndex++;
        }

        for (int i = 0; i < activeNotesPerTrack.Length; i++)
        {
            var noteList = activeNotesPerTrack[i];
            if (noteList.Count > 0)
            {
                var item = noteList[0];
                if (elapsedSongTime > item.targetHitTime + missWindow)
                {
                    RemoveNote(i, item);
                    _proxy.MissCount++;
                    PalySE(missSE);
                }
            }
        }
    }

    void SpawnNote(NoteData data)
    {
        int track = data.trackIndex;
        Transform spawnPoint = spawnPositions[track];
        Transform hitPoint = hitPositions[track];

        NoteController noteController = GetNoteFromPool();

        if (noteController != null)
        {
            RectTransform rectTransform = hitPoint.GetComponent<RectTransform>();

            float hitPosY = rectTransform.anchoredPosition.y;
            float height = spawnPoint.GetComponent<RectTransform>().rect.height;
            noteController.Initialize(
                spawnPoint.localPosition,
                new Vector3(spawnPoint.localPosition.x, spawnPoint.localPosition.y - height + hitPosY, spawnPoint.localPosition.z),
                data.time,
                notePreSpawnTime,
                songStartTime,
                track
            );
            activeNotesPerTrack[track].Add(noteController);
        }
    }
}