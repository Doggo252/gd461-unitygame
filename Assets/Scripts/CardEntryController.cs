using System;
using UnityEngine;
using UnityEngine.UI;

// Attached to each CardEntry prefab instantiated by DeckBuilderUI.
// Shows a compact card view (name, flag, 3D preview thumbnail, key stats).
// Clicking the card opens CardDetailPanel for the full orbit view + stats.
public class CardEntryController : MonoBehaviour
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

        if (_button != null)
        {
            _button.onClick.RemoveListener(OnClick);
            _button.onClick.AddListener(OnClick);
        }
        SetSelected(false);
    }

    void OnDestroy()
    {
        if (_button != null) _button.onClick.RemoveListener(OnClick);
    }

    // ── Selection ─────────────────────────────────────────────────────────────────

    public void SetSelected(bool value)
    {
        _selected = value;
        if (_selectionBorder != null)
            _selectionBorder.color = _selected ? _selectedColor : Color.clear;
        if (_baseBackground != null)
            _baseBackground.color = _selected
                ? new Color(0.16f, 0.20f, 0.28f, 1f)
                : new Color(0.08f, 0.10f, 0.14f, 1f);
    }

    // ── Click → open detail panel ────────────────────────────────────────────────

    void OnClick()
    {
        // Open the full detail panel with orbit view; deck toggle is handled there
        CardDetailPanel.Instance?.Show(_card, OnDetailToggle, _selected);
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
