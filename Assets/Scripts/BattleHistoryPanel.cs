using System;
using UnityEngine;
using UnityEngine.UI;

// Menu panel that browses past battles from BattleHistoryService. Two modes that
// share one scroll view:
//   • LIST   — one clickable row per battle (outcome, faction, reason, kills,
//              date) + a W/L/D summary header. Click a row to open its log.
//   • DETAIL — the full battle log (every kill) of the selected battle, reached
//              by clicking a list row; BACK returns to the list.
// The panel structure (root, scroll content, title, buttons) is authored in the
// scene; this script only fills the scroll content with transient rows (the §5
// data-driven-entry exception) and swaps the title / button visibility.
public class BattleHistoryPanel : MonoBehaviour
{
    [Header("UI — wire in scene")]
    [SerializeField] GameObject    _root;        // panel, hidden by default
    [SerializeField] RectTransform _content;     // scroll content (VerticalLayoutGroup)
    [SerializeField] Text          _emptyText;   // placeholder when nothing to show
    [SerializeField] Text          _summaryText; // W/L/D header (list) or kill counts (detail)
    [SerializeField] Text          _titleText;   // "BATTLE RECORDS" or a battle's header
    [SerializeField] Button        _openButton;
    [SerializeField] Button        _closeButton;
    [SerializeField] Button        _clearButton; // list mode only
    [SerializeField] Button        _backButton;  // detail mode only (→ list)
    [SerializeField] Font          _entryFont;   // row font (real asset → build-safe)

    [Header("Clear confirmation (wire in scene)")]
    [SerializeField] GameObject _confirmPanel;     // modal shown before wiping history
    [SerializeField] Button     _confirmYesButton; // CONFIRM → clears
    [SerializeField] Button     _confirmNoButton;  // CANCEL  → dismiss

    const string ListTitle = "BATTLE RECORDS";

    static readonly Color Victory = new Color(0.45f, 0.85f, 0.40f);
    static readonly Color Defeat  = new Color(0.95f, 0.40f, 0.36f);
    static readonly Color Draw    = new Color(0.78f, 0.78f, 0.80f);

    Font _font;

