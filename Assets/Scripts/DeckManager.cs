using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Owns the player's 8-card deck and the 4-card hand shown in the HUD.
// CardDragDeploy calls SelectCard / ConsumeCard / SpawnUnit.
// EnemyAISummoner calls SpawnUnit directly (with its own deck list).
public class DeckManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] DeckConfigSO         _deckConfig;
    [SerializeField] UnitRegistrySO       _registry;
    [SerializeField] KillEventSO          _killEvent;
    [SerializeField] CardPrefabRegistrySO _prefabRegistry;

    List<UnitDataSO> _deck      = new();
    UnitDataSO[]     _hand      = new UnitDataSO[4];
    int              _deckHead;
    int              _selectedHandIndex = -1;

    public UnitDataSO[] Hand              => _hand;
    public int          SelectedHandIndex => _selectedHandIndex;
    public UnitDataSO   SelectedCard      => _selectedHandIndex >= 0 ? _hand[_selectedHandIndex] : null;

    // Fires whenever the hand changes (card played, initial deal).
    public event Action HandChanged;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Start()
    {
        if (_deckConfig != null && _deckConfig.IsValid)
        {
            _deck.AddRange(_deckConfig.playerDeck);
        }
        else
        {
            // Fallback for testing without going through MenuScene.
            var all = Resources.FindObjectsOfTypeAll<UnitDataSO>()
                               .OrderBy(d => d.name)
                               .Take(8);
            _deck.AddRange(all);
            Debug.LogWarning("[DeckManager] DeckConfig not set — using first 8 UnitDataSOs as fallback.");
        }

        _deckHead = 0;
        for (int i = 0; i < 4 && i < _deck.Count; i++)
            _hand[i] = DrawNext();

        HandChanged?.Invoke();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void SelectCard(int index)
    {
        if (index < 0 || index >= 4 || _hand[index] == null) return;
        _selectedHandIndex = index;
    }

    public void CancelSelection() => _selectedHandIndex = -1;

    // Called by CardDragDeploy after a successful deploy.
    public void ConsumeCard(int handIndex)
    {
        if (handIndex < 0 || handIndex >= 4) return;
        _hand[handIndex]   = DrawNext();
        _selectedHandIndex = -1;
        HandChanged?.Invoke();
    }

    // Instantiate a prefab at worldPos for the given team.
    // Used by both CardDragDeploy (player) and EnemyAISummoner (AI).
    public void SpawnUnit(UnitDataSO card, Vector3 worldPos, int team)
    {
        if (card == null) return;
        if (_prefabRegistry == null || !_prefabRegistry.TryGetPrefab(card.tankType, out var prefab))
        {
            Debug.LogError($"[DeckManager] No prefab registered for {card.tankType}");
            return;
        }
        // Team 0 (player, blue, left side) attacks right (+X).
        // Team 1 (AI,  red, right side) attacks left  (-X).
        var spawnRot  = team == 0
            ? Quaternion.LookRotation(Vector3.right, Vector3.up)
            : Quaternion.LookRotation(Vector3.left,  Vector3.up);
        var go        = Instantiate(prefab, worldPos, spawnRot);
        var combatant = go.GetComponent<TankCombatant>();
        combatant?.InitializeSpawned(team);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    UnitDataSO DrawNext()
    {
        if (_deck.Count == 0) return null;
        return _deck[_deckHead++ % _deck.Count];
    }
}
