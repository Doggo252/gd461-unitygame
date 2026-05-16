using UnityEngine;
using UnityEngine.UI;

// Controls the main menu flow: MainMenu → FactionSelect → DeckBuilder.
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

    [Header("Back Buttons (wire in Inspector)")]
    [SerializeField] Button _factionBackButton;    // FactionSelectPanel → MainMenu
    [SerializeField] Button _deckBuilderBackButton; // DeckBuilderPanel → FactionSelect

    [Header("Credits / Settings overlays (optional)")]
    [SerializeField] GameObject _creditsOverlay;
    [SerializeField] GameObject _settingsOverlay;

    // ── Lifecycle ────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (_playButton           != null) _playButton.onClick.AddListener(OnPlay);
        if (_settingsButton       != null) _settingsButton.onClick.AddListener(OnSettings);
        if (_creditsButton        != null) _creditsButton.onClick.AddListener(OnCredits);
        if (_factionBackButton    != null) _factionBackButton.onClick.AddListener(GoToMainMenu);
        if (_deckBuilderBackButton != null) _deckBuilderBackButton.onClick.AddListener(GoToFactionSelect);
    }

    void OnDisable()
    {
        if (_playButton           != null) _playButton.onClick.RemoveListener(OnPlay);
        if (_settingsButton       != null) _settingsButton.onClick.RemoveListener(OnSettings);
        if (_creditsButton        != null) _creditsButton.onClick.RemoveListener(OnCredits);
        if (_factionBackButton    != null) _factionBackButton.onClick.RemoveListener(GoToMainMenu);
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

    public void GoToDeckBuilder()   => ShowPanel(_deckBuilderPanel);
    public void GoToMainMenu()      => ShowPanel(_mainMenuPanel);
    public void GoToFactionSelect() => ShowPanel(_factionSelectPanel);

    void ShowPanel(GameObject target)
    {
        if (_mainMenuPanel       != null) _mainMenuPanel.SetActive(_mainMenuPanel == target);
        if (_factionSelectPanel  != null) _factionSelectPanel.SetActive(_factionSelectPanel == target);
        if (_deckBuilderPanel    != null) _deckBuilderPanel.SetActive(_deckBuilderPanel == target);
    }

    void ToggleOverlay(GameObject overlay)
    {
        if (overlay == null) return;
        overlay.SetActive(!overlay.activeSelf);
    }
}
