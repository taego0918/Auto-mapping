using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class tartPanel : MonoBehaviour
{
    public GameObject startPanel;        // 整個啟動選單面板
    public TextMeshProUGUI statusText;   // 顯示「載入中...」的文字
    public Button startButton;           // 「點擊開始」按鈕
    [SerializeField] Proxy _proxy;
    void Start()
    {
        _proxy.OnIsPlayingChanged += OnIsPlayingChanged;
        _proxy.OnAudioReady += OnAudioReady;
        startButton.onClick.AddListener(OnStartButtonClicked); // 綁定點擊事件
    }

    void OnStartButtonClicked()
    {
        _proxy.IsPlaying = true;
    }

    void OnAudioReady()
    {
        if (statusText != null) statusText.text = "";
        // 直接開始遊戲
        OnStartButtonClicked();
        //if (startButton != null) startButton.gameObject.SetActive(true); // 顯示「點擊開始遊戲」按鈕
    }

    void OnIsPlayingChanged()
    {
        if (_proxy.IsPlaying)
        {
            // 隱藏整個 Panel
            if (startPanel != null) startPanel.SetActive(false);
        }
        else
        {
            if (startButton != null) startButton.gameObject.SetActive(false); // 先隱藏按鈕
            if (statusText != null) statusText.text = "Audio file loading...";
        }
    }
    void OnDestroy()
    {
        _proxy.OnIsPlayingChanged -= OnIsPlayingChanged;
        startButton.onClick.RemoveListener(OnStartButtonClicked);
    }
}
