using System.Collections.Generic;
using UnityEngine;

// Central registry for all active combatants and objectives.
// Units self-register in OnEnable / OnDisable — no runtime Find queries needed.
// Store one instance under Assets/Data/Events/ and wire it to all prefabs.
[CreateAssetMenu(fileName = "UnitRegistry", menuName = "Tank Royale/Unit Registry")]
public class UnitRegistrySO : ScriptableObject
{
    readonly List<ICombatant>      _combatants = new();
    readonly List<ObjectiveTarget> _objectives = new();
    readonly List<TankAI>          _tankAIs    = new();

    public IReadOnlyList<ICombatant>      Combatants => _combatants;
    public IReadOnlyList<ObjectiveTarget> Objectives => _objectives;
    public IReadOnlyList<TankAI>          TankAIs    => _tankAIs;

    // ScriptableObjects persist in the editor between Play sessions.
    // OnEnable fires on domain reload / play-mode entry, clearing stale refs.
    void OnEnable()
    {
        _combatants.Clear();
        _objectives.Clear();
        _tankAIs.Clear();
    }

    public void Register(ICombatant c)        => _combatants.Add(c);
    public void Unregister(ICombatant c)      => _combatants.Remove(c);
    public void Register(ObjectiveTarget o)   => _objectives.Add(o);
    public void Unregister(ObjectiveTarget o) => _objectives.Remove(o);
    public void Register(TankAI ai)           => _tankAIs.Add(ai);
    public void Unregister(TankAI ai)         => _tankAIs.Remove(ai);
}
