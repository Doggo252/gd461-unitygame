using UnityEngine;
using UnityEngine.Serialization;

// All game sound effects in one draggable asset (AGENTS.md §4 — data lives in
// ScriptableObjects). Drop one or more clips into each slot; if a slot has
// several clips one is chosen at random per play for variety. Leaving a slot
// empty simply plays nothing for that event, so the game runs fine before any
// audio is added.
[CreateAssetMenu(fileName = "AudioProfile", menuName = "Tank Royale/Audio Profile")]
public class AudioProfileSO : ScriptableObject
{
    [System.Serializable]
    public class Sfx
    {
        public AudioClip[] clips;
        [Range(0f, 1f)]   public float volume      = 1f;
        [Range(0f, 0.4f)] public float pitchJitter = 0.08f; // ± random pitch per play

        public AudioClip Pick()
            => (clips == null || clips.Length == 0) ? null : clips[Random.Range(0, clips.Length)];
        public bool HasClips => clips != null && clips.Length > 0;
    }

    [Header("Per-unit SFX")]
    public Sfx unitSpawn;          // played when a unit is deployed
    public Sfx engineIdle;         // looped while the unit is stationary
    [FormerlySerializedAs("engineLoop")]
    public Sfx engineMoving;       // looped while the unit is moving
    public Sfx fire;               // each shot
    public Sfx hit;                // taking damage
    public Sfx tankDestroyed;      // unit destroyed

    [Header("Structure / global SFX")]
    public Sfx structureDestroyed; // FOB or HQ destroyed
    public Sfx lowHpSiren;         // a friendly FOB drops below 25% HP
    [FormerlySerializedAs("cpFull")]
    public Sfx cpGain;             // each command point gained
    public Sfx uiClick;            // generic UI button

    [Header("Master")]
    [Range(0f, 1f)] public float masterVolume = 1f;
}
