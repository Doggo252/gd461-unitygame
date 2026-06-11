using System.Collections.Generic;
using UnityEngine;

// Visualises the valid deployment zone while the player drags a card:
//   • submesh 0 — translucent green FILL over every spawnable grid cell
//   • submesh 1 — thick, dark OUTLINE along the zone boundary (drawn inside the
//     edge cells), so trees/structures/river read as crisp holes in the zone.
// Driven by the same DeployRules as the deploy action, so it can never lie.
// While visible it rebuilds a few times a second — the frontline expands live
// as friendly tanks push forward.
//
// The mesh is a world-space grid; keep this GameObject at the origin with
// identity rotation/scale. Assign TWO materials on the MeshRenderer:
// element 0 = fill (light green, low alpha), element 1 = border (dark, opaque).
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class DeployZoneOverlay : MonoBehaviour
{
    [SerializeField] FrontlineService _frontline;
    [SerializeField] UnitRegistrySO   _registry;
    [SerializeField] int   _team     = 0;
    [SerializeField] float _zMin     = -14f;
    [SerializeField] float _zMax     =  14f;
    [SerializeField] float _cellSize = 1.4f;
    [SerializeField] float _y        = 0.06f;   // sit just above the ground
    [SerializeField] float _borderThickness = 0.5f;
    [Tooltip("Seconds between live rebuilds while visible (frontline expansion).")]
    [SerializeField] float _refreshInterval = 0.25f;

    MeshFilter   _mf;
    MeshRenderer _mr;
    Mesh         _mesh;
    float        _refreshTimer;
    readonly List<Vector3> _verts       = new();
    readonly List<int>     _fillTris    = new();
    readonly List<int>     _borderTris  = new();

    void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
        _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _mr.receiveShadows    = false;
        _mesh = new Mesh { name = "DeployZoneMesh" };
        _mesh.MarkDynamic();
        _mf.sharedMesh = _mesh;
        _mr.enabled = false;
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
    }

    public void Show() { Rebuild(); _refreshTimer = _refreshInterval; _mr.enabled = true; }
    public void Hide() { _mr.enabled = false; }

    // Live refresh while shown — the frontline moves with the furthest ally.
    void Update()
    {
        if (!_mr.enabled) return;
        _refreshTimer -= Time.unscaledDeltaTime;
        if (_refreshTimer > 0f) return;
        _refreshTimer = _refreshInterval;
        Rebuild();
    }

    void Rebuild()
    {
        _verts.Clear();
        _fillTris.Clear();
        _borderTris.Clear();

        float lo, hi;
        if (_team == 0)
        {
            lo = _frontline != null ? _frontline.MapMinX : -28f;
            hi = _frontline != null ? _frontline.GetFrontlineX(0) : 0f;
        }
        else
        {
            lo = _frontline != null ? _frontline.GetFrontlineX(1) : 0f;
            hi = _frontline != null ? _frontline.MapMaxX : 28f;
        }

        // sample the grid once, then emit fill + boundary border
        int nx = Mathf.Max(1, Mathf.CeilToInt((hi - lo) / _cellSize));
        int nz = Mathf.Max(1, Mathf.CeilToInt((_zMax - _zMin) / _cellSize));
        var ok = new bool[nx, nz];
        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            var c = new Vector3(lo + (ix + 0.5f) * _cellSize, _y, _zMin + (iz + 0.5f) * _cellSize);
            if (c.x > hi) continue;
            ok[ix, iz] = DeployRules.IsSpawnable(c, _frontline, _team, _zMin, _zMax, _registry);
        }

        float t = Mathf.Min(_borderThickness, _cellSize * 0.5f);
        for (int ix = 0; ix < nx; ix++)
        for (int iz = 0; iz < nz; iz++)
        {
            if (!ok[ix, iz]) continue;
            float x0 = lo + ix * _cellSize, x1 = Mathf.Min(x0 + _cellSize, hi);
            float z0 = _zMin + iz * _cellSize, z1 = Mathf.Min(z0 + _cellSize, _zMax);

            Quad(_fillTris, x0, z0, x1, z1);

            // boundary edges → inner border strips (darker, thicker)
            bool wOpen = ix == 0      || !ok[ix - 1, iz];
            bool eOpen = ix == nx - 1 || !ok[ix + 1, iz];
            bool sOpen = iz == 0      || !ok[ix, iz - 1];
            bool nOpen = iz == nz - 1 || !ok[ix, iz + 1];
            if (wOpen) Quad(_borderTris, x0,     z0, x0 + t, z1);
            if (eOpen) Quad(_borderTris, x1 - t, z0, x1,     z1);
            if (sOpen) Quad(_borderTris, x0,     z0, x1,     z0 + t);
            if (nOpen) Quad(_borderTris, x0, z1 - t, x1,     z1);
        }

        _mesh.Clear();
        _mesh.subMeshCount = 2;
        _mesh.SetVertices(_verts);
        _mesh.SetTriangles(_fillTris,   0);
        _mesh.SetTriangles(_borderTris, 1);
        _mesh.RecalculateBounds();
        // Re-assert in case something read MeshFilter.mesh and detached a clone.
        if (_mf.sharedMesh != _mesh) _mf.sharedMesh = _mesh;
    }

    void Quad(List<int> tris, float x0, float z0, float x1, float z1)
    {
        int b = _verts.Count;
        _verts.Add(new Vector3(x0, _y, z0));
        _verts.Add(new Vector3(x0, _y, z1));
        _verts.Add(new Vector3(x1, _y, z1));
        _verts.Add(new Vector3(x1, _y, z0));
        tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
        tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
    }
}
