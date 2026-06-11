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
    // Alpha 0 → transparent renders, so the tank silhouette can sit on top
    // of any UI background (deck-builder cards, sidebar slots, etc.) without
    // a coloured rectangle around it.
    [SerializeField] Color _bgColor      = new Color(0f, 0f, 0f, 0f);

    [Header("Viewport framing")]
    [Tooltip("Global Y shift applied to every tank. Positive = raises tank in frame.")]
    [SerializeField] float _globalRaise = 0.45f;
    [Tooltip("Per-tank fine-tune. Positive = raises tank in frame, negative = lowers.")]
    [SerializeField] List<TankViewportEntry> _yBiasOverrides = new();

    [System.Serializable]
    public struct TankViewportEntry
    {
        public TankType type;
        public float    yBias;   // positive = raise model in frame, negative = lower
    }

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

        // Key light — upper-right-front, warm white. Intensities run hot on
        // purpose: the showcase must read camo patterns and panel detail on a
        // small card thumbnail, so it is lit brighter than the battlefield.
        var kl = new GameObject("[CRL_Key]"); kl.transform.SetParent(stageGO.transform, false);
        kl.transform.localPosition = new Vector3(3f, 5f, -3f);
        var key       = kl.AddComponent<Light>();
        key.type      = LightType.Point;
        key.intensity = 34f;
        key.range     = 25f;
        key.shadows   = LightShadows.None;

        // Fill light — left side, near-white with a cool hint (a strong blue
        // tint muddied the camo colours)
        var fl = new GameObject("[CRL_Fill]"); fl.transform.SetParent(stageGO.transform, false);
        fl.transform.localPosition = new Vector3(-4f, 2f, 1f);
        var fill       = fl.AddComponent<Light>();
        fill.type      = LightType.Point;
        fill.intensity = 16f;
        fill.range     = 25f;
        fill.color     = new Color(0.85f, 0.90f, 1f);
        fill.shadows   = LightShadows.None;

        // Rim light — behind model, gold tint for silhouette separation
        var rl = new GameObject("[CRL_Rim]"); rl.transform.SetParent(stageGO.transform, false);
        rl.transform.localPosition = new Vector3(0f, 3f, 5f);
        var rim       = rl.AddComponent<Light>();
        rim.type      = LightType.Point;
        rim.intensity = 17f;
        rim.range     = 20f;
        rim.color     = new Color(1.0f, 0.88f, 0.55f);
        rim.shadows   = LightShadows.None;

        // Low front bounce — lifts the lower hull / running gear out of shadow
        var bl = new GameObject("[CRL_Bounce]"); bl.transform.SetParent(stageGO.transform, false);
        bl.transform.localPosition = new Vector3(0f, 0.4f, -4f);
        var bounce       = bl.AddComponent<Light>();
        bounce.type      = LightType.Point;
        bounce.intensity = 8f;
        bounce.range     = 18f;
        bounce.color     = new Color(1f, 0.97f, 0.92f);
        bounce.shadows   = LightShadows.None;

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
                    // Wait extra frames so the GPU can upload textures before capture
                    // (critical for high-poly models like AMX-13 with many PBR textures)
                    yield return null;
                    yield return null;
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
        var   bounds      = CombinedBounds(_activeModel);
        float dist        = FitDistance(bounds, _fov, _padding);
        float baseCenterY = bounds.center.y - _pedestal.position.y;   // local Y of model centre

        // Camera sits relative to tank centre; lookAt is shifted DOWNWARD to raise
        // the tank in the rendered frame. Positive raise → look at a lower point →
        // tank appears higher.
        float lookAtY = baseCenterY - (_globalRaise + GetYBias(tankType));

        _cam.transform.localPosition =
            new Vector3(0f,
                        baseCenterY + Mathf.Tan(_pitchDeg * Mathf.Deg2Rad) * dist,
                        -dist);
        _cam.transform.LookAt(_pedestal.position + new Vector3(0f, lookAtY, 0f));

        return rt;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    /// Per-tank Y-axis viewport bias — positive raises the model in frame, negative lowers it.
    float GetYBias(TankType type)
    {
        foreach (var e in _yBiasOverrides)
            if (e.type == type) return e.yBias;
        return 0f;
    }

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
