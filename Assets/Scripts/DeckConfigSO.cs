using System.Collections.Generic;
using UnityEngine;

// Cross-scene persistent carrier for the player's chosen 8-card deck and faction.
// ScriptableObject assets survive SceneManager.LoadScene without DontDestroyOnLoad.
[CreateAssetMenu(fileName = "DeckConfig", menuName = "Tank Royale/Deck/Deck Config")]
public class DeckConfigSO : ScriptableObject
{
    /// <summary>The faction chosen on the faction-select screen (Allies or Axis).</summary>
    public Faction      chosenFaction = Faction.Allies;

    public List<UnitDataSO> playerDeck = new();

    public bool IsValid => playerDeck != null && playerDeck.Count == 8;

    public void SetDeck(IEnumerable<UnitDataSO> cards)
    {
        playerDeck.Clear();
        playerDeck.AddRange(cards);
    }
}
