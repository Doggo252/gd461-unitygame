using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

// Singleton service that renders live 3D tank models into cached per-TankType
// RenderTextures using an off-screen showcase stage.
//
// Camera distance and height adapt to each model's mesh bounds so every tank
// fills the frame. Yaw is a deterministic ±10° variation around 180° (front-on)
// so each tank looks slightly different but never shows the side or rear.
[DefaultExecutionOrder(-50)]
public class CardModelRenderer : MonoBehaviour
{
    public static CardModelRenderer Instance { get; private set; }

    [Header("Data")]
    [SerializeField] CardPrefabRegistrySO _prefabRegistry;

    [Header("Render Settings")]
    [SerializeField] int   _textureSize  = 256;
    [SerializeField] float _stageY       = -500f;
    [SerializeField] float _pitchDeg     = 8f;      // low pitch = more frontal, less top-down
    [SerializeField] float _fov          = 40f;
    [SerializeField] float _padding      = 0.85f;   // zoom in — tank fills ~85% of frame
    [SerializeField] Color _bgColor      = new Color(0.06f, 0.07f, 0.10f, 1f);

    readonly Dictionary<TankType, RenderTexture> _cache = new();
    readonly Queue<RenderRequest>                _queue = new();

    struct RenderRequest
    {
        public TankType              tankType;
        public Action<RenderTexture> callback;
    }

    Camera    _cam;
    Transform _pedestal;
    bool      _running;
    GameObject _activeModel;

    // ── Lifecycle ────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildStage();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        foreach (var rt in _cache.Values) if (rt != null) rt.Release();
        _cache.Clear();
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    public void RequestRender(TankType tankType, Action<RenderTexture> callback)
    {
        if (_cache.TryGetValue(tankType, out var cached)) { callback?.Invoke(cached); return; }
        _queue.Enqueue(new RenderRequest { tankType = tankType, callback = callback });
        if (!_running) StartCoroutine(ProcessQueue());
    }

    // ── Stage ────────────────────────────────────────────────────────────────────

    void BuildStage()
    {
        var stageGO = new GameObject("[CardRenderStage]");
        stageGO.transform.position = new Vector3(0f, _stageY, 0f);
        _pedestal = stageGO.transform;
        DontDestroyOnLoad(stageGO);

        // Key light — positioned relative to stage
        var kl = new GameObject("[CRL_Key]"); kl.transform.SetParent(stageGO.transform, false);
        kl.transform.localPosition = new Vector3(2f, 4f, -2f);
        var key       = kl.AddComponent<Light>();
        key.type      = LightType.Point;
        key.intensity = 6f;
        key.range     = 18f;
        key.shadows   = LightShadows.None;

        // Fill light (left side, cool tint)
        var fl = new GameObject("[CRL_Fill]"); fl.transform.SetParent(stageGO.transform, false);
        fl.transform.localPosition = new Vector3(-3f, 2f, 1f);
        var fill       = fl.AddComponent<Light>();
        fill.type      = LightType.Point;
        fill.intensity = 3f;
        fill.range     = 18f;
        fill.color     = new Color(0.65f, 0.75f, 1f);
        fill.shadows   = LightShadows.None;

        // Camera — position is updated per-render in PrepareForRender()
        var camGO = new GameObject("[CRL_Camera]"); camGO.transform.SetParent(stageGO.transform, false);
        DontDestroyOnLoad(camGO);

        _cam                 = camGO.AddComponent<Camera>();
        _cam.clearFlags      = CameraClearFlags.SolidColor;
        _cam.backgroundColor = _bgColor;
        _cam.fieldOfView     = _fov;
        _cam.nearClipPlane   = 0.05f;
        _cam.farClipPlane    = 40f;
        _cam.enabled         = false;
        _cam.cullingMask     = ~0;

        var uacd = camGO.AddComponent<UniversalAdditionalCameraData>();
        uacd.renderType           = CameraRenderType.Base;
        uacd.renderPostProcessing = false;
        uacd.antialiasing         = AntialiasingMode.None;
        uacd.requiresDepthTexture = false;
        uacd.requiresColorTexture = false;
    }

    // ── Render queue ─────────────────────────────────────────────────────────────

    IEnumerator ProcessQueue()
    {
        _running = true;
        while (_queue.Count > 0)
        {
            var req = _queue.Dequeue();
            if (!_cache.TryGetValue(req.tankType, out var rt))
            {
                rt = PrepareForRender(req.tankType);
                if (rt != null)
                {
                    _cam.targetTexture = rt;
                    _cam.enabled       = true;
                    yield return null;              // URP renders this frame
                    _cam.enabled       = false;
                    _cam.targetTexture = null;

                    if (_activeModel != null) { Destroy(_activeModel); _activeModel = null; }
                    _cache[req.tankType] = rt;
                }
            }
            req.callback?.Invoke(rt);
            yield return null;
        }
        _running = false;
    }

    // ── Per-render setup ─────────────────────────────────────────────────────────

    RenderTexture PrepareForRender(TankType tankType)
    {
        if (_prefabRegistry == null || !_prefabRegistry.TryGetPrefab(tankType, out var prefab))
        {
            Debug.LogWarning($"[CardModelRenderer] No prefab for {tankType}");
            return null;
        }

        var rt = new RenderTexture(_textureSize, _textureSize, 16, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 1
        };
        rt.Create();

        // Place model — deterministic ±10° frontal yaw unique per tank
        _activeModel = Instantiate(prefab, _pedestal);
        _activeModel.transform.localPosition = Vector3.zero;
        _activeModel.transform.localRotation = Quaternion.Euler(0f, FrontalYaw(tankType), 0f);

        foreach (var mb  in _activeModel.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled  = false;
        foreach (var col in _activeModel.GetComponentsInChildren<Collider>(true))      col.enabled = false;
        foreach (var nav in _activeModel.GetComponentsInChildren<NavMeshAgent>(true))  nav.enabled = false;

        // Adaptive camera: zoom out for big tanks, in for small
        var    bounds  = CombinedBounds(_activeModel);
        float  dist    = FitDistance(bounds, _fov, _padding);
        float  centerY = bounds.center.y - _pedestal.position.y;   // local Y of model centre

        // Camera at -Z (local) looks at model's front face (model rotated ~180° faces -Z)
        _cam.transform.localPosition =
            new Vector3(0f,
                        centerY + Mathf.Tan(_pitchDeg * Mathf.Deg2Rad) * dist,
                        -dist);
        _cam.transform.LookAt(_pedestal.position + new Vector3(0f, centerY, 0f));

        return rt;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// Deterministic yaw between 170° and 190° (front-on ±10°), unique per TankType.
    public static float FrontalYaw(TankType type)
    {
        uint hash = (uint)((int)type * 2654435761u);   // Knuth multiplicative hash
        float t   = (hash % 201) / 200f;               // 0 → 1
        return 175f + t * 10f;                          // 175° → 185° (±5° from straight front)
    }

    /// Combined renderer bounds of a model in world space.
    public static Bounds CombinedBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 2f);
        var b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
        return b;
    }

    /// Camera distance so the model fills the frame (with padding).
    public static float FitDistance(Bounds bounds, float fovDeg, float padding)
    {
        float half   = fovDeg * 0.5f * Mathf.Deg2Rad;
        float extent = Mathf.Max(bounds.extents.x, bounds.extents.y, bounds.extents.z);
        return Mathf.Clamp((extent * padding) / Mathf.Tan(half), 1.5f, 12f);
    }
}
