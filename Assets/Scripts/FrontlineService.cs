using UnityEngine;

// Computes the dynamic deployment frontline for each team based on
// the furthest-pushed friendly tank. Used by CardDragController (player)
// and EnemyAISummoner (AI) to decide where deployments are valid.
//
// Team 0 (P1, blue) deploys from the LEFT and pushes toward +X.
// Team 1 (P2, red)  deploys from the RIGHT and pushes toward -X.
public class FrontlineService : MonoBehaviour
{
    [Header("Map bounds")]
    [SerializeField] float _mapMinX        = -28f;
    [SerializeField] float _mapMaxX        =  28f;
    [SerializeField] float _riverHalfWidth =   3f; // river spans [-3..+3] around X=0
    [SerializeField] float _pushBuffer     =   0f; // extra units past furthest ally (0 = exact)

    [Header("Source")]
    [SerializeField] UnitRegistrySO _registry;

    public float MapMinX => _mapMinX;
    public float MapMaxX => _mapMaxX;

    // For team 0 (P1): valid X ∈ [_mapMinX, GetFrontlineX(0)]
    // For team 1 (P2): valid X ∈ [GetFrontlineX(1), _mapMaxX]
    //
    // Initial frontline is at each team's bank (one river-half-width away from the centre),
    // so neither team can deploy IN the river by default. The frontline expands forward
    // only when an ally tank crosses past it.
    public float GetFrontlineX(int team)
    {
        if (team == 0)
        {
            // Team 0 deploys from the LEFT. Initial cap: west bank at -riverHalfWidth.
            float furthest = -_riverHalfWidth;
            if (_registry != null)
            {
                foreach (var c in _registry.Combatants)
                {
                    if (c == null || c.IsDead || c.Team != 0) continue;
                    float x = c.Transform.position.x;
                    if (x > furthest) furthest = x;
                }
            }
            return Mathf.Min(_mapMaxX, furthest + _pushBuffer);
        }
        else
        {
            // Team 1 deploys from the RIGHT. Initial cap: east bank at +riverHalfWidth.
            float furthest = _riverHalfWidth;
            if (_registry != null)
            {
                foreach (var c in _registry.Combatants)
                {
                    if (c == null || c.IsDead || c.Team != 1) continue;
                    float x = c.Transform.position.x;
                    if (x < furthest) furthest = x;
                }
            }
            return Mathf.Max(_mapMinX, furthest - _pushBuffer);
        }
    }

    public bool IsValidDeployX(int team, float x)
        => team == 0
            ? x >= _mapMinX && x <= GetFrontlineX(0)
            : x >= GetFrontlineX(1) && x <= _mapMaxX;
}
