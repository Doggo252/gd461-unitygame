using UnityEngine;
using UnityEngine.AI;

// Single source of truth for "can a unit be deployed at this world point?" so the
// deploy action (CardDragController) and the visual overlay (DeployZoneOverlay)
// never disagree. Three independent gates:
//   1. Within the team's deploy rectangle (Z bounds + dynamic frontline X).
//   2. On walkable NavMesh within a TIGHT tolerance — this rejects trees/rocks,
//      which are baked as NavMesh holes (a loose tolerance let units snap onto
//      them, the original bug).
//   3. Outside every structure's keep-out radius — FOBs/HQ are NOT carved out of
//      the NavMesh, so they need an explicit no-deploy footprint.
public static class DeployRules
{
    // Tight enough to reject a click inside a tree/rock hole, loose enough to
    // forgive a click a hair off the mesh edge.
    public const float NavTolerance = 0.6f;

    public static bool IsSpawnable(Vector3 p, FrontlineService frontline, int team,
                                   float zMin, float zMax, UnitRegistrySO registry)
    {
        // 1. Deploy rectangle
        if (p.z < zMin || p.z > zMax) return false;
        if (frontline != null)
        {
            if (!frontline.IsValidDeployX(team, p.x)) return false;
        }
        else
        {
            if (team == 0 && p.x > 0f) return false;
            if (team == 1 && p.x < 0f) return false;
        }

        // 2. Must be on walkable NavMesh (area 0) within a tight tolerance.
        if (!NavMesh.SamplePosition(p, out _, NavTolerance, 1 << 0)) return false;

        // 3. Not inside any structure's keep-out footprint.
        if (registry != null)
        {
            foreach (var o in registry.Objectives)
            {
                if (o == null) continue;
                float r = o.KeepOutRadius;
                if (r <= 0f) continue;
                float dx = p.x - o.transform.position.x;
                float dz = p.z - o.transform.position.z;
                if (dx * dx + dz * dz < r * r) return false;
            }
        }

        return true;
    }
}
