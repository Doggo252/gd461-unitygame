using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public enum SortField { None, CP, HP, ATK, ARM, PEN }

// Manages the MenuScene deck builder.
// Instantiates CardEntry and DeckSlot prefabs at runtime (legal transient-data exception per AGENTS §5).
// Player selects exactly 8 cards; Start Battle becomes enabled once 8 are chosen.
public class DeckBuilderUI : MonoBehaviour
{
    [Header("Card Pool — assign all 38 UnitDataSOs")]
    [SerializeField] List<UnitDataSO> _allCards = new();

    [Header("UI — wire in Inspector")]
    [SerializeField] RectTransform _cardGridContent;
    [SerializeField] GameObject    _cardEntryPrefab;
    [SerializeField] Button        _startButton;
    [SerializeField] Text          _selectionCountText;
    [SerializeField] Text          _factionLabel;

    [Header("Nation Filter — one button + value per nation (all 8)")]
    [SerializeField] Button[] _nationFilterButtons;
    [SerializeField] Nation[] _nationFilterValues;
    [SerializeField] Image[]  _nationFilterHighlights;

    [Header("Sort Bar — 5 buttons in order: CP, HP, ATK, ARM, PEN")]
    [SerializeField] Button[] _sortButtons;
    [SerializeField] Text[]   _sortButtonLabels;
    [SerializeField] Button   _clearSortButton;   // resets sorting to None

    [Header("Deck management")]
    [SerializeField] Button _resetDeckButton;     // empties all 8 slots at once
    [SerializeField] Text   _deckFullWarning;     // floats to the card you tried to add

    [Header("Keyword Filter — one button + value per keyword")]
    [SerializeField] Button[]      _keywordFilterButtons;
    [SerializeField] UnitKeyword[] _keywordFilterValues;
    [SerializeField] Image[]       _keywordFilterHighlights;

    [Header("Selected Deck Sidebar (pre-built slots — drag the 8 DeckSlot instances here)")]
    [Tooltip("Pre-built DeckSlot instances laid out in the scene. The script will Bind/SetEmpty " +
             "them as cards are added/removed — it does NOT instantiate at runtime so you can " +
             "freely reposition each slot in the Editor.")]
    [SerializeField] DeckSlotUI[] _deckSlots = new DeckSlotUI[8];

    // Legacy fallback — only used if _deckSlots is empty (won't be after the
    // sidebar is wired up in the Editor).
    [SerializeField] Transform  _selectedDeckContent;
    [SerializeField] GameObject _deckSlotPrefab;

    [Header("Data")]
    [SerializeField] DeckConfigSO _deckConfig;
    [SerializeField] string       _battleSceneName = "MainScene";

    [Header("Pulse tuning")]
    [SerializeField] float _pulseSpeed = 1.8f;

    // ── Sort definitions (must match _sortButtons order) ──────────────────────────
    static readonly (SortField field, string label, Func<UnitDataSO, float> fn)[] SortDefs =
    {
        (SortField.CP,  "CP",  c => c.cpCost),
        (SortField.HP,  "HP",  c => c.maxHp),
        (SortField.ATK, "ATK", c => c.atk),
        (SortField.ARM, "ARM", c => c.arm),
        (SortField.PEN, "PEN", c => c.pen),
    };

    // ── State ─────────────────────────────────────────────────────────────────────
    readonly List<UnitDataSO>          _selected      = new();
    readonly List<CardEntryController> _controllers   = new();
    readonly HashSet<Nation>           _nationFilter  = new();
    readonly HashSet<UnitKeyword>      _keywordFilter = new();
    SortField _sortField     = SortField.None;
    bool      _sortAscending = true;
    Faction?  _loadedFaction;          // which faction's saved deck is loaded

    Coroutine _pulseRoutine;
    Coroutine _deckFullRoutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────────

    void Start()
    {
        // Wire filter/sort button callbacks in code since they carry an index argument
        for (int i = 0; i < _nationFilterButtons?.Length; i++)
        {
            int idx = i;
            _nationFilterButtons[i]?.onClick.AddListener(() => ToggleNationFilter(idx));
        }
        for (int i = 0; i < SortDefs.Length && i < _sortButtons?.Length; i++)
        {
            int idx = i;
            _sortButtons[i]?.onClick.AddListener(() => SetSort(idx));
        }
        for (int i = 0; i < _keywordFilterButtons?.Length; i++)
        {
            int idx = i;
            _keywordFilterButtons[i]?.onClick.AddListener(() => ToggleKeywordFilter(idx));
        }
        _clearSortButton?.onClick.AddListener(ClearSort);
        _resetDeckButton?.onClick.AddListener(ResetDeck);
        if (_deckFullWarning != null) _deckFullWarning.gameObject.SetActive(false);
    }

