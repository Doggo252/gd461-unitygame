using UnityEngine;
using UnityEngine.UI;

// One pooled floating damage number. Lives on a dedicated high-sort-order
// overlay canvas so it draws above ALL other UI (health bars, kill feed) —
// transient data-driven entries are the allowed §5 exception. Tracks a world
// position each frame, rises, and fades. Driven entirely by CombatTextService.
[RequireComponent(typeof(Text))]
public class FloatingCombatText : MonoBehaviour
{
    Text          _text;
    Outline       _outline;
    RectTransform _rt;
    Camera        _cam;
    Color         _color;
    Vector3       _worldPos;
    float         _life, _maxLife, _riseSpeed;
    float         _screenOffsetX;   // lane offset so rapid hits on one target don't overlap
    int           _baseFontSize;
    bool          _active;
    System.Action<FloatingCombatText> _onDone;

    public bool Active => _active;

    void Awake()
    {
        _rt      = (RectTransform)transform;
        _text    = GetComponent<Text>();
        _outline = GetComponent<Outline>();
        _text.enabled = false;
    }

    // Immediately retire this number (used to clear text on match end).
    public void Hide()
    {
        if (!_active) return;
        _active = false;
        if (_text != null) _text.enabled = false;
        _onDone?.Invoke(this);
    }

    public void Play(string text, Color color, float scale, int baseFontSize, Vector3 worldPos,
                     float life, float rise, Camera cam, System.Action<FloatingCombatText> onDone,
                     float screenOffsetX = 0f)
    {
        _cam           = cam != null ? cam : Camera.main;
        _onDone        = onDone;
        _color         = color;
        _maxLife       = Mathf.Max(0.1f, life);
        _life          = _maxLife;
        _riseSpeed     = rise;
        _worldPos      = worldPos;
        _screenOffsetX = screenOffsetX;
        _baseFontSize  = baseFontSize;

        _text.text     = text;
        _text.fontSize = Mathf.RoundToInt(baseFontSize * scale);
        _active        = true;
        _text.enabled  = true;
        Apply(1f);
        Reposition();
    }

    void Apply(float a)
    {
        _text.color = new Color(_color.r, _color.g, _color.b, a);
        if (_outline != null) _outline.effectColor = new Color(0f, 0f, 0f, 0.9f * a);
    }

    void Reposition()
    {
        if (_cam == null) return;
        Vector3 sp = _cam.WorldToScreenPoint(_worldPos);
        if (sp.z < 0f) { _text.enabled = false; return; }
        _text.enabled = true;
        // lane offset scales with resolution so it matches the scaled canvas
        float ox = _screenOffsetX * (Screen.height / 1080f);
        _rt.position  = new Vector3(sp.x + ox, sp.y, 0f);
    }

    void LateUpdate()
    {
        if (!_active) return;

        _life -= Time.deltaTime;
        if (_life <= 0f)
        {
            _active = false;
            _text.enabled = false;
            _onDone?.Invoke(this);
            return;
        }

        _worldPos += Vector3.up * (_riseSpeed * Time.deltaTime);
        Reposition();

        float t = _life / _maxLife;          // 1 → 0 over lifetime
        Apply(Mathf.SmoothStep(0f, 1f, t));  // fade out
    }
}
