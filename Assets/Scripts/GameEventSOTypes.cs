using UnityEngine;

// All concrete GameEventSO<T> channels live here.
// Unity requires a non-generic [CreateAssetMenu] class to produce .asset files —
// each entry below is a one-line subclass that inherits all behaviour from GameEventSO<T>.
//
// To add a new channel type: append one line following the pattern below.
// Asset menu path: Tank Royale / Events / <Type> Event

// ── Primitives ────────────────────────────────────────────────────────────────
[CreateAssetMenu(fileName = "IntEvent",     menuName = "Tank Royale/Events/Int Event")]
public class IntEventSO     : GameEventSO<int>     { }

[CreateAssetMenu(fileName = "FloatEvent",   menuName = "Tank Royale/Events/Float Event")]
public class FloatEventSO   : GameEventSO<float>   { }

[CreateAssetMenu(fileName = "BoolEvent",    menuName = "Tank Royale/Events/Bool Event")]
public class BoolEventSO    : GameEventSO<bool>    { }

[CreateAssetMenu(fileName = "StringEvent",  menuName = "Tank Royale/Events/String Event")]
public class StringEventSO  : GameEventSO<string>  { }

[CreateAssetMenu(fileName = "Vector3Event", menuName = "Tank Royale/Events/Vector3 Event")]
public class Vector3EventSO : GameEventSO<Vector3> { }

// ── Game-specific payloads ────────────────────────────────────────────────────

// Payload for KillEventSO — unit name and team index.
public struct KillInfo
{
    public string unitName;
    public int    team;      // 0 = P1 blue, 1 = P2 red
}

[CreateAssetMenu(fileName = "KillEvent", menuName = "Tank Royale/Events/Kill Event")]
public class KillEventSO : GameEventSO<KillInfo> { }