    void OnEnable() => Rebuild();

    // Public hook for FactionSelectUI to force a rebuild after the user picks a
    // faction. Necessary because SetActive(true) on an already-active panel
    // does NOT fire OnEnable, so a hand-edited "DeckBuilderPanel left active"
    // scene would otherwise show stale cards for the previous faction.
    public void RebuildPublic() => Rebuild();

    void OnDisable()
    {
        if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
    }

    // ── Filter / Sort public callbacks ───────────────────────────────────────────

    void ToggleNationFilter(int index)
    {
        if (_nationFilterValues == null || index >= _nationFilterValues.Length) return;
        var n = _nationFilterValues[index];
        if (!_nationFilter.Remove(n)) _nationFilter.Add(n);
        RefreshNationFilterVisuals();
        Rebuild();
    }

    void SetSort(int index)
    {
        if (index >= SortDefs.Length) return;
        var field = SortDefs[index].field;
        if (_sortField == field) _sortAscending = !_sortAscending;
        else { _sortField = field; _sortAscending = true; }
        RefreshSortButtonVisuals();
        Rebuild();
    }

    void ToggleKeywordFilter(int index)
    {
        if (_keywordFilterValues == null || index >= _keywordFilterValues.Length) return;
        var kw = _keywordFilterValues[index];
        if (!_keywordFilter.Remove(kw)) _keywordFilter.Add(kw);
        RefreshKeywordFilterVisuals();
        Rebuild();
    }

    // ── Rebuild ───────────────────────────────────────────────────────────────────

    void Rebuild()
    {
        if (_cardEntryPrefab == null || _cardGridContent == null) return;

        foreach (var ctrl in _controllers)
            if (ctrl != null) Destroy(ctrl.gameObject);
        _controllers.Clear();

        // Remove any selected cards that no longer pass the current filter
        _selected.RemoveAll(c => !PassesFactionFilter(c));

        if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
        ResetButtonColour();
        if (_startButton != null) _startButton.interactable = false;

        Faction chosen = _deckConfig != null ? _deckConfig.chosenFaction : Faction.Allies;

        // Restore this faction's last-used deck (per-faction memory, PlayerPrefs).
        if (_loadedFaction != chosen)
        {
            _loadedFaction = chosen;
            LoadSavedDeck(chosen);
        }

        if (_factionLabel != null)
            _factionLabel.text = chosen == Faction.Allies ? "ALLIES DECK" : "AXIS DECK";

        RefreshNationButtonVisibility(chosen);

        // Build filtered + sorted list
        var visible = _allCards
            .Where(c => c != null && PassesAllFilters(c))
            .ToList();

        if (_sortField != SortField.None)
        {
            var fn = SortDefs.First(d => d.field == _sortField).fn;
            visible = _sortAscending
                ? visible.OrderBy(fn).ToList()
                : visible.OrderByDescending(fn).ToList();
        }

        foreach (var card in visible)
        {
            var go   = Instantiate(_cardEntryPrefab, _cardGridContent);
            var ctrl = go.GetComponent<CardEntryController>();
            if (ctrl == null) { Debug.LogError("[DeckBuilderUI] CardEntry missing CardEntryController.", go); continue; }
            ctrl.Bind(card, OnCardToggled);
            if (_selected.Contains(card)) ctrl.SetSelected(true);
            _controllers.Add(ctrl);
        }

        RefreshSidebarDisplay();
        UpdateDisplay();
    }

    // Faction filter only — used to validate selected cards when faction changes
    bool PassesFactionFilter(UnitDataSO card)
    {
        Faction chosen = _deckConfig != null ? _deckConfig.chosenFaction : Faction.Allies;
        return card.faction == Faction.Both || card.faction == chosen;
    }

    // Full filter: faction + nation + keyword
    bool PassesAllFilters(UnitDataSO card)
    {
        if (!PassesFactionFilter(card)) return false;
        if (_nationFilter.Count > 0 && !_nationFilter.Contains(card.nation)) return false;
        if (_keywordFilter.Count > 0 && !_keywordFilter.Any(kw => (card.keywords & kw) != 0)) return false;
        return true;
    }

    // ── Card toggle callback ──────────────────────────────────────────────────────

