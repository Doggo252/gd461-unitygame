using UnityEngine;
using UnityEngine.UI;

// Shows "WINS X   LOSSES Y   DRAWS Z" in the Menu scene.
// Reads from MatchStatsService (PlayerPrefs-backed).
public class MenuStatsDisplay : MonoBehaviour
{
    [SerializeField] Text _statsText;

    void Start() => Refresh();
    void OnEnable() => Refresh();

    public void Refresh()
    {
        if (_statsText == null) return;
        _statsText.text = $"WINS  {MatchStatsService.Wins}     LOSSES  {MatchStatsService.Losses}     DRAWS  {MatchStatsService.Draws}";
    }
}
