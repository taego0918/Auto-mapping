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
    public GameObject startPanel;        // 整個啟動選單面板
    public TextMeshProUGUI statusText;   // 顯示「載入中...」的文字
    public Button startButton;           // 「點擊開始」按鈕
    [Header("Audio Settings")]
    public AudioSource audioSource;

    [Header("Generator Settings")]
    [Tooltip("分析時每區塊的 Sample 數量 (須為 2 的次方)")]
    public int sampleChunkSize = 1024;
    [Tooltip("判定為音符的能量倍率門檻 (越高音符越少，越低音符越多)")]
    public float thresholdMultiplier = 1.5f;
    [Tooltip("兩個音符之間的最小時間間隔 (秒)，防止音符疊在一起")]
    public float minNoteInterval = 0.15f;

    [Header("Game Play Settings")]
    public GameObject notePrefab;       // 音符的 Prefab
    public Transform[] spawnPositions;  // 各軌道的生成點
    public Transform[] hitPositions;    // 各軌道的判定點 (終點)
    public float noteSpeed = 10f;       // 音符移動速度
    public float notePreSpawnTime = 2f; // 音符需要提前多久生成 (讓玩家反應)

    private List<NoteData> beatmap = new List<NoteData>();
    private int currentNoteIndex = 0;
    private double songStartTime;
    private bool isPlaying = false;

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

        int totalChunks = rawSamples.Length / sampleChunkSize;
        float[] chunkEnergies = new float[totalChunks];

        for (int i = 0; i < totalChunks; i++)
        {
            float sum = 0;
            for (int j = 0; j < sampleChunkSize; j++)
            {
                float sample = rawSamples[i * sampleChunkSize + j];
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

            if (chunkEnergies[i] > localAverageEnergy * thresholdMultiplier)
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
        Debug.Log($"[AutoBeatmap] 譜面分析完成！總共分析出 {beatmap.Count} 個音符。");
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
    }

    void SpawnNote(NoteData data)
    {
        int track = data.trackIndex;
        Transform spawnPoint = spawnPositions[track];
        Transform hitPoint = hitPositions[track];

        GameObject noteObj = Instantiate(notePrefab, transform, false);

        if (noteObj.TryGetComponent<NoteController>(out var noteController))
        {
            noteController.Initialize(
                spawnPoint.localPosition,
                new Vector3(spawnPoint.localPosition.x, hitPoint.localPosition.y, spawnPoint.localPosition.z),
                data.time,
                notePreSpawnTime,
                songStartTime
            );
        }
    }
}