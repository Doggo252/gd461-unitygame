using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

// Manages the MenuScene deck builder.
// Instantiates CardEntry prefabs at runtime (legal transient-data exception per AGENTS §5).
// Player selects exactly 8 cards; Start Battle becomes enabled once 8 are chosen.
// Reads DeckConfigSO.chosenFaction (set by FactionSelectUI) and filters the card pool
// to show only tanks belonging to that faction (plus neutral Swedish tanks).
public class DeckBuilderUI : MonoBehaviour
{
    [Header("Card Pool — assign all 38 UnitDataSOs")]
    [SerializeField] List<UnitDataSO> _allCards = new();

    [Header("UI — wire in Inspector")]
    [SerializeField] RectTransform    _cardGridContent;   // Content of ScrollRect
    [SerializeField] GameObject       _cardEntryPrefab;   // Assets/Prefabs/UI/CardEntry.prefab
    [SerializeField] Button           _startButton;
    [SerializeField] Text             _selectionCountText;
    [SerializeField] Text             _factionLabel;       // e.g. "ALLIES DECK" / "AXIS DECK"

    [Header("Data")]
    [SerializeField] DeckConfigSO     _deckConfig;
    [SerializeField] string           _battleSceneName = "MainScene";

    [Header("Pulse tuning")]
    [SerializeField] float _pulseSpeed = 1.8f;   // calmer pulse

    readonly List<UnitDataSO>          _selected    = new();
    readonly List<CardEntryController> _controllers = new();

    Coroutine _pulseRoutine;

    // Called once at the start AND every time the panel re-enables (e.g. after returning
    // from FactionSelect with a different faction chosen).
    void OnEnable()  => Rebuild();
    void Start()     { }   // Rebuild already fired from OnEnable

    void Rebuild()
    {
        if (_cardEntryPrefab == null || _cardGridContent == null) return;

        // Clear any previously instantiated cards and selection state
        foreach (var ctrl in _controllers)
            if (ctrl != null) Destroy(ctrl.gameObject);
        _controllers.Clear();
        _selected.Clear();

        if (_pulseRoutine != null) { StopCoroutine(_pulseRoutine); _pulseRoutine = null; }
        ResetButtonColour();
        if (_startButton != null) _startButton.interactable = false;

        Faction chosen = _deckConfig != null ? _deckConfig.chosenFaction : Faction.Allies;

        if (_factionLabel != null)
            _factionLabel.text = chosen == Faction.Allies ? "ALLIES DECK" : "AXIS DECK";

        foreach (var card in _allCards)
        {
            if (card == null) continue;
            if (card.faction != Faction.Both && card.faction != chosen) continue;

            var go   = Instantiate(_cardEntryPrefab, _cardGridContent);
            var ctrl = go.GetComponent<CardEntryController>();
            if (ctrl == null) { Debug.LogError("[DeckBuilderUI] CardEntry missing CardEntryController.", go); continue; }
            ctrl.Bind(card, OnCardToggled);
            _controllers.Add(ctrl);
        }

        UpdateDisplay();
    }

    // ── Card toggle callback ───────────────────────────────────────────────────

    void OnCardToggled(UnitDataSO card, bool isSelected)
    {
        var ctrl = _controllers.Find(c => c.Card == card);

        if (isSelected)
        {
            if (_selected.Count >= 8 || _selected.Contains(card))
            {
                // Reject: revert both the grid card and the detail panel
                ctrl?.SetSelected(false);
                CardDetailPanel.Instance?.UpdateSelectionState(false);
                return;
            }
            _selected.Add(card);
        }
        else
        {
            _selected.Remove(card);
        }

        ctrl?.SetSelected(isSelected);
        UpdateDisplay();
    }

    // ── UI helpers ─────────────────────────────────────────────────────────────

    void UpdateDisplay()
    {
        if (_selectionCountText != null)
            _selectionCountText.text = $"{_selected.Count} / 8 selected";

        bool ready = _selected.Count == 8;
        if (_startButton != null) _startButton.interactable = ready;

        if (ready && _pulseRoutine == null)
            _pulseRoutine = StartCoroutine(PulseStartButton());
        else if (!ready && _pulseRoutine != null)
        {
            StopCoroutine(_pulseRoutine);
            _pulseRoutine = null;
            ResetButtonColour();
        }
    }

    IEnumerator PulseStartButton()
    {
        while (true)
        {
            float t  = (Mathf.Sin(Time.unscaledTime * _pulseSpeed) + 1f) * 0.5f;
            var  cb  = _startButton.colors;
            // Subtle warm tint instead of full saturated yellow
            cb.normalColor = Color.Lerp(Color.white, new Color(1f, 0.95f, 0.55f), t);
            _startButton.colors = cb;
            yield return null;
        }
    }

    void ResetButtonColour()
    {
        if (_startButton == null) return;
        var cb = _startButton.colors;
        cb.normalColor = Color.white;
        _startButton.colors = cb;
    }

    // Wired to Start Battle Button.onClick in the Inspector.
    public void OnStartBattleClicked()
    {
        if (_selected.Count != 8) return;
        if (_deckConfig != null) _deckConfig.SetDeck(_selected);
        SceneTransitionService.Goto(_battleSceneName);   // slide-wipe transition
    }
}
