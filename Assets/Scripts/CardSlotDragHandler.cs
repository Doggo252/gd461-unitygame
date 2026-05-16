using UnityEngine;
using UnityEngine.EventSystems;

// Attached to each CardSlot panel. Forwards drag lifecycle events
// (begin/drag/end) to the central CardDragController, which manages
// the ghost preview and deployment logic.
public class CardSlotDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [SerializeField] int _slotIndex;
    [SerializeField] CardDragController _controller;

    public int SlotIndex => _slotIndex;

    public void Initialize(int slotIndex, CardDragController controller)
    {
        _slotIndex  = slotIndex;
        _controller = controller;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_controller != null) _controller.BeginDrag(_slotIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_controller != null) _controller.DragUpdate(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (_controller != null) _controller.EndDrag(eventData);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        // Tap-to-select fallback (touch devices). Not strictly required when drag works,
        // but kept for parity with the GDD touch-to-deploy flow.
        if (_controller != null) _controller.TapSelect(_slotIndex);
    }
}
