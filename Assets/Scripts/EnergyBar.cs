using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class EnergyBar : MonoBehaviour
{
    [SerializeField] Proxy _proxy;
    public Scrollbar _energyBar;
    Tween _delayTween;

    void Start()
    {
        _proxy.OnEnergyChanged += OnEnergyChanged;
        _proxy.OnIsPlayingChanged += OnIsPlayingChanged;
    }

    void OnIsPlayingChanged()
    {
        if (_proxy.IsPlaying)
        {
            gameObject.SetActive(true);
            OnEnergyChanged();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void OnEnergyChanged()
    {
        if (!_proxy.IsPlaying) return;
        _delayTween?.Kill();
        float targetSize = _proxy.Energy / 100f;

        _delayTween = DOVirtual.Float(_energyBar.size, targetSize, 0.5f, value =>
        {
            _energyBar.size = value;
        })
        .SetEase(Ease.OutCubic);
    }

    void OnDestroy()
    {
        _proxy.OnEnergyChanged -= OnEnergyChanged;
        _proxy.OnIsPlayingChanged -= OnIsPlayingChanged;
    }
}
