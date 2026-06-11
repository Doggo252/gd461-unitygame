using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Single-responsibility: drives the NavMeshAgent to a destination and exposes
// helper methods used by TankAI (SetDestination, Stop, FaceToward).
// NavMesh handles obstacle avoidance automatically around rocks, walls, and trees.
[RequireComponent(typeof(NavMeshAgent))]
public class UnitMovement : MonoBehaviour
{
    NavMeshAgent _agent;

    // FaceToward is requested in Update; applied same frame by manually rotating.
    bool    _pendingFace;
    Vector3 _faceTarget;
    float   _faceSpeed;

    // Base movement speed (from UnitDataSO.mov) before terrain scaling.
    float _baseSpeed = 1f;
    // Active terrain zones the unit is standing in (keyed by the zone). The
    // most-recently-entered zone's multiplier wins when zones overlap.
    readonly List<KeyValuePair<object, float>> _terrainZones = new();

    // True while the agent is actually moving — drives the engine-loop SFX.
    public bool IsMoving { get; private set; }

    // Last destination we forwarded to the NavMeshAgent. Used to dedupe per-frame
    // SetDestination calls — NavMeshAgent.SetDestination unconditionally restarts
    // path computation, so calling it every Update with the same target leaves
    // pathPending = true forever and the agent never starts moving (especially
    // for partial / partially-unreachable destinations like a FOB origin).
    Vector3 _lastRequestedDest = new Vector3(float.NaN, float.NaN, float.NaN);
    const float DEST_DEDUPE_SQR = 0.04f; // 0.2 world-unit threshold on X/Z

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.angularSpeed     = 120f;
        _agent.acceleration     = 8f;
        _agent.stoppingDistance = 0.3f;
        _agent.updateRotation   = true;
        _agent.updatePosition   = true;
        _agent.autoBraking      = true;
        _agent.areaMask         = 1 << 0; // Walkable area only — excludes river (carved by NavMeshObstacle)
    }

    public void Initialize(float speed)
    {
        _baseSpeed   = speed;
        _agent.speed = speed;
    }

    // ── Terrain speed modifiers (roads faster, rubble/mud slower) ───────────────
    // Called by TerrainModifierZone trigger callbacks as the unit enters/leaves a
    // terrain patch. Effective speed = base × the most-recently-entered zone's
    // multiplier (overlapping zones: last one entered wins).

    public void PushTerrain(object zone, float multiplier)
    {
        _terrainZones.RemoveAll(kv => ReferenceEquals(kv.Key, zone));
        _terrainZones.Add(new KeyValuePair<object, float>(zone, multiplier));
        ApplyTerrainSpeed();
    }

    public void PopTerrain(object zone)
    {
        _terrainZones.RemoveAll(kv => ReferenceEquals(kv.Key, zone));
        ApplyTerrainSpeed();
    }

    void ApplyTerrainSpeed()
    {
        float mult = _terrainZones.Count > 0 ? _terrainZones[_terrainZones.Count - 1].Value : 1f;
        if (_agent != null) _agent.speed = _baseSpeed * mult;
    }

    public void SetDestination(Vector3 worldPos)
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped      = false;
        _agent.updateRotation = true;

        float dx = worldPos.x - _lastRequestedDest.x;
        float dz = worldPos.z - _lastRequestedDest.z;
        if (dx * dx + dz * dz < DEST_DEDUPE_SQR) return;

        _agent.SetDestination(new Vector3(worldPos.x, transform.position.y, worldPos.z));
        _lastRequestedDest = worldPos;
    }

    public void Stop()
    {
        if (!_agent.isOnNavMesh) return;
        _agent.isStopped = true;
        _agent.velocity  = Vector3.zero;
        // Re-arm dedupe so the next SetDestination is forwarded even with the same target.
        _lastRequestedDest = new Vector3(float.NaN, float.NaN, float.NaN);
    }

    // Queue a facing rotation; TankAI calls this each Update frame while attacking.
    public void FaceToward(Vector3 worldPos, float slerp = 3f)
    {
        _agent.updateRotation = false; // hand rotation to us while tracking
        _pendingFace          = true;
        _faceTarget           = worldPos;
        _faceSpeed            = slerp;
    }

    void Update()
    {
        if (_pendingFace)
        {
            Vector3 dir = _faceTarget - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(dir), _faceSpeed * Time.deltaTime);
            _pendingFace = false;
        }

        // Track movement for the engine-loop SFX (UnitAudio reads this).
        IsMoving = _agent.isOnNavMesh && !_agent.isStopped && _agent.velocity.sqrMagnitude > 0.04f;
    }
}
