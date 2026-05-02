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

    HealthComponent _health;
    public HealthComponent Health  => _health;
    public bool            IsAlive => _health != null && !_health.IsDead;

    void Awake() => _health = GetComponent<HealthComponent>();
    void Start()  => _health.Initialize(MaxHp);

    void OnEnable()  { if (_registry != null) _registry.Register(this); }
    void OnDisable() { if (_registry != null) _registry.Unregister(this); }
}
