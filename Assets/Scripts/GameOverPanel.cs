using UnityEngine;
using UnityEngine.UI;

// Listens for MatchEndEventSO and shows a VICTORY/DEFEAT/DRAW overlay.
// The game is paused (Time.timeScale = 0) while the panel is visible.
// Press the Continue button (or click anywhere on the overlay) to return to
// the menu — there is no auto-return timer.
public class GameOverPanel : MonoBehaviour
{
    [SerializeField] MatchEndEventSO _matchEndEvent;
    [SerializeField] string          _menuSceneName = "MenuScene";
    [SerializeField] int             _playerTeam    = 0;

    [Header("UI — wire in scene")]
    [SerializeField] GameObject _root;          // the whole panel (initially disabled)
    [SerializeField] Text       _resultText;    // "VICTORY" / "DEFEAT" / "DRAW"
    [SerializeField] Text       _reasonText;    // "by Tower Destruction" / etc.
    [SerializeField] Text       _statsText;     // W/L tally
    [SerializeField] Text       _continueHint;  // static hint text
    [SerializeField] Button     _clickArea;     // full-panel click target

    [Header("Colours")]
    [SerializeField] Color _winColor  = new Color(0.95f, 0.85f, 0.10f);
    [SerializeField] Color _loseColor = new Color(0.95f, 0.20f, 0.10f);
    [SerializeField] Color _drawColor = new Color(0.70f, 0.70f, 0.70f);

    bool _shown;

    void Awake()
    {
        if (_root != null) _root.SetActive(false);
    }

    void OnEnable()
    {
        if (_matchEndEvent != null) _matchEndEvent.OnRaised += OnMatchEnd;
        if (_clickArea     != null) _clickArea.onClick.AddListener(GotoMenu);
    }

    void OnDisable()
    {
        if (_matchEndEvent != null) _matchEndEvent.OnRaised -= OnMatchEnd;
        if (_clickArea     != null) _clickArea.onClick.RemoveListener(GotoMenu);
    }

    void OnMatchEnd(MatchEndInfo info)
    {
        if (_shown) return;
        _shown = true;
        Show(info);
    }

    void Show(MatchEndInfo info)
    {
        // Pause the game while the overlay is up
        Time.timeScale = 0f;

        if (_root != null) _root.SetActive(true);

        bool isWin  = info.winnerTeam == _playerTeam;
        bool isDraw = info.winnerTeam < 0;

        if (_resultText != null)
        {
            _resultText.text  = isDraw ? "DRAW" : isWin ? "VICTORY" : "DEFEAT";
            _resultText.color = isDraw ? _drawColor : isWin ? _winColor : _loseColor;
        }

        if (_reasonText != null)
            _reasonText.text = ReasonLabel(info.reason);

        if (_statsText != null)
            _statsText.text = $"Wins {MatchStatsService.Wins}    Losses {MatchStatsService.Losses}    Draws {MatchStatsService.Draws}";

        if (_continueHint != null)
            _continueHint.text = "Click anywhere to continue";
    }

    static string ReasonLabel(string r) => r switch
    {
        "HQDestroyed" => "by Headquarters Destruction",
        "SuddenDeath" => "in Sudden Death",
        "Timer"       => "by FOB Count at Time Expiry",
        "Casualties"  => "by Enemy Casualties",
        "Draw"        => "Match Drawn",
        _             => r ?? ""
    };

    void GotoMenu()
    {
        if (!_shown) return;
        _shown = false;
        Time.timeScale = 1f;   // restore time before scene load
        SceneTransitionService.Goto(_menuSceneName);
    }
}
