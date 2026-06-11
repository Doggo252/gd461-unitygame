using UnityEngine;

// Singleton-like ScriptableObject that carries the player's chosen difficulty
// across scenes without DontDestroyOnLoad. Assign the three difficulty assets
// (Easy/Normal/Hard) in the Inspector, and set Active at runtime via DifficultySelectUI.
[CreateAssetMenu(fileName = "SelectedDifficulty", menuName = "Tank Royale/Selected Difficulty")]
public class SelectedDifficultySO : ScriptableObject
{
    [SerializeField] AIDifficultyConfigSO _easy;
    [SerializeField] AIDifficultyConfigSO _normal;
    [SerializeField] AIDifficultyConfigSO _hard;

    public AIDifficultyConfigSO Easy   => _easy;
    public AIDifficultyConfigSO Normal => _normal;
    public AIDifficultyConfigSO Hard   => _hard;

    // The currently selected config — written by DifficultySelectUI.
    public AIDifficultyConfigSO Active { get; set; }

    void OnEnable()
    {
        // Default to Normal if nothing is selected yet.
        if (Active == null) Active = _normal;
    }
}
