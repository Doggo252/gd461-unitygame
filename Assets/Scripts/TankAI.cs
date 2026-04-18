using UnityEngine;
using UnityEngine.AI;

// Four-state autonomous AI loop for tank units.
//
// SEARCH   — polls for any living enemy (tank or objective) within the detection
//            cone and with a clear line-of-sight through cover.
// PRIORITY — decides the best target: fight enemy tanks first; attack objectives
//            only once all enemy tanks are eliminated.
// PATHFIND — navigates toward the chosen target. Wide / shallow flanks are
//            computed dynamically each frame so the path updates as tanks move.
// ATTACK   — stops, faces, and fires. Validates detection cone for unit targets.
//
// Transitions:
//   Search → Priority  (target found)
//   Priority → Pathfind (target chosen)
//   Pathfind → Attack   (within RNG or arrived at closest reachable point)
//   Attack → Search     (target dead / lost)
//   Attack → Pathfind   (target backed away)
//   * → Search          (target invalidated at any state)

[RequireComponent(typeof(UnitMovement))]
public class TankAI : MonoBehaviour
{
    // ── Injected ────────────────────────────────────────────────────────────────
    UnitDataSO   _data;
    int          _team;
    UnitMovement _movement;
    ICombatant   _self;

    // ── State ───────────────────────────────────────────────────────────────────
    TankAIState     _state = TankAIState.Search;
    ICombatant      _unitTarget;    // enemy tank
    ObjectiveTarget _buildTarget;   // enemy FOB / HQ

    // ── Tactical approach (unit combat only) ────────────────────────────────────
    ApproachStyle _approach;
    int           _flankSide;      // +1 = right, -1 = left; fixed per target acquisition
    ICombatant    _lastRolledTarget;

    // ── Timers ──────────────────────────────────────────────────────────────────
    float _scanTimer;
    float _attackTimer;

    const float SCAN_INTERVAL  = 0.5f;
    const float CONE_DROP_MULT = 1.5f;  // drop unit target if > rng * mult outside cone
    const float RANGE_BUFFER   = 1.2f;  // return to Pathfind if > rng * buffer

    // ── Public API ──────────────────────────────────────────────────────────────

    public void Initialize(UnitDataSO data, int team, UnitMovement movement, ICombatant self)
    {
        _data     = data;
        _team     = team;
        _movement = movement;
        _self     = self;
    }

    public TankAIState CurrentState => _state;

    // ── Update loop ─────────────────────────────────────────────────────────────

    void Update()
    {
        if (_data == null) return;

        switch (_state)
        {
            case TankAIState.Search:   TickSearch();   break;
            case TankAIState.Priority: TickPriority(); break;
            case TankAIState.Pathfind: TickPathfind(); break;
            case TankAIState.Attack:   TickAttack();   break;
        }
    }

    // ── SEARCH ──────────────────────────────────────────────────────────────────

