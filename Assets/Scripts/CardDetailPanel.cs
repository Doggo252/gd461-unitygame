using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Singleton overlay that shows full stats + live orbit-able 3D view for a card.
// Opened by CardEntryController.OnClick. The "Add / Remove" button fires the
// same onToggle callback that DeckBuilderUI registered.
//
// AGENTS §5: all UI structure is pre-built in the Editor. This script only
// populates text/images and wires the OrbitTankViewer.
public class CardDetailPanel : MonoBehaviour
{
    public static CardDetailPanel Instance { get; private set; }

    [Header("Panel root — enable/disable this to show/hide")]
    [SerializeField] GameObject _panel;

    [Header("Identity")]
    [SerializeField] Text _nameText;
    [SerializeField] Text _nationText;
    [SerializeField] Image _flagImage;
    [SerializeField] Text _cpText;
    [SerializeField] Text _factionText;

    [Header("3D Orbit View")]
    [SerializeField] RawImage _orbitView;     // shows OrbitTankViewer.PreviewRT
    [SerializeField] EventTrigger _dragTarget;    // EventTrigger on _orbitView

    [Header("Full Stats")]
    [SerializeField] Text _hpText;
    [SerializeField] Text _atkText;
    [SerializeField] Text _armText;
    [SerializeField] Text _penText;
    [SerializeField] Text _spdText;
    [SerializeField] Text _movText;
    [SerializeField] Text _rngText;
    [SerializeField] Text _keywordsText;

    [Header("Deck Actions")]
    [SerializeField] Button _deckToggleButton;
    [SerializeField] Text _deckToggleLabel;     // shows "ADD TO DECK" or "REMOVE"
    [SerializeField] Button _closeButton;

    [Header("Keyboard Shortcuts")]
    [SerializeField] InputActionAsset _cardActionsAsset;

    [Header("Colours")]
    [SerializeField] Color _addColor = new Color(0.18f, 0.52f, 0.28f, 1f);   // muted green
    [SerializeField] Color _removeColor = new Color(0.52f, 0.18f, 0.18f, 1f);   // muted red

    UnitDataSO _current;
    Action<UnitDataSO, bool> _onToggle;    // provided by CardEntryController
    bool _isSelected;
    Vector2 _lastPointerPos;

    InputAction _closeAction;
    InputAction _confirmAction;

    // ── Lifecycle ────────────────────────────────────────────────────────────────

    void Awake()
    {
        // Single instance per scene — we deliberately do NOT use DontDestroyOnLoad,
        // so each scene gets a fresh CardDetailPanel. The previous duplicate-guard
        // (`if Instance != null && Instance != this` → Destroy self) was buggy on
        // scene reload: Unity destroys the old GameObject lazily, so the old
        // Instance reference is still non-null when the new one's Awake runs,
        // causing the new instance to destroy itself. Always claim Instance here
        // and let OnDestroy null it out cleanly.
        Instance = this;

        // Set up CanvasGroup-based hide/show during Awake so the panel is in a
        // safe state even if Show() runs before Start() (e.g. when triggered
        // from another script's Awake/early-frame logic). HideImmediate sets
        // alpha=0 so the panel is invisible by default.
        EnsurePanelGroup();
        HideImmediate();

        if (_cardActionsAsset != null)
        {
            var map = _cardActionsAsset.FindActionMap("CardActions");
            _closeAction = map?.FindAction("CloseDetail");
            _confirmAction = map?.FindAction("ConfirmDetail");
        }
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        if (_closeButton != null) _closeButton.onClick.AddListener(Close);
        if (_deckToggleButton != null) _deckToggleButton.onClick.AddListener(OnDeckToggle);

        if (_closeAction != null) { _closeAction.Enable(); _closeAction.performed += OnClosePerformed; }
        if (_confirmAction != null) { _confirmAction.Enable(); _confirmAction.performed += OnConfirmPerformed; }
    }

    void OnDisable()
    {
        if (_closeButton != null) _closeButton.onClick.RemoveListener(Close);
        if (_deckToggleButton != null) _deckToggleButton.onClick.RemoveListener(OnDeckToggle);

        if (_closeAction != null) { _closeAction.performed -= OnClosePerformed; _closeAction.Disable(); }
        if (_confirmAction != null) { _confirmAction.performed -= OnConfirmPerformed; _confirmAction.Disable(); }
    }

    // Cached CanvasGroup used to hide/show the panel without ever calling
    // SetActive(false) on the singleton GameObject — that would prevent Awake
    // from running on scene reload and leave Instance permanently null.
    CanvasGroup _panelGroup;
    bool        _isVisible;

    void Start()
    {
        SetupDragEvents();          // wire once; EventTrigger entries persist on the component
        // CanvasGroup + HideImmediate already done in Awake; nothing else needed here
    }

    void EnsurePanelGroup()
    {
        if (_panel == null) return;
        _panelGroup = _panel.GetComponent<CanvasGroup>() ?? _panel.AddComponent<CanvasGroup>();
    }

