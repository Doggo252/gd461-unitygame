using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Drives the player's card-deployment UX:
//   • Drag-from-card via CardSlotDragHandler (UI EventSystem)
//   • Ghost preview (actual tank model) that follows the cursor
//   • Tints green (valid+affordable) or red (invalid position OR can't afford)
//   • Floating label explains the reason when red
//   • Frontline-aware deployment via FrontlineService
public class CardDragController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] DeckManager          _deckManager;
    [SerializeField] CommandPointsManager _cpManager;
    [SerializeField] FrontlineService     _frontlineService;
    [SerializeField] Camera               _cam;
    [SerializeField] CardPrefabRegistrySO _prefabRegistry;   // for model-accurate ghost
    [SerializeField] GameObject           _ghostPrefab;       // fallback if prefab not found
    [SerializeField] Material             _ghostMaterial;     // transparent ghost material
    [SerializeField] int                  _playerTeam = 0;
    [SerializeField] UnitRegistrySO       _registry;          // for structure keep-out (DeployRules)
    [SerializeField] DeployZoneOverlay    _deployOverlay;     // green spawnable-area overlay

    [Header("Map Z bounds (X is dynamic via FrontlineService)")]
    [SerializeField] float _deployZMin = -14f;
    [SerializeField] float _deployZMax =  14f;

    [Header("Ghost tints")]
    [SerializeField] Color _validTint   = new Color(0.30f, 1.00f, 0.40f, 0.70f);
    [SerializeField] Color _invalidTint = new Color(1.00f, 0.20f, 0.20f, 0.70f);

    [Header("Drag label — assign Assets/Prefabs/UI/GhostDragLabel.prefab")]
    [SerializeField] GameObject _dragLabelPrefab;

    GameObject  _ghost;
    Renderer[]  _ghostRenderers;
    bool        _dragging;
    int         _dragIndex = -1;
    UnitDataSO  _dragCard;           // card currently being dragged

    // ── Floating drag label (screen-space overlay, created once) ──────────────
    GameObject _labelCanvasGO;
    RectTransform _labelRootRT;
    Text          _labelText;

    // ── Lifecycle ────────────────────────────────────────────────────────────

    void Awake()
    {
        InitLabelFromPrefab();
    }

    void OnDisable()
    {
        DestroyGhost();
    }

    // ── Drag handlers (called by CardSlotDragHandler) ────────────────────────

    public void BeginDrag(int slotIndex, PointerEventData ev)
    {
        if (_deckManager == null) return;
        var hand = _deckManager.Hand;
        if (slotIndex < 0 || slotIndex >= hand.Length) return;
        var card = hand[slotIndex];
        if (card == null) return;

        _dragging  = true;
        _dragIndex = slotIndex;
        _dragCard  = card;
        _deckManager.SelectCard(slotIndex);
        SpawnGhost(card);
        if (_deployOverlay != null) _deployOverlay.Show();
        CursorController.BeginDrag();
        UpdateGhostFromScreen(ev.position);
    }

    public void DragUpdate(PointerEventData ev)
    {
        if (!_dragging) return;
        UpdateGhostFromScreen(ev.position);
    }

    public void EndDrag(PointerEventData ev)
    {
        if (!_dragging) { ResetDrag(); return; }
        if (TryGetWorldPos(ev.position, out Vector3 wp))
            DeployAt(_dragIndex, wp);
        ResetDrag();
    }

    public void TapSelect(int slotIndex)
    {
        if (_deckManager == null) return;
        _deckManager.SelectCard(slotIndex);
    }

    // ── Deploy logic ─────────────────────────────────────────────────────────

    void DeployAt(int slot, Vector3 worldPos)
    {
        if (_deckManager == null || _cpManager == null) return;
        var hand = _deckManager.Hand;
        if (slot < 0 || slot >= hand.Length) return;
        var card = hand[slot];
        if (card == null) return;

        if (!IsValidWorldPos(worldPos)) return;

        // Snap onto the mesh with a TIGHT tolerance — IsValidWorldPos already
        // rejected obstacle interiors, so this only nudges to the nearest valid cell.
        if (NavMesh.SamplePosition(worldPos, out NavMeshHit hit, DeployRules.NavTolerance, 1 << 0))
            worldPos = hit.position;

        if (!_cpManager.TrySpend(card.cpCost)) return;
        _deckManager.SpawnUnit(card, worldPos, _playerTeam);
        _deckManager.ConsumeCard(slot);
    }

    bool TryGetWorldPos(Vector2 screenPos, out Vector3 worldPos)
    {
        worldPos = Vector3.zero;
        if (_cam == null) return false;
        Ray ray   = _cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float dist)) return false;
        worldPos = ray.GetPoint(dist);
        return true;
    }

    bool IsValidWorldPos(Vector3 worldPos)
        => DeployRules.IsSpawnable(worldPos, _frontlineService, _playerTeam,
                                   _deployZMin, _deployZMax, _registry);

    // ── Ghost preview ────────────────────────────────────────────────────────

    void SpawnGhost(UnitDataSO card)
    {
        DestroyGhost();

        GameObject source = _ghostPrefab;
        if (card != null && _prefabRegistry != null
            && _prefabRegistry.TryGetPrefab(card.tankType, out var tankPrefab))
            source = tankPrefab;

        if (source == null) return;
        _ghost = Instantiate(source);

        // Disable every gameplay component — ghost is visual only
        foreach (var mb in _ghost.GetComponentsInChildren<MonoBehaviour>(true))
            mb.enabled = false;
        foreach (var col in _ghost.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
        foreach (var nav in _ghost.GetComponentsInChildren<NavMeshAgent>(true))
            nav.enabled = false;

        // Face the direction this team attacks
        _ghost.transform.rotation = _playerTeam == 0
            ? Quaternion.LookRotation(Vector3.right, Vector3.up)
            : Quaternion.LookRotation(Vector3.left,  Vector3.up);

        // Apply ghost material to every renderer
        _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>(true);
        if (_ghostMaterial != null)
        {
            foreach (var r in _ghostRenderers)
            {
                if (r == null) continue;
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                    mats[i] = _ghostMaterial;
                r.materials = mats;
            }
        }
    }

    void UpdateGhostFromScreen(Vector2 sp)
    {
        if (_ghost == null) return;
        if (!TryGetWorldPos(sp, out Vector3 wp)) return;

        _ghost.transform.position = wp + Vector3.up * 0.05f;

        // Evaluate affordability and position validity separately
        bool canAfford = _dragCard != null && _cpManager != null
                         && _cpManager.CurrentCp >= _dragCard.cpCost;
        bool validPos  = IsValidWorldPos(wp);
        bool allOk     = canAfford && validPos;

        TintGhost(allOk ? _validTint : _invalidTint);

        // Floating label: show reason when red, hide when green
        if (!canAfford)
        {
            int have = _cpManager != null ? _cpManager.CurrentCp : 0;
            int need = _dragCard != null ? _dragCard.cpCost : 0;
            ShowLabel($"NOT ENOUGH CP  ({have} / {need} needed)", sp);
        }
        else if (!validPos)
        {
            ShowLabel("CAN'T DEPLOY HERE", sp);
        }
        else
        {
            HideLabel();
        }
    }

    void TintGhost(Color c)
    {
        if (_ghostRenderers == null) return;
        foreach (var r in _ghostRenderers)
        {
            if (r == null) continue;
            foreach (var mat in r.materials)
            {
                if (mat == null) continue;
                if      (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                else if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     c);
            }
        }
    }

    void DestroyGhost()
    {
        if (_ghost != null) Destroy(_ghost);
        _ghost = null;
        _ghostRenderers = null;
        HideLabel();
    }

    void ResetDrag()
    {
        _dragging  = false;
        _dragIndex = -1;
        _dragCard  = null;
        DestroyGhost();
        if (_deployOverlay != null) _deployOverlay.Hide();
        CursorController.EndDrag();
    }

    // ── Floating drag label (screen-space) ───────────────────────────────────

    void InitLabelFromPrefab()
    {
        if (_dragLabelPrefab != null)
        {
            // Preferred path: instantiate the pre-built prefab (Assets/Prefabs/UI/GhostDragLabel.prefab).
            _labelCanvasGO = Instantiate(_dragLabelPrefab);
            DontDestroyOnLoad(_labelCanvasGO);
            _labelRootRT   = _labelCanvasGO.GetComponentInChildren<RectTransform>();
            _labelText     = _labelCanvasGO.GetComponentInChildren<Text>();
            _labelCanvasGO.SetActive(false);
            return;
        }

        // Fallback: build procedurally if prefab not yet assigned.
        // TODO: create Assets/Prefabs/UI/GhostDragLabel.prefab and assign it to _dragLabelPrefab.
        _labelCanvasGO = new GameObject("[GhostDragLabel]");
        DontDestroyOnLoad(_labelCanvasGO);
        var canvas = _labelCanvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = _labelCanvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        var rootGO = new GameObject("LabelRoot");
        rootGO.transform.SetParent(_labelCanvasGO.transform, false);
        _labelRootRT           = rootGO.AddComponent<RectTransform>();
        _labelRootRT.sizeDelta = new Vector2(360, 48);
        _labelRootRT.pivot     = new Vector2(0.5f, 1f);

        var bgImg = rootGO.AddComponent<Image>();
        bgImg.color = new Color(0.04f, 0.04f, 0.06f, 0.84f);
        bgImg.raycastTarget = false;

        var textGO = new GameObject("Text");
        textGO.transform.SetParent(rootGO.transform, false);
        var textRT = textGO.AddComponent<RectTransform>();
        textRT.anchorMin        = Vector2.zero;
        textRT.anchorMax        = Vector2.one;
        textRT.anchoredPosition = Vector2.zero;
        textRT.sizeDelta        = Vector2.zero;

        _labelText                    = textGO.AddComponent<Text>();
        _labelText.font               = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _labelText.fontSize           = 18;
        _labelText.fontStyle          = FontStyle.Bold;
        _labelText.color              = Color.white;
        _labelText.alignment          = TextAnchor.MiddleCenter;
        _labelText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _labelText.verticalOverflow   = VerticalWrapMode.Overflow;
        _labelText.raycastTarget      = false;

        var outline = textGO.AddComponent<Outline>();
        outline.effectColor    = new Color(0, 0, 0, 0.9f);
        outline.effectDistance = new Vector2(1, -1);

        _labelCanvasGO.SetActive(false);
    }

    void ShowLabel(string message, Vector2 screenPos)
    {
        if (_labelCanvasGO == null) return;
        if (!_labelCanvasGO.activeSelf) _labelCanvasGO.SetActive(true);
        if (_labelText != null) _labelText.text = message;
        // Position pill just below the cursor in screen pixels
        if (_labelRootRT != null)
            _labelRootRT.position = new Vector3(screenPos.x, screenPos.y - 20f, 0f);
    }

    void HideLabel()
    {
        if (_labelCanvasGO != null && _labelCanvasGO.activeSelf)
            _labelCanvasGO.SetActive(false);
    }
}
