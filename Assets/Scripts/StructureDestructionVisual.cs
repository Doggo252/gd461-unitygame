using UnityEngine;

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
    [SerializeField] GameObject _destroyedLabelPrefab; // optional slanted "DESTROYED" world-space label
    [SerializeField] Vector3    _labelLocalOffset = new Vector3(0f, 2.5f, 0f);

    HealthComponent _health;
    Renderer[]      _renderers;

    void Awake()
    {
        _health    = GetComponent<HealthComponent>();
        _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    void OnEnable()  { if (_health != null) _health.OnDeath += ApplyRuinVisual; }
    void OnDisable() { if (_health != null) _health.OnDeath -= ApplyRuinVisual; }

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

        // Spawn world-space "DESTROYED" label (prefab-driven; AGENTS §5 compliant).
        if (_destroyedLabelPrefab != null)
        {
            var label = Instantiate(_destroyedLabelPrefab, transform);
            label.transform.localPosition = _labelLocalOffset;
        }
    }
}
