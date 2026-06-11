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
    UnitDataSO     _data;
    int            _team;
    UnitMovement   _movement;
    ICombatant     _self;
    UnitRegistrySO _registry;

    // ── State ────────────────────────────────────────────────────────────────────
    TankAIState     _state = TankAIState.Search;
    ICombatant      _unitTarget;
    ObjectiveTarget _buildTarget;

    // The FOB/HQ we are currently registered with for defense return-fire.
    ObjectiveTarget _registeredDefense;

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
    const float AIM_TIME        = 0.35f; // minimum settle time before the first shot at a new target

    // ── Public API ───────────────────────────────────────────────────────────────

    public void Initialize(UnitDataSO data, int team, UnitMovement movement, ICombatant self, UnitRegistrySO registry)
    {
        _data     = data;
        _team     = team;
        _movement = movement;
        _self     = self;
        _registry = registry;
    }

    public TankAIState CurrentState    => _state;
    public ICombatant  CurrentUnitTarget => _unitTarget;

    // ── Update loop ──────────────────────────────────────────────────────────────

    void Update()
    {
        if (_data == null) return;

        // The gun reloads while driving/scanning too — TickAttack only counts
        // down while in Attack, so tick it here for every other state. Without
        // this (plus the per-target zeroing that used to happen on entering
        // Attack) a tank dropped beside a cluster fired an instant free shot at
        // EVERY new target and mowed the whole group down in a few frames.
        if (_state != TankAIState.Attack)
            _attackTimer = Mathf.Max(0f, _attackTimer - Time.deltaTime);

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

        var bestTank = FindBestUnitTarget();
        if (bestTank != null)
        {
            _unitTarget  = bestTank;
            _buildTarget = null;
            _state       = TankAIState.Priority;
            return;
        }

        var bestObj = FindBestObjectiveTarget();
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
        // Tower defense: if an enemy is attacking a friendly objective uncontested, prioritize it
        var structureThreat = FindUndefendedStructureAttacker();
        if (structureThreat != null)
        {
            _unitTarget       = structureThreat;
            _buildTarget      = null;
            UnregisterDefense();
            if (_unitTarget != _lastRolledTarget)
            {
                _lastRolledTarget = _unitTarget;
                _stagingReached   = false;
                _approach         = ApproachStyle.Direct;  // no flanking — intercept directly
                _flankSide        = 1;
            }
            Debug.Log($"[AI] {name}: DEFENDING — intercepting {structureThreat.Transform.name} (Direct)");
            _state = TankAIState.Pathfind;
            return;
        }

        var bestTank = FindBestUnitTarget();
        if (bestTank != null)
        {
            _unitTarget  = bestTank;
            _buildTarget = null;
            UnregisterDefense(); // switching to unit combat

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

        var bestObj = FindBestObjectiveTarget();
        if (bestObj != null)
        {
            _buildTarget = bestObj;
            _unitTarget  = null;
            _state = TankAIState.Pathfind;
            return;
        }

        _state = TankAIState.Search;
    }

    // ── Target scanning helpers ───────────────────────────────────────────────────

    ICombatant FindBestUnitTarget()
    {
        ICombatant best     = null;
        float      bestDist = float.MaxValue;
        foreach (var c in _registry.Combatants)
        {
            if (c.Team == _team || c.IsDead) continue;
            float d = Vector3.Distance(transform.position, c.Transform.position);
            if (d > _data.aggroRange || !IsInDetectionCone(c.Transform)) continue;
            if (d < bestDist) { bestDist = d; best = c; }
        }
        return best;
    }

    ObjectiveTarget FindBestObjectiveTarget()
    {
        ObjectiveTarget best     = null;
        float           bestDist = float.MaxValue;
        foreach (var o in _registry.Objectives)
        {
            if (o.Team == _team || !o.IsAlive) continue;
            float d = Vector3.Distance(transform.position, o.transform.position);
            if (d < bestDist) { bestDist = d; best = o; }
        }
        return best;
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
                // Respect the remaining reload AND a short aim settle — never an
                // instant free shot per acquired target.
                _attackTimer = Mathf.Max(_attackTimer, AIM_TIME);
                _state = TankAIState.Attack;
                return;
            }

            _movement.SetDestination(DirectApproachPosition());
        }
        else if (_buildTarget != null)
        {
            if (!_buildTarget.IsAlive)
            {
                UnregisterDefense();
                _buildTarget = null;
                _scanTimer   = 0f;
                _state       = TankAIState.Search;
                return;
            }

            // While advancing on a structure, periodically re-scan for enemy
            // tanks. Without this, two tanks heading to opposite bases would
            // walk straight past each other ignoring the threat.
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = SCAN_INTERVAL;
                var threat = FindBestUnitTarget();
                if (threat != null)
                {
                    _unitTarget       = threat;
                    _buildTarget      = null;
                    _lastRolledTarget = null;        // force fresh approach roll in Priority
                    UnregisterDefense();
                    _state = TankAIState.Priority;
                    return;
                }
            }

            Vector3 objPos  = _buildTarget.transform.position;
            float   distToObj = Vector3.Distance(transform.position, objPos);

            if (distToObj <= _data.rng * RANGE_BUFFER)
            {
                _attackTimer = Mathf.Max(_attackTimer, AIM_TIME);
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
                // Record attacker BEFORE TakeDamage so OnDeath sees the correct name
                // if this shot is the killing blow.
                string attackerDisplayName = _data != null
                    ? (!string.IsNullOrEmpty(_data.tankName)
                        ? _data.tankName
                        : _data.tankType.ToString().Replace('_', ' '))
                    : name;
                _unitTarget.RecordLastAttacker(attackerDisplayName, _team);
                float dmg = CalcUnitDamage(_unitTarget);
                _unitTarget.Health.TakeDamage(dmg);
                string arc  = HitArc(_unitTarget.Transform);
                Debug.Log($"[DMG] {name} → {_unitTarget.Transform.name}: {dmg:F0} [{arc}] | " +
                          $"HP {_unitTarget.Health.Current:F0}/{_unitTarget.Health.Max:F0}");
                CombatFeedback.Raise(new CombatFeedback.Hit {
                    position = _unitTarget.Transform.position,
                    damage   = dmg,
                    arc      = HitArcEnum(_unitTarget.Transform),
                    ricochet = _lastShotRicochet,
                    lethal   = _unitTarget.IsDead });
                (_self as TankCombatant)?.NotifyFired();
                _attackTimer = 1f / Mathf.Max(0.01f, _data.spd);
            }
        }
        else if (_buildTarget != null)
        {
            if (!_buildTarget.IsAlive)
            {
                UnregisterDefense();
                _buildTarget = null;
                _scanTimer   = 0f;
                _state       = TankAIState.Search;
                return;
            }

            // While shelling a structure, periodically check for enemy tanks
            // entering our detection cone. Per GDD §2 tanks should engage other
            // tanks BEFORE structures, so a fresh threat must interrupt this.
            _scanTimer -= Time.deltaTime;
            if (_scanTimer <= 0f)
            {
                _scanTimer = SCAN_INTERVAL;
                var threat = FindBestUnitTarget();
                if (threat != null)
                {
                    _unitTarget       = threat;
                    _buildTarget      = null;
                    _lastRolledTarget = null;
                    UnregisterDefense();
                    _state = TankAIState.Priority;
                    return;
                }
            }

            float dist = Vector3.Distance(transform.position, _buildTarget.transform.position);
            if (dist > _data.rng * RANGE_BUFFER * 4f) { _state = TankAIState.Pathfind; return; }

            RegisterDefense(_buildTarget); // idempotent — safe every frame while attacking
            _movement.Stop();
            _movement.FaceToward(_buildTarget.transform.position, _data.turnSpeed);

            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                bool  isHe = HasKw(UnitKeyword.RocketArtillery) || _data.pen <= 0f;
                float pen  = isHe
                    ? _data.pen / Mathf.Max(1f, _buildTarget.Arm * 0.35f)
                    : _data.pen / Mathf.Max(1f, _buildTarget.Arm);
                float dmg  = isHe
                    ? _data.atk * Mathf.Clamp(pen, 0.3f, 1.0f)
                    : _data.atk * Mathf.Clamp(pen, 0.1f, 1.0f);
                _buildTarget.Health.TakeDamage(dmg);
                Debug.Log($"[DMG] {name} → {_buildTarget.name}: {dmg:F0} | " +
                          $"HP {_buildTarget.Health.Current:F0}/{_buildTarget.Health.Max:F0}");
                CombatFeedback.Raise(new CombatFeedback.Hit {
                    position = _buildTarget.transform.position,
                    damage   = dmg,
                    arc      = CombatFeedback.Arc.Front,  // structures don't have arcs
                    ricochet = !isHe && pen < 1.0f,
                    lethal   = !_buildTarget.IsAlive });
                (_self as TankCombatant)?.NotifyFired();
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
        // HE shells (GDD §5.4): RocketArtillery AND any gun with no PEN stat
        // (Leopard 40/70, R3, M109G). HE damages through overpressure — it has a
        // 30% floor against armour and conceptually cannot "ricochet", so the
        // ricochet flag stays off (these tanks used to read RICOCHET every shot).
        if (HasKw(UnitKeyword.RocketArtillery) || _data.pen <= 0f)
        {
            float heFactor = arm * 0.35f;
            float heRatio  = _data.pen / Mathf.Max(1f, heFactor);
            _lastShotRicochet = false;
            damage = Mathf.Max(atk * 0.3f, atk * Mathf.Clamp(heRatio, 0.3f, 1.0f));
        }
        else
        {
            float penRatio = _data.pen / Mathf.Max(1f, arm);
            _lastShotRicochet = penRatio < 1.0f;
            damage = Mathf.Max(atk * 0.1f, atk * Mathf.Clamp(penRatio, 0.1f, 1.0f));
            if (HasKw(UnitKeyword.Devastating))
                damage = Mathf.Max(atk * _data.devastatingMinFloor, damage);
        }

        damage *= target.DirectionalDamageMult(transform.position);
        return damage;
    }

    // Set by CalcUnitDamage: true when the shell only partially penetrated.
    bool _lastShotRicochet;

    CombatFeedback.Arc HitArcEnum(Transform target)
    {
        float angle = Vector3.Angle(target.forward, transform.position - target.position);
        if      (angle < 45f)  return CombatFeedback.Arc.Front;
        else if (angle < 135f) return CombatFeedback.Arc.Side;
        else                   return CombatFeedback.Arc.Rear;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    void LoseTarget()
    {
        _unitTarget     = null;
        _stagingReached = false;
        _scanTimer      = 0f;
        _state          = TankAIState.Search;
    }

    // ── Defense registration ──────────────────────────────────────────────────

    // Call when we start attacking a FOB/HQ. Idempotent — no-op if already registered.
    void RegisterDefense(ObjectiveTarget t)
    {
        if (_registeredDefense == t) return;
        _registeredDefense?.UnregisterAttacker(_self);
        _registeredDefense = t;
        t?.RegisterAttacker(_self);
    }

    // Call whenever we stop attacking objectives (unit target found, self destroyed, etc.)
    void UnregisterDefense()
    {
        _registeredDefense?.UnregisterAttacker(_self);
        _registeredDefense = null;
    }

    void OnDisable() => UnregisterDefense();

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
        foreach (var ai in _registry.TankAIs)
        {
            if (ai == this || ai._team != _team) continue;
            if (ai._unitTarget == target) return true;
        }
        return false;
    }

    // Returns an enemy combatant that is currently attacking a friendly objective AND
    // no other ally is already targeting it. The closest such attacker within extended
    // aggro range is returned so we don't teleport across the map to defend.
    ICombatant FindUndefendedStructureAttacker()
    {
        ICombatant best     = null;
        float      bestDist = float.MaxValue;

        foreach (var obj in _registry.Objectives)
        {
            if (obj.Team != _team || !obj.IsAlive) continue;

            foreach (var attacker in obj.RegisteredAttackers)
            {
                if (attacker == null || attacker.IsDead) continue;
                if (AllyAlsoTargeting(attacker)) continue;   // already defended

                float d = Vector3.Distance(transform.position, attacker.Transform.position);
                if (d > _data.aggroRange * 2.5f) continue;  // outside extended defense range

                if (d < bestDist) { bestDist = d; best = attacker; }
            }
        }
        return best;
    }
}
