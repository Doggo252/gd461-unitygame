using UnityEngine;
using UnityEngine.UI;

// Counts down the match timer, fires SurgePhaseEvent when 60 s remain,
// and updates the HUD timer Text every frame.
public class MatchTimerManager : MonoBehaviour
{
    [SerializeField] MatchTimerSO      _config;
    [SerializeField] SurgePhaseEventSO _surgeEvent;
    [SerializeField] Text              _timerText;

    float _remaining;
    bool  _surgeFired;

    public float Remaining => _remaining;
    public bool  InSurge   => _config != null && _remaining <= _config.surgeThreshold;

    void Start()
    {
        if (_config == null) { Debug.LogError("[MatchTimerManager] MatchTimerSO not assigned.", this); return; }
        _remaining  = _config.matchDuration;
        _surgeFired = false;
    }

    void Update()
    {
        if (_config == null || _remaining <= 0f) return;

        _remaining -= Time.deltaTime;

        if (!_surgeFired && InSurge)
        {
            _surgeFired = true;
            _surgeEvent?.Raise();
            Debug.Log("[Timer] Surge Phase activated.");
        }

        _remaining = Mathf.Max(0f, _remaining);
        UpdateTimerText();
    }

    void UpdateTimerText()
    {
        if (_timerText == null) return;
        int m = Mathf.FloorToInt(_remaining / 60f);
        int s = Mathf.FloorToInt(_remaining % 60f);
        _timerText.text  = $"{m}:{s:D2}";
        _timerText.color = InSurge ? new Color(1f, 0.3f, 0.3f) : Color.white;
    }
}
