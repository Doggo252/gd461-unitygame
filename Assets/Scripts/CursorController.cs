using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Swaps the hardware cursor between three states:
//   default arrow  → pointing-hand while hovering anything clickable
//                  → closed/grabbing hand while dragging a card
// Unity has no API for the OS's native hand/grab cursors, so the hand shapes
// are small cursor textures (authored under Assets/UI/Cursors); the default
// state hands control back to the real system arrow (SetCursor(null)).
// One instance per scene. Drag state is reported by CardDragController via
// the static BeginDrag/EndDrag hooks.
public class CursorController : MonoBehaviour
{
    [Header("Cursor textures (32×32, imported as Cursor)")]
    [SerializeField] Texture2D _hand;
    [SerializeField] Texture2D _grab;
    [SerializeField] Vector2   _handHotspot = new Vector2(11f, 4f);
    [SerializeField] Vector2   _grabHotspot = new Vector2(14f, 12f);

    enum State { Default, Hover, Grab }
    State _applied = State.Default;

    static bool _dragging;
    public static void BeginDrag() => _dragging = true;
    public static void EndDrag()   => _dragging = false;

    readonly List<RaycastResult> _hits = new();

    void OnDisable()
    {
        _dragging = false;
        Apply(State.Default, force: true);
    }

    void Update()
    {
        State want = _dragging ? State.Grab
                   : PointerOverClickable() ? State.Hover
                   : State.Default;
        Apply(want);
    }

    bool PointerOverClickable()
    {
        var es = EventSystem.current;
        var mouse = Mouse.current;
        if (es == null || mouse == null) return false;

        var ev = new PointerEventData(es) { position = mouse.position.ReadValue() };
        _hits.Clear();
        es.RaycastAll(ev, _hits);
        if (_hits.Count == 0) return false;

        var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(_hits[0].gameObject);
        if (handler == null) return false;

        // respect disabled buttons — no hand over something that won't react
        var sel = handler.GetComponent<UnityEngine.UI.Selectable>();
        return sel == null || sel.IsInteractable();
    }

    void Apply(State s, bool force = false)
    {
        if (!force && s == _applied) return;
        _applied = s;
        switch (s)
        {
            case State.Hover: Cursor.SetCursor(_hand, _handHotspot, CursorMode.Auto); break;
            case State.Grab:  Cursor.SetCursor(_grab, _grabHotspot, CursorMode.Auto); break;
            default:          Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);  break;
        }
    }
}
