using UnityEngine;
using UnityEngine.UI;

// Reacts to HealthComponent.OnDeath by tinting all child Renderers to a
// charred/ruined look. Does NOT procedurally create or parent any objects;
// it only updates colour values on pre-existing renderers (AGENTS.md §5).
[RequireComponent(typeof(HealthComponent))]
public class StructureDestructionVisual : MonoBehaviour
{
    [Header("Ruin Tint")]
    [SerializeField] Color _ruinColor     = new Color(0.12f, 0.10f, 0.08f); // dark charcoal
    [SerializeField] float _ruinEmission  = 0.04f; // faint glow from embers

    [Header("Destroyed Label")]
    [SerializeField] GameObject _destroyedLabelPrefab; // screen-space "DESTROYED" label (overlay canvas in prefab)
    [SerializeField] Vector3    _labelLocalOffset = new Vector3(0f, 2.5f, 0f);
    [Tooltip("Match-end channel — the DESTROYED stamp is in-match feedback and is hidden when the result screen appears.")]
    [SerializeField] MatchEndEventSO _matchEndEvent;

    HealthComponent _health;
    Renderer[]      _renderers;
    Camera          _cam;
    GameObject      _labelInstance; // spawned DESTROYED stamp (whole clone)
    RectTransform   _labelRect;     // its Text rect; positioned in screen space each frame
    bool            _matchEnded;

    void Awake()
    {
        _health    = GetComponent<HealthComponent>();
        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
        _cam       = Camera.main;
    }

    void OnEnable()
    {
        if (_health != null) _health.OnDeath += ApplyRuinVisual;
        if (_matchEndEvent != null) _matchEndEvent.OnRaised += OnMatchEnd;
    }

    void OnDisable()
    {
        if (_health != null) _health.OnDeath -= ApplyRuinVisual;
        if (_matchEndEvent != null) _matchEndEvent.OnRaised -= OnMatchEnd;
    }

    // The DESTROYED stamp belongs to the live battle; clear it so it never floats
    // on top of the VICTORY/DEFEAT result screen.
    void OnMatchEnd(MatchEndInfo _)
    {
        _matchEnded = true;
        if (_labelInstance != null) _labelInstance.SetActive(false);
    }

    void ApplyRuinVisual()
    {
        foreach (var r in _renderers)
        {
            if (r == null) continue;

            // Work on a copy so we don't dirty the shared asset.
            var mat = r.material;

            if      (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", _ruinColor);
            else if (mat.HasProperty("_Color"))       mat.SetColor("_Color",     _ruinColor);

            // Light emission so the ruin glows faintly.
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", _ruinColor * _ruinEmission);
            }
        }

        // Spawn the "DESTROYED" label (prefab-driven; AGENTS §5 compliant). The
        // prefab uses a screen-space-overlay canvas and we track the structure in
        // screen space, so the stamp draws ABOVE the screen-space health bars —
        // a world-space label always loses to a screen-space-overlay canvas, which
        // is why it used to disappear behind the bar.
        if (_destroyedLabelPrefab != null && _labelInstance == null && !_matchEnded)
        {
            _labelInstance = Instantiate(_destroyedLabelPrefab);
            var txt = _labelInstance.GetComponentInChildren<Text>(true);
            if (txt != null) _labelRect = txt.rectTransform;
            PositionLabel();
        }
    }

    void LateUpdate()
    {
        if (_labelRect != null && !_matchEnded) PositionLabel();
    }

    void PositionLabel()
    {
        if (_matchEnded) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null || _labelRect == null) return;
        Vector3 sp = _cam.WorldToScreenPoint(transform.position + _labelLocalOffset);
        bool visible = sp.z > 0f;
        if (_labelRect.gameObject.activeSelf != visible) _labelRect.gameObject.SetActive(visible);
        if (visible) _labelRect.position = new Vector3(sp.x, sp.y, 0f);
    }
}
