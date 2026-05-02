using UnityEngine;
using UnityEngine.AI;

// Four-state autonomous AI loop for tank units.
//
// SEARCH   — polls every 0.5 s for the nearest visible enemy (tank or objective)
//            within the detection cone and with a clear line-of-sight.
// PRIORITY — decides the best target: fight enemy tanks first; attack objectives
//            only once all enemy tanks are eliminated.
// PATHFIND — navigates toward the chosen target using a two-phase approach:
//            flankers first move to a STAGING position on the target's flank/rear
//            (using the target's OWN facing so the staging point doesn't drift as
//            the attacker moves), then close in to attack range.
// ATTACK   — stops, faces target at per-tank turn speed, fires.
//            Validates detection cone for unit targets each tick.
//
// Flanking (GDD §2.1):
//   WideFlank  — staging at ~135° off target's forward (rear quarter), then attack.
//   ShallowFlank — staging at ~45° off target's forward (side), then attack.
//   Direct     — straight charge to attack range.
//
// The heavy's slow turnSpeed means fast flankers can out-rotate it and reach the
// rear arc before the heavy can track them, exploiting the directional ARM system.

[RequireComponent(typeof(UnitMovement))]
public class TankAI : MonoBehaviour
{
    // ── Injected ─────────────────────────────────────────────────────────────────
    UnitDataSO   _data;
    int          _team;
    UnitMovement _movement;
    ICombatant   _self;

    // ── State ────────────────────────────────────────────────────────────────────
    TankAIState     _state = TankAIState.Search;
    ICombatant      _unitTarget;
    ObjectiveTarget _buildTarget;

    // ── Tactical approach (unit combat) ─────────────────────────────────────────
    ApproachStyle _approach;
    int           _flankSide;         // +1 = target's right, -1 = target's left
    bool          _stagingReached;    // true once the flanker is near the staging point
    ICombatant    _lastRolledTarget;

    // ── Timers ───────────────────────────────────────────────────────────────────
    float _scanTimer;
    float _attackTimer;

    const float SCAN_INTERVAL   = 0.5f;
    const float CONE_DROP_MULT  = 1.5f;
    const float RANGE_BUFFER    = 1.2f;
    const float STAGING_RADIUS  = 1.8f;  // arrive within this distance of staging point

    // ── Public API ───────────────────────────────────────────────────────────────

    public void Initialize(UnitDataSO data, int team, UnitMovement movement, ICombatant self)
    {
        _data     = data;
        _team     = team;
        _movement = movement;
        _self     = self;
    }

    public TankAIState CurrentState => _state;

    // ── Update loop ──────────────────────────────────────────────────────────────

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

    // ── SEARCH ───────────────────────────────────────────────────────────────────

