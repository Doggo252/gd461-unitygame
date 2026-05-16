using UnityEngine;

[CreateAssetMenu(fileName = "MatchTimer", menuName = "Tank Royale/Timer/Match Timer")]
public class MatchTimerSO : ScriptableObject
{
    [Tooltip("Total match length in seconds (GDD §2: 180 s = 3 minutes).")]
    public float matchDuration  = 180f;
    [Tooltip("Seconds remaining when Surge Phase starts (GDD §2: 60 s).")]
    public float surgeThreshold = 60f;
}
