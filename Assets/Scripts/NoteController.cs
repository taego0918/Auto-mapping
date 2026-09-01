using UnityEngine;

public class NoteController : MonoBehaviour
{
    private Vector3 startPos;
    private Vector3 endPos;
    public float targetHitTime;
    private float notePreSpawnTime;
    public double songStartTime;

    private RectTransform rectTransform;
    private bool isInitialized = false;

    /// <summary>
    /// 初始化音符 (純 UI 座標版)
    /// </summary>
    public void Initialize(Vector3 localStart, Vector3 localEnd, float targetHitTime, float preSpawnTime, double songStartTime)
    {
        this.targetHitTime = targetHitTime;
        this.notePreSpawnTime = preSpawnTime;
        this.songStartTime = songStartTime;

        if (!TryGetComponent<RectTransform>(out rectTransform))
        {
            Debug.LogError("Note Prefab 缺少 RectTransform！");
            isInitialized = false;
            return;
        }

        // 1. 強制讓 Z 軸為 0，避免被 Camera 剪裁掉
        this.startPos = new Vector3(localStart.x, localStart.y, 0f);
        this.endPos = new Vector3(localEnd.x, localEnd.y, 0f);

        rectTransform.localPosition = startPos;

        // 2. 關鍵修正：Web 平台常出現 Scale 變成 0 的問題，強制重置為 1
        rectTransform.localScale = Vector3.one;
        isInitialized = true;
    }

    void Update()
    {
        if (!isInitialized) return;

        double currentSongTime = AudioSettings.dspTime - songStartTime;

        float spawnTime = targetHitTime - notePreSpawnTime;
        float progress = (float)((currentSongTime - spawnTime) / notePreSpawnTime);

        // 使用純 UI 的本地座標插值移動
        rectTransform.localPosition = Vector3.Lerp(startPos, endPos, progress);
    }
}