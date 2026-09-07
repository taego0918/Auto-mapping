using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using DG.Tweening;

public class TrackButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public int trackIndex;

    public Image _img;
    [SerializeField] private Image _flashImg; // 疊在原本 Img 上方的白光圖片

    [Header("鍵盤按鍵設定")]
    public InputAction keyAction; // 可以在 Inspector 直接指定按鍵 (例如 <Keyboard>/a)

    private void OnEnable()
    {
        if (keyAction != null)
        {
            // 按下指定鍵時觸發
            keyAction.started += OnKeyPressed;
            keyAction.canceled += OnKeyReleased;
            keyAction.Enable();
        }
    }

    // --- 鍵盤觸發 ---
    private void OnKeyPressed(InputAction.CallbackContext context)
    {
        TriggerPress();
        if (_img != null)
        {
            _img.transform.DOKill();
            _img.transform.localScale = Vector3.one; // 歸位
            _img.transform.DOPunchScale(new Vector3(0.2f, 0.2f, 0), 0.15f, vibrato: 1, elasticity: 0.5f);
        }
        TriggerFlash();
    }

    private void OnKeyReleased(InputAction.CallbackContext context)
    {
        TriggerRelease();
    }

    public void TriggerFlash()
    {
        if (_flashImg != null)
        {
            _flashImg.DOKill();
            _flashImg.color = new Color(1f, 1f, 1f, 0.8f); // 瞬間變亮 (Alpha = 0.8)
            _flashImg.DOFade(0f, 0.2f);                    // 0.2 秒內淡出
        }
    }

    // --- UI 觸控/滑鼠觸發 ---
    public void OnPointerDown(PointerEventData eventData) => TriggerPress();
    public void OnPointerUp(PointerEventData eventData) => TriggerRelease();

    // 統一呼叫點
    private void TriggerPress()
    {
        if (AutoBeatmapGenerator.Instance != null)
        {
            AutoBeatmapGenerator.Instance.OnTrackPressed(trackIndex);
        }
    }

    private void TriggerRelease()
    {
        if (AutoBeatmapGenerator.Instance != null)
        {
            AutoBeatmapGenerator.Instance.OnTrackReleased(trackIndex);
        }
    }
    private void OnDisable()
    {
        if (keyAction != null)
        {
            keyAction.started -= OnKeyPressed;
            keyAction.canceled -= OnKeyReleased;
            keyAction.Disable();
        }
    }
}