using UnityEngine;

// ── Enums ──────────────────────────────────────────────────────────────────────

public enum TankType
{
    Original, Alternative, Light, Heavy, Crawler,
    Monster, Spike, Shark, Droid, UTV, MegaBall, RocketShip, UFO
}

public enum DamageType { Kinetic, HE }

// Four states of the autonomous tank AI loop (GDD §2.1).
public enum TankAIState { Search, Priority, Pathfind, Attack }

// How this unit approaches a target it cannot yet fire on.
// Rolled once per new target acquisition; weights are set per tank type in UnitDataSO.
public enum ApproachStyle
{
    Direct,       // straight charge to just inside attack range
    ShallowFlank, // ~45° angle to one side — clips the flank
    WideFlank,    // ~90° sweep — goes fully around to side/rear
}

[System.Flags]
public enum UnitKeyword
{
    None            = 0,
    FastFlanker     = 1 << 0,  // Light:      always seeks to attack from the flank
    Fortress        = 1 << 1,  // Crawler:    +stationaryArmBonus ARM while stationary
    Devastating     = 1 << 2,  // Monster:    devastatingMinFloor fraction min damage
    ArmorPiercer    = 1 << 3,  // Spike:      ignores armorPierceIgnore ARM per shot
    Aggressive      = 1 << 4,  // Shark:      +aggressiveAtkBonus ATK when ally shares target
    SelfRepair      = 1 << 5,  // Droid:      heals selfRepairAmount every selfRepairInterval s
    Scout           = 1 << 6,  // UTV:        (visual / UI — no combat mechanic yet)
    Rollout         = 1 << 7,  // MegaBall:   (handled on deploy — future)
    RocketArtillery = 1 << 8,  // RocketShip: uses HE formula §5.4; maintains max range
    Hover           = 1 << 9,  // UFO:        ignores terrain slow (future NavMesh modifier)
}

// ── ScriptableObject ───────────────────────────────────────────────────────────

[CreateAssetMenu(fileName = "UnitData", menuName = "Tank Royale/Unit Data")]
public class UnitDataSO : ScriptableObject
{
    [Header("Identity")]
    public TankType tankType;
    public int      cpCost;

    [Header("Base Stats (GDD §7)")]
    public float maxHp;
    public float atk;
    public float arm;
    public float pen;
    public float spd;        // shots per second
    public float mov;        // world-units per second (1 unit = 1 tile)
    public float rng;        // attack range in world units
    public float aggroRange; // detection radius (set = rng * 2 by default via asset creator)

    [Header("Keywords")]
    public UnitKeyword keywords;

    [Header("Keyword Values")]
    public float armorPierceIgnore;   // Spike:  ARM to strip per shot (30)
    public float aggressiveAtkBonus;  // Shark:  fraction bonus (0.15 = +15%)
    public float selfRepairAmount;    // Droid:  HP healed per tick (50)
    public float selfRepairInterval;  // Droid:  seconds between ticks (5)
    public float stationaryArmBonus;  // Crawler: ARM added while stationary (20)
    public float devastatingMinFloor; // Monster: min damage as fraction of ATK (0.30)

    [Header("Tactical Profile (GDD §2.1)")]
    [Tooltip("Relative probability of charging straight at the target.")]
    public float weightDirect;
    [Tooltip("Relative probability of a shallow ~45° flanking approach.")]
    public float weightShallowFlank;
    [Tooltip("Relative probability of a wide flanking sweep around the target.")]
    public float weightWideFlank;
    [Tooltip("Full detection cone angle in degrees. 360 = omnidirectional. Enemies outside the forward arc are not scanned or tracked.")]
    public float detectionAngle = 360f;
}
