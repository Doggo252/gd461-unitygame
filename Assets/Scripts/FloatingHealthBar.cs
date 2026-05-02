using UnityEngine;
using UnityEngine.UI;

// Reusable world-space health bar controller.
// All Canvas/Slider/Image structure is created in the Unity Editor — this script
// only subscribes to HealthComponent events and updates the pre-built UI references.
[RequireComponent(typeof(HealthComponent))]
public class FloatingHealthBar : MonoBehaviour
{
    [Header("UI References — wire in prefab")]
    [SerializeField] Transform _barRoot;   // the Canvas transform to billboard
    [SerializeField] Slider    _slider;
    [SerializeField] Image     _fill;
    [SerializeField] Image     _border;

    [Header("Colours")]
    [SerializeField] Color _fullColor = new Color(0.12f, 0.85f, 0.12f);
    [SerializeField] Color _midColor  = new Color(1.00f, 0.75f, 0.00f);
    [SerializeField] Color _lowColor  = new Color(0.90f, 0.15f, 0.05f);

    HealthComponent _health;
    Camera          _cam;

    void Awake()
    {
        _health = GetComponent<HealthComponent>();
        _cam    = Camera.main;
    }

    void OnEnable()  { if (_health != null) _health.OnHealthChanged += Refresh; }
    void OnDisable() { if (_health != null) _health.OnHealthChanged -= Refresh; }

    void LateUpdate()
    {
        if (_barRoot == null) return;
        if (_cam == null) _cam = Camera.main;
        if (_cam != null)
            _barRoot.rotation = Quaternion.LookRotation(_cam.transform.forward, _cam.transform.up);
    }

    // Called by TankCombatant after spawn to apply team tint to the border.
    public void SetBorderColor(Color c)
    {
        if (_border != null) _border.color = c;
    }

    void Refresh(float current, float max)
    {
        if (_slider == null || max <= 0f) return;
        float t       = current / max;
        _slider.value = t;
        if (_fill != null)
            _fill.color = t > 0.5f
                ? Color.Lerp(_midColor, _fullColor, (t - 0.5f) * 2f)
                : Color.Lerp(_lowColor, _midColor,  t * 2f);
    }
}
