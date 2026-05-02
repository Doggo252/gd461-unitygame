using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Kill feed controller — top-right corner of the screen.
// The Canvas and panel are created in the Unity Editor. This script only
// manages spawning and fading transient kill-entry GameObjects at runtime.
public class KillFeedUI : MonoBehaviour
{
    [Header("Event Channel")]
    [SerializeField] KillEventSO _killEvent;

    [Header("UI References — wire in scene")]
    [SerializeField] RectTransform _panel;   // the VerticalLayoutGroup panel

    [Header("Feed Settings")]
    [SerializeField] int   _maxEntries = 6;
    [SerializeField] float _holdTime   = 2.0f;
    [SerializeField] float _fadeTime   = 1.5f;

    static readonly Color BlueTeam = new Color(0.40f, 0.70f, 1.00f);
    static readonly Color RedTeam  = new Color(1.00f, 0.40f, 0.40f);

    void OnEnable()  { if (_killEvent != null) _killEvent.OnRaised += OnKill; }
    void OnDisable() { if (_killEvent != null) _killEvent.OnRaised -= OnKill; }

    void OnKill(KillInfo info)
    {
        AddEntry(info.unitName, info.team == 0 ? BlueTeam : RedTeam);
    }

    // ── Entry management ──────────────────────────────────────────────────────
    // Individual kill entries are transient data — created and destroyed at
    // runtime. The static panel structure lives in the scene.

    void AddEntry(string label, Color color)
    {
        if (_panel == null) return;

        if (_panel.childCount >= _maxEntries)
            Destroy(_panel.GetChild(_panel.childCount - 1).gameObject);

        var entry         = new GameObject("Entry");
        entry.transform.SetParent(_panel, false);
        entry.transform.SetAsFirstSibling();
        entry.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 32f);

        var bg            = entry.AddComponent<Image>();
        bg.color          = new Color(0f, 0f, 0f, 0.50f);

        var labelGo       = new GameObject("Label");
        labelGo.transform.SetParent(entry.transform, false);
        var labelRt       = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(10f, 2f);
        labelRt.offsetMax = new Vector2(-10f, -2f);

        var text          = labelGo.AddComponent<Text>();
        text.font         = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize     = 20;
        text.fontStyle    = FontStyle.Bold;
        text.alignment    = TextAnchor.MiddleRight;
        text.color        = color;
        text.text         = label;

        StartCoroutine(FadeEntry(entry, bg, text));
    }

    IEnumerator FadeEntry(GameObject entry, Image bg, Text text)
    {
        yield return new WaitForSeconds(_holdTime);

        Color startBg   = bg.color;
        Color startText = text.color;
        float elapsed   = 0f;

        while (elapsed < _fadeTime)
        {
            elapsed  += Time.deltaTime;
            float a   = 1f - Mathf.Clamp01(elapsed / _fadeTime);
            if (bg   != null) bg.color   = new Color(startBg.r,   startBg.g,   startBg.b,   startBg.a * a);
            if (text != null) text.color = new Color(startText.r, startText.g, startText.b, a);
            yield return null;
        }

        if (entry != null) Destroy(entry);
    }
}
