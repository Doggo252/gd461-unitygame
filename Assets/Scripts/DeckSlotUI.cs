using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One slot in the selected-deck sidebar. Shows preview + flag + name + CP for a
// chosen card, or a numbered empty placeholder.
// Single-click on a filled slot opens the detail panel.
// Double-click on a filled slot removes the card from the deck (same as X).
public class DeckSlotUI : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] Image    _background;
    [SerializeField] Image    _flagImage;
    [SerializeField] RawImage _tankModelView;
    [SerializeField] Text     _nameText;
    [SerializeField] Text     _cpText;
    [SerializeField] Text     _slotNumberText;
    [SerializeField] Button   _removeButton;

    [SerializeField] Color _emptyColor  = new Color(0.11f, 0.12f, 0.09f, 0.8f);
    [SerializeField] Color _filledColor = new Color(0.18f, 0.29f, 0.11f, 1f);

    Action _onRemove;
    Action _onOpenDetail;
    bool   _isFilled;

    public void Bind(UnitDataSO card, Action onRemove, Action onOpenDetail = null)
    {
        _onRemove     = onRemove;
        _onOpenDetail = onOpenDetail;
        _isFilled     = true;

        if (_background != null) _background.color = _filledColor;

        if (_flagImage != null)
        {
            _flagImage.sprite  = card.flagSprite;
            _flagImage.enabled = card.flagSprite != null;
        }

        string displayName = !string.IsNullOrEmpty(card.tankName)
            ? card.tankName
            : card.tankType.ToString().Replace('_', ' ');

        // Include CP cost inline with the name so we don't need a separate
        // "blank-rectangle" CP field on the slot — keeps the layout clean.
        if (_nameText != null) _nameText.text = $"{displayName.ToUpper()}   CP {card.cpCost}";
        if (_cpText   != null) _cpText.text   = "";   // hidden — CP now lives inside the name string

        // Keep the slot number visible even when filled — it shows in its own
        // dedicated column to the left of the tank preview.
        if (_slotNumberText != null) _slotNumberText.gameObject.SetActive(true);

        if (_tankModelView != null && CardModelRenderer.Instance != null)
        {
            _tankModelView.texture = null;
            _tankModelView.enabled = true;
            CardModelRenderer.Instance.RequestRender(card.tankType, rt =>
            {
                if (_tankModelView != null) _tankModelView.texture = rt;
            });
        }

        if (_removeButton != null)
        {
            _removeButton.gameObject.SetActive(true);
            _removeButton.onClick.RemoveAllListeners();
            _removeButton.onClick.AddListener(() => _onRemove?.Invoke());
        }
    }

    public void SetEmpty(int slotNumber)
    {
        _onRemove     = null;
        _onOpenDetail = null;
        _isFilled     = false;

        if (_background    != null) _background.color    = _emptyColor;
        if (_flagImage     != null) _flagImage.enabled   = false;
        if (_tankModelView != null) { _tankModelView.texture = null; _tankModelView.enabled = false; }
        if (_nameText      != null) _nameText.text = "";
        if (_cpText        != null) _cpText.text   = "";

        if (_slotNumberText != null)
        {
            _slotNumberText.gameObject.SetActive(true);
            _slotNumberText.text = slotNumber.ToString();
        }

        if (_removeButton != null)
        {
            _removeButton.onClick.RemoveAllListeners();
            _removeButton.gameObject.SetActive(false);
        }
    }

    // Single-click → open detail panel.  Double-click → remove from deck.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!_isFilled) return;
        // The X button has its own raycast and click handling — let those fire
        // independently. Unity already routes pointer clicks to the deepest
        // handler, so this method only runs for clicks on the slot body.
        if (eventData.clickCount >= 2) _onRemove?.Invoke();
        else                            _onOpenDetail?.Invoke();
    }

    void OnDestroy()
    {
        if (_removeButton != null) _removeButton.onClick.RemoveAllListeners();
    }
}
