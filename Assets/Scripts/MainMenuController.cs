using UnityEngine;
using UnityEngine.UI;

// Controls the main menu flow: MainMenu → FactionSelect → DeckBuilder.
// Difficulty is set via the Settings overlay (not a flow step).
// AGENTS §5: all panels pre-built in the Editor; this script only shows/hides them.
public class MainMenuController : MonoBehaviour
{
    [Header("Panels (mutually exclusive)")]
    [SerializeField] GameObject _mainMenuPanel;
    [SerializeField] GameObject _factionSelectPanel;
    [SerializeField] GameObject _deckBuilderPanel;

    [Header("Main Menu Buttons")]
    [SerializeField] Button _playButton;
    [SerializeField] Button _settingsButton;
    [SerializeField] Button _creditsButton;
    [SerializeField] Button _exitButton;

    [Header("Back Buttons (wire in Inspector)")]
    [SerializeField] Button _factionBackButton;      // FactionSelectPanel → MainMenu
    [SerializeField] Button _deckBuilderBackButton;  // DeckBuilderPanel → FactionSelect

    [Header("Credits / Settings overlays (optional)")]
    [SerializeField] GameObject _creditsOverlay;
    [SerializeField] GameObject _settingsOverlay;

    // ── Lifecycle ────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Defensive: CardDetailPanel and OrbitTankViewer use Awake-singletons.
        // If they're left inactive in the Editor while authoring, Awake never
        // fires and their Instance stays null — making single-click on a card
        // silently no-op. Force them active here BEFORE Start() so their
        // Awake/Start run normally.
        foreach (var cdp in FindObjectsOfType<CardDetailPanel>(true))
            if (!cdp.gameObject.activeSelf) cdp.gameObject.SetActive(true);
        foreach (var otv in FindObjectsOfType<OrbitTankViewer>(true))
            if (!otv.gameObject.activeSelf) otv.gameObject.SetActive(true);
    }

    void OnEnable()
    {
        if (_playButton            != null) _playButton.onClick.AddListener(OnPlay);
        if (_settingsButton        != null) _settingsButton.onClick.AddListener(OnSettings);
        if (_creditsButton         != null) _creditsButton.onClick.AddListener(OnCredits);
        if (_exitButton            != null) _exitButton.onClick.AddListener(OnExit);
        if (_factionBackButton     != null) _factionBackButton.onClick.AddListener(GoToMainMenu);
        if (_deckBuilderBackButton != null) _deckBuilderBackButton.onClick.AddListener(GoToFactionSelect);
    }

    void OnDisable()
    {
        if (_playButton            != null) _playButton.onClick.RemoveListener(OnPlay);
        if (_settingsButton        != null) _settingsButton.onClick.RemoveListener(OnSettings);
        if (_creditsButton         != null) _creditsButton.onClick.RemoveListener(OnCredits);
        if (_exitButton            != null) _exitButton.onClick.RemoveListener(OnExit);
        if (_factionBackButton     != null) _factionBackButton.onClick.RemoveListener(GoToMainMenu);
        if (_deckBuilderBackButton != null) _deckBuilderBackButton.onClick.RemoveListener(GoToFactionSelect);
    }

    void Start()
    {
        ShowPanel(_mainMenuPanel);
        if (_creditsOverlay  != null) _creditsOverlay.SetActive(false);
        if (_settingsOverlay != null) _settingsOverlay.SetActive(false);
    }

    // ── Navigation ───────────────────────────────────────────────────────────────

    void OnPlay()     => ShowPanel(_factionSelectPanel);
    void OnSettings() => ToggleOverlay(_settingsOverlay);
    void OnCredits()  => ToggleOverlay(_creditsOverlay);

    void OnExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void GoToDeckBuilder()   => ShowPanel(_deckBuilderPanel);
    public void GoToMainMenu()      => ShowPanel(_mainMenuPanel);
    public void GoToFactionSelect() => ShowPanel(_factionSelectPanel);
    public void CloseSettings()     => SetPanel(_settingsOverlay, false);

    void ShowPanel(GameObject target)
    {
        SetPanel(_mainMenuPanel,      _mainMenuPanel == target);
        SetPanel(_factionSelectPanel, _factionSelectPanel == target);
        SetPanel(_deckBuilderPanel,   _deckBuilderPanel == target);
    }

    // Animate via PanelTransition when one is attached; hard toggle otherwise.
    static void SetPanel(GameObject panel, bool on)
    {
        if (panel == null) return;
        if (panel.activeSelf == on) return;
        var tr = panel.GetComponent<PanelTransition>();
        if (tr != null) { if (on) tr.Show(); else tr.Hide(); }
        else panel.SetActive(on);
    }

    void ToggleOverlay(GameObject overlay)
    {
        if (overlay == null) return;
        SetPanel(overlay, !overlay.activeSelf);
    }
}
