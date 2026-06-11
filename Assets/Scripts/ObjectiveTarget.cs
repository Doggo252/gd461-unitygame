using System.Collections.Generic;
using UnityEngine;

// Marks a FOB or Command HQ as a damageable structure that TankAI targets
// when no enemy units remain on the field (GDD §2 Priority logic).
// Attach to the root of each FOB/HQ parent GameObject.
[RequireComponent(typeof(HealthComponent))]
public class ObjectiveTarget : MonoBehaviour
{
    [Header("Structure")]
    public int            Team;
    public float          Arm      = 30f;    // armour rating for kinetic damage calc
    public float          MaxHp    = 5000f;  // FOBs: 5000, Command HQ: 8000
    [SerializeField] UnitRegistrySO _registry;

    [Header("Defense")]
    [SerializeField] float _defenseInterval  = 5f;
    [SerializeField] float _defenseDamagePct = 0.35f; // fraction of attacker's max HP per tick

    [Header("Deployment")]
    [Tooltip("Extra margin added to the structure footprint for the no-deploy radius.")]
    [SerializeField] float _keepOutMargin = 1.0f;
    // World-space radius inside which units may not be deployed (DeployRules).
    // Computed from the structure's renderer footprint at Start.
    public float KeepOutRadius { get; private set; }

    HealthComponent _health;
    public HealthComponent Health  => _health;
    public bool            IsAlive => _health != null && !_health.IsDead;

    // Tanks currently in attack range; tracked by TankAI via Register/Unregister.
    readonly List<ICombatant> _attackers = new();
    public IReadOnlyList<ICombatant> RegisteredAttackers => _attackers;
    float _defenseTimer;

    void Awake() => _health = GetComponent<HealthComponent>();

    void Start()
    {
        _health.Initialize(MaxHp);
        _defenseTimer = _defenseInterval;
        ComputeKeepOutRadius();
    }

    void ComputeKeepOutRadius()
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        float half = 0f;
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            half = Mathf.Max(b.extents.x, b.extents.z);
        }
        KeepOutRadius = half + _keepOutMargin;
    }

    void OnEnable()  { if (_registry != null) _registry.Register(this); }
    void OnDisable() { if (_registry != null) _registry.Unregister(this); }

    // ── Defense fire ──────────────────────────────────────────────────────────

    public void RegisterAttacker(ICombatant attacker)
    {
        if (attacker != null && !_attackers.Contains(attacker))
            _attackers.Add(attacker);
    }

    public void UnregisterAttacker(ICombatant attacker)
    {
        _attackers.Remove(attacker);
    }

    void Update()
    {
        if (!IsAlive || _attackers.Count == 0) return;

        _defenseTimer -= Time.deltaTime;
        if (_defenseTimer > 0f) return;

        _defenseTimer = _defenseInterval;

        // GDD §5.6: defense fire engages the NEAREST attacker only — never the
        // whole siege at once (batch deaths read as a phantom multi-kill).
        ICombatant nearest  = null;
        float      bestDist = float.MaxValue;
        for (int i = _attackers.Count - 1; i >= 0; i--)
        {
            var c = _attackers[i];
            if (c == null || c.IsDead) { _attackers.RemoveAt(i); continue; }
            float d = (c.Transform.position - transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; nearest = c; }
        }
        if (nearest == null) return;

        // Record this structure as the killer so the kill feed shows correctly.
        nearest.RecordLastAttacker(name, Team);
        float dmg = nearest.Health.Max * _defenseDamagePct;
        nearest.Health.TakeDamage(dmg);

        // Surface the hit as a floating damage number, same as tank fire.
        CombatFeedback.Raise(new CombatFeedback.Hit
        {
            position = nearest.Transform.position,
            damage   = dmg,
            arc      = CombatFeedback.Arc.Front,
            ricochet = false,
            lethal   = nearest.Health.Current <= 0f,
        });
    }
}
