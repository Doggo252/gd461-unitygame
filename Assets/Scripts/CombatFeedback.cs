using UnityEngine;

// Lightweight static event bus for transient combat feedback (AGENTS.md §2 —
// "a static or ScriptableObject-based event channel"). The attacker (TankAI)
// raises a Hit when a shot lands; CombatTextService listens and shows floating
// numbers. Keeps combat logic decoupled from presentation and needs no
// per-prefab wiring.
public static class CombatFeedback
{
    public enum Arc { Front, Side, Rear }

    public struct Hit
    {
        public Vector3 position;   // world position to show the number at
        public float   damage;
        public Arc     arc;
        public bool    ricochet;   // partial penetration (shell mostly bounced)
        public bool    lethal;     // this shot destroyed the target
    }

    public static event System.Action<Hit> OnHit;

    public static void Raise(Hit hit) => OnHit?.Invoke(hit);
}