    void TickSearch()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f) return;
        _scanTimer = SCAN_INTERVAL;

        // Scan for the nearest visible enemy tank (detection cone + line-of-sight).
        ICombatant bestTank     = null;
        float      bestTankDist = float.MaxValue;

        foreach (var tc in FindObjectsByType<TankCombatant>(FindObjectsSortMode.None))
        {
            if (tc.Team == _team || tc.IsDead) continue;
            if (!IsInDetectionCone(tc.transform))   continue;
            if (!HasLineOfSight(tc.transform))       continue;
            float d = Vector3.Distance(transform.position, tc.Transform.position);
            if (d < bestTankDist) { bestTankDist = d; bestTank = tc; }
        }

        if (bestTank != null)
        {
            _unitTarget  = bestTank;
            _buildTarget = null;
            _state       = TankAIState.Priority;
            return;
        }

        // No visible tanks — look for enemy objectives (always globally visible).
        ObjectiveTarget bestObj     = null;
        float           bestObjDist = float.MaxValue;

        foreach (var o in FindObjectsByType<ObjectiveTarget>(FindObjectsSortMode.None))
        {
            if (o.Team == _team || !o.IsAlive) continue;
            float d = Vector3.Distance(transform.position, o.transform.position);
            if (d < bestObjDist) { bestObjDist = d; bestObj = o; }
        }

        if (bestObj != null)
        {
            _buildTarget = bestObj;
            _unitTarget  = null;
            Debug.Log($"[BATTLE] {name}: no enemy tanks detected — advancing on {bestObj.name}");
            _state = TankAIState.Priority;
        }
    }

    // ── PRIORITY ─────────────────────────────────────────────────────────────────

    void TickPriority()
    {
        // Rule 1: fight any visible enemy tank first.
        ICombatant bestTank     = null;
        float      bestTankDist = float.MaxValue;

        foreach (var tc in FindObjectsByType<TankCombatant>(FindObjectsSortMode.None))
        {
            if (tc.Team == _team || tc.IsDead) continue;
            if (!IsInDetectionCone(tc.transform)) continue;
            if (!HasLineOfSight(tc.transform))    continue;
            float d = Vector3.Distance(transform.position, tc.Transform.position);
            if (d < bestTankDist) { bestTankDist = d; bestTank = tc; }
        }

        if (bestTank != null)
        {
            _unitTarget  = bestTank;
            _buildTarget = null;

            if (_unitTarget != _lastRolledTarget)
            {
                _lastRolledTarget = _unitTarget;
                RollApproachStyle();
            }

            string side = _flankSide > 0 ? "right" : "left";
            Debug.Log($"[BATTLE] {name}: engaging {bestTank.Transform.name} — {_approach} ({side})");
            _state = TankAIState.Pathfind;
            return;
        }

        // Rule 2: no tanks visible — attack nearest enemy objective.
        ObjectiveTarget bestObj     = null;
        float           bestObjDist = float.MaxValue;

        foreach (var o in FindObjectsByType<ObjectiveTarget>(FindObjectsSortMode.None))
        {
            if (o.Team == _team || !o.IsAlive) continue;
            float d = Vector3.Distance(transform.position, o.transform.position);
            if (d < bestObjDist) { bestObjDist = d; bestObj = o; }
        }

        if (bestObj != null)
        {
            _buildTarget = bestObj;
            _unitTarget  = null;
            Debug.Log($"[BATTLE] {name}: all enemy tanks eliminated — advancing on {bestObj.name}");
            _state = TankAIState.Pathfind;
            return;
        }

        _state = TankAIState.Search;
    }

    // ── PATHFIND ─────────────────────────────────────────────────────────────────

    void TickPathfind()
    {
        if (_unitTarget != null)
        {
            if ((_unitTarget as UnityEngine.Object) == null || _unitTarget.IsDead)
            {
                Debug.Log($"[BATTLE] {name}: target lost — scanning for next");
                _unitTarget = null; _scanTimer = 0f; _state = TankAIState.Search; return;
            }

            float dist = Vector3.Distance(transform.position, _unitTarget.Transform.position);
            if (dist <= _data.rng) { _attackTimer = 0f; _state = TankAIState.Attack; return; }

            _movement.SetDestination(UnitApproachPosition());
        }
        else if (_buildTarget != null)
        {
            if (!_buildTarget.IsAlive)
            {
                Debug.Log($"[BATTLE] {name}: {_buildTarget.name} destroyed — scanning");
                _buildTarget = null; _scanTimer = 0f; _state = TankAIState.Search; return;
            }

            // Find closest walkable point near the objective; walled compounds may
            // prevent reaching the centre, so attack from the closest reachable spot.
            Vector3 objPos = _buildTarget.transform.position;
            Vector3 dest   = objPos;
            if (NavMesh.SamplePosition(objPos, out NavMeshHit hit, 30f, NavMesh.AllAreas))
                dest = hit.position;

            float distToObj  = Vector3.Distance(transform.position, objPos);
            float distToDest = Vector3.Distance(transform.position, dest);

            bool inRange  = distToObj  <= _data.rng;
            bool atWall   = distToDest < 1.5f;
            if (inRange || atWall) { _attackTimer = 0f; _state = TankAIState.Attack; return; }

            _movement.SetDestination(dest);
        }
        else
        {
            _state = TankAIState.Search;
        }
    }

    // ── ATTACK ───────────────────────────────────────────────────────────────────

    void TickAttack()
    {
        if (_unitTarget != null)
        {
            if ((_unitTarget as UnityEngine.Object) == null || _unitTarget.IsDead)
            {
                Debug.Log($"[BATTLE] {name}: enemy tank DESTROYED — scanning for next target");
                _unitTarget = null;
                _scanTimer  = 0f;
                _state      = TankAIState.Search;
                return;
            }

            float dist = Vector3.Distance(transform.position, _unitTarget.Transform.position);

            // Heavy detection cone: lose lock if target exits the forward arc at distance.
            if (!IsInDetectionCone(_unitTarget.Transform) && dist > _data.rng * CONE_DROP_MULT)
            {
                Debug.Log($"[BATTLE] {name}: {_unitTarget.Transform.name} left detection cone — lost lock");
                _unitTarget = null;
                _scanTimer  = 0f;
                _state      = TankAIState.Search;
                return;
            }

            if (dist > _data.rng * RANGE_BUFFER) { _state = TankAIState.Pathfind; return; }

            _movement.Stop();
            _movement.FaceToward(_unitTarget.Transform.position);

            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                float dmg = CalcUnitDamage(_unitTarget);
                _unitTarget.Health.TakeDamage(dmg);
                string arc = HitArc(_unitTarget.Transform);
                float hp   = _unitTarget.Health.Current;
                float hpMax = _unitTarget.Health.Max;
                Debug.Log($"[BATTLE] {name} → {_unitTarget.Transform.name}: {dmg:F0} dmg [{arc}] | HP {hp:F0}/{hpMax:F0}");
                _attackTimer = 1f / Mathf.Max(0.01f, _data.spd);
            }
        }
        else if (_buildTarget != null)
        {
            if (!_buildTarget.IsAlive)
            {
                Debug.Log($"[BATTLE] {name}: {_buildTarget.name} DESTROYED — scanning for next target");
                _buildTarget = null;
                _scanTimer   = 0f;
                _state       = TankAIState.Search;
                return;
            }

            float dist = Vector3.Distance(transform.position, _buildTarget.transform.position);
            // Wide buffer for structures: walls prevent reaching exact rng so only
            // return to Pathfind if truly far away.
            if (dist > _data.rng * RANGE_BUFFER * 4f) { _state = TankAIState.Pathfind; return; }

            _movement.Stop();
            _movement.FaceToward(_buildTarget.transform.position);

            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                float pen = _data.pen / Mathf.Max(1f, _buildTarget.Arm);
                float dmg = _data.atk * Mathf.Clamp(pen, 0.1f, 1.0f);
                _buildTarget.Health.TakeDamage(dmg);
                float hp    = _buildTarget.Health.Current;
                float hpMax = _buildTarget.Health.Max;
                Debug.Log($"[BATTLE] {name} → {_buildTarget.name}: {dmg:F0} dmg | HP {hp:F0}/{hpMax:F0}");
                _attackTimer = 1f / Mathf.Max(0.01f, _data.spd);
            }
        }
        else
        {
            _state = TankAIState.Search;
        }
    }

    // ── Approach Style ──────────────────────────────────────────────────────────

    void RollApproachStyle()
    {
        if (HasKw(UnitKeyword.RocketArtillery)) { _approach = ApproachStyle.Direct; return; }

        float total = _data.weightDirect + _data.weightShallowFlank + _data.weightWideFlank;
        if (total <= 0f) { _approach = ApproachStyle.Direct; return; }

        float roll = Random.value * total;
        if      (roll < _data.weightDirect)                             _approach = ApproachStyle.Direct;
        else if (roll < _data.weightDirect + _data.weightShallowFlank) _approach = ApproachStyle.ShallowFlank;
        else                                                            _approach = ApproachStyle.WideFlank;

        _flankSide = Random.value > 0.5f ? 1 : -1;
        // No fixed staging point — WideFlank destination is computed dynamically
        // every frame in UnitApproachPosition so the path tracks target movement.
    }

    // Destination is recomputed every Pathfind tick so flanking paths adjust
    // continuously as both tanks move.  NavMesh routes around obstacles.
    Vector3 UnitApproachPosition()
    {
        if (HasKw(UnitKeyword.RocketArtillery))
        {
            Vector3 toSelf = (transform.position - _unitTarget.Transform.position).normalized;
            return _unitTarget.Transform.position + toSelf * (_data.rng * 0.9f);
        }

        Vector3 toTarget = (_unitTarget.Transform.position - transform.position).normalized;
        Vector3 right    = Vector3.Cross(Vector3.up, toTarget).normalized * _flankSide;
        float   closeIn  = _data.rng * 0.85f;

        switch (_approach)
        {
            case ApproachStyle.Direct:
                // Straight charge to just inside attack range.
                return _unitTarget.Transform.position - toTarget * closeIn;

            case ApproachStyle.ShallowFlank:
                // ~45° oblique approach from the chosen side.
                return _unitTarget.Transform.position - (toTarget + right).normalized * closeIn;

            case ApproachStyle.WideFlank:
                // Target a position 90° off the current line-of-sight at attack range.
                // Continuously updated each frame — NavMesh handles obstacle routing.
                return _unitTarget.Transform.position + right * closeIn;

            default:
                return _unitTarget.Transform.position - toTarget * closeIn;
        }
    }

    // ── Detection Cone ──────────────────────────────────────────────────────────

    bool IsInDetectionCone(Transform t)
    {
        if (_data.detectionAngle >= 360f) return true;
        float angle = Vector3.Angle(transform.forward, (t.position - transform.position).normalized);
        return angle <= _data.detectionAngle * 0.5f;
    }

    // ── Line-of-Sight ───────────────────────────────────────────────────────────

    // Returns false if a rock, tree, or wall blocks the view to the target.
    bool HasLineOfSight(Transform target)
    {
        Vector3 origin    = transform.position + Vector3.up * 0.5f;
        Vector3 targetPos = target.position    + Vector3.up * 0.5f;
        Vector3 dir       = targetPos - origin;
        float   dist      = dir.magnitude;

        if (Physics.Raycast(origin, dir.normalized, out RaycastHit hit, dist))
        {
            Transform hitRoot = hit.transform.root;
            // Not blocked by the target itself or by any tank/objective
            if (hitRoot == target.root)                                          return true;
            if (hitRoot.GetComponentInChildren<TankCombatant>()  != null)        return true;
            if (hitRoot.GetComponentInChildren<ObjectiveTarget>() != null)        return true;
            // Hit solid cover (rock, tree, wall)
            return false;
        }
        return true;
    }

    // ── Damage ──────────────────────────────────────────────────────────────────

    float CalcUnitDamage(ICombatant target)
    {
        float arm = target.EffectiveArm(transform.position);

        if (HasKw(UnitKeyword.ArmorPiercer))
            arm = Mathf.Max(0f, arm - _data.armorPierceIgnore);

        float atk = _data.atk;
        if (HasKw(UnitKeyword.Aggressive) && AllyAlsoTargeting(target))
            atk *= 1f + _data.aggressiveAtkBonus;

        float damage;
        if (HasKw(UnitKeyword.RocketArtillery))
        {
            float heFactor = arm * 0.35f;
            float heRatio  = _data.pen / Mathf.Max(1f, heFactor);
            damage = atk * Mathf.Clamp(heRatio, 0.3f, 1.0f);
            damage = Mathf.Max(atk * 0.3f, damage);
        }
        else
        {
            float penRatio = _data.pen / Mathf.Max(1f, arm);
            damage = atk * Mathf.Clamp(penRatio, 0.1f, 1.0f);
            damage = Mathf.Max(atk * 0.1f, damage);
            if (HasKw(UnitKeyword.Devastating))
                damage = Mathf.Max(atk * _data.devastatingMinFloor, damage);
        }

        damage *= target.DirectionalDamageMult(transform.position);

        return damage;
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    string HitArc(Transform target)
    {
        float angle = Vector3.Angle(target.forward,
                                    transform.position - target.position);
        if      (angle < 45f)  return "Front";
        else if (angle < 135f) return "Side";
        else                   return "Rear";
    }

    bool HasKw(UnitKeyword kw) => (_data.keywords & kw) != 0;

    bool AllyAlsoTargeting(ICombatant target)
    {
        foreach (var ai in FindObjectsByType<TankAI>(FindObjectsSortMode.None))
        {
            if (ai == this || ai._team != _team) continue;
            if (ai._unitTarget == target) return true;
        }
        return false;
    }
}
