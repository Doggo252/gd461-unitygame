using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Attached to each CardEntry prefab instantiated by DeckBuilderUI.
// Shows a compact card view (name, flag, 3D preview thumbnail, key stats).
// Clicking the card opens CardDetailPanel for the full orbit view + stats.
public class CardEntryController : MonoBehaviour, IPointerClickHandler
{
    [Header("Identity")]
    [SerializeField] Text  _nameText;
    [SerializeField] Image _flagImage;
    [SerializeField] Text  _nationText;

    [Header("3D Model Preview (thumbnail)")]
    [SerializeField] RawImage _tankModelView;

    [Header("Compact Stats (on card face)")]
    [SerializeField] Text _cpText;
    [SerializeField] Text _hpText;
    [SerializeField] Text _atkText;
    [SerializeField] Text _armText;
    [SerializeField] Text _penText;
    [SerializeField] Text _keywordsText;

    [Header("Legacy fields (kept for back-compat)")]
    [SerializeField] Text _typeText;
    [SerializeField] Text _spdText;
    [SerializeField] Text _movText;
    [SerializeField] Text _rngText;

    [Header("Selection visuals")]
    [SerializeField] RawImage _selectionBorder;
    [SerializeField] Button   _button;
    [SerializeField] Color    _selectedColor = new Color(0.25f, 0.72f, 0.32f, 0.50f); // subtle green tint
    [SerializeField] RawImage _baseBackground;

    UnitDataSO               _card;
    Action<UnitDataSO, bool> _onToggle;
    bool                     _selected;


    public UnitDataSO Card     => _card;
    public bool       Selected => _selected;

    // ── Bind ─────────────────────────────────────────────────────────────────────

    public void Bind(UnitDataSO card, Action<UnitDataSO, bool> onToggle)
    {
        _card     = card;
        _onToggle = onToggle;

        string displayName = !string.IsNullOrEmpty(card.tankName) ? card.tankName
                           : card.tankType.ToString().Replace('_', ' ');

        if (_nameText   != null) _nameText.text   = displayName.ToUpper();
        if (_typeText   != null) _typeText.text   = displayName.ToUpper();
        if (_nationText != null) _nationText.text = card.nation.ToString().Replace("GreatBritain", "UK");
        if (_flagImage  != null) _flagImage.sprite = card.flagSprite;

        // Compact stats on the card face
        if (_cpText  != null) _cpText.text  = $"CP {card.cpCost}";
        if (_hpText  != null) _hpText.text  = $"HP  {card.maxHp:F0}";
        if (_atkText != null) _atkText.text = $"ATK {card.atk:F0}";
        if (_armText != null) _armText.text = $"ARM {card.arm:F0}";
        if (_penText != null) _penText.text = $"PEN {card.pen:F0}";
        if (_spdText != null) _spdText.text = $"{card.spd:F1}/s";
        if (_movText != null) _movText.text = $"{card.mov:F1}t/s";
        if (_rngText != null) _rngText.text = $"RNG {card.rng:F1}";
        if (_keywordsText != null)
            _keywordsText.text = card.keywords == 0 ? ""
                : card.keywords.ToString().Replace(",", " · ");

        // 3D preview thumbnail via CardModelRenderer
        if (_tankModelView != null && CardModelRenderer.Instance != null)
        {
            _tankModelView.texture = null;
            CardModelRenderer.Instance.RequestRender(card.tankType, rt =>
            {
                if (_tankModelView != null && _card == card)
                    _tankModelView.texture = rt;
            });
        }

        SetSelected(false);
    }

    // ── Selection ─────────────────────────────────────────────────────────────────

    public void SetSelected(bool value)
    {
        _selected = value;
        // The SelectionBorder GameObject is toggled wholesale — when an Outline
        // component is attached, leaving it active with a transparent graphic
        // still draws the outline (it has its own alpha). Toggling the
        // GameObject is the only way to guarantee no overlay when unselected.
        if (_selectionBorder != null)
        {
            _selectionBorder.gameObject.SetActive(_selected);
            _selectionBorder.color = _selectedColor;
        }
        if (_baseBackground != null)
            _baseBackground.color = _selected
                ? new Color(0.22f, 0.18f, 0.08f, 1f)   // selected: warm amber wash
                : new Color(0.07f, 0.09f, 0.13f, 1f);  // unselected: dark navy
    }

    // ── Click handling ───────────────────────────────────────────────────────────

    // Left-click → open detail panel.  RIGHT-click → toggle in/out of the deck.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right)
        {
            _onToggle?.Invoke(_card, !_selected);
        }
        else if (eventData.button == PointerEventData.InputButton.Left)
        {
            CardDetailPanel.Instance?.Show(_card, OnDetailToggle, _selected);
        }
    }

    // Callback from CardDetailPanel when the user clicks Add/Remove
    void OnDetailToggle(UnitDataSO card, bool selected)
    {
        // Validate via DeckBuilderUI by going through the registered callback
        bool prev = _selected;
        _selected = selected;

        // Fire through the registered handler (DeckBuilderUI.OnCardToggled)
        _onToggle?.Invoke(card, selected);

        // If the deck was full and DeckBuilderUI rejected, revert visual
        // (DeckBuilderUI sets _selected back via SetSelected)
    }
}