    void OnCardToggled(UnitDataSO card, bool isSelected)
    {
        var ctrl = _controllers.Find(c => c.Card == card);

        if (isSelected)
        {
            if (_selected.Contains(card))
            {
                ctrl?.SetSelected(false);
                CardDetailPanel.Instance?.UpdateSelectionState(false);
                return;
            }
            if (_selected.Count >= 8)
            {
                // Deck full — refuse the add and warn right at the card the
                // user clicked, where their eyes already are.
                ctrl?.SetSelected(false);
                CardDetailPanel.Instance?.UpdateSelectionState(false);
                FlashDeckFullError();
                ShowDeckFullWarningAt(ctrl != null ? ctrl.transform as RectTransform : null);
                return;
            }
            _selected.Add(card);
        }
        else
        {
            _selected.Remove(card);
        }

        ctrl?.SetSelected(isSelected);
        SaveDeck();
        RefreshSidebarDisplay();
        UpdateDisplay();
    }

    Coroutine _errorFlashRoutine;
    void FlashDeckFullError()
    {
        if (_selectionCountText == null) return;
        if (_errorFlashRoutine != null) StopCoroutine(_errorFlashRoutine);
        _errorFlashRoutine = StartCoroutine(FlashDeckFullErrorRoutine());
    }

    IEnumerator FlashDeckFullErrorRoutine()
    {
        var defaultColor = new Color(0.91f, 0.86f, 0.71f, 1f);   // parchment
        var errorColor   = new Color(0.95f, 0.30f, 0.20f, 1f);   // alarm red
        var originalText = _selectionCountText.text;

        _selectionCountText.text  = "DECK FULL — REMOVE A CARD FIRST";
        _selectionCountText.color = errorColor;

        // Quick triple-pulse over ~1.2 s so the message is unmistakable
        for (int i = 0; i < 3; i++)
        {
            yield return new WaitForSecondsRealtime(0.18f);
            _selectionCountText.color = defaultColor;
            yield return new WaitForSecondsRealtime(0.18f);
            _selectionCountText.color = errorColor;
        }

        yield return new WaitForSecondsRealtime(0.4f);
        _selectionCountText.text  = originalText;
        _selectionCountText.color = defaultColor;
        _errorFlashRoutine = null;
    }

    // ── Sidebar ───────────────────────────────────────────────────────────────────

    void RefreshSidebarDisplay()
    {
        // Preferred path: 8 pre-built DeckSlot instances laid out in the scene.
        // This lets the designer position each slot manually in the Editor —
        // no runtime instantiation, no LayoutGroup constraints.
        if (_deckSlots != null && _deckSlots.Length > 0)
        {
            for (int i = 0; i < _deckSlots.Length; i++)
            {
                var slot = _deckSlots[i];
                if (slot == null) continue;
                if (i < _selected.Count)
                {
                    var card = _selected[i];
                    slot.Bind(card,
                              onRemove:     () => RemoveFromDeck(card),
                              onOpenDetail: () => CardDetailPanel.Instance?.Show(card, OnSidebarDetailToggle, true));
                }
                else
                {
                    slot.SetEmpty(i + 1);
                }
            }
            return;
        }

        // Legacy fallback — only runs if the static-slots array wasn't wired.
        if (_selectedDeckContent == null || _deckSlotPrefab == null) return;
        foreach (Transform child in _selectedDeckContent)
            Destroy(child.gameObject);

        for (int i = 0; i < 8; i++)
        {
            var go   = Instantiate(_deckSlotPrefab, _selectedDeckContent);
            var slot = go.GetComponent<DeckSlotUI>();
            if (slot == null) continue;
            if (i < _selected.Count)
            {
                var card = _selected[i];
                slot.Bind(card,
                          onRemove:     () => RemoveFromDeck(card),
                          onOpenDetail: () => CardDetailPanel.Instance?.Show(card, OnSidebarDetailToggle, true));
            }
            else
                slot.SetEmpty(i + 1);
        }
    }

    // Sidebar slot's detail-panel Add/Remove callback. The card is already in the
    // deck (since the user opened it from the sidebar), so toggling = remove.
    void OnSidebarDetailToggle(UnitDataSO card, bool selected)
    {
        if (!selected) RemoveFromDeck(card);
    }

    void RemoveFromDeck(UnitDataSO card)
    {
        if (!_selected.Contains(card)) return;
        _selected.Remove(card);
        var ctrl = _controllers.Find(c => c.Card == card);
        ctrl?.SetSelected(false);
        CardDetailPanel.Instance?.UpdateSelectionState(false);
        SaveDeck();
        RefreshSidebarDisplay();
        UpdateDisplay();
    }

