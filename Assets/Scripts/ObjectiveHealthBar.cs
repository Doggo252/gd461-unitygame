using UnityEngine;
using UnityEngine.UI;

// World-space health bar above an ObjectiveTarget.
// Billboards toward the camera every LateUpdate so it is always readable
// from any camera angle.  Subscribes/unsubscribes per AGENTS.md Rule 2.
[RequireComponent(typeof(ObjectiveTarget))]
public class ObjectiveHealthBar : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] float _yOffset   = 4f;   // height above structure pivot
    [SerializeField] float _barWidth  = 7f;
    [SerializeField] float _barHeight = 1.2f;

    [Header("Colours")]
    [SerializeField] Color _fullColor = new Color(0.10f, 0.90f, 0.10f);
    [SerializeField] Color _midColor  = new Color(1.00f, 0.80f, 0.00f);
    [SerializeField] Color _lowColor  = new Color(0.90f, 0.10f, 0.05f);

    HealthComponent _health;
    ObjectiveTarget _target;
    Image           _fill;
    Text            _label;
    Transform       _barRoot;

    void Awake()
    {
        _health = GetComponent<HealthComponent>();
        _target = GetComponent<ObjectiveTarget>();
        BuildBar();
    }

    void OnEnable()
    {
        if (_health != null) _health.OnHealthChanged += Refresh;
    }

    void OnDisable()
    {
        if (_health != null) _health.OnHealthChanged -= Refresh;
    }

    void LateUpdate()
    {
        // Canvas front face is in -localZ.  Setting rotation = camera.rotation
        // makes -localZ point back toward the camera — correct billboarding.
        if (_barRoot != null && Camera.main != null)
            _barRoot.rotation = Camera.main.transform.rotation;
    }

    void Refresh(float current, float max)
    {
        if (_fill == null || max <= 0f) return;
        float t          = current / max;
        _fill.fillAmount = t;
        _fill.color      = t > 0.5f
            ? Color.Lerp(_midColor, _fullColor, (t - 0.5f) * 2f)
            : Color.Lerp(_lowColor, _midColor,  t * 2f);

        if (_label != null)
            _label.text = $"{name}  {Mathf.CeilToInt(current)}/{Mathf.CeilToInt(max)}";
    }

    void BuildBar()
    {
        var root = new GameObject("HealthBar");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = new Vector3(0f, _yOffset, 0f);
        _barRoot = root.transform;

        var canvas          = root.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.WorldSpace;
        canvas.sortingOrder = 25;

        var rt        = root.GetComponent<RectTransform>();
        float totalH  = _barHeight + 1.6f;   // bar + label strip
        rt.sizeDelta  = new Vector2(_barWidth, totalH);

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 50f;  // lower = larger text/elements on screen

        // ── Team-coloured border ─────────────────────────────────────────────
        Color teamColor = _target.Team == 0
            ? new Color(0.20f, 0.45f, 1.00f, 1f)
            : new Color(1.00f, 0.25f, 0.25f, 1f);
        AddRect(root, "Border", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, teamColor);

        // ── Dark background ──────────────────────────────────────────────────
        float barFrac = _barHeight / totalH;
        AddRect(root, "BG",
                new Vector2(0f, 0f), new Vector2(1f, barFrac),
                new Vector2(4f, 4f), new Vector2(-4f, -4f),
                new Color(0.06f, 0.06f, 0.06f, 0.95f));

        // ── Fill ─────────────────────────────────────────────────────────────
        var fillGo       = new GameObject("Fill");
        fillGo.transform.SetParent(root.transform, false);
        var fillRt       = fillGo.AddComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, barFrac);
        fillRt.offsetMin = new Vector2(7f,  7f);
        fillRt.offsetMax = new Vector2(-7f, -7f);
        _fill            = fillGo.AddComponent<Image>();
        _fill.type       = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillAmount = 1f;
        _fill.color      = _fullColor;

        // ── Label ────────────────────────────────────────────────────────────
        var labelGo       = new GameObject("Label");
        labelGo.transform.SetParent(root.transform, false);
        var labelRt       = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin = new Vector2(0f, barFrac);
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(4f, 2f);
        labelRt.offsetMax = new Vector2(-4f, -2f);
        _label            = labelGo.AddComponent<Text>();
        _label.font       = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _label.fontSize   = 22;
        _label.fontStyle  = FontStyle.Bold;
        _label.alignment  = TextAnchor.MiddleCenter;
        _label.color      = Color.white;
        _label.text       = name;
    }

    static void AddRect(GameObject parent, string goName,
                        Vector2 anchorMin, Vector2 anchorMax,
                        Vector2 offsetMin, Vector2 offsetMax,
                        Color color)
    {
        var go       = new GameObject(goName);
        go.transform.SetParent(parent.transform, false);
        var rt       = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        go.AddComponent<Image>().color = color;
    }
}