    void HideImmediate()
    {
        _isVisible = false;
        if (_panelGroup == null) return;
        _panelGroup.alpha = 0f;
        _panelGroup.interactable   = false;
        _panelGroup.blocksRaycasts = false;
    }

    void ShowImmediate()
    {
        _isVisible = true;
        if (_panelGroup == null) return;
        _panelGroup.alpha = 1f;
        _panelGroup.interactable   = true;
        _panelGroup.blocksRaycasts = true;
    }

    void OnClosePerformed(InputAction.CallbackContext _)
    {
        if (_isVisible) Close();
    }

    void OnConfirmPerformed(InputAction.CallbackContext _)
    {
        if (_isVisible) OnDeckToggle();
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    public void Show(UnitDataSO card, Action<UnitDataSO, bool> onToggle, bool alreadySelected)
    {
        _current = card;
        _onToggle = onToggle;
        _isSelected = alreadySelected;

        EnsurePanelGroup();
        ShowImmediate();
        // Allow rapid double-clicks on a card to land on the card behind: drop
        // blocksRaycasts for one double-click window so a second click passes
        // through to the originating card.
        StartCoroutine(SuppressBackgroundRaycastBriefly());

        // Orbit view
        if (OrbitTankViewer.Instance != null)
        {
            OrbitTankViewer.Instance.ShowTank(card.tankType);
            if (_orbitView != null)
                _orbitView.texture = OrbitTankViewer.Instance.PreviewRT;
        }

        // Flag
        if (_flagImage != null) _flagImage.sprite = card.flagSprite;

        // Identity text
        string name = !string.IsNullOrEmpty(card.tankName) ? card.tankName
                    : card.tankType.ToString().Replace('_', ' ');

        if (_nameText != null) _nameText.text = name.ToUpper();
        if (_nationText != null) _nationText.text = card.nation.ToString().Replace("GreatBritain", "Great Britain");
        if (_cpText != null) _cpText.text = $"CP  {card.cpCost}";
        if (_factionText != null) _factionText.text = card.faction == Faction.Both ? "Both Factions"
                                                    : card.faction.ToString();

        // Full stats
        if (_hpText != null) _hpText.text = $"HIT POINTS      {card.maxHp:F0}";
        if (_atkText != null) _atkText.text = $"ATTACK          {card.atk:F0}";
        if (_armText != null) _armText.text = $"ARMOUR          {card.arm:F0}";
        if (_penText != null) _penText.text = $"PENETRATION     {card.pen:F0}";
        if (_spdText != null) _spdText.text = $"FIRE RATE       {card.spd:F2}/s";
        if (_movText != null) _movText.text = $"MOVE SPEED      {card.mov:F1} tiles/s";
        if (_rngText != null) _rngText.text = $"RANGE           {card.rng:F1} tiles";
        if (_keywordsText != null)
            _keywordsText.text = card.keywords == 0 ? ""
                : card.keywords.ToString().Replace(",", " · ");

        UpdateDeckButton();
    }

    public void Close()
    {
        OrbitTankViewer.Instance?.HideTank();
        HideImmediate();
        _current  = null;
        _onToggle = null;
    }

    IEnumerator SuppressBackgroundRaycastBriefly()
    {
        if (_panelGroup == null) yield break;
        _panelGroup.blocksRaycasts = false;
        yield return new WaitForSecondsRealtime(0.22f);
        // Only re-enable if the panel is still meant to be visible.
        if (_isVisible) _panelGroup.blocksRaycasts = true;
    }

    // Called externally when selection state changes (e.g. deck hit 8 and card was deselected)
    public void UpdateSelectionState(bool selected)
    {
        _isSelected = selected;
        UpdateDeckButton();
    }

    // ── Internal ─────────────────────────────────────────────────────────────────

    void OnDeckToggle()
    {
        _isSelected = !_isSelected;
        _onToggle?.Invoke(_current, _isSelected);
        UpdateDeckButton();
    }

    void UpdateDeckButton()
    {
        if (_deckToggleLabel != null)
            _deckToggleLabel.text = _isSelected ? "REMOVE FROM DECK" : "ADD TO DECK";
        if (_deckToggleButton != null)
        {
            var colors = _deckToggleButton.colors;
            colors.normalColor = _isSelected ? _removeColor : _addColor;
            _deckToggleButton.colors = colors;
        }
    }

    void SetupDragEvents()
    {
        if (_dragTarget == null) return;
        _dragTarget.triggers.Clear();

        AddEntry(EventTriggerType.BeginDrag, (d) =>
            _lastPointerPos = ((PointerEventData)d).position);

        AddEntry(EventTriggerType.Drag, (d) =>
        {
            var pos = ((PointerEventData)d).position;
            var delta = pos - _lastPointerPos;
            _lastPointerPos = pos;
            OrbitTankViewer.Instance?.OnDrag(delta);
        });
    }

    void AddEntry(EventTriggerType type, UnityEngine.Events.UnityAction<BaseEventData> cb)
    {
        var e = new EventTrigger.Entry { eventID = type };
        e.callback.AddListener(cb);
        _dragTarget.triggers.Add(e);
    }
}