    void TickSearch()
    {
        _scanTimer -= Time.deltaTime;
        if (_scanTimer > 0f) return;
        _scanTimer = SCAN_INTERVAL;

        ICombatant bestTank     = null;
        float      bestTankDist = float.MaxValue;

        foreach (var tc in FindObjectsByType<TankCombatant>(FindObjectsSortMode.None))
        {
            if (tc.Team == _team || tc.IsDead) continue;
            float d = Vector3.Distance(transform.position, tc.Transform.position);
            if (d > _data.aggroRange) continue;
            if (!IsInDetectionCone(tc.transform)) continue;
            if (d < bestTankDist) { bestTankDist = d; bestTank = tc; }
        }

        if (bestTank != null)
        {
            _unitTarget  = bestTank;
            _buildTarget = null;
            _state       = TankAIState.Priority;
            return;
        }

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
            Debug.Log($"[AI] {name}: no enemy tanks — advancing on {bestObj.name}");
            _state = TankAIState.Priority;
        }
    }

    // ── PRIORITY ──────────────────────────────────────────────────────────────────

    void TickPriority()
    {
        ICombatant bestTank     = null;
        float      bestTankDist = float.MaxValue;

        foreach (var tc in FindObjectsByType<TankCombatant>(FindObjectsSortMode.None))
        {
            if (tc.Team == _team || tc.IsDead) continue;
            float d = Vector3.Distance(transform.position, tc.Transform.position);
            if (d > _data.aggroRange) continue;
            if (!IsInDetectionCone(tc.transform)) continue;
            if (d < bestTankDist) { bestTankDist = d; bestTank = tc; }
        }

        if (bestTank != null)
        {
            _unitTarget  = bestTank;
            _buildTarget = null;

            if (_unitTarget != _lastRolledTarget)
            {
                _lastRolledTarget = _unitTarget;
                _stagingReached   = false;
                RollApproachStyle();
            }

            string side = _flankSide > 0 ? "right" : "left";
            Debug.Log($"[AI] {name}: engaging {bestTank.Transform.name} — {_approach} ({side})");
            _state = TankAIState.Pathfind;
            return;
        }

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
            _state = TankAIState.Pathfind;
            return;
        }

        _state = TankAIState.Search;
    }

    // ── PATHFIND ──────────────────────────────────────────────────────────────────

    void TickPathfind()
    {
        if (_unitTarget != null)
        {
            if ((_unitTarget as UnityEngine.Object) == null || _unitTarget.IsDead)
            {
                LoseTarget();
                return;
            }

            float dist = Vector3.Distance(transform.position, _unitTarget.Transform.position);

            // Flanking tanks use a two-phase approach:
            //   Phase 1 — navigate to the staging position (side/rear of target).
            //   Phase 2 — once staged, close straight in to attack range.
            if (!_stagingReached && _approach != ApproachStyle.Direct)
            {
                Vector3 staging = StagingPosition();
                float   stagingDist = Vector3.Distance(transform.position, staging);

                // Staging reached, or close enough to the target anyway — go to phase 2.
                if (stagingDist <= STAGING_RADIUS || dist <= _data.rng)
                {
                    _stagingReached = true;
                }
                else
                {
                    _movement.SetDestination(staging);
                    return;
                }
            }

            // Phase 2 (or Direct): move to just inside attack range of the target.
            if (dist <= _data.rng)
            {
                _attackTimer = 0f;
                _state = TankAIState.Attack;
                return;
            }

            _movement.SetDestination(DirectApproachPosition());
        }
        else if (_buildTarget != null)
        {
            if (!_buildTarget.IsAlive)
            {
                _buildTarget = null;
                _scanTimer   = 0f;
                _state       = TankAIState.Search;
                return;
            }

            Vector3 objPos  = _buildTarget.transform.position;
            float   distToObj = Vector3.Distance(transform.position, objPos);

            if (distToObj <= _data.rng * RANGE_BUFFER)
            {
                _attackTimer = 0f;
                _state = TankAIState.Attack;
                return;
            }

            // Navigate to nearest walkable NavMesh point outside the building.
            Vector3 dest = objPos;
            if (NavMesh.SamplePosition(objPos, out NavMeshHit hit, 30f, NavMesh.AllAreas))
                dest = hit.position;
            _movement.SetDestination(dest);
        }
        else
        {
            _state = TankAIState.Search;
        }
    }

    // ── ATTACK ────────────────────────────────────────────────────────────────────

    void TickAttack()
    {
        if (_unitTarget != null)
        {
            if ((_unitTarget as UnityEngine.Object) == null || _unitTarget.IsDead)
            {
                Debug.Log($"[AI] {name}: target DESTROYED — scanning");
                _scanTimer = 0f;
                _unitTarget = null;
                _state = TankAIState.Search;
                return;
            }

            float dist = Vector3.Distance(transform.position, _unitTarget.Transform.position);

            // Lose lock if target exits detection cone AND is well outside weapon range.
            if (!IsInDetectionCone(_unitTarget.Transform) && dist > _data.rng * CONE_DROP_MULT)
            {
                Debug.Log($"[AI] {name}: {_unitTarget.Transform.name} exited detection cone — lost lock");
                LoseTarget();
                return;
            }

            if (dist > _data.rng * RANGE_BUFFER)
            {
                _state = TankAIState.Pathfind;
                return;
            }

            _movement.Stop();
            _movement.FaceToward(_unitTarget.Transform.position, _data.turnSpeed);

            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                float dmg   = CalcUnitDamage(_unitTarget);
                _unitTarget.Health.TakeDamage(dmg);
                string arc  = HitArc(_unitTarget.Transform);
                Debug.Log($"[DMG] {name} → {_unitTarget.Transform.name}: {dmg:F0} [{arc}] | " +
                          $"HP {_unitTarget.Health.Current:F0}/{_unitTarget.Health.Max:F0}");
                _attackTimer = 1f / Mathf.Max(0.01f, _data.spd);
            }
        }
        else if (_buildTarget != null)
        {
            if (!_buildTarget.IsAlive)
            {
                _buildTarget = null;
                _scanTimer   = 0f;
                _state       = TankAIState.Search;
                return;
            }

            float dist = Vector3.Distance(transform.position, _buildTarget.transform.position);
            if (dist > _data.rng * RANGE_BUFFER * 4f) { _state = TankAIState.Pathfind; return; }

            _movement.Stop();
            _movement.FaceToward(_buildTarget.transform.position, _data.turnSpeed);

            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                float pen = _data.pen / Mathf.Max(1f, _buildTarget.Arm);
                float dmg = _data.atk * Mathf.Clamp(pen, 0.1f, 1.0f);
                _buildTarget.Health.TakeDamage(dmg);
                Debug.Log($"[DMG] {name} → {_buildTarget.name}: {dmg:F0} | " +
                          $"HP {_buildTarget.Health.Current:F0}/{_buildTarget.Health.Max:F0}");
                _attackTimer = 1f / Mathf.Max(0.01f, _data.spd);
            }
        }
        else
        {
            _state = TankAIState.Search;
        }
    }

    // ── Approach Positions ────────────────────────────────────────────────────────

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
    }

    // Phase-1 staging position — computed from the TARGET'S OWN facing direction,
    // not the approach vector.  This keeps the staging point anchored to the
    // target's rear/side regardless of where the attacker starts.
    Vector3 StagingPosition()
    {
        Transform tgt     = _unitTarget.Transform;
        Vector3   tgtFwd  = new Vector3(tgt.forward.x, 0f, tgt.forward.z).normalized;
        if (tgtFwd.sqrMagnitude < 0.001f) tgtFwd = Vector3.forward;

        Vector3 tgtRight  = Vector3.Cross(Vector3.up, tgtFwd).normalized * _flankSide;

        // Staging radius is wider than attack range so the tank circles AROUND the
        // target before it enters weapon range and transitions to Attack.
        float stagingRadius = _data.rng * 1.8f;

        switch (_approach)
        {
            case ApproachStyle.WideFlank:
                // Rear quarter: behind and to the side.
                // -fwd * 0.8 + right * 0.7 → ~131° off forward = solid rear arc.
                return tgt.position + (-tgtFwd * 0.8f + tgtRight * 0.7f).normalized * stagingRadius;

            case ApproachStyle.ShallowFlank:
                // Side quarter: 90° off to the flank side.
                return tgt.position + tgtRight * stagingRadius;

            default:
                return DirectApproachPosition();
        }
    }

    // Phase-2 (and Direct) destination: just inside attack range along the
    // current self-to-target vector.  Also used by RocketArtillery to hang back.
    Vector3 DirectApproachPosition()
    {
        if (HasKw(UnitKeyword.RocketArtillery))
        {
            Vector3 toSelf = (transform.position - _unitTarget.Transform.position).normalized;
            return _unitTarget.Transform.position + toSelf * (_data.rng * 0.9f);
        }

        Vector3 toTarget = (_unitTarget.Transform.position - transform.position).normalized;
        return _unitTarget.Transform.position - toTarget * (_data.rng * 0.85f);
    }

    // ── Detection Cone ────────────────────────────────────────────────────────────

    bool IsInDetectionCone(Transform t)
    {
        if (_data.detectionAngle >= 360f) return true;
        Vector3 dir   = (t.position - transform.position);
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return true;
        float angle = Vector3.Angle(new Vector3(transform.forward.x, 0f, transform.forward.z), dir.normalized);
        return angle <= _data.detectionAngle * 0.5f;
    }

    // ── Damage ────────────────────────────────────────────────────────────────────

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
            damage = Mathf.Max(atk * 0.3f, atk * Mathf.Clamp(heRatio, 0.3f, 1.0f));
        }
        else
        {
            float penRatio = _data.pen / Mathf.Max(1f, arm);
            damage = Mathf.Max(atk * 0.1f, atk * Mathf.Clamp(penRatio, 0.1f, 1.0f));
            if (HasKw(UnitKeyword.Devastating))
                damage = Mathf.Max(atk * _data.devastatingMinFloor, damage);
        }

        damage *= target.DirectionalDamageMult(transform.position);
        return damage;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    void LoseTarget()
    {
        _unitTarget     = null;
        _stagingReached = false;
        _scanTimer      = 0f;
        _state          = TankAIState.Search;
    }

    string HitArc(Transform target)
    {
        float angle = Vector3.Angle(target.forward, transform.position - target.position);
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
