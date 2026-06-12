using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Handles ESC/Pause input: freezes time, shows the pause overlay, and routes
// Resume / Restart / Change Deck / Quit actions.
// Add this to a [PauseManager] GameObject in MainScene; wire the panel and
// buttons in the Inspector.
//
// Pausing is only allowed while the battle is actually running: the pre-battle
// "deploy a unit to begin" hold and the game-over screen both freeze
// Time.timeScale themselves, and un-pausing into those states would corrupt
// them. The rule "pause only when timeScale > 0" covers both for free.
// While paused, all game audio is paused too (AudioListener.pause); UI click
// sounds opt out via AudioSource.ignoreListenerPause so menus stay audible.
public class PauseManager : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] InputActionAsset _actions;

    [Header("Panel")]
    [SerializeField] GameObject _pausePanel;

    [Header("Buttons")]
    [SerializeField] Button _resumeBtn;
    [SerializeField] Button _restartBtn;
    [SerializeField] Button _changeDeckBtn;
    [SerializeField] Button _quitBtn;

    [Header("Data (for Quit — clears deck)")]
    [SerializeField] DeckConfigSO _deckConfig;

    InputAction _pauseAction;
    bool        _paused;

    public bool IsPaused => _paused;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_actions != null)
            _pauseAction = _actions.FindAction("Player/Pause", throwIfNotFound: false);

        // Enforce starting state — the panel may have been left visible in the
        // Editor while authoring, but at runtime it must always start hidden so
        // the player isn't greeted by a pause overlay on match start.
        if (_pausePanel != null) _pausePanel.SetActive(false);
        AudioListener.pause = false;   // defensive: never carry pause across scene loads
    }

    void OnEnable()
    {
        _pauseAction?.Enable();
        if (_pauseAction != null) _pauseAction.performed += OnPauseInput;

        if (_resumeBtn     != null) _resumeBtn.onClick.AddListener(Resume);
        if (_restartBtn    != null) _restartBtn.onClick.AddListener(Restart);
        if (_changeDeckBtn != null) _changeDeckBtn.onClick.AddListener(ChangeDeck);
        if (_quitBtn       != null) _quitBtn.onClick.AddListener(Quit);
    }

    void OnDisable()
    {
        if (_pauseAction != null) _pauseAction.performed -= OnPauseInput;
        _pauseAction?.Disable();

        if (_resumeBtn     != null) _resumeBtn.onClick.RemoveListener(Resume);
        if (_restartBtn    != null) _restartBtn.onClick.RemoveListener(Restart);
        if (_changeDeckBtn != null) _changeDeckBtn.onClick.RemoveListener(ChangeDeck);
        if (_quitBtn       != null) _quitBtn.onClick.RemoveListener(Quit);

        AudioListener.pause = false;
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    void OnPauseInput(InputAction.CallbackContext _)
    {
        if (_paused) Resume();
        else         Pause();
    }

    // ── Actions ───────────────────────────────────────────────────────────────

    void Pause()
    {
        // Only pausable while the battle clock is actually running — the
        // pre-battle hold and the game-over screen own timeScale 0 themselves.
        if (Time.timeScale <= 0f) return;

        _paused = true;
        Time.timeScale      = 0f;
        AudioListener.pause = true;
        ShowPanel(true);
    }

    void Resume()
    {
        if (!_paused) return;
        _paused = false;
        Time.timeScale      = 1f;
        AudioListener.pause = false;
        ShowPanel(false);
    }

    void ShowPanel(bool on)
    {
        if (_pausePanel == null) return;
        var tr = _pausePanel.GetComponent<PanelTransition>();
        if (tr != null) { if (on) tr.Show(); else tr.Hide(); }
        else _pausePanel.SetActive(on);
    }

    void Restart()
    {
        _paused = false;
        Time.timeScale      = 1f;
        AudioListener.pause = false;
        SceneTransitionService.Goto("MainScene");
    }

    void ChangeDeck()
    {
        _paused = false;
        Time.timeScale      = 1f;
        AudioListener.pause = false;
        // Ask the menu to open straight into the deck builder, not the main menu.
        MainMenuController.OpenDeckBuilderOnLoad = true;
        SceneTransitionService.Goto("MenuScene");
    }

    void Quit()
    {
        if (_deckConfig != null) _deckConfig.playerDeck.Clear();
        _paused = false;
        Time.timeScale      = 1f;
        AudioListener.pause = false;
        SceneTransitionService.Goto("MenuScene");
    }
}
