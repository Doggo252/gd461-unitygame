using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering.Universal;

// Singleton service. Manages a single orbiting showcase camera for interactive
// 3D tank previews. Call ShowTank() to place a model; use BeginDrag/UpdateDrag
// to orbit it. PreviewRT is the live RenderTexture — assign it to a RawImage.
[DefaultExecutionOrder(-40)]
public class OrbitTankViewer : MonoBehaviour
{
    public static OrbitTankViewer Instance { get; private set; }

    [Header("Data")]
    [SerializeField] CardPrefabRegistrySO _registry;

    [Header("Stage")]
    [SerializeField] float _stageY       = -600f;    // separate from CardModelRenderer stage
    [SerializeField] int   _rtSize       = 512;

    [Header("Orbit")]
    [SerializeField] float _startPitch   = 8f;    // match CardModelRenderer pitch for continuity
    [SerializeField] float _sensitivity  = 0.35f;
    [SerializeField] float _pitchMin     = 5f;
    [SerializeField] float _pitchMax     = 60f;
    [SerializeField] float _padding      = 0.9f;   // zoom in to match card thumbnail
    [SerializeField] float _fov          = 36f;    // must match camera FOV below

    float _distance = 4.2f;   // overwritten per-tank in ShowTank()

    public RenderTexture PreviewRT { get; private set; }

    Camera    _cam;
    Transform _pivot;
    GameObject _model;
    float     _yaw, _pitch, _centerY;
    bool      _active;

    // ── Lifecycle ────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _yaw   = 0f;
        _pitch = _startPitch;

        BuildStage();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (PreviewRT != null) { PreviewRT.Release(); PreviewRT = null; }
    }

    void LateUpdate()
    {
        if (_active) ApplyCameraOrbit();
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    public void ShowTank(TankType type)
    {
        if (_registry == null || !_registry.TryGetPrefab(type, out var prefab)) return;

        if (_model != null) Destroy(_model);

        _model = Instantiate(prefab, _pivot);
        _model.transform.localPosition = Vector3.zero;
        // Use the same deterministic frontal yaw as CardModelRenderer so the orbit
        // starts at the same view the card thumbnail showed.
        float startYaw = CardModelRenderer.FrontalYaw(type);
        _model.transform.localRotation = Quaternion.Euler(0f, startYaw, 0f);

        foreach (var mb  in _model.GetComponentsInChildren<MonoBehaviour>(true)) mb.enabled  = false;
        foreach (var col in _model.GetComponentsInChildren<Collider>(true))      col.enabled = false;
        foreach (var nav in _model.GetComponentsInChildren<NavMeshAgent>(true))  nav.enabled = false;

        // Adaptive camera distance based on model bounds
        var   bounds  = CardModelRenderer.CombinedBounds(_model);
        _distance     = CardModelRenderer.FitDistance(bounds, _fov, _padding);
        _centerY      = bounds.center.y - _pivot.position.y;

        // Model is rotated ~180° so its front faces -Z.
        // Camera at yaw=180° → offset.z = cos(180°)*dist = -dist → camera at -Z → sees front.
        _yaw    = 180f;
        _pitch  = _startPitch;
        _active = true;
        ApplyCameraOrbit();
    }

    public void HideTank()
    {
        _active = false;
        if (_model != null) { Destroy(_model); _model = null; }
    }

    // Called by the UI pointer-drag handler
    public void OnDrag(Vector2 delta)
    {
        _yaw   += delta.x * _sensitivity;
        _pitch  = Mathf.Clamp(_pitch - delta.y * _sensitivity, _pitchMin, _pitchMax);
        ApplyCameraOrbit();
    }

    // ── Stage ────────────────────────────────────────────────────────────────────

    void BuildStage()
    {
        PreviewRT = new RenderTexture(_rtSize, _rtSize, 16, RenderTextureFormat.ARGB32)
        {
            antiAliasing = 2
        };
        PreviewRT.Create();

        var stageGO = new GameObject("[OrbitStage]");
        stageGO.transform.position = new Vector3(200f, _stageY, 0f);  // offset X to avoid CardModelRenderer stage
        _pivot = stageGO.transform;
        DontDestroyOnLoad(stageGO);

        // Key light — upper-right-front, warm white
        var kl = new GameObject("[OrbitLight_Key]");
        kl.transform.SetParent(stageGO.transform, false);
        kl.transform.localPosition = new Vector3(3f, 5f, -3f);
        var key       = kl.AddComponent<Light>();
        key.type      = LightType.Point;
        key.intensity = 22f;
        key.range     = 25f;
        key.shadows   = LightShadows.None;

        // Fill light — left side, cool tint
        var fl = new GameObject("[OrbitLight_Fill]");
        fl.transform.SetParent(stageGO.transform, false);
        fl.transform.localPosition = new Vector3(-4f, 2f, 1f);
        var fill       = fl.AddComponent<Light>();
        fill.type      = LightType.Point;
        fill.intensity = 9f;
        fill.range     = 25f;
        fill.color     = new Color(0.65f, 0.75f, 1f);
        fill.shadows   = LightShadows.None;

        // Rim light — behind model, gold tint for silhouette separation
        var rl = new GameObject("[OrbitLight_Rim]");
        rl.transform.SetParent(stageGO.transform, false);
        rl.transform.localPosition = new Vector3(0f, 3f, 5f);
        var rim       = rl.AddComponent<Light>();
        rim.type      = LightType.Point;
        rim.intensity = 12f;
        rim.range     = 20f;
        rim.color     = new Color(1.0f, 0.88f, 0.55f);
        rim.shadows   = LightShadows.None;

        // Camera
        var camGO = new GameObject("[OrbitCamera]");
        camGO.transform.SetParent(stageGO.transform, false);
        DontDestroyOnLoad(camGO);

        _cam                  = camGO.AddComponent<Camera>();
        _cam.clearFlags       = CameraClearFlags.SolidColor;
        _cam.backgroundColor  = new Color(0.10f, 0.11f, 0.16f, 1f);
        _cam.fieldOfView      = 36f;
        _cam.nearClipPlane    = 0.1f;
        _cam.farClipPlane     = 25f;
        _cam.targetTexture    = PreviewRT;
        _cam.cullingMask      = ~0;
        _cam.enabled          = true;    // always on — live view

        var uacd = camGO.AddComponent<UniversalAdditionalCameraData>();
        uacd.renderType           = CameraRenderType.Base;
        uacd.renderPostProcessing = false;
        uacd.antialiasing         = AntialiasingMode.None;
    }

    void ApplyCameraOrbit()
    {
        float pitchRad = _pitch * Mathf.Deg2Rad;
        float yawRad   = _yaw   * Mathf.Deg2Rad;

        var offset = new Vector3(
            Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
            Mathf.Sin(pitchRad),
            Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
        ) * _distance;

        Vector3 lookTarget = _pivot.position + new Vector3(0f, _centerY * 0.6f, 0f);
        _cam.transform.position = lookTarget + offset;
        _cam.transform.LookAt(lookTarget);
    }
}
