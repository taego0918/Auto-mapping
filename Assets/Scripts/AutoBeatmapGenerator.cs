using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    public AudioSource audioSource;

    [Header("Generator Settings")]
    [Tooltip("分析時每區塊的 Sample 數量 (須為 2 的次方)")]
    int sampleChunkSize = 1024;
    [Tooltip("判定為音符的能量倍率門檻 (越高音符越少，越低音符越多)")]
    float thresholdMultiplier = 1.5f;//1.5f;
    [Tooltip("兩個音符之間的最小時間間隔 (秒)，防止音符疊在一起")]
    float minNoteInterval = 0.15f;
    [Tooltip("絕對音量保底門檻，低於此音量的靜音段落絕對不生成音符")]
    float minEnergyThreshold = 0.05f; // [新增] 可在 Inspector 微調，預設可給 0.01 ~ 0.05

    [Header("Game Play Settings")]
    public GameObject notePrefab;       // 音符的 Prefab
    [SerializeField] Proxy _proxy;
    Transform[] spawnPositions;  // 各軌道的生成點
    Transform[] hitPositions;    // 各軌道的判定點 (終點)
    GameObject[] lightBars;

    float notePreSpawnTime = 3f; // 音符需要提前多久生成 (讓玩家反應)

    List<NoteData> beatmap = new List<NoteData>();
    int currentNoteIndex = 0;
    double songStartTime;
    // 假設每條軌道都有一個 List 存放「畫面上已經生成、但還沒被打擊」的音符物件
    List<NoteController>[] activeNotesPerTrack;
    // 定義判定時間區間 (秒)
    float perfectWindow = 0.05f; // ±50ms
    float greatWindow = 0.1f;   // ±100ms
    float missWindow = 0.15f;    // ±150ms

    //_delay;

    public static AutoBeatmapGenerator Instance { get; private set; }

    private void Awake()
    {
        // 確保場景中只有一個 Instance
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
        // 為陣列中的每個軌道實例化 List
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

        initData();
        _proxy.OnIsPlayingChanged += OnStartButtonClicked;
        StartCoroutine(InitBeatmapRoutine());
    }

    IEnumerator InitBeatmapRoutine()
    {
        AudioClip clip = audioSource.clip;

        // 強制載入音檔數據
        clip.LoadAudioData();

        // 等待解壓完成
        while (clip.loadState == AudioDataLoadState.Loading)
        {
            yield return null;
        }
        yield return null; // 讓畫面先刷新文字

        GenerateBeatmap();
        _proxy.OnAudioReady?.Invoke();
    }

    // 玩家點擊按鈕時觸發
    void OnStartButtonClicked()
    {
        // 正式啟動遊戲音樂與計時
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
        AudioClip clip = audioSource.clip;
        int channels = clip.channels;
        float[] rawSamples = new float[clip.samples * channels];

        bool success = clip.GetData(rawSamples, 0);

        if (!success)
        {
            Debug.LogError("[AutoBeatmap] GetData 失敗！");
            return;
        }

        // ==================== [新增] 低通濾波器 (Low-pass Filter) ====================
        float sampleRate = clip.frequency;

        // 計算 RC 低通濾波器的衰減係數 (Alpha)
        float dt = 1f / sampleRate;
        // 1. 低通濾波：切掉 250 Hz 以上 (去除人聲、吉他、高音)
        float lowPassCutoff = 250f;
        float lowPassRC = 1f / (2f * Mathf.PI * lowPassCutoff);
        float alphaLow = dt / (lowPassRC + dt);

        // 2. 高通濾波：切掉 60 Hz 以下 (去除極低頻嗡嗡聲、Sub-bass 殘波)
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

            // 先過低通
            lastLowPass = lastLowPass + alphaLow * (currentSample - lastLowPass);

            // 再過高通 (拿到最終乾淨的 60Hz~250Hz 重拍頻段)
            float currentHighPass = alphaHigh * (lastHighPass + lastLowPass - lastRawSample);

            lastRawSample = lastLowPass;
            lastHighPass = currentHighPass;

            filteredSamples[i] = currentHighPass;
        }
        // ============================================================================

        int totalChunks = filteredSamples.Length / sampleChunkSize;
        float[] chunkEnergies = new float[totalChunks];

        // 接下來全部改用濾波後的數據 (filteredSamples) 來計算能量
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

            // [修改] 必須同時滿足：1. 倍率超過門檻  2. 絕對音量大於保底門檻
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
        //Debug.Log($"[AutoBeatmap] 譜面分析完成！總共分析出 {beatmap.Count} 個音符。");
        //Debug.Log($"time: {beatmap[0].time} ,index: {beatmap[0].trackIndex}");
    }

    public void OnTrackPressed(int trackIndex)
    {
        lightBars[trackIndex].GetComponent<LightBar>().shoot();
        if (!_proxy.IsPlaying) return;

        var trackNotes = activeNotesPerTrack[trackIndex];
        if (trackNotes.Count == 0) return;

        double elapsedSongTime = AudioSettings.dspTime - songStartTime;

        // 1. 尋找該軌道中距離當前時間點「最近」的音符
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

        // 若沒找到合適音符或超出判定最大上限（代表玩家亂按空揮），直接返回
        if (closestNote == null || minTimeDiff > missWindow + 1f) return;
        string txt = "";
        // 2. 進行分級判定
        if (minTimeDiff <= perfectWindow)
        {
            // AddScore(1000);
            _proxy.PerfectCount++;
            txt = "Perfect";
        }
        else if (minTimeDiff <= greatWindow)
        {
            // AddScore(700);
            _proxy.GreatCount++;
            txt = "Great";
        }
        else if (minTimeDiff <= missWindow)
        {
            // AddScore(0);
            _proxy.BadCount++;
            txt = "Bad";
        }

        if (txt != "")
        {
            RemoveNote(trackIndex, closestNote);
        }
    }

    // 放開按鍵/觸爆時呼叫
    public void OnTrackReleased(int trackIndex)
    {

        // TODO: 這裡放 Hold 音符結束放開的判定邏輯
    }

    private void RemoveNote(int trackIndex, NoteController note)
    {
        activeNotesPerTrack[trackIndex].Remove(note);
        Destroy(note.gameObject); // 或是改用 Object Pooling 回收
    }


    void StartGame()
    {
        currentNoteIndex = 0;
        songStartTime = AudioSettings.dspTime + notePreSpawnTime;
        audioSource.PlayScheduled(songStartTime);
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
            // 從列表最後一個元素倒著往前檢查
            if (noteList.Count > 0)
            {
                var item = noteList[0];
                if (elapsedSongTime > item.targetHitTime + missWindow)
                {
                    RemoveNote(i, item);
                    _proxy.MissCount++;
                }
            }
        }
    }

    void SpawnNote(NoteData data)
    {
        int track = data.trackIndex;
        Transform spawnPoint = spawnPositions[track];
        Transform hitPoint = hitPositions[track];

        GameObject noteObj = Instantiate(notePrefab, transform, false);

        if (noteObj.TryGetComponent<NoteController>(out var noteController))
        {
            RectTransform rectTransform = hitPoint.GetComponent<RectTransform>();

            float hitPosY = rectTransform.anchoredPosition.y;
            float height = spawnPoint.GetComponent<RectTransform>().rect.height;
            noteController.Initialize(
                spawnPoint.localPosition,
                new Vector3(spawnPoint.localPosition.x, spawnPoint.localPosition.y - height + hitPosY, spawnPoint.localPosition.z),
                data.time,
                notePreSpawnTime,
                songStartTime
            );
            activeNotesPerTrack[track].Add(noteController);
        }
    }
}