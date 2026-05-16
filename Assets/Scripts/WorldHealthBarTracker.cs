using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(HealthComponent))]
public class WorldHealthBarTracker : MonoBehaviour
{
    [Header("Service & Prefabs")]
    [SerializeField] HealthBarServiceSO _service;
    [SerializeField] GameObject         _barPrefab;
    [SerializeField] GameObject         _nameLabelPrefab;  // TankNameLabel.prefab — leave null on objectives

    [Header("Anchor")]
    [SerializeField] Transform _anchor;
    [SerializeField] Vector2   _screenOffset = new Vector2(0f, 12f);

    [Header("Display name (used by objectives; tanks use SetNameTag instead)")]
    [SerializeField] string _displayName = "";

    [Header("Colours")]
    [SerializeField] Color _fullColor = new Color(0.12f, 0.85f, 0.12f);
    [SerializeField] Color _midColor  = new Color(1.00f, 0.75f, 0.00f);
    [SerializeField] Color _lowColor  = new Color(0.90f, 0.15f, 0.05f);

    HealthComponent       _health;
    Camera                _cam;

    GameObject            _barInstance;
    WorldHealthBarBinding _binding;
    Vector3               _barWorldOffset;

    GameObject       _labelInstance;
    NameLabelBinding _labelBinding;
    Vector3          _labelWorldOffset;

    void Awake()
    {
        _health = GetComponent<HealthComponent>();
        _cam    = Camera.main;
        if (_anchor == null) _anchor = transform;
    }

    void OnEnable()
    {
        if (_health != null)
        {
            _health.OnHealthChanged += Refresh;
            _health.OnDeath         += HideAll;
        }
    }

    void Start()
    {
        ComputeAdaptiveOffsets();
        SpawnBar();
        SpawnNameLabel();
        if (_health != null && _health.Max > 0f) Refresh(_health.Current, _health.Max);
    }

    void OnDisable()
    {
        if (_health != null)
        {
            _health.OnHealthChanged -= Refresh;
            _health.OnDeath         -= HideAll;
        }
        if (_service != null)
        {
            if (_barInstance   != null) _service.Despawn(_barInstance);
            if (_labelInstance != null) _service.Despawn(_labelInstance);
        }
        _barInstance   = null; _binding      = null;
        _labelInstance = null; _labelBinding = null;
    }

    // ── Adaptive height ──────────────────────────────────────────────────────────

    void ComputeAdaptiveOffsets()
    {
        float topLocal = 1.8f;
        foreach (var r in GetComponentsInChildren<Renderer>(false))
        {
            float h = r.bounds.max.y - transform.position.y;
            if (h > topLocal) topLocal = h;
        }
        _barWorldOffset   = new Vector3(0f, topLocal + 0.30f, 0f);
        _labelWorldOffset = new Vector3(0f, topLocal + 0.85f, 0f);
    }

    // ── Spawn ─────────────────────────────────────────────────────────────────────

    void SpawnBar()
    {
        if (_barInstance != null || _service == null || _barPrefab == null) return;
        _barInstance = _service.Spawn(_barPrefab);
        if (_barInstance == null) return;
        _binding = _barInstance.GetComponent<WorldHealthBarBinding>();
        if (_binding != null && _binding.root == null)
            _binding.root = _barInstance.GetComponent<RectTransform>();
        if (_binding?.nameText != null)
        {
            if (_nameLabelPrefab != null)
            {
                // Tank units have a separate floating nametag — suppress the bar's built-in name.
                _binding.nameText.gameObject.SetActive(false);
            }
            else
            {
                // Objectives/towers have no floating label — show the name inside the bar.
                _binding.nameText.text = string.IsNullOrEmpty(_displayName)
                    ? name.Replace('_', ' ')
                    : _displayName;
            }
        }
    }

    void SpawnNameLabel()
    {
        if (_labelInstance != null || _service == null || _nameLabelPrefab == null) return;
        _labelInstance = _service.Spawn(_nameLabelPrefab);
        if (_labelInstance == null) return;
        _labelBinding = _labelInstance.GetComponent<NameLabelBinding>();
        if (_labelBinding != null && _labelBinding.root == null)
            _labelBinding.root = _labelInstance.GetComponent<RectTransform>();
    }

    // ── Public API ────────────────────────────────────────────────────────────────

    public void SetBorderColor(Color c)
    {
        SpawnBar();
        if (_binding?.border != null) _binding.border.color = c;
    }

    public void SetNameTag(string tankName, Color teamColor)
    {
        SpawnNameLabel();
        if (_labelBinding?.nameText == null) return;
        _labelBinding.nameText.text  = tankName;
        _labelBinding.nameText.color = teamColor;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────────

    void HideAll()
    {
        if (_barInstance   != null) _barInstance.SetActive(false);
        if (_labelInstance != null) _labelInstance.SetActive(false);
    }

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;
        PositionElement(_binding?.root,      _barWorldOffset);
        PositionElement(_labelBinding?.root, _labelWorldOffset);
    }

    void PositionElement(RectTransform rt, Vector3 worldOff)
    {
        if (rt == null) return;
        Vector3 sp = _cam.WorldToScreenPoint(_anchor.position + worldOff);
        bool visible = sp.z > 0f
                    && sp.x > 0f && sp.x < Screen.width
                    && sp.y > 0f && sp.y < Screen.height;
        rt.gameObject.SetActive(visible);
        if (visible)
            rt.position = new Vector3(sp.x + _screenOffset.x, sp.y + _screenOffset.y, 0f);
    }

    void Refresh(float current, float max)
    {
        if (_binding?.slider == null || max <= 0f) return;
        float t = current / max;
        _binding.slider.value = t;
        if (_binding.fill != null)
            _binding.fill.color = t > 0.5f
                ? Color.Lerp(_midColor, _fullColor, (t - 0.5f) * 2f)
                : Color.Lerp(_lowColor, _midColor,  t * 2f);
        if (_binding.hpText != null)
            _binding.hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }
}
