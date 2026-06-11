using System.Collections.Generic;
using UnityEngine;

// Tears down the live battle presentation the instant the match ends, so the
// VICTORY/DEFEAT overlay sits on a clean, dimmed scene:
//   • despawns all tanks (which also removes their world health bars + name tags)
//   • clears frozen floating damage numbers
//   • hides the kill feed
//   • drops the world health-bar canvas behind the game-over dim so the surviving
//     structure bars read as part of the dimmed background
//
// Listens on the same MatchEndEventSO that GameOverPanel uses. Purely a teardown
// step — it owns no display logic of its own.
public class BattleEndCleanup : MonoBehaviour
{
    [SerializeField] MatchEndEventSO _matchEndEvent;
    [SerializeField] UnitRegistrySO  _registry;

    [Header("Scene refs — wire in Inspector")]
    [Tooltip("Kill-feed canvas (top-right) — disabled on match end.")]
    [SerializeField] GameObject _killFeedCanvas;
    [Tooltip("World health-bar canvas — its sorting order is lowered behind the dim on match end.")]
    [SerializeField] Canvas _worldHealthBarCanvas;
    [Tooltip("Sorting order to drop the health-bar canvas to (must be below the game-over canvas).")]
    [SerializeField] int _dimmedHealthBarOrder = 5;

    bool _done;

    void OnEnable()  { if (_matchEndEvent != null) _matchEndEvent.OnRaised += OnMatchEnd; }
    void OnDisable() { if (_matchEndEvent != null) _matchEndEvent.OnRaised -= OnMatchEnd; }

    void OnMatchEnd(MatchEndInfo _)
    {
        if (_done) return;
        _done = true;

        // Despawn tanks (snapshot first — disabling unregisters them mid-iteration).
        if (_registry != null)
        {
            var snapshot = new List<ICombatant>(_registry.Combatants);
            foreach (var c in snapshot)
                if (c?.Transform != null) c.Transform.gameObject.SetActive(false);
        }

        // Clear frozen damage numbers.
        if (CombatTextService.Instance != null) CombatTextService.Instance.HideAll();

        // Hide the kill feed.
        if (_killFeedCanvas != null) _killFeedCanvas.SetActive(false);

        // Sink the surviving structure health bars behind the dim overlay.
        if (_worldHealthBarCanvas != null) _worldHealthBarCanvas.sortingOrder = _dimmedHealthBarOrder;

        // Freeze the river (and any other time-driven shaders) behind the result screen.
        ShaderTimeDriver.GlobalPaused = true;
    }
}
