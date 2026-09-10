using UnityEngine;
using System;
[CreateAssetMenu(fileName = "NewProxy", menuName = "Custom/Proxy Data")]
public class Proxy : ScriptableObject
{
    public event Action OnIsPlayingChanged;
    public Action OnAudioReady;
    public event Action OnPerfectCountChanged;
    public event Action OnGreatCountChanged;
    public event Action OnBadCountChanged;
    public event Action OnMissCountChanged;
    public event Action OnEnergyChanged;
    int _perfectCount = 0;
    int _greatCount = 0;
    int _badCount = 0;
    int _missCount = 0;
    int _energy = 40;

    string _defaultBgmAddress = "The Chainsmokers"; //Haruhikage

    bool _isPlaying = false;

    public bool IsPlaying
    {
        get { return _isPlaying; }
        set
        {
            _isPlaying = value;
            OnIsPlayingChanged?.Invoke();
        }
    }

    public int Energy
    {
        get { return _energy; }
        set
        {
            if (value < 0)
            {
                _energy = 0;
            }
            else if (value > 100)
            {
                _energy = 100;
            }
            else
            {
                _energy = value;
            }

            OnEnergyChanged?.Invoke();
        }
    }

    public int PerfectCount
    {
        get { return _perfectCount; }
        set
        {
            _perfectCount = value;
            Energy += 1;
            OnPerfectCountChanged?.Invoke();
        }
    }
    public int GreatCount
    {
        get { return _greatCount; }
        set
        {
            _greatCount = value;
            OnGreatCountChanged?.Invoke();
        }
    }
    public int BadCount
    {
        get { return _badCount; }
        set
        {
            Energy -= 1;
            _badCount = value;
            OnBadCountChanged?.Invoke();
        }
    }
    public int MissCount
    {
        get { return _missCount; }
        set
        {
            Energy -= 2;
            _missCount = value;
            OnMissCountChanged?.Invoke();
        }
    }

    public string DefaultBgmAddress
    {
        get { return _defaultBgmAddress; }
        set
        {
            _defaultBgmAddress = value;
        }
    }
}