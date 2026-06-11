using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Spawns floating damage numbers in response to CombatFeedback.OnHit, surfacing
// the armour/penetration mechanic to the player: plain white for a clean
// frontal hit, "FLANK"/"REAR" for arc bonuses, and "RICOCHET" when a shell only
// partially penetrates. Entries are pooled UI Texts under a dedicated
// high-sort-order overlay canvas (assign _container in the scene) so they draw
// above every other HUD element, including health bars and the kill feed.
public class CombatTextService : MonoBehaviour
{
    public static CombatTextService Instance { get; private set; }

    [Header("Container — child of the [CombatTextCanvas] overlay (sort order 400)")]
    [SerializeField] RectTransform _container;

    [Header("Tuning")]
    [SerializeField] int   _poolSize     = 28;
    [SerializeField] int   _baseFontSize = 34;
    [SerializeField] float _lifetime     = 0.95f;
    [SerializeField] float _riseSpeed    = 2.6f;
    [SerializeField] float _yOffset      = 1.6f;
    [SerializeField] Font  _numberFont;          // theme font (fallback: built-in)

    static readonly Color White  = new Color(1.00f, 1.00f, 1.00f);
    static readonly Color Orange = new Color(1.00f, 0.60f, 0.15f); // flank
    static readonly Color Gold   = new Color(1.00f, 0.82f, 0.20f); // rear
    static readonly Color Red    = new Color(1.00f, 0.35f, 0.28f); // ricochet

    readonly List<FloatingCombatText> _pool = new();
    Camera _cam;
    Font   _font;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _font = _numberFont != null ? _numberFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        for (int i = 0; i < Mathf.Max(4, _poolSize); i++) _pool.Add(Create(i));
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnEnable()  { CombatFeedback.OnHit += OnHit; }
    void OnDisable() { CombatFeedback.OnHit -= OnHit; }

    // Retire every active number at once (match end cleanup).
    public void HideAll()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (_pool[i] != null) _pool[i].Hide();
    }

    FloatingCombatText Create(int i)
    {
        var go = new GameObject($"CombatText_{i}");           // transient entry (§5 exception)
        go.transform.SetParent(_container != null ? (Transform)_container : transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(320f, 110f);
        var t = go.AddComponent<Text>();
        t.font               = _font;
        t.fontStyle          = FontStyle.Bold;
        t.alignment          = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Overflow;
        t.raycastTarget      = false;
        var o = go.AddComponent<Outline>();
        o.effectDistance = new Vector2(2f, -2f);
        return go.AddComponent<FloatingCombatText>();
    }

    FloatingCombatText GetFree()
    {
        for (int i = 0; i < _pool.Count; i++)
            if (!_pool[i].Active) return _pool[i];
        var extra = Create(_pool.Count); // grow if all busy
        _pool.Add(extra);
        return extra;
    }

    void OnHit(CombatFeedback.Hit hit)
    {
        int dmg = Mathf.Max(1, Mathf.RoundToInt(hit.damage));
        string text;
        Color  color;
        float  scale;

        if (hit.ricochet)            { text = $"RICOCHET\n{dmg}"; color = Red;    scale = 0.95f; }
        else if (hit.arc == CombatFeedback.Arc.Rear) { text = $"REAR x2.5\n{dmg}"; color = Gold;   scale = 1.35f; }
        else if (hit.arc == CombatFeedback.Arc.Side) { text = $"FLANK x1.5\n{dmg}"; color = Orange; scale = 1.15f; }
        else                         { text = dmg.ToString();     color = White;  scale = 1.0f;  }

        if (hit.lethal) scale *= 1.25f;

        if (_cam == null) _cam = Camera.main;
        var item = GetFree();
        item.Play(text, color, scale, _baseFontSize, hit.position + Vector3.up * _yOffset,
                  _lifetime, _riseSpeed, _cam, null);
    }
}
