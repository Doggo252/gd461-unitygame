using UnityEngine;

// ── Nation & Faction ───────────────────────────────────────────────────────────

public enum Nation
{
    USA, Germany, USSR, GreatBritain, Japan, Italy, France, Sweden
}

/// <summary>
/// Allies = USA, USSR, GreatBritain, France.
/// Axis   = Germany, Japan, Italy.
/// Both   = Sweden (neutral — available to either faction).
/// </summary>
public enum Faction { Allies, Axis, Both }

// ── Tank roster (one value per historical vehicle) ────────────────────────────

public enum TankType
{
    // ── USA ────────────────────────────────────────────────────────────────────
    M4A2_Sherman,
    M4A3E2_Jumbo,
    M4A1_76_Sherman,
    M26_Pershing,
    M18_Hellcat,
    T34_Heavy,

    // ── Germany ────────────────────────────────────────────────────────────────
    Tiger_H1,
    Panther_A,
    Tiger_II_H,
    Tiger_II_Nr1_50,
    Pz_IV_G,
    SdKfz_234_2,
    Hetzer,
    Maus,

    // ── USSR ───────────────────────────────────────────────────────────────────
    PT_76B,
    KV_1,
    IS_2,
    T34_85,
    T34_57,

    // ── Great Britain ──────────────────────────────────────────────────────────
    Churchill_VII,
    Concept_3,
    FV4005,
    Comet_I,

    // ── Japan ──────────────────────────────────────────────────────────────────
    M24_Chaffee,
    M36_GMC,
    Ho_Ri_Production,
    ST_A3,

    // ── Italy ──────────────────────────────────────────────────────────────────
    Leopard_40_70,
    M109G,
    Sherman_Firefly,
    R3_T20_FA_HS,

    // ── France ─────────────────────────────────────────────────────────────────
    AMX_13,
    ARL_44,
    M4A4_SA50,
    EBR_1951,

    // ── Sweden (neutral) ───────────────────────────────────────────────────────
    Strv_m40L,
    Strv_74,
    ZSU_57_2,

    // ── Appended (keep LAST — enum ordinals are serialized as `tankType` on every
    //    UnitDataSO/prefab, so inserting mid-list would shift them all). ──
    L3_33_CC,   // Italy — anti-tank tankette (20mm Solothurn S18/1000)
}

public enum DamageType { Kinetic, HE }

// Four states of the autonomous tank AI loop (GDD §2.1).
public enum TankAIState { Search, Priority, Pathfind, Attack }

// How this unit approaches a target it cannot yet fire on.
public enum ApproachStyle
{
    Direct,       // straight charge to just inside attack range
    ShallowFlank, // ~45° angle to one side — clips the flank
    WideFlank,    // ~90° sweep — goes fully around to side/rear
}

[System.Flags]
public enum UnitKeyword
{
    None             = 0,
    FastFlanker      = 1 << 0,  // always seeks to attack from the flank
    HeavyArmor       = 1 << 1,  // high ARM; prefers head-on engagement
    Devastating      = 1 << 2,  // single massive shot; min 30% damage floor
    ArmorPiercer     = 1 << 3,  // ignores armorPierceIgnore ARM per shot
    Aggressive       = 1 << 4,  // +aggressiveAtkBonus ATK when ally shares target
    SelfRepair       = 1 << 5,  // heals selfRepairAmount every selfRepairInterval s
    Scout            = 1 << 6,  // visual/UI role; fast recon
    RocketArtillery  = 1 << 7,  // uses HE formula §5.4; maintains max range
    LimitedTraverse  = 1 << 8,  // casemate TD — very slow turret/hull rotation
}

// ── ScriptableObject ───────────────────────────────────────────────────────────

[CreateAssetMenu(fileName = "UnitData", menuName = "Tank Royale/Unit Data")]
public class UnitDataSO : ScriptableObject
{
    [Header("Identity")]
    public TankType tankType;
    [Tooltip("Human-readable display name shown on cards.")]
    public string   tankName;
    public Nation   nation;
    public Faction  faction;
    [Tooltip("Nation flag sprite. Loaded from Assets/Asset Packs/flags/.")]
    public Sprite   flagSprite;
    public int      cpCost;

    [Header("Base Stats (GDD §7)")]
    public float maxHp;
    public float atk;
    public float arm;
    public float pen;
    public float spd;        // shots per second
    public float mov;        // world-units per second (1 unit = 1 tile)
    public float rng;        // attack range in world units
    public float aggroRange; // detection radius (default rng * 2)

    [Header("Damage Type")]
    public DamageType damageType = DamageType.Kinetic;

    [Header("Keywords")]
    public UnitKeyword keywords;

    [Header("Keyword Values")]
    public float armorPierceIgnore;   // ARM stripped per shot (ArmorPiercer)
    public float aggressiveAtkBonus;  // fraction ATK bonus (Aggressive: 0.15)
    public float selfRepairAmount;    // HP healed per tick (SelfRepair: 50)
    public float selfRepairInterval;  // seconds between ticks (SelfRepair: 5)
    public float devastatingMinFloor; // min damage fraction of ATK (Devastating: 0.30)

    [Header("Tactical Profile (GDD §2.1)")]
    [Tooltip("Relative probability of charging straight at the target.")]
    public float weightDirect;
    [Tooltip("Relative probability of a shallow ~45° flanking approach.")]
    public float weightShallowFlank;
    [Tooltip("Relative probability of a wide flanking sweep around the target.")]
    public float weightWideFlank;
    [Tooltip("Full detection cone angle in degrees. 360 = omnidirectional.")]
    public float detectionAngle = 360f;
    [Tooltip("Turret/hull rotation speed (slerp factor). Heavy ~0.9, Medium ~3.0, Light ~5.0.")]
    public float turnSpeed = 3f;
}
