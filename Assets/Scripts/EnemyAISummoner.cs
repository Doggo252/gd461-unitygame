using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

// Enemy AI that periodically deploys affordable units onto its half of the map.
// Also reacts immediately to the player's first deployment via CpChangedEventSO.
public class EnemyAISummoner : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] float _thinkInterval = 2f;
    [SerializeField] float _deployChance  = 0.7f;

    [Header("AI map bounds (X is dynamic via FrontlineService when set)")]
    [SerializeField] float _fallbackXMin  =  3f;
    [SerializeField] float _fallbackXMax  = 26f;
    [SerializeField] float _deployZMin    = -13f;
    [SerializeField] float _deployZMax    =  13f;
    [SerializeField] int   _enemyTeam     =  1;

    [Header("References")]
    [SerializeField] CommandPointsManager _cpManager;
    [SerializeField] DeckManager          _deckManager;
    [SerializeField] FrontlineService     _frontlineService;

    [Header("Reaction — fire first unit when player deploys")]
    [Tooltip("Subscribe to the player's CP event. When their CP decreases for the " +
             "first time the AI immediately deploys a unit.")]
    [SerializeField] CpChangedEventSO     _playerCpChangedEvent;

    [Header("AI Deck")]
    [SerializeField] List<UnitDataSO> _aiDeck = new();

    [Header("Faction — set to read player faction from DeckConfigSO")]
    [SerializeField] DeckConfigSO _deckConfig;

    float _timer;
    bool  _reactedToPlayer;
    int   _lastKnownPlayerCp = -1;

    const int   MAX_PLACEMENT_RETRIES = 8;
    const float NAVMESH_SAMPLE_RADIUS = 1.5f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        _timer = _thinkInterval;

        // Filter to the faction opposing the player's chosen faction.
        if (_deckConfig != null && _aiDeck.Count > 0)
        {
            Faction opposing = _deckConfig.chosenFaction == Faction.Allies
                ? Faction.Axis : Faction.Allies;
            _aiDeck = _aiDeck
                .Where(c => c != null && (c.faction == opposing || c.faction == Faction.Both))
                .ToList();
            Debug.Log($"[EnemyAISummoner] AI deck filtered to {opposing}: {_aiDeck.Count} cards available.");
        }
    }

    void OnEnable()
    {
        if (_playerCpChangedEvent != null)
            _playerCpChangedEvent.OnRaised += OnPlayerCpChanged;
    }

    void OnDisable()
    {
        if (_playerCpChangedEvent != null)
            _playerCpChangedEvent.OnRaised -= OnPlayerCpChanged;
    }

    // ── Player CP reaction ────────────────────────────────────────────────────

    void OnPlayerCpChanged(CpChangedInfo info)
    {
        if (_reactedToPlayer) return;

        // First time the player spends CP (their count drops) → AI deploys immediately
        if (_lastKnownPlayerCp < 0)
        {
            _lastKnownPlayerCp = info.currentCp;
            return;
        }

        if (info.currentCp < _lastKnownPlayerCp)
        {
            _reactedToPlayer = true;
            TryDeploy(guaranteed: true);   // first reaction is always guaranteed
        }
        _lastKnownPlayerCp = info.currentCp;
    }

    // ── Periodic think tick ───────────────────────────────────────────────────

    void Update()
    {
        _timer -= Time.deltaTime;
        if (_timer > 0f) return;
        _timer = _thinkInterval;
        TryDeploy(guaranteed: false);
    }

    void TryDeploy(bool guaranteed = false)
    {
        if (_cpManager == null || _deckManager == null) return;
        if (_aiDeck == null || _aiDeck.Count == 0) return;

        // Periodic deploys use a chance roll; the first reaction deploy is guaranteed
        if (!guaranteed && Random.value > _deployChance) return;

        var affordable = _aiDeck
            .Where(c => c != null && _cpManager.CurrentCp >= c.cpCost)
            .ToList();
        if (affordable.Count == 0) return;

        if (!TryFindSpawnPosition(out Vector3 spawn)) return;

        var chosen = affordable[Random.Range(0, affordable.Count)];
        if (!_cpManager.TrySpend(chosen.cpCost)) return;

        _deckManager.SpawnUnit(chosen, spawn, _enemyTeam);
    }

    // ── Spawn position ────────────────────────────────────────────────────────

    bool TryFindSpawnPosition(out Vector3 spawn)
    {
        spawn = Vector3.zero;

        float xMin, xMax;
        if (_frontlineService != null)
        {
            float frontline = _frontlineService.GetFrontlineX(_enemyTeam);
            xMin = Mathf.Max(frontline, _fallbackXMin);
            xMax = _frontlineService.MapMaxX > xMin ? _frontlineService.MapMaxX : _fallbackXMax;
        }
        else
        {
            xMin = _fallbackXMin;
            xMax = _fallbackXMax;
        }

        for (int attempt = 0; attempt < MAX_PLACEMENT_RETRIES; attempt++)
        {
            float x         = Random.Range(xMin, xMax);
            float z         = Random.Range(_deployZMin, _deployZMax);
            var   candidate = new Vector3(x, 0f, z);

            if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, NAVMESH_SAMPLE_RADIUS, 1))
            {
                if (_frontlineService != null
                    && !_frontlineService.IsValidDeployX(_enemyTeam, hit.position.x))
                    continue;

                spawn = hit.position;
                return true;
            }
        }
        return false;
    }
}
