using UnityEngine;

// A trigger volume that scales the movement speed of any unit standing in it
// (GDD §2 Terrain — roads faster, rubble/craters/mud slower). Reusable: knows
// nothing about specific units; it just pushes/pops a multiplier on whatever
// UnitMovement enters or leaves.
//
// Setup: add to a GameObject with a trigger Collider covering the patch and
// assign a TerrainTypeSO. A kinematic Rigidbody is added automatically so
// trigger callbacks fire against NavMeshAgent units (which have no Rigidbody).
[RequireComponent(typeof(Collider))]
public class TerrainModifierZone : MonoBehaviour
{
    [SerializeField] TerrainTypeSO _terrain;
    [Tooltip("Used only when no TerrainTypeSO is assigned.")]
    [SerializeField, Range(0.1f, 2f)] float _fallbackMultiplier = 0.6f;

    float Multiplier => _terrain != null ? _terrain.movMultiplier : _fallbackMultiplier;

    void Reset()
    {
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
        EnsureKinematicBody();
    }

    void Awake() => EnsureKinematicBody();

    // Trigger callbacks require a Rigidbody on at least one of the overlapping
    // objects; units don't have one, so the zone carries a kinematic body.
    void EnsureKinematicBody()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
    }

    void OnTriggerEnter(Collider other)
    {
        var mv = other.GetComponentInParent<UnitMovement>();
        if (mv != null) mv.PushTerrain(this, Multiplier);
    }

    void OnTriggerExit(Collider other)
    {
        var mv = other.GetComponentInParent<UnitMovement>();
        if (mv != null) mv.PopTerrain(this);
    }
}
