using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Handles ESC/Pause input: freezes time, shows a pause overlay, and routes
// Resume / Restart / Change Deck / Quit actions.
// Add this to a [PauseManager] GameObject in MainScene; wire the panel and
// buttons in the Inspector.
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

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_actions != null)
            _pauseAction = _actions.FindAction("Player/Pause", throwIfNotFound: false);

        // Enforce starting state — the panel may have been left visible in the
        // Editor while authoring, but at runtime it must always start hidden so
        // the player isn't greeted by a pause overlay on match start.
        if (_pausePanel != null) _pausePanel.SetActive(false);
        Time.timeScale = 1f;
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
        _paused = true;
        Time.timeScale = 0f;
        if (_pausePanel != null) _pausePanel.SetActive(true);
    }

    void Resume()
    {
        _paused = false;
        Time.timeScale = 1f;
        if (_pausePanel != null) _pausePanel.SetActive(false);
    }

    void Restart()
    {
        Time.timeScale = 1f;
        SceneTransitionService.Goto("MainScene");
    }

    void ChangeDeck()
    {
        Time.timeScale = 1f;
        SceneTransitionService.Goto("MenuScene");
    }

    void Quit()
    {
        if (_deckConfig != null) _deckConfig.playerDeck.Clear();
        Time.timeScale = 1f;
        SceneTransitionService.Goto("MenuScene");
    }
}