    // RESET DECK button — empty all 8 slots at once.
    void ResetDeck()
    {
        if (_selected.Count == 0) return;
        _selected.Clear();
        foreach (var c in _controllers) c?.SetSelected(false);
        CardDetailPanel.Instance?.UpdateSelectionState(false);
        SaveDeck();
        RefreshSidebarDisplay();
        UpdateDisplay();
    }

    // CLEAR button on the sort bar — back to the unsorted card pool.
    void ClearSort()
    {
        _sortField     = SortField.None;
        _sortAscending = true;
        RefreshSortButtonVisuals();
        Rebuild();
    }

    // ── Per-faction deck persistence (PlayerPrefs) ───────────────────────────────

    static string DeckKey(Faction f) => $"SavedDeck_{f}";

    void SaveDeck()
    {
        Faction chosen = _deckConfig != null ? _deckConfig.chosenFaction : Faction.Allies;
        var names = new List<string>();
        foreach (var c in _selected) if (c != null) names.Add(c.name);
        PlayerPrefs.SetString(DeckKey(chosen), string.Join(";", names));
        PlayerPrefs.Save();
    }

    void LoadSavedDeck(Faction faction)
    {
        _selected.Clear();
        string saved = PlayerPrefs.GetString(DeckKey(faction), "");
        if (string.IsNullOrEmpty(saved)) return;
        foreach (var n in saved.Split(';'))
        {
            if (_selected.Count >= 8) break;
            var card = _allCards.Find(c => c != null && c.name == n);
            if (card != null && PassesFactionFilter(card) && !_selected.Contains(card))
                _selected.Add(card);
        }
    }

    // ── Deck-full warning anchored to the clicked card ───────────────────────────

    void ShowDeckFullWarningAt(RectTransform anchor)
    {
        if (_deckFullWarning == null) return;
        if (_deckFullRoutine != null) StopCoroutine(_deckFullRoutine);
        _deckFullRoutine = StartCoroutine(DeckFullWarningRoutine(anchor));
    }

    IEnumerator DeckFullWarningRoutine(RectTransform anchor)
    {
        var rt = _deckFullWarning.GetComponent<RectTransform>();
        if (anchor != null)
        {
            // sit just under the clicked card
            var corners = new Vector3[4];
            anchor.GetWorldCorners(corners);                    // 0=BL 3=BR
            rt.position = new Vector3((corners[0].x + corners[3].x) * 0.5f, corners[0].y - 14f, 0f);
        }
        _deckFullWarning.text = "DECK FULL — REMOVE A CARD FIRST";
        _deckFullWarning.gameObject.SetActive(true);

        Vector3 basePos = rt.position;
        float t = 0f;
        const float life = 1.5f;
        while (t < life)
        {
            t += Time.unscaledDeltaTime;
            float k = t / life;
            rt.position = basePos + Vector3.up * (14f * k);     // drift up
            var c = _deckFullWarning.color; c.a = 1f - Mathf.SmoothStep(0.55f, 1f, k);
            _deckFullWarning.color = c;
            yield return null;
        }
        _deckFullWarning.gameObject.SetActive(false);
        var rc = _deckFullWarning.color; rc.a = 1f; _deckFullWarning.color = rc;
        _deckFullRoutine = null;
    }

    // ── Button visuals ────────────────────────────────────────────────────────────

    static readonly Color ActiveFilterColor       = new Color(0.35f, 0.72f, 0.12f, 0.92f);  // bright saturated green
    static readonly Color InactiveFilterColor     = new Color(0f, 0f, 0f, 0f);              // transparent — let base button show
    static readonly Color ActiveLabelColor        = new Color(0.91f, 0.86f, 0.71f, 1f);     // parchment #E8DDB5
    static readonly Color InactiveLabelColor      = new Color(0.42f, 0.44f, 0.37f, 1f);     // dim muted olive
    static readonly Color ActiveKeywordColor      = new Color(0.78f, 0.72f, 0.42f, 0.92f);  // brass gold

    void RefreshNationButtonVisibility(Faction chosen)
    {
        if (_nationFilterButtons == null || _nationFilterValues == null) return;
        for (int i = 0; i < _nationFilterButtons.Length && i < _nationFilterValues.Length; i++)
        {
            if (_nationFilterButtons[i] == null) continue;
            var n = _nationFilterValues[i];
            // Sweden (Both) is available to either faction; others are faction-specific
            bool inFaction = n == Nation.Sweden
                || (chosen == Faction.Allies && (n == Nation.USA || n == Nation.USSR || n == Nation.GreatBritain || n == Nation.France))
                || (chosen == Faction.Axis   && (n == Nation.Germany || n == Nation.Japan || n == Nation.Italy));
            _nationFilterButtons[i].gameObject.SetActive(inFaction);
        }
        // Remove any nations that are now hidden (faction changed)
        if (_nationFilterValues != null && _nationFilterButtons != null)
        {
            _nationFilter.RemoveWhere(n => {
                for (int j = 0; j < _nationFilterValues.Length && j < _nationFilterButtons.Length; j++)
                    if (_nationFilterValues[j] == n) return _nationFilterButtons[j]?.gameObject.activeSelf != true;
                return true;
            });
        }
    }

