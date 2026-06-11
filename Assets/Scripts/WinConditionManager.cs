using UnityEngine;

// Watches objectives + match timer to determine the match winner.
// HQ destroyed             → instant win for opposing team
// Timer expires            → compare destroyed-FOB count
// Tied at timer expiry     → enter Sudden Death (no time cap, first tower kill wins)
//
// Raises MatchEndEventSO once when the match has truly ended.
[RequireComponent(typeof(MatchTimerManager))]
public class WinConditionManager : MonoBehaviour
{
    [SerializeField] UnitRegistrySO     _registry;
    [SerializeField] MatchEndEventSO    _matchEndEvent;
    [SerializeField] MatchTimerManager  _timer;
    [SerializeField] KillEventSO        _killEvent;   // for the casualty tiebreaker

    // Units destroyed, indexed by the destroyed unit's team. Enemy casualties
    // inflicted by team T = _casualtiesByTeam[1 - T].
    readonly int[] _casualtiesByTeam = new int[2];

    [Header("UI — wired in scene")]
    [SerializeField] GameObject _suddenDeathBanner; // small overlay shown during overtime

    bool _matchEnded;
    bool _inSuddenDeath;

    public bool InSuddenDeath => _inSuddenDeath;

    void Awake()
    {
        if (_timer == null) _timer = GetComponent<MatchTimerManager>();
        if (_suddenDeathBanner != null) _suddenDeathBanner.SetActive(false);
    }

    void OnEnable()  { }   // too early — objectives haven't registered yet
    void OnDisable() { UnsubscribeAll(); }

    // Start() runs after all OnEnable() calls in the scene, so objectives
    // are guaranteed to be registered in _registry.Objectives by this point.
    void Start()
    {
        SubscribeAll();
        // Immediately check in case an objective was already dead at start
        EvaluateOnObjectiveLoss();
    }

    void SubscribeAll()
    {
        if (_killEvent != null) { _killEvent.OnRaised -= OnUnitKilled; _killEvent.OnRaised += OnUnitKilled; }
        if (_registry == null) return;
        foreach (var o in _registry.Objectives)
        {
            if (o == null || o.Health == null) continue;
            o.Health.OnDeath -= OnObjectiveDestroyed_Generic; // avoid duplicates
            o.Health.OnDeath += OnObjectiveDestroyed_Generic;
        }
    }

    void UnsubscribeAll()
    {
        if (_killEvent != null) _killEvent.OnRaised -= OnUnitKilled;
        if (_registry == null) return;
        foreach (var o in _registry.Objectives)
        {
            if (o == null || o.Health == null) continue;
            o.Health.OnDeath -= OnObjectiveDestroyed_Generic;
        }
    }

    void OnUnitKilled(KillInfo info)
    {
        if (info.team == 0 || info.team == 1) _casualtiesByTeam[info.team]++;
    }

    void OnObjectiveDestroyed_Generic()
    {
        // Health.OnDeath is parameterless — re-evaluate every objective.
        EvaluateOnObjectiveLoss();
    }

    void Update()
    {
        if (_matchEnded || _timer == null) return;

        // Poll every frame as a safety net in case event subscriptions were missed
        EvaluateOnObjectiveLoss();

        if (_timer.Remaining <= 0f)
            EvaluateOnTimerExpiry();
    }

    // ── Evaluations ──────────────────────────────────────────────────────────

    void EvaluateOnObjectiveLoss()
    {
        if (_matchEnded || _registry == null) return;

        // 1. HQ destroyed → instant win
        ObjectiveTarget destroyedHq = null;
        foreach (var o in _registry.Objectives)
        {
            if (o == null || o.Health == null) continue;
            if (!IsHq(o)) continue;
            if (o.Health.IsDead) { destroyedHq = o; break; }
        }
        if (destroyedHq != null)
        {
            int winner = 1 - destroyedHq.Team;
            EndMatch(winner, "HQDestroyed");
            return;
        }

        // 2. In sudden death → first tower destruction (FOB or HQ) decides
        if (_inSuddenDeath)
        {
            // Find the most recently destroyed objective and award the opposing team.
            // Approximation: any destroyed objective ends the match.
            ObjectiveTarget any = null;
            foreach (var o in _registry.Objectives)
            {
                if (o != null && o.Health != null && o.Health.IsDead) { any = o; break; }
            }
            if (any != null)
            {
                int winner = 1 - any.Team;
                EndMatch(winner, "SuddenDeath");
            }
        }
    }

    void EvaluateOnTimerExpiry()
    {
        if (_registry == null) { EndMatch(-1, "Draw"); return; }

        int p1FobsLost = CountDestroyedFobs(team: 0);
        int p2FobsLost = CountDestroyedFobs(team: 1);

        // More enemy FOBs destroyed = winning side
        if (p2FobsLost > p1FobsLost) { EndMatch(0, "Timer"); return; }
        if (p1FobsLost > p2FobsLost) { EndMatch(1, "Timer"); return; }

        // Tiebreaker (GDD §2): more enemy unit casualties inflicted wins.
        int p1Inflicted = _casualtiesByTeam[1]; // enemy (team 1) units team 0 destroyed
        int p2Inflicted = _casualtiesByTeam[0];
        if (p1Inflicted > p2Inflicted) { EndMatch(0, "Casualties"); return; }
        if (p2Inflicted > p1Inflicted) { EndMatch(1, "Casualties"); return; }

        // Still tied → enter sudden death (no time cap)
        if (!_inSuddenDeath)
        {
            _inSuddenDeath = true;
            if (_suddenDeathBanner != null) _suddenDeathBanner.SetActive(true);
            Debug.Log("[Win] Tied at timer expiry — SUDDEN DEATH begins.");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    int CountDestroyedFobs(int team)
    {
        int n = 0;
        foreach (var o in _registry.Objectives)
        {
            if (o == null || o.Health == null) continue;
            if (o.Team != team) continue;
            if (IsHq(o)) continue; // FOBs only
            if (o.Health.IsDead) n++;
        }
        return n;
    }

    static bool IsHq(ObjectiveTarget o)
        => o != null && o.name.IndexOf("HQ", System.StringComparison.OrdinalIgnoreCase) >= 0;

    void EndMatch(int winnerTeam, string reason)
    {
        if (_matchEnded) return;
        _matchEnded = true;

        // Update local W/L tally — player is team 0
        if      (winnerTeam == 0) MatchStatsService.RecordWin();
        else if (winnerTeam == 1) MatchStatsService.RecordLoss();
        else                       MatchStatsService.RecordDraw();

        Debug.Log($"[Win] Match ended — winner=team{winnerTeam} reason={reason}");
        _matchEndEvent?.Raise(new MatchEndInfo
        {
            winnerTeam = winnerTeam,
            reason     = reason,
        });
    }
}
