using UnityEngine;

[CreateAssetMenu(fileName = "CommandPoints", menuName = "Tank Royale/CP/Command Points")]
public class CommandPointsSO : ScriptableObject
{
    [Header("Starting value")]
    [Tooltip("CP granted immediately at match start. 8 guarantees an affordable opener with any legal deck.")]
    public int startingCp = 8;

    [Header("Regeneration (GDD §2)")]
    [Tooltip("CPs gained per second. Default 1/2.8 ≈ 0.357.")]
    public float regenRate         = 0.357143f;
    [Tooltip("Rate multiplier during Surge Phase (final 60 s).")]
    public float surgeMultiplier   = 2.0f;
    [Tooltip("Maximum CP the bar can hold.")]
    public int   maxCp             = 10;

    [Header("Kill Bonus")]
    [Tooltip("Fractional CP progress added to the accumulator per kill (0.5 = half a CP).")]
    public float killBonusProgress = 0.5f;
}
