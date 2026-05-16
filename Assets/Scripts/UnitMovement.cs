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

    float _stationaryTimer;
    const float STATIONARY_SECONDS = 0.6f;

    // Last destination we forwarded to the NavMeshAgent. Used to dedupe per-frame
    // SetDestination calls — NavMeshAgent.SetDestination unconditionally restarts
    // path computation, so calling it every Update with the same target leaves
    // pathPending = true forever and the agent never starts moving (especially
    // for partial / partially-unreachable destinations like a FOB origin).
    Vector3 _lastRequestedDest = new Vector3(float.NaN, float.NaN, float.NaN);
    const float DEST_DEDUPE_SQR = 0.04f; // 0.2 world-unit threshold on X/Z

    public bool IsStationary { get; private set; }

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
        _agent.speed = speed;
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

        // Track standstill for Crawler's Fortress keyword.
        bool moving = _agent.isOnNavMesh && _agent.velocity.sqrMagnitude > 0.01f;
        if (!moving)
        {
            _stationaryTimer += Time.deltaTime;
            IsStationary      = _stationaryTimer >= STATIONARY_SECONDS;
        }
        else
        {
            _stationaryTimer = 0f;
            IsStationary     = false;
        }
    }
}
