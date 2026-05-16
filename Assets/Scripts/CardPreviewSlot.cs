using UnityEngine;
using UnityEngine.UI;

// Attach to any card slot (menu CardEntry or HUD slot) that shows a live 3D
// tank model preview. Calls CardModelRenderer.RequestRender and pipes the
// resulting RenderTexture to the assigned RawImage.
//
// Wire _rawImage in the Inspector (the RawImage inside the card slot panel).
public class CardPreviewSlot : MonoBehaviour
{
    [SerializeField] RawImage _rawImage;

    TankType? _current;

    // ── Public ────────────────────────────────────────────────────────────────

    public void SetCard(UnitDataSO card)
    {
        if (card == null) { Clear(); return; }
        if (_current.HasValue && _current.Value == card.tankType) return;   // unchanged

        _current = card.tankType;
        if (_rawImage != null) _rawImage.texture = null;   // blank while pending

        if (CardModelRenderer.Instance != null)
        {
            var expected = card.tankType;   // capture for closure
            CardModelRenderer.Instance.RequestRender(expected, rt =>
            {
                // Guard: slot may have been reassigned while request was queued
                if (_rawImage != null && _current.HasValue && _current.Value == expected)
                    _rawImage.texture = rt;
            });
        }
    }

    public void Clear()
    {
        _current = null;
        if (_rawImage != null) _rawImage.texture = null;
    }
}
