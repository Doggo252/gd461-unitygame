using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Records every kill of the match (same KillEventSO the kill feed listens to) and,
// on demand, lists the full history in a scrollable panel reachable from the
// game-over screen. The panel structure (root, scroll view, close button) is
// authored in the scene; this script only fills the scroll content with one
// transient row per kill — the allowed data-driven-entry exception to §5.
public class BattleLogPanel : MonoBehaviour
{
    [SerializeField] KillEventSO   _killEvent;

    [Header("UI — wire in scene")]
    [SerializeField] GameObject    _root;       // whole panel, hidden by default
    [SerializeField] RectTransform _content;    // scroll content (VerticalLayoutGroup)
    [SerializeField] Text          _emptyText;  // "No kills this match" placeholder
    [SerializeField] Button        _openButton; // "BATTLE LOG" on the victory screen
    [SerializeField] Button        _closeButton;
    [SerializeField] Font          _entryFont;  // theme font for rows (fallback: built-in)

    readonly List<KillInfo> _log = new();
    Font _font;

    void Awake()
    {
        _font = _entryFont != null ? _entryFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_root != null) _root.SetActive(false);
    }

    void OnEnable()
    {
        if (_killEvent   != null) _killEvent.OnRaised += Record;
        if (_openButton  != null) _openButton.onClick.AddListener(Show);
        if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
    }

    void OnDisable()
    {
        if (_killEvent   != null) _killEvent.OnRaised -= Record;
        if (_openButton  != null) _openButton.onClick.RemoveListener(Show);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
    }

    void Record(KillInfo info) => _log.Add(info);

    public void Show()
    {
        if (_root == null) return;
        Rebuild();
        _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    void Rebuild()
    {
        if (_content == null) return;
        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);

        if (_emptyText != null) _emptyText.gameObject.SetActive(_log.Count == 0);

        int n = 1;
        foreach (var info in _log) AddRow(n++, info);
    }

    void AddRow(int n, KillInfo info)
    {
        var go = new GameObject("LogEntry");
        go.transform.SetParent(_content, false);

        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 30f; le.preferredHeight = 30f;

        var t = go.AddComponent<Text>();
        t.font               = _font;
        t.fontSize           = 18;
        t.fontStyle          = FontStyle.Bold;
        t.alignment          = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Truncate;
        t.color              = Color.white;
        t.supportRichText    = true;

        string kc    = TeamHex(info.killerTeam);
        string vc    = TeamHex(info.team);
        string kName = string.IsNullOrEmpty(info.killerName) ? "Unknown" : info.killerName;
        t.text = $"<color=#777777>{n,2}.</color>   <color={kc}>{kName}</color> <color=#FFFFFF>▶</color> <color={vc}>{info.unitName}</color>";
    }

    static string TeamHex(int team) =>
        team == 0 ? "#66B2FF" :
        team == 1 ? "#FF6666" :
                    "#AAAAAA";
}
