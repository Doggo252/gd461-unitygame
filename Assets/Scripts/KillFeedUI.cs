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
    [SerializeField] Font  _entryFont;          // theme font for rows (fallback: built-in)

    static readonly Color BlueTeam = new Color(0.40f, 0.70f, 1.00f);
    static readonly Color RedTeam  = new Color(1.00f, 0.40f, 0.40f);

    void OnEnable()  { if (_killEvent != null) _killEvent.OnRaised += OnKill; }
    void OnDisable() { if (_killEvent != null) _killEvent.OnRaised -= OnKill; }

    void OnKill(KillInfo info)
    {
        string kc    = TeamHex(info.killerTeam);
        string vc    = TeamHex(info.team);
        string kName = string.IsNullOrEmpty(info.killerName) ? "Unknown" : info.killerName;
        AddEntry($"<color={kc}>{kName}</color> <color=#FFFFFF>►</color> <color={vc}>{info.unitName}</color>");
    }

    static string TeamHex(int team) =>
        team == 0 ? "#66B2FF" :
        team == 1 ? "#FF6666" :
                    "#AAAAAA";

    // ── Entry management ──────────────────────────────────────────────────────
    // Individual kill entries are transient data — created and destroyed at
    // runtime. The static panel structure lives in the scene.

    void AddEntry(string label)
    {
        if (_panel == null) return;

        if (_panel.childCount >= _maxEntries)
            Destroy(_panel.GetChild(_panel.childCount - 1).gameObject);

        var entry         = new GameObject("Entry");
        entry.transform.SetParent(_panel, false);
        entry.transform.SetAsFirstSibling();
        entry.AddComponent<RectTransform>().sizeDelta = new Vector2(0f, 32f);

        var bg            = entry.AddComponent<Image>();
        bg.color          = new Color(0f, 0f, 0f, 0.55f);

        var labelGo       = new GameObject("Label");
        labelGo.transform.SetParent(entry.transform, false);
        var labelRt       = labelGo.AddComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero;
        labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(10f, 2f);
        labelRt.offsetMax = new Vector2(-10f, -2f);

        var text          = labelGo.AddComponent<Text>();
        text.font         = _entryFont != null ? _entryFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize     = 18;
        text.fontStyle    = FontStyle.Bold;
        text.alignment    = TextAnchor.MiddleRight;
        text.color          = Color.white;
        text.supportRichText = true;
        text.text           = label;

        StartCoroutine(FadeEntry(entry, bg, text));
    }

    IEnumerator FadeEntry(GameObject entry, Image bg, Text text)
    {
        yield return new WaitForSeconds(_holdTime);

        Color startBg   = bg.color;
        Color startText = text.color;
        float elapsed   = 0f;

        // The row's box is owned by the layout group, so the slide animates the
        // label INSIDE the row — the entry drifts upward as it fades.
        var labelRt = text != null ? text.GetComponent<RectTransform>() : null;
        Vector2 labelStart = labelRt != null ? labelRt.anchoredPosition : Vector2.zero;
        const float slideUp = 26f;

        while (elapsed < _fadeTime)
        {
            elapsed  += Time.deltaTime;
            float k   = Mathf.Clamp01(elapsed / _fadeTime);
            float a   = 1f - k;
            if (bg      != null) bg.color   = new Color(startBg.r,   startBg.g,   startBg.b,   startBg.a * a);
            if (text    != null) text.color = new Color(startText.r, startText.g, startText.b, a);
            if (labelRt != null) labelRt.anchoredPosition = labelStart + Vector2.up * (slideUp * Mathf.SmoothStep(0f, 1f, k));
            yield return null;
        }

        if (entry != null) Destroy(entry);
    }
}
