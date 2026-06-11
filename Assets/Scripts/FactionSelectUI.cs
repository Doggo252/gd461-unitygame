using UnityEngine;
using UnityEngine.UI;

// Shown before the deck builder. The player clicks Allies or Axis to choose
// their side; the choice is written to DeckConfigSO and the deck builder panel
// is revealed.
//
// AGENTS §5: All canvas structure is pre-built in the Editor.
// This script only shows/hides existing panels and writes to the data SO.
public class FactionSelectUI : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] GameObject _factionSelectPanel;
    [SerializeField] GameObject _deckBuilderPanel;

    [Header("Buttons")]
    [SerializeField] Button _alliesButton;
    [SerializeField] Button _axisButton;

    [Header("Data")]
    [SerializeField] DeckConfigSO _deckConfig;

    [Header("Deck Builder (on a sibling GameObject, not the panel)")]
    [SerializeField] DeckBuilderUI _deckBuilderUI;

    // ── Lifecycle ────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (_alliesButton != null) _alliesButton.onClick.AddListener(OnAlliesClicked);
        if (_axisButton   != null) _axisButton.onClick.AddListener(OnAxisClicked);
    }

    void OnDisable()
    {
        if (_alliesButton != null) _alliesButton.onClick.RemoveListener(OnAlliesClicked);
        if (_axisButton   != null) _axisButton.onClick.RemoveListener(OnAxisClicked);
    }

    void Start()
    {
        // Panel visibility is managed by MainMenuController.
        // FactionSelectUI only reacts to button clicks — no Start() visibility changes.
    }

    // ── Button handlers ──────────────────────────────────────────────────────────

    void OnAlliesClicked() => SelectFaction(Faction.Allies);
    void OnAxisClicked()   => SelectFaction(Faction.Axis);

    void SelectFaction(Faction faction)
    {
        if (_deckConfig != null) _deckConfig.chosenFaction = faction;

        Toggle(_factionSelectPanel, false);
        Toggle(_deckBuilderPanel,   true);

        // DeckBuilderUI lives on its own controller GameObject (not on the
        // panel), so SetActive(true) on the panel doesn't fire its OnEnable.
        // We must explicitly invoke Rebuild after writing chosenFaction.
        var dbUI = _deckBuilderUI != null ? _deckBuilderUI : FindObjectOfType<DeckBuilderUI>(true);
        if (dbUI != null) dbUI.RebuildPublic();
        else Debug.LogWarning("[FactionSelectUI] No DeckBuilderUI found — cards will not rebuild for new faction");
    }

    // Animate via PanelTransition when present; hard toggle otherwise.
    static void Toggle(GameObject panel, bool on)
    {
        if (panel == null || panel.activeSelf == on) return;
        var tr = panel.GetComponent<PanelTransition>();
        if (tr != null) { if (on) tr.Show(); else tr.Hide(); }
        else panel.SetActive(on);
    }
}
