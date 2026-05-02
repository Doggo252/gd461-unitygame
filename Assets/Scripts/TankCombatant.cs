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
[RequireComponent(typeof(FloatingHealthBar))]
public class TankCombatant : MonoBehaviour, ICombatant
{
    [Header("Tank Configuration")]
    [SerializeField] private TankType       _tankType = TankType.Original;
    [SerializeField] private int            _team;
    [SerializeField] private UnitRegistrySO _registry;
    [SerializeField] private KillEventSO    _killEvent;

    [Header("Data (auto-populated from TankType)")]
    [SerializeField] private UnitDataSO _data;

    HealthComponent _health;
    UnitMovement    _movement;
    TankAI          _ai;

    // ── ICombatant ──────────────────────────────────────────────────────────────

    public int             Team      => _team;
    public HealthComponent Health    => _health;
    public UnitDataSO      Data      => _data;
    // True if health component is gone (object destroyed) OR HP reached zero.
    public bool            IsDead    => _health == null || _health.IsDead;
    public Transform       Transform => transform;

    // Directional armour: front = full ARM, sides = 65%, rear = 40%.
    // Crawler Fortress keyword adds stationaryArmBonus when not moving.
    public float EffectiveArm(Vector3 attackerWorldPos)
    {
        float arm = _data.arm;

        if (HasKw(UnitKeyword.Fortress) && _movement.IsStationary)
            arm += _data.stationaryArmBonus;

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

        // Ensure a Collider exists so FindObjectsByType + Physics queries can find this unit.
        if (GetComponentInChildren<Collider>() == null)
        {
            var col    = gameObject.AddComponent<CapsuleCollider>();
            col.radius = 0.6f;
            col.height = 1.6f;
        }
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

        GetComponent<FloatingHealthBar>().SetBorderColor(_team == 0
            ? new Color(0.20f, 0.45f, 0.95f)
            : new Color(0.95f, 0.20f, 0.20f));

        ApplyTeamColor();

        if (HasKw(UnitKeyword.SelfRepair))
            StartCoroutine(SelfRepairRoutine());
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
                unitName = _data != null ? _data.tankType.ToString() : name,
                team     = _team
            });
        Destroy(gameObject);
    }

    // ── Visuals ──────────────────────────────────────────────────────────────────

    void ApplyTeamColor()
    {
        Color teamColor = _team == 0
            ? new Color(0.20f, 0.45f, 0.95f)
            : new Color(0.95f, 0.20f, 0.20f);

        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            var mat = r.material;
            if      (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", teamColor);
            else if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     teamColor);
        }
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
