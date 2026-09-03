using TMPro;
using UnityEngine;

public class Scoreboard : MonoBehaviour
{
    [Header("分數")]

    public TextMeshProUGUI perfectCountText;
    public TextMeshProUGUI greatCountText;
    public TextMeshProUGUI badCountText;
    public TextMeshProUGUI missCountText;
    [SerializeField] Proxy _proxy;

    void Start()
    {
        _proxy.OnPerfectCountChanged += onPerfectCountChanged;
        _proxy.OnGreatCountChanged += onGreatCountChanged;
        _proxy.OnBadCountChanged += onBadCountChanged;
        _proxy.OnMissCountChanged += onMissCountChanged;
        _proxy.OnIsPlayingChanged += OnIsPlayingChanged;
    }

    void OnIsPlayingChanged()
    {
        if (_proxy.IsPlaying)
        {
            gameObject.SetActive(true);
            onPerfectCountChanged();
            onGreatCountChanged();
            onBadCountChanged();
            onMissCountChanged();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    void onPerfectCountChanged()
    {
        perfectCountText.text = _proxy.PerfectCount.ToString();
    }
    void onGreatCountChanged()
    {
        greatCountText.text = _proxy.GreatCount.ToString();
    }
    void onBadCountChanged()
    {
        badCountText.text = _proxy.BadCount.ToString();
    }
    void onMissCountChanged()
    {
        missCountText.text = _proxy.MissCount.ToString();
    }

    void OnDestroy()
    {
        _proxy.OnPerfectCountChanged -= onPerfectCountChanged;
        _proxy.OnGreatCountChanged -= onGreatCountChanged;
        _proxy.OnBadCountChanged -= onBadCountChanged;
        _proxy.OnMissCountChanged -= onMissCountChanged;
        _proxy.OnIsPlayingChanged -= OnIsPlayingChanged;
    }
}
