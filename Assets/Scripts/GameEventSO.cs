using System;
using System.Collections.Generic;
using UnityEngine;

// ── Parameterless event channel ───────────────────────────────────────────────
//
// Usage:
//   1. Create an asset: right-click → Create → Tank Royale → Events → Game Event
//   2. Raise it:   myEvent.Raise();
//   3. Listen:     myEvent.OnRaised += Handler;   (unsub in OnDisable)
//
// ScriptableObject-based channels decouple senders from receivers — neither
// needs a direct reference to the other, only to the shared channel asset.
// All assets live under Assets/Data/Events/.

[CreateAssetMenu(fileName = "GameEvent", menuName = "Tank Royale/Events/Game Event")]
public class GameEventSO : ScriptableObject
{
    public event Action OnRaised;

    // Keep a debug listener list so we can warn when Raise() fires with no subscribers.
    readonly List<string> _listenerNames = new();

    public void Raise()
    {
        if (OnRaised == null)
            Debug.LogWarning($"[GameEventSO] '{name}' raised but has no subscribers.");
        OnRaised?.Invoke();
    }

    // Optional: named registration for editor debugging.
    public void RegisterDebugName(string listenerName) => _listenerNames.Add(listenerName);
    public void UnregisterDebugName(string listenerName) => _listenerNames.Remove(listenerName);
}

// ── Single-value typed event channel ─────────────────────────────────────────
//
// Cannot create a generic ScriptableObject asset directly in Unity,
// so concrete subtypes are declared below and in GameEventSO.Types.cs.
// Add new types there as the project grows.

public abstract class GameEventSO<T> : ScriptableObject
{
    public event Action<T> OnRaised;

    public void Raise(T value)
    {
        if (OnRaised == null)
            Debug.LogWarning($"[GameEventSO<{typeof(T).Name}>] '{name}' raised but has no subscribers.");
        OnRaised?.Invoke(value);
    }
}
