using UnityEngine;
using UnityEngine.UI;

// The victory-screen "BATTLE LOG": lists every kill of the just-finished match.
//
// Build note: this used to subscribe to KillEventSO and accumulate its own list,
// which silently recorded nothing in IL2CPP builds (the log showed "No kills
// this match" even after a bloody match). It now reads the just-finished match's
// log straight from BattleHistoryService (PlayerPrefs-backed, written by
// BattleHistoryRecorder on match end) — PlayerPrefs behaves identically in the
// editor and a player build, so the log is build-safe.
//
// The panel structure (root, scroll view, close button) is authored in the
// scene; this script only fills the scroll content with one transient row per
// kill — the allowed data-driven-entry exception to §5.
public class BattleLogPanel : MonoBehaviour
{
    [Header("UI — wire in scene")]
    [SerializeField] GameObject    _root;       // whole panel, hidden by default
    [SerializeField] RectTransform _content;    // scroll content (VerticalLayoutGroup)
    [SerializeField] Text          _emptyText;  // "No kills this match" placeholder
    [SerializeField] Button        _openButton; // "BATTLE LOG" on the victory screen
    [SerializeField] Button        _closeButton;
    [SerializeField] Font          _entryFont;  // theme font for rows (fallback: built-in)

    Font _font;

    void Awake()
    {
        _font = _entryFont != null ? _entryFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_root != null) _root.SetActive(false);
    }

    void OnEnable()
    {
        if (_openButton  != null) _openButton.onClick.AddListener(Show);
        if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
    }

    void OnDisable()
    {
        if (_openButton  != null) _openButton.onClick.RemoveListener(Show);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
    }

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

        // Most-recent record is the match that just ended (the recorder wrote it
        // on match end, before the player can click BATTLE LOG).
        var all   = BattleHistoryService.All;
        var kills = all.Count > 0 ? all[0].kills : null;
        int count = kills != null ? kills.Count : 0;

        if (_emptyText != null) _emptyText.gameObject.SetActive(count == 0);
        for (int i = 0; i < count; i++) AddRow(i + 1, kills[i]);
    }

    void AddRow(int n, BattleHistoryService.KillEntry k)
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
        t.text               = BattleHistoryService.FormatKillRow(n, k);
    }
}
