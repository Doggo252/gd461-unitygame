using System;
using UnityEngine;
using UnityEngine.UI;

// Menu panel that lists every past battle from BattleHistoryService. The panel
// structure (root, scroll content, buttons) is authored in the scene; this
// script fills the scroll content with one transient row per record (the §5
// data-driven-entry exception) and toggles the panel via PanelTransition.
public class BattleHistoryPanel : MonoBehaviour
{
    [Header("UI — wire in scene")]
    [SerializeField] GameObject    _root;        // panel, hidden by default
    [SerializeField] RectTransform _content;     // scroll content (VerticalLayoutGroup)
    [SerializeField] Text          _emptyText;   // "No battles yet" placeholder
    [SerializeField] Text          _summaryText; // "Wins X · Losses Y · Draws Z"
    [SerializeField] Button        _openButton;
    [SerializeField] Button        _closeButton;
    [SerializeField] Button        _clearButton;
    [SerializeField] Font          _entryFont;   // row font (real asset → build-safe)

    static readonly Color Victory = new Color(0.45f, 0.85f, 0.40f);
    static readonly Color Defeat  = new Color(0.95f, 0.40f, 0.36f);
    static readonly Color Draw    = new Color(0.78f, 0.78f, 0.80f);

    void Awake()
    {
        if (_entryFont == null) _entryFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_root != null) _root.SetActive(false);
    }

    void OnEnable()
    {
        if (_openButton  != null) _openButton.onClick.AddListener(Show);
        if (_closeButton != null) _closeButton.onClick.AddListener(Hide);
        if (_clearButton != null) _clearButton.onClick.AddListener(ClearAll);
    }

    void OnDisable()
    {
        if (_openButton  != null) _openButton.onClick.RemoveListener(Show);
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Hide);
        if (_clearButton != null) _clearButton.onClick.RemoveListener(ClearAll);
    }

    public void Show()
    {
        if (_root == null) return;
        Rebuild();
        var tr = _root.GetComponent<PanelTransition>();
        if (tr != null) tr.Show(); else _root.SetActive(true);
    }

    public void Hide()
    {
        if (_root == null) return;
        var tr = _root.GetComponent<PanelTransition>();
        if (tr != null) tr.Hide(); else _root.SetActive(false);
    }

    void ClearAll()
    {
        BattleHistoryService.Clear();
        Rebuild();
    }

    void Rebuild()
    {
        if (_content == null) return;
        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);

        var all = BattleHistoryService.All;
        if (_emptyText != null) _emptyText.gameObject.SetActive(all.Count == 0);

        int wins = 0, losses = 0, draws = 0;
        foreach (var r in all)
        {
            if (r.outcome == (int)BattleHistoryService.Outcome.Victory) wins++;
            else if (r.outcome == (int)BattleHistoryService.Outcome.Defeat) losses++;
            else draws++;
        }
        if (_summaryText != null)
            _summaryText.text = all.Count == 0 ? ""
                : $"<color=#73D966>{wins}W</color>   <color=#F26659>{losses}L</color>   <color=#C8C8CC>{draws}D</color>   ·   {all.Count} battles";

        for (int i = 0; i < all.Count; i++) AddRow(all.Count - i, all[i]);
    }

    void AddRow(int number, BattleHistoryService.BattleRecord r)
    {
        var go = new GameObject("HistoryRow");
        go.transform.SetParent(_content, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 34f; le.preferredHeight = 34f;

        var t = go.AddComponent<Text>();
        t.font               = _entryFont;
        t.fontSize           = 18;
        t.fontStyle          = FontStyle.Bold;
        t.alignment          = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow   = VerticalWrapMode.Truncate;
        t.supportRichText    = true;

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
        t.color = Color.white;
        t.text  = $"<color=#777777>{number,2}.</color>  " +
                  $"<color=#{hex}>{label,-7}</color>  " +
                  $"<color=#BFC4CC>{r.faction}</color>   " +
                  $"<color=#9AA0A8>by {Pretty(r.reason)}</color>   " +
                  $"<color=#73D966>{r.allyKills}</color>/<color=#F26659>{r.enemyKills}</color> kills   " +
                  $"<color=#6B7079>{when}</color>";
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
