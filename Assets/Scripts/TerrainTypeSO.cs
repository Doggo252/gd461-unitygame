using UnityEngine;

// Data definition for a terrain patch's effect on units (GDD §2 Terrain).
// Roads speed units up, rubble/craters and mud slow them down. Designers create
// one asset per terrain kind and drop it on TerrainModifierZone components.
[CreateAssetMenu(fileName = "TerrainType", menuName = "Tank Royale/Terrain Type")]
public class TerrainTypeSO : ScriptableObject
{
    [Tooltip("Designer-facing label (Road, Open Ground, Rubble, Mud, ...).")]
    public string displayName = "Open Ground";

    [Tooltip("Movement-speed multiplier for units on this terrain. " +
             "1 = normal, >1 faster (roads), <1 slower (rubble/mud).")]
    [Range(0.1f, 2f)] public float movMultiplier = 1f;
}
