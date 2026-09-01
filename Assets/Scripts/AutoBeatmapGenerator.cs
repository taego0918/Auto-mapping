using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[System.Serializable]
public class NoteData
{
    public float time;       // 音符應該被擊中的時間點 (秒)
    public int trackIndex;   // 軌道編號 (0, 1, 2...)
}

public class AutoBeatmapGenerator : MonoBehaviour
{
    [Header("UI Reference")]
    public GameObject startPanel;        // 整個啟動選單面板
    public TextMeshProUGUI statusText;   // 顯示「載入中...」的文字
    public TextMeshProUGUI trackStateText;
    public Button startButton;           // 「點擊開始」按鈕
    [Header("Audio Settings")]
    public AudioSource audioSource;

    [Header("Generator Settings")]
    [Tooltip("分析時每區塊的 Sample 數量 (須為 2 的次方)")]
    public int sampleChunkSize = 1024;
    [Tooltip("判定為音符的能量倍率門檻 (越高音符越少，越低音符越多)")]
    public float thresholdMultiplier = 5f;//1.5f;
    [Tooltip("兩個音符之間的最小時間間隔 (秒)，防止音符疊在一起")]
    public float minNoteInterval = 0.15f;
    [Tooltip("絕對音量保底門檻，低於此音量的靜音段落絕對不生成音符")]
    public float minEnergyThreshold = 0.05f; // [新增] 可在 Inspector 微調，預設可給 0.01 ~ 0.05

    [Header("分數")]

    public TextMeshProUGUI perfectCountText;
    public TextMeshProUGUI greatCountText;
    public TextMeshProUGUI badCountText;
    public TextMeshProUGUI missCountText;

    [Header("Game Play Settings")]
    public GameObject notePrefab;       // 音符的 Prefab
    public Transform[] spawnPositions;  // 各軌道的生成點
    public Transform[] hitPositions;    // 各軌道的判定點 (終點)
    float notePreSpawnTime = 5f; // 音符需要提前多久生成 (讓玩家反應)

    List<NoteData> beatmap = new List<NoteData>();
    int currentNoteIndex = 0;
    double songStartTime;
    bool isPlaying = false;
    // 假設每條軌道都有一個 List 存放「畫面上已經生成、但還沒被打擊」的音符物件
    List<NoteController>[] activeNotesPerTrack = new List<NoteController>[4];
    private Tween _delayTween;
    // 定義判定時間區間 (秒)
    float perfectWindow = 0.05f; // ±50ms
    float greatWindow = 0.1f;   // ±100ms
    float missWindow = 0.15f;    // ±150ms

    int perfectCount = 0;
    int greatCount = 0;
    int badCount = 0;
    int missCount = 0;

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

        // 為陣列中的每個軌道實例化 List
        for (int i = 0; i < activeNotesPerTrack.Length; i++)
        {
            activeNotesPerTrack[i] = new List<NoteController>();
        }
    }

    void Start()
    {
        // 1. 初始化 UI 狀態
        if (startButton != null)
        {
            startButton.gameObject.SetActive(false); // 先隱藏按鈕
            startButton.onClick.AddListener(OnStartButtonClicked); // 綁定點擊事件
        }

        if (statusText != null)
        {
            statusText.text = "音檔讀取中...";
        }

        // 2. 開始非同步載入音檔與分析
        StartCoroutine(InitBeatmapRoutine());
    }

    IEnumerator InitBeatmapRoutine()
    {
        AudioClip clip = audioSource.clip;

        if (clip == null)
        {
            if (statusText != null) statusText.text = "Error!!";
            yield break;
        }

        // 強制載入音檔數據
        clip.LoadAudioData();

        // 等待解壓完成
        while (clip.loadState == AudioDataLoadState.Loading)
        {
            yield return null;
        }

        if (clip.loadState != AudioDataLoadState.Loaded)
        {
            if (statusText != null) statusText.text = "Error!";
            yield break;
        }

        // 音檔 Ready，開始生成譜面
        if (statusText != null) statusText.text = "Loading...";
        yield return null; // 讓畫面先刷新文字

        GenerateBeatmap();

        // 3. 譜面分析完成，顯示開始按鈕！
        if (statusText != null)
        {
            statusText.text = "";
        }

        if (startButton != null)
        {
            startButton.gameObject.SetActive(true); // 顯示「點擊開始遊戲」按鈕
        }
    }

    // 玩家點擊按鈕時觸發
    void OnStartButtonClicked()
    {
        // 隱藏整個 Panel
        if (startPanel != null)
        {
            startPanel.SetActive(false);
        }

        // 正式啟動遊戲音樂與計時
        StartGame();
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
        //Debug.Log(activeNotesPerTrack[trackIndex]);
        if (!isPlaying) return;
        // 1. 如果該軌道畫面上完全沒有音符，代表亂按（空揮），直接跳過或算 Late/Miss
        if (activeNotesPerTrack[trackIndex].Count == 0) return;
        // 2. 取出該軌道最前端（最早生成）的音符
        NoteController targetNote = activeNotesPerTrack[trackIndex][0];
        double elapsedSongTime = AudioSettings.dspTime - songStartTime;
        // 3. 計算按下時間與音符目標時間的絕對時間差
        double currentDspTime = AudioSettings.dspTime;
        double timeDiff = System.Math.Abs(elapsedSongTime - targetNote.targetHitTime);

        // 4. 判定加減分
        if (timeDiff <= perfectWindow)
        {
            //AddScore(1000); // Perfect!
            RemoveNote(trackIndex, targetNote);
            SetTrackStateText("Perfect", new Color(0f, 1f, 0f), trackIndex);
        }
        else if (timeDiff <= greatWindow)
        {
            //AddScore(700);  // Great!
            RemoveNote(trackIndex, targetNote);
            SetTrackStateText("Great", new Color(1f, 1f, 0f), trackIndex);
        }
        else if (timeDiff <= missWindow)
        {
            //AddScore(0);    // Bad / Miss
            RemoveNote(trackIndex, targetNote);
            SetTrackStateText("Bad", new Color(1f, 0f, 0f), trackIndex);
        }
        // 如果 timeDiff > missWindow，代表按太早了，可以選擇忽略不處理
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
        isPlaying = true;
    }

    void Update()
    {
        if (!isPlaying) return;

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
                    SetTrackStateText("Miss", new Color(1f, 0f, 0f), i);
                }
            }
        }
    }

    void SetTrackStateText(string text, Color color, int index)
    {
        _delayTween?.Kill();
        trackStateText.text = $"{text} {index}";
        trackStateText.color = color;
        _delayTween = DOVirtual.DelayedCall(1f, () =>
        {
            trackStateText.text = "";
        });
        switch (text)
        {
            case "Perfect":
                perfectCount++;
                perfectCountText.text = perfectCount.ToString();
                break;
            case "Great":
                greatCount++;
                greatCountText.text = greatCount.ToString();
                break;
            case "Bad":
                badCount++;
                badCountText.text = badCount.ToString();
                break;
            case "Miss":
                missCount++;
                missCountText.text = missCount.ToString();
                break;
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
            float height = spawnPoint.GetComponent<RectTransform>().rect.height;
            noteController.Initialize(
                spawnPoint.localPosition,
                new Vector3(spawnPoint.localPosition.x, spawnPoint.localPosition.y - height, spawnPoint.localPosition.z),
                data.time,
                notePreSpawnTime,
                songStartTime
            );
            activeNotesPerTrack[track].Add(noteController);
        }
    }
}