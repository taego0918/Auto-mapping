using UnityEngine;
using UnityEngine.UI;

public class EnergyBar : MonoBehaviour
{
    [SerializeField] Proxy _proxy;
    public Scrollbar _energyBar;
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
        _energyBar.size = _proxy.Energy / 100f;
    }

    void OnDestroy()
    {
        _proxy.OnEnergyChanged -= OnEnergyChanged;
        _proxy.OnIsPlayingChanged -= OnIsPlayingChanged;
    }
}
