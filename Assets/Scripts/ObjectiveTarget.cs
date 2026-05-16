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

    HealthComponent _health;
    public HealthComponent Health  => _health;
    public bool            IsAlive => _health != null && !_health.IsDead;

    // Tanks currently in attack range; tracked by TankAI via Register/Unregister.
    readonly List<ICombatant> _attackers = new();
    float _defenseTimer;

    void Awake() => _health = GetComponent<HealthComponent>();

    void Start()
    {
        _health.Initialize(MaxHp);
        _defenseTimer = _defenseInterval;
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

        for (int i = _attackers.Count - 1; i >= 0; i--)
        {
            var c = _attackers[i];
            if (c == null || c.IsDead)
            {
                _attackers.RemoveAt(i);
                continue;
            }
            // Record this structure as the killer so the kill feed shows correctly.
            c.RecordLastAttacker(name, Team);
            float dmg = c.Health.Max * _defenseDamagePct;
            c.Health.TakeDamage(dmg);
            Debug.Log($"[DEF] {name} → {c.Transform.name}: {dmg:F0} defense damage ({_defenseDamagePct * 100f:F0}% of {c.Health.Max:F0})");
        }
    }
}
