using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// On-screen alerts for structure events, driven by the objective registry:
//   • Ally structure drops below the low-HP threshold → a big warning banner
//     holds for a few seconds, then "genies" — shrinks and flies down to the
//     endangered structure — so your eye lands exactly on the thing in danger.
//   • Any structure destroyed → a big centre announcement pops, holds, fades
//     ("ENEMY RIGHT FOB DESTROYED" gold for progress, red when you lose one).
// The two Text elements are authored in the scene (HUDCanvas/[StructureAlerts]);
// this script only animates them. Uses unscaled time so it works at timeScale 0.
public class StructureAlertsUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] UnitRegistrySO _registry;
    [SerializeField] int   _playerTeam     = 0;
    [SerializeField] float _lowHpFraction  = 0.25f;

    [Header("UI — authored in scene")]
    [SerializeField] Text _warningText;    // low-HP warning (genies to the tower)
    [SerializeField] Text _announceText;   // "X DESTROYED" pop

    [Header("Timing")]
    [SerializeField] float _warningHold   = 2.6f;
    [SerializeField] float _genieDuration = 0.7f;
    [SerializeField] float _announceHold  = 1.6f;
    [SerializeField] float _announceFade  = 0.8f;

    static readonly Color WarnColor    = new Color(1.00f, 0.30f, 0.20f, 1f);
    static readonly Color EnemyDownGold = new Color(1.00f, 0.82f, 0.20f, 1f);
    static readonly Color AllyDownRed   = new Color(1.00f, 0.25f, 0.18f, 1f);

    readonly Dictionary<ObjectiveTarget, Action<float, float>> _hpHandlers    = new();
    readonly Dictionary<ObjectiveTarget, Action>               _deathHandlers = new();
    readonly HashSet<ObjectiveTarget>                          _warned        = new();

    Camera    _cam;
    Coroutine _warnRoutine;
    Coroutine _announceRoutine;

    void Awake()
    {
        _cam = Camera.main;
        if (_warningText  != null) _warningText.gameObject.SetActive(false);
        if (_announceText != null) _announceText.gameObject.SetActive(false);
    }

    // Objectives register in their own OnEnable — subscribe after, in Start.
    void Start()
    {
        if (_registry == null) return;
        foreach (var o in _registry.Objectives)
        {
            if (o == null || o.Health == null || _deathHandlers.ContainsKey(o)) continue;
            var obj = o;
            Action<float, float> hp    = (c, m) => OnHp(obj, c, m);
            Action               death = () => OnDeath(obj);
            obj.Health.OnHealthChanged += hp;
            obj.Health.OnDeath         += death;
            _hpHandlers[obj]    = hp;
            _deathHandlers[obj] = death;
        }
    }

    void OnDisable()
    {
        foreach (var kv in _hpHandlers)
            if (kv.Key != null && kv.Key.Health != null) kv.Key.Health.OnHealthChanged -= kv.Value;
        foreach (var kv in _deathHandlers)
            if (kv.Key != null && kv.Key.Health != null) kv.Key.Health.OnDeath -= kv.Value;
        _hpHandlers.Clear();
        _deathHandlers.Clear();
    }

    // ── Low HP warning + genie ────────────────────────────────────────────────

    void OnHp(ObjectiveTarget o, float current, float max)
    {
        if (o == null || o.Team != _playerTeam || max <= 0f || current <= 0f) return;
        if (current / max > _lowHpFraction || _warned.Contains(o)) return;
        _warned.Add(o);

        if (_warnRoutine != null) StopCoroutine(_warnRoutine);
        _warnRoutine = StartCoroutine(WarnRoutine(o));
    }

    IEnumerator WarnRoutine(ObjectiveTarget target)
    {
        if (_warningText == null) yield break;
        var rt = _warningText.GetComponent<RectTransform>();

        _warningText.text  = $"⚠  {target.name.ToUpper()} UNDER HEAVY FIRE  ⚠";
        _warningText.color = WarnColor;
        _warningText.gameObject.SetActive(true);

        // remember the authored anchored pose so the banner resets next time
        Vector2 homePos   = rt.anchoredPosition;
        Vector3 homeScale = Vector3.one;
        rt.localScale = homeScale;

        // hold (blink gently)
        float t = 0f;
        while (t < _warningHold)
        {
            t += Time.unscaledDeltaTime;
            float blink = 0.75f + 0.25f * Mathf.Sin(t * 9f);
            _warningText.color = new Color(WarnColor.r, WarnColor.g, WarnColor.b, blink);
            yield return null;
        }

        // genie: shrink + fly to the tower's screen position
        if (_cam == null) _cam = Camera.main;
        float g = 0f;
        Vector3 startWorld = rt.position;
        while (g < _genieDuration)
        {
            g += Time.unscaledDeltaTime;
            float k = Mathf.SmoothStep(0f, 1f, g / _genieDuration);
            Vector3 targetScreen = _cam != null
                ? _cam.WorldToScreenPoint(target.transform.position + Vector3.up * 2f)
                : startWorld;
            rt.position   = Vector3.Lerp(startWorld, new Vector3(targetScreen.x, targetScreen.y, 0f), k);
            rt.localScale = Vector3.one * Mathf.Lerp(1f, 0.05f, k);
            _warningText.color = new Color(WarnColor.r, WarnColor.g, WarnColor.b, 1f - k * 0.6f);
            yield return null;
        }

        _warningText.gameObject.SetActive(false);
        rt.anchoredPosition = homePos;
        rt.localScale       = homeScale;
        _warnRoutine = null;
    }

    // ── Destroyed announcement ────────────────────────────────────────────────

    void OnDeath(ObjectiveTarget o)
    {
        if (o == null || _announceText == null) return;
        bool enemyDown = o.Team != _playerTeam;
        if (_announceRoutine != null) StopCoroutine(_announceRoutine);
        _announceRoutine = StartCoroutine(AnnounceRoutine(
            $"{o.name.ToUpper()} DESTROYED",
            enemyDown ? EnemyDownGold : AllyDownRed));
    }

    IEnumerator AnnounceRoutine(string msg, Color color)
    {
        var rt = _announceText.GetComponent<RectTransform>();
        _announceText.text  = msg;
        _announceText.color = color;
        _announceText.gameObject.SetActive(true);

        // pop in (scale overshoot)
        float t = 0f;
        const float popTime = 0.28f;
        while (t < popTime)
        {
            t += Time.unscaledDeltaTime;
            float k = t / popTime;
            rt.localScale = Vector3.one * Mathf.Lerp(0.4f, 1f, 1f - (1f - k) * (1f - k)); // ease-out
            yield return null;
        }
        rt.localScale = Vector3.one;

        yield return new WaitForSecondsRealtime(_announceHold);

        t = 0f;
        while (t < _announceFade)
        {
            t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(t / _announceFade);
            _announceText.color = new Color(color.r, color.g, color.b, a);
            yield return null;
        }
        _announceText.gameObject.SetActive(false);
        _announceRoutine = null;
    }
}