    void Awake()
    {
        _font = _entryFont != null ? _entryFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_root != null) _root.SetActive(false);
        if (_confirmPanel != null) _confirmPanel.SetActive(false);
    }

    void OnEnable()
    {
        if (_openButton       != null) _openButton.onClick.AddListener(Show);
        if (_closeButton      != null) _closeButton.onClick.AddListener(Hide);
        if (_clearButton      != null) _clearButton.onClick.AddListener(RequestClear);
        if (_backButton       != null) _backButton.onClick.AddListener(ShowList);
        if (_confirmYesButton != null) _confirmYesButton.onClick.AddListener(ConfirmClear);
        if (_confirmNoButton  != null) _confirmNoButton.onClick.AddListener(CancelClear);
    }

    void OnDisable()
    {
        if (_openButton       != null) _openButton.onClick.RemoveListener(Show);
        if (_closeButton      != null) _closeButton.onClick.RemoveListener(Hide);
        if (_clearButton      != null) _clearButton.onClick.RemoveListener(RequestClear);
        if (_backButton       != null) _backButton.onClick.RemoveListener(ShowList);
        if (_confirmYesButton != null) _confirmYesButton.onClick.RemoveListener(ConfirmClear);
        if (_confirmNoButton  != null) _confirmNoButton.onClick.RemoveListener(CancelClear);
    }

    public void Show()
    {
        if (_root == null) return;
        ShowList();
        var tr = _root.GetComponent<PanelTransition>();
        if (tr != null) tr.Show(); else _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root == null) return;
        var tr = _root.GetComponent<PanelTransition>();
        if (tr != null) tr.Hide(); else _root.SetActive(false);
    }

    // CLEAR is destructive and irreversible → ask first.
    void RequestClear()
    {
        if (_confirmPanel != null) _confirmPanel.SetActive(true);
        else ClearAll();   // no modal wired → fall back to immediate clear
    }

    void ConfirmClear()
    {
        if (_confirmPanel != null) _confirmPanel.SetActive(false);
        ClearAll();
    }

    void CancelClear()
    {
        if (_confirmPanel != null) _confirmPanel.SetActive(false);
    }

    void ClearAll()
    {
        BattleHistoryService.Clear();
        ShowList();
    }

    // ── List mode ─────────────────────────────────────────────────────────────

    void ShowList()
    {
        if (_confirmPanel != null) _confirmPanel.SetActive(false);
        if (_titleText   != null) _titleText.text = ListTitle;
        if (_clearButton != null) _clearButton.gameObject.SetActive(true);
        if (_backButton  != null) _backButton.gameObject.SetActive(false);

        ClearContent();
        var all = BattleHistoryService.All;

        if (_emptyText != null)
        {
            _emptyText.text = "No battles fought yet";
            _emptyText.gameObject.SetActive(all.Count == 0);
        }

        int wins = 0, losses = 0, draws = 0;
        foreach (var r in all)
        {
            if      (r.outcome == (int)BattleHistoryService.Outcome.Victory) wins++;
            else if (r.outcome == (int)BattleHistoryService.Outcome.Defeat)  losses++;
            else                                                             draws++;
        }
        if (_summaryText != null)
            _summaryText.text = all.Count == 0 ? ""
                : $"<color=#73D966>{wins}W</color>   <color=#F26659>{losses}L</color>   <color=#C8C8CC>{draws}D</color>   ·   {all.Count} battles";

        for (int i = 0; i < all.Count; i++) AddBattleRow(all.Count - i, all[i]);
    }

    void AddBattleRow(int number, BattleHistoryService.BattleRecord r)
    {
        // Clickable row: an Image (the button target / hover highlight) with a
        // child Text. Opens this battle's log on click.
        var go = new GameObject("HistoryRow", typeof(RectTransform));
        go.transform.SetParent(_content, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 34f; le.preferredHeight = 34f;

        var img = go.AddComponent<Image>();
        img.color = Color.white;           // overridden by the button colour states
        img.raycastTarget = true;

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var cb = btn.colors;
        cb.normalColor      = new Color(1f, 1f, 1f, 0f);     // invisible at rest
        cb.highlightedColor = new Color(1f, 1f, 1f, 0.10f);  // faint white wash on hover
        cb.pressedColor     = new Color(1f, 1f, 1f, 0.18f);
        cb.selectedColor    = new Color(1f, 1f, 1f, 0f);
        cb.fadeDuration     = 0.08f;
        btn.colors = cb;
        var captured = r;
        btn.onClick.AddListener(() => ShowDetail(captured));

        var t = MakeRowText(go.transform, FormatBattleRow(number, r));
        t.raycastTarget = false;           // let clicks fall through to the button
    }

    string FormatBattleRow(int number, BattleHistoryService.BattleRecord r)
    {
        string label; Color col;
        switch ((BattleHistoryService.Outcome)r.outcome)
        {
            case BattleHistoryService.Outcome.Victory: label = "VICTORY"; col = Victory; break;
            case BattleHistoryService.Outcome.Defeat:  label = "DEFEAT";  col = Defeat;  break;
            default:                                   label = "DRAW";    col = Draw;    break;
        }
        string when = "";
        try { when = new DateTime(r.utcTicks, DateTimeKind.Utc).ToLocalTime().ToString("MMM d  HH:mm"); }
        catch { /* corrupt tick → leave blank */ }

        string hex = ColorUtility.ToHtmlStringRGB(col);
        return $"<color=#777777>{number,2}.</color>  " +
               $"<color=#{hex}>{label,-7}</color>  " +
               $"<color=#BFC4CC>{r.faction}</color>   " +
               $"<color=#9AA0A8>by {Pretty(r.reason)}</color>   " +
               $"<color=#73D966>{r.allyKills}</color>/<color=#F26659>{r.enemyKills}</color> kills   " +
               $"<color=#6B7079>{when}</color>   <color=#5A5F68>›</color>";
    }

    // ── Detail mode (one battle's full log) ────────────────────────────────────

    void ShowDetail(BattleHistoryService.BattleRecord r)
    {
        if (_clearButton != null) _clearButton.gameObject.SetActive(false);
        if (_backButton  != null) _backButton.gameObject.SetActive(true);

        if (_titleText != null)
        {
            string outcome = ((BattleHistoryService.Outcome)r.outcome).ToString().ToUpper();
            string when = "";
            try { when = new DateTime(r.utcTicks, DateTimeKind.Utc).ToLocalTime().ToString("MMM d  HH:mm"); }
            catch { }
            _titleText.text = $"{outcome} · {r.faction} · {when}";
        }
        if (_summaryText != null)
            _summaryText.text = $"<color=#73D966>{r.allyKills}</color> ally   ·   <color=#F26659>{r.enemyKills}</color> enemy kills";

        ClearContent();
        int count = r.kills != null ? r.kills.Count : 0;
        if (_emptyText != null)
        {
            _emptyText.text = "No kills in this battle";
            _emptyText.gameObject.SetActive(count == 0);
        }
        for (int i = 0; i < count; i++) AddKillRow(i + 1, r.kills[i]);
    }

    void AddKillRow(int n, BattleHistoryService.KillEntry k)
    {
        var go = new GameObject("KillRow", typeof(RectTransform));
        go.transform.SetParent(_content, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 30f; le.preferredHeight = 30f;
        MakeRowText(go.transform, BattleHistoryService.FormatKillRow(n, k));
    }

    // ── Shared helpers ─────────────────────────────────────────────────────────

    Text MakeRowText(Transform parent, string richText)
    {
        var tgo = new GameObject("Text", typeof(RectTransform));
        tgo.transform.SetParent(parent, false);
        var rt = (RectTransform)tgo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10f, 0f); rt.offsetMax = new Vector2(-10f, 0f);

        var t = tgo.AddComponent<Text>();
        t.font               = _font;
        t.fontSize           = 18;
        t.fontStyle          = FontStyle.Bold;
        t.alignment          = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Truncate;
        t.supportRichText    = true;
        t.color              = Color.white;
        t.text               = richText;
        return t;
    }

    void ClearContent()
    {
        if (_content == null) return;
        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);
    }

    static string Pretty(string reason) => reason switch
    {
        "HQDestroyed" => "HQ destruction",
        "Timer"       => "FOB count",
        "Casualties"  => "casualties",
        "SuddenDeath" => "sudden death",
        "Draw"        => "draw",
        _             => reason ?? "",
    };
}
