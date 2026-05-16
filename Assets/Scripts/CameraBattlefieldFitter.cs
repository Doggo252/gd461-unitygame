using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraBattlefieldFitter : MonoBehaviour
{
    [SerializeField] Vector2 _battlefieldSize  = new Vector2(57f, 29f);
    [SerializeField] float   _padding          = 1.05f;

    [Header("HUD Insets (0–1 fraction of screen height)")]
    [SerializeField] float   _hudInsetTop      = 0.08f;  // TopStrip  = 8%
    [SerializeField] float   _hudInsetBottom   = 0.22f;  // BottomTray = 22%

    Camera _cam;
    int _lastW, _lastH;

    void Awake()    { _cam = GetComponent<Camera>(); Fit(); }
    void OnEnable() { Fit(); }

    void Update()
    {
        if (Screen.width != _lastW || Screen.height != _lastH) Fit();
    }

    void Fit()
    {
        if (_cam == null || !_cam.orthographic) return;
        _lastW = Screen.width; _lastH = Screen.height;

        float tiltRad      = transform.eulerAngles.x * Mathf.Deg2Rad;
        float sinTilt      = Mathf.Max(0.001f, Mathf.Sin(tiltRad));
        float aspect       = (float)Screen.width / Screen.height;

        // Camera orthoSize covers the full screen height.
        // The battlefield should fit within the playable vertical fraction
        // (screen minus top/bottom HUD bands). We therefore inflate the raw
        // orthoSize by 1/playableFraction so the battlefield appears
        // centred in the visible middle area.
        float playFrac     = Mathf.Max(0.10f, 1f - _hudInsetTop - _hudInsetBottom);
        float sizeForWidth = (_battlefieldSize.x * 0.5f) / aspect;
        float sizeForDepth = (_battlefieldSize.y * 0.5f) / sinTilt;
        _cam.orthographicSize = Mathf.Max(sizeForWidth, sizeForDepth) * _padding / playFrac;
    }
}