    void RefreshNationFilterVisuals()
    {
        if (_nationFilterHighlights == null || _nationFilterValues == null) return;
        for (int i = 0; i < _nationFilterHighlights.Length && i < _nationFilterValues.Length; i++)
        {
            if (_nationFilterHighlights[i] == null) continue;
            bool active = _nationFilter.Contains(_nationFilterValues[i]);
            _nationFilterHighlights[i].color = active ? ActiveFilterColor : InactiveFilterColor;
            // Also dim/brighten the label text for immediate clarity
            var lbl = _nationFilterHighlights[i].transform.parent?.Find("Label")?.GetComponent<Text>();
            if (lbl != null) lbl.color = active ? ActiveLabelColor : InactiveLabelColor;
        }
    }

    void RefreshSortButtonVisuals()
    {
        for (int i = 0; i < SortDefs.Length && i < _sortButtonLabels?.Length; i++)
        {
            if (_sortButtonLabels[i] == null) continue;
            var (field, label, _) = SortDefs[i];
            _sortButtonLabels[i].text = _sortField == field
                ? label + (_sortAscending ? " ▲" : " ▼")
                : label;
        }
    }

    void RefreshKeywordFilterVisuals()
    {
        if (_keywordFilterHighlights == null || _keywordFilterValues == null) return;
        for (int i = 0; i < _keywordFilterHighlights.Length && i < _keywordFilterValues.Length; i++)
        {
            if (_keywordFilterHighlights[i] == null) continue;
            bool active = _keywordFilter.Contains(_keywordFilterValues[i]);
            _keywordFilterHighlights[i].color = active ? ActiveKeywordColor : InactiveFilterColor;
            var lbl = _keywordFilterHighlights[i].transform.parent?.Find("Label")?.GetComponent<Text>();
            if (lbl != null) lbl.color = active ? ActiveLabelColor : InactiveLabelColor;
        }
    }

    // ── Start button ──────────────────────────────────────────────────────────────

    void UpdateDisplay()
    {
        if (_selectionCountText != null)
            _selectionCountText.text = $"{_selected.Count} / 8 SELECTED";

        bool ready = _selected.Count == 8;
        if (_startButton != null) _startButton.interactable = ready;

        if (ready)
        {
            // Bright label while pulsing
            var label = _startButton != null ? _startButton.GetComponentInChildren<UnityEngine.UI.Text>(true) : null;
            if (label != null) label.color = new Color(0.96f, 0.93f, 0.82f, 1f);
            if (_pulseRoutine == null) _pulseRoutine = StartCoroutine(PulseStartButton());
        }
        else
        {
            if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
            ResetButtonColour();
        }
    }

    IEnumerator PulseStartButton()
    {
        var dark  = new Color(0.29f, 0.36f, 0.18f);  // #4A5C2E
        var light = new Color(0.48f, 0.61f, 0.24f);  // #7A9A3E
        var img   = _startButton != null ? _startButton.targetGraphic : null;
        while (true)
        {
            float t = (Mathf.Sin(Time.unscaledTime * _pulseSpeed) + 1f) * 0.5f;
            if (img != null) img.color = Color.Lerp(dark, light, t);
            yield return null;
        }
    }

    // Apply the visibly-disabled grey directly to the button's target graphic.
    // Setting `colors.disabledColor` alone isn't enough because the project
    // disables per-state tinting on buttons for multi-select consistency.
    void ResetButtonColour()
    {
        if (_startButton == null) return;
        var img = _startButton.targetGraphic;
        if (img != null) img.color = new Color(0.18f, 0.20f, 0.16f, 0.85f);

        // Dim the button label too so it reads as clearly inactive.
        var label = _startButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
        if (label != null) label.color = new Color(0.55f, 0.55f, 0.52f, 0.9f);
    }

    public void OnStartBattleClicked()
    {
        if (_selected.Count != 8) return;
        if (_deckConfig != null) _deckConfig.SetDeck(_selected);
        SceneTransitionService.Goto(_battleSceneName);
    }
}
