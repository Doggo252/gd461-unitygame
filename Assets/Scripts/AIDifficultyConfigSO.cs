using UnityEngine;

// Stores difficulty parameters for the enemy AI summoner.
// Assets live in Assets/Data/Difficulty/.
[CreateAssetMenu(fileName = "AIDifficultyConfig", menuName = "Tank Royale/AI Difficulty Config")]
public class AIDifficultyConfigSO : ScriptableObject
{
    [Tooltip("Seconds between each deploy attempt. Lower = more aggressive.")]
    public float thinkInterval  = 2f;

    [Tooltip("Probability (0-1) of actually deploying on each think tick.")]
    public float deployChance   = 0.7f;

    [Tooltip("Max CP cost of units the AI will deploy. 0 = no limit (all units allowed).")]
    public int   maxUnitCpCost  = 0;

    [Tooltip("Multiplier on AI Command Point regeneration (1.0 = same as player).")]
    public float cpRegenMult    = 1.0f;
}
