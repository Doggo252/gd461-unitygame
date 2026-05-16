using UnityEngine;
using UnityEngine.InputSystem;

// Handles player unit deployment via drag-and-drop and hotkeys 1-4.
// Uses New Input System (CardActions.inputactions) — no legacy Input calls.
//
// Flow:
//   Hotkey 1-4    → SelectCard(index), start drag mode, ghost follows cursor
//   Left-click    → if card selected, start drag; on release deploy if valid zone
//   Right-click   → cancel selection and ghost
//   Valid zone    → X ∈ [_deployXMin, _deployXMax] (P1 left half by default)
public class CardDragDeploy : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] InputActionAsset     _cardActionsAsset;

    [Header("References")]
    [SerializeField] DeckManager          _deckManager;
    [SerializeField] CommandPointsManager _cpManager;
    [SerializeField] CardHandUI           _handUI;
    [SerializeField] Camera               _cam;
    [SerializeField] GameObject           _ghostPrefab;

    [Header("P1 Deploy Zone")]
    [SerializeField] float _deployXMin = -28f;
    [SerializeField] float _deployXMax =   0f;
    [SerializeField] float _deployZMin = -14f;
    [SerializeField] float _deployZMax =  14f;
    [SerializeField] int   _playerTeam =   0;

    InputActionMap _map;
    InputAction    _sel1, _sel2, _sel3, _sel4;
    InputAction    _deploy, _cancelDeploy, _pointerPos;

    GameObject _ghost;
    bool       _dragging;
    int        _dragIndex = -1;

    // ── Setup ──────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (_cardActionsAsset == null) { Debug.LogError("[CardDragDeploy] CardActions asset not assigned.", this); return; }
        _map        = _cardActionsAsset.FindActionMap("CardActions", throwIfNotFound: true);
        _sel1       = _map.FindAction("SelectCard1",    throwIfNotFound: true);
        _sel2       = _map.FindAction("SelectCard2",    throwIfNotFound: true);
        _sel3       = _map.FindAction("SelectCard3",    throwIfNotFound: true);
        _sel4       = _map.FindAction("SelectCard4",    throwIfNotFound: true);
        _deploy     = _map.FindAction("Deploy",         throwIfNotFound: true);
        _cancelDeploy = _map.FindAction("CancelDeploy", throwIfNotFound: true);
        _pointerPos = _map.FindAction("PointerPosition",throwIfNotFound: true);
    }

    void OnEnable()
    {
        if (_map == null) return;
        _map.Enable();
        _sel1.performed       += OnSel1;
        _sel2.performed       += OnSel2;
        _sel3.performed       += OnSel3;
        _sel4.performed       += OnSel4;
        _deploy.started       += OnDeployPress;
        _deploy.canceled      += OnDeployRelease;
        _cancelDeploy.performed += OnCancel;
    }

    void OnDisable()
    {
        if (_map == null) return;
        _sel1.performed       -= OnSel1;
        _sel2.performed       -= OnSel2;
        _sel3.performed       -= OnSel3;
        _sel4.performed       -= OnSel4;
        _deploy.started       -= OnDeployPress;
        _deploy.canceled      -= OnDeployRelease;
        _cancelDeploy.performed -= OnCancel;
        _map.Disable();
        DestroyGhost();
    }

    // ── Input callbacks (named methods — safe to unsubscribe) ──────────────────

    void OnSel1(InputAction.CallbackContext _) => StartDrag(0);
    void OnSel2(InputAction.CallbackContext _) => StartDrag(1);
    void OnSel3(InputAction.CallbackContext _) => StartDrag(2);
    void OnSel4(InputAction.CallbackContext _) => StartDrag(3);

    void OnDeployPress(InputAction.CallbackContext _)
    {
        if (_deckManager == null || _deckManager.SelectedHandIndex < 0) return;
        StartDrag(_deckManager.SelectedHandIndex);
    }

    void OnDeployRelease(InputAction.CallbackContext _)
    {
        if (!_dragging || _dragIndex < 0) { ResetDrag(); return; }

        Vector2 sp = _pointerPos.ReadValue<Vector2>();
        if (TryGetWorldPos(sp, out Vector3 wp))
        {
            var card = _deckManager.Hand[_dragIndex];
            if (card != null && _cpManager != null && _cpManager.TrySpend(card.cpCost))
            {
                _deckManager.SpawnUnit(card, wp, _playerTeam);
                _deckManager.ConsumeCard(_dragIndex);
            }
        }

        ResetDrag();
    }

    void OnCancel(InputAction.CallbackContext _)
    {
        _deckManager?.CancelSelection();
        ResetDrag();
        _handUI?.Refresh();
    }

    // ── Ghost follows cursor each frame ────────────────────────────────────────

    void Update()
    {
        if (!_dragging || _ghost == null || _pointerPos == null) return;
        if (TryGetWorldPos(_pointerPos.ReadValue<Vector2>(), out Vector3 wp))
            _ghost.transform.position = wp + Vector3.up * 0.3f;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    void StartDrag(int index)
    {
        if (_deckManager == null) return;
        _deckManager.SelectCard(index);
        _dragIndex = index;
        _dragging  = true;
        SpawnGhost();
        _handUI?.Refresh();
    }

    void ResetDrag()
    {
        _dragging  = false;
        _dragIndex = -1;
        DestroyGhost();
    }

    // Raycast against the ground plane (Y = 0) and clamp to deploy zone.
    bool TryGetWorldPos(Vector2 screenPos, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        if (_cam == null) return false;

        Ray ray   = _cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float dist)) return false;

        Vector3 hit = ray.GetPoint(dist);
        if (hit.x < _deployXMin || hit.x > _deployXMax) return false;
        if (hit.z < _deployZMin || hit.z > _deployZMax) return false;

        worldPos = hit;
        return true;
    }

    void SpawnGhost()
    {
        DestroyGhost();
        if (_ghostPrefab != null) _ghost = Instantiate(_ghostPrefab);
    }

    void DestroyGhost()
    {
        if (_ghost != null) { Destroy(_ghost); _ghost = null; }
    }
}
