using TMPro;
using UnityEngine;
using DG.Tweening;

public class TrackStateBar : MonoBehaviour
{
    [SerializeField] Proxy _proxy;
    public TextMeshProUGUI _stateText;
    private Tween _delayTween;
    void Start()
    {
        _proxy.OnPerfectCountChanged += onPerfectCountChanged;
        _proxy.OnGreatCountChanged += onGreatCountChanged;
        _proxy.OnBadCountChanged += onBadCountChanged;
        _proxy.OnMissCountChanged += onMissCountChanged;
    }

    void onPerfectCountChanged()
    {
        if (!_proxy.IsPlaying) return;
        _delayTween?.Kill();
        _stateText.text = "Perfect";
        _stateText.color = new Color(0f, 1f, 0f);
        SetTween();
    }
    void onGreatCountChanged()
    {
        if (!_proxy.IsPlaying) return;
        _delayTween?.Kill();
        _stateText.text = "Great";
        _stateText.color = new Color(1f, 1f, 0f);
        SetTween();
    }
    void onBadCountChanged()
    {
        if (!_proxy.IsPlaying) return;
        _delayTween?.Kill();
        _stateText.text = "Bad";
        _stateText.color = new Color(0f, 0f, 1f);
        SetTween();
    }
    void onMissCountChanged()
    {
        if (!_proxy.IsPlaying) return;
        _delayTween?.Kill();
        _stateText.text = "Miss";
        _stateText.color = new Color(1f, 0f, 0f);
        SetTween();
    }

    void SetTween()
    {
        _delayTween = DOVirtual.DelayedCall(1f, () =>
        {
            _stateText.text = "";
        });
    }

    void OnDestroy()
    {
        _proxy.OnPerfectCountChanged -= onPerfectCountChanged;
        _proxy.OnGreatCountChanged -= onGreatCountChanged;
        _proxy.OnBadCountChanged -= onBadCountChanged;
        _proxy.OnMissCountChanged -= onMissCountChanged;
    }
}
