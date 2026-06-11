using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Orchestrator that implements ICombatant and wires HealthComponent,
// UnitMovement, and TankAI together from a single UnitDataSO.
//
// Usage: Add this component to any tank prefab. Select a TankType from the
// dropdown — the matching UnitDataSO will auto-populate. Set Team (0 = blue,
// 1 = red) and press Play; the tank will automatically find and engage enemies.
[RequireComponent(typeof(HealthComponent))]
[RequireComponent(typeof(UnitMovement))]
[RequireComponent(typeof(TankAI))]
[RequireComponent(typeof(WorldHealthBarTracker))]
public class TankCombatant : MonoBehaviour, ICombatant
{
    [Header("Tank Configuration")]
    [SerializeField] private TankType       _tankType = TankType.M4A2_Sherman;
    [SerializeField] private int            _team;
    [SerializeField] private UnitRegistrySO _registry;
    [SerializeField] private KillEventSO    _killEvent;

    [Header("Data (auto-populated from TankType)")]
    [SerializeField] private UnitDataSO _data;

    HealthComponent _health;
    UnitMovement    _movement;
    TankAI          _ai;

    string    _lastKillerName = "Unknown";
    int       _lastKillerTeam = -1;
    Coroutine _selfRepairRoutine;

    // ── ICombatant ──────────────────────────────────────────────────────────────

    // Raised each time this unit fires a shot (drives the fire SFX in UnitAudio).
    public event System.Action Fired;
    public void NotifyFired() => Fired?.Invoke();

    public int             Team      => _team;
    public HealthComponent Health    => _health;
    public UnitDataSO      Data      => _data;
    // True if health component is gone (object destroyed) OR HP reached zero.
    public bool            IsDead    => _health == null || _health.IsDead;
    public Transform       Transform => transform;

    // Directional armour: front = full ARM, sides = 40%, rear = 15% (GDD §5.0).
    public float EffectiveArm(Vector3 attackerWorldPos)
    {
        float arm = _data.arm;

        Vector3 toAttacker = attackerWorldPos - transform.position;
        toAttacker.y = 0f;
        if (toAttacker.sqrMagnitude > 0.001f)
        {
            float angle = Vector3.Angle(transform.forward, toAttacker);
            if      (angle < 45f)  arm *= 1.00f; // front  — full armour
            else if (angle < 135f) arm *= 0.40f; // sides  — 40% armour
            else                   arm *= 0.15f; // rear   — 15% armour
        }

        return arm;
    }

    // Separate ATK multiplier on top of the ARM reduction.
    // Hitting a tank from the side/rear is realistically far more lethal
    // than the penetration formula alone expresses.
    public float DirectionalDamageMult(Vector3 attackerWorldPos)
    {
        Vector3 toAttacker = attackerWorldPos - transform.position;
        toAttacker.y = 0f;
        if (toAttacker.sqrMagnitude < 0.001f) return 1f;

        float angle = Vector3.Angle(transform.forward, toAttacker);
        if      (angle < 45f)  return 1.0f; // front
        else if (angle < 135f) return 1.5f; // sides
        else                   return 2.5f; // rear
    }

    // ── Lifecycle ────────────────────────────────────────────────────────────────

    void Awake()
    {
        _health   = GetComponent<HealthComponent>();
        _movement = GetComponent<UnitMovement>();
        _ai       = GetComponent<TankAI>();

        if (GetComponentInChildren<Collider>() == null)
            Debug.LogWarning($"[TankCombatant] {name} has no Collider — add a CapsuleCollider to the prefab.", this);
    }

    void OnEnable()
    {
        if (_health != null) _health.OnDeath += HandleDeath;
        if (_registry != null)
        {
            _registry.Register((ICombatant)this);
            if (_ai != null) _registry.Register(_ai);
        }
    }

    void OnDisable()
    {
        if (_health != null) _health.OnDeath -= HandleDeath;
        if (_registry != null)
        {
            _registry.Unregister((ICombatant)this);
            if (_ai != null) _registry.Unregister(_ai);
        }
        if (_selfRepairRoutine != null) { StopCoroutine(_selfRepairRoutine); _selfRepairRoutine = null; }
    }

    void Start()
    {
        if (_data == null)
        {
            Debug.LogError($"[TankCombatant] {name}: UnitDataSO not assigned — " +
                           "select a TankType in the Inspector.", this);
            return;
        }

        _health.Initialize(_data.maxHp);
        _movement.Initialize(_data.mov);
        _ai.Initialize(_data, _team, _movement, this, _registry);

        GetComponent<WorldHealthBarTracker>().SetBorderColor(_team == 0
            ? new Color(0.20f, 0.45f, 0.95f)
            : new Color(0.95f, 0.20f, 0.20f));

        SetupNameTag();

        if (HasKw(UnitKeyword.SelfRepair))
            _selfRepairRoutine = StartCoroutine(SelfRepairRoutine());
    }

    // ── Keyword Behaviours ───────────────────────────────────────────────────────

    IEnumerator SelfRepairRoutine()
    {
        var wait = new WaitForSeconds(_data.selfRepairInterval);
        while (!IsDead)
        {
            yield return wait;
            if (!IsDead) _health.Heal(_data.selfRepairAmount);
        }
    }

    // ── Death ────────────────────────────────────────────────────────────────────

    void HandleDeath()
    {
        if (_killEvent != null)
            _killEvent.Raise(new KillInfo
            {
                unitName   = _data != null && !string.IsNullOrEmpty(_data.tankName)
                                 ? _data.tankName : name,
                team       = _team,
                killerName = _lastKillerName,
                killerTeam = _lastKillerTeam
            });
        Destroy(gameObject);
    }

    public void RecordLastAttacker(string killerName, int killerTeam)
    {
        _lastKillerName = killerName;
        _lastKillerTeam = killerTeam;
    }

    // Called by DeckManager.SpawnUnit immediately after Instantiate, before Start().
    // Overrides _team so the spawned unit fights for the correct side.
    // _registry and _killEvent stay as the prefab's pre-wired values — they are
    // shared assets already set in the prefab Inspector.
    public void InitializeSpawned(int team) => _team = team;

    // ── Visuals ──────────────────────────────────────────────────────────────────

    void SetupNameTag()
    {
        var tracker = GetComponent<WorldHealthBarTracker>();
        if (tracker == null) return;

        string displayName = _data != null && !string.IsNullOrEmpty(_data.tankName)
            ? _data.tankName
            : gameObject.name.Replace("(Clone)", "").Replace('_', ' ').Trim();

        Color teamColor = _team == 0
            ? new Color(0.20f, 0.45f, 0.95f)
            : new Color(0.95f, 0.20f, 0.20f);

        tracker.SetNameTag(displayName, teamColor);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    bool HasKw(UnitKeyword kw) => _data != null && (_data.keywords & kw) != 0;

#if UNITY_EDITOR
    void OnValidate()
    {
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            var guids = AssetDatabase.FindAssets("t:UnitDataSO");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var so   = AssetDatabase.LoadAssetAtPath<UnitDataSO>(path);
                if (so != null && so.tankType == _tankType)
                {
                    if (_data == so) break;
                    _data = so;
                    EditorUtility.SetDirty(this);
                    break;
                }
            }
        };
    }
#endif
}
