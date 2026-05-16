using UnityEngine;
using UnityEngine.UI;

// Manages Command Points for one team.
// Place two instances in MainScene — one per team, each wired to its own
// CommandPointsSO, event channels, and HUD elements.
//
// CP accumulator works like Clash Royale: a float fills 0→1, then 1 integer CP
// is awarded and the accumulator wraps. The progress bar shows the fractional fill.
public class CommandPointsManager : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] CommandPointsSO   _config;
    [SerializeField] int               _team;  // 0 = P1, 1 = P2

    [Header("Event Channels")]
    [SerializeField] KillEventSO       _killEvent;
    [SerializeField] SurgePhaseEventSO _surgeEvent;
    [SerializeField] CpChangedEventSO  _cpChangedEvent;

    [Header("UI — wire in Inspector")]
    [SerializeField] Slider            _cpProgressBar; // value 0..1 = fractional fill
    [SerializeField] Text              _cpCountText;   // shows integer CP

    float _accumulator;
    int   _currentCp;
    bool  _inSurge;

    public int  CurrentCp => _currentCp;
    public bool IsFull    => _config != null && _currentCp >= _config.maxCp;

    // Called by CardDragDeploy / EnemyAISummoner before deploying a unit.
    public bool TrySpend(int cost)
    {
        if (cost <= 0 || _currentCp < cost) return false;
        _currentCp -= cost;
        FireCpChanged();
        return true;
    }

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (_killEvent  != null) _killEvent.OnRaised  += OnKill;
        if (_surgeEvent != null) _surgeEvent.OnRaised += OnSurge;
    }

    void OnDisable()
    {
        if (_killEvent  != null) _killEvent.OnRaised  -= OnKill;
        if (_surgeEvent != null) _surgeEvent.OnRaised -= OnSurge;
    }

    void Start()
    {
        if (_config == null) { Debug.LogError("[CommandPointsManager] CommandPointsSO not assigned.", this); return; }
        _currentCp   = Mathf.Clamp(_config.startingCp, 0, _config.maxCp);
        _accumulator = 0f;
        _inSurge     = false;
        FireCpChanged();
    }

    // ── Regen tick ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (_config == null || IsFull) return;

        float rate = _config.regenRate * (_inSurge ? _config.surgeMultiplier : 1f);
        _accumulator += rate * Time.deltaTime;

        bool gained = false;
        while (_accumulator >= 1f && !IsFull)
        {
            _accumulator -= 1f;
            _currentCp    = Mathf.Min(_currentCp + 1, _config.maxCp);
            gained        = true;
        }
        if (IsFull) _accumulator = 0f;

        if (gained) FireCpChanged();
        else        UpdateUI();   // update progress bar each frame without raising event
    }

    // ── Event handlers ─────────────────────────────────────────────────────────

    void OnKill(KillInfo info)
    {
        if (info.killerTeam != _team || IsFull || _config == null) return;

        _accumulator += _config.killBonusProgress;
        while (_accumulator >= 1f && !IsFull)
        {
            _accumulator -= 1f;
            _currentCp    = Mathf.Min(_currentCp + 1, _config.maxCp);
        }
        if (IsFull) _accumulator = 0f;
        FireCpChanged();
    }

    void OnSurge()
    {
        _inSurge = true;
        Debug.Log($"[CP P{_team + 1}] Surge Phase — regen rate doubled.");
    }

    // ── UI helpers ─────────────────────────────────────────────────────────────

    void FireCpChanged()
    {
        UpdateUI();
        _cpChangedEvent?.Raise(new CpChangedInfo
        {
            team      = _team,
            currentCp = _currentCp,
            progress  = _accumulator,
        });
    }

    void UpdateUI()
    {
        if (_cpProgressBar != null) _cpProgressBar.value = _accumulator;
        if (_cpCountText   != null) _cpCountText.text    = _currentCp.ToString();
    }
}
