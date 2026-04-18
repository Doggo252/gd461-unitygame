using UnityEngine;

public interface ICombatant
{
    int            Team      { get; }
    HealthComponent Health   { get; }
    UnitDataSO     Data      { get; }
    bool           IsDead    { get; }
    Transform      Transform { get; }

    // Returns effective ARM from the attacker's world position,
    // accounting for directional armour and Fortress bonus.
    float EffectiveArm(Vector3 attackerWorldPos);

    // Returns a damage multiplier based on which arc the attacker is in.
    // Front = 1.0x, Side = 1.5x, Rear = 2.5x.
    float DirectionalDamageMult(Vector3 attackerWorldPos);
}
