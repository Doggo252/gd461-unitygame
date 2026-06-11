using UnityEngine;
using UnityEngine.UI;

// Per-slot affordability indicator.
// Subscribes to CpChangedEventSO and to DeckManager.HandChanged.
// Affordable cards read normal / full colour; unaffordable cards are dimmed
// hard (and their glow overlay stays off). The old pulsing yellow full-card
// glow washed the art out — affordability is now conveyed by dimming only.
[RequireComponent(typeof(RectTransform))]
public class CardSlotAffordabilityGlow : MonoBehaviour
{
    [SerializeField] int               _slotIndex;
    [SerializeField] DeckManager       _deckManager;
    [SerializeField] CommandPointsManager _cpManager;
    [SerializeField] CpChangedEventSO  _cpChangedEvent;

    [Header("UI — wire to slot children")]
    [SerializeField] Graphic _glowBorder;        // legacy overlay — kept transparent
    [SerializeField] Graphic[] _dimmableGraphics; // entire slot contents — dimmed when unaffordable

    [Header("Tuning")]
    [SerializeField] float _unaffordableAlpha = 0.35f;
    [SerializeField] float _affordableAlpha   = 1.00f;

    bool _affordable;
    System.Action _onHandChanged;
    System.Action<CpChangedInfo> _onCpChanged;

    public void Initialize(int slotIndex, DeckManager dm, CommandPointsManager cp,
                           CpChangedEventSO evt, Graphic glow, Graphic[] dimmable)
    {
        _slotIndex      = slotIndex;
        _deckManager    = dm;
        _cpManager      = cp;
        _cpChangedEvent = evt;
        _glowBorder     = glow;
        _dimmableGraphics = dimmable;
    }

    void Awake()
    {
        _onHandChanged = Refresh;
        _onCpChanged   = _ => Refresh();
    }

    void OnEnable()
    {
        if (_deckManager    != null) _deckManager.HandChanged += _onHandChanged;
        if (_cpChangedEvent != null) _cpChangedEvent.OnRaised  += _onCpChanged;
    }

    void OnDisable()
    {
        if (_deckManager    != null) _deckManager.HandChanged -= _onHandChanged;
        if (_cpChangedEvent != null) _cpChangedEvent.OnRaised  -= _onCpChanged;
    }

    void Start()
    {
        // The legacy glow overlay stays permanently transparent.
        if (_glowBorder != null)
        {
            var c = _glowBorder.color; c.a = 0f;
            _glowBorder.color = c;
        }
        Refresh();
    }

    public void Refresh()
    {
        if (_deckManager == null) return;
        var hand = _deckManager.Hand;
        var card = hand != null && _slotIndex < hand.Length ? hand[_slotIndex] : null;

        _affordable = card != null && _cpManager != null && _cpManager.CurrentCp >= card.cpCost;

        // Dim entire slot when unaffordable
        if (_dimmableGraphics != null)
        {
            float a = _affordable ? _affordableAlpha : _unaffordableAlpha;
            foreach (var g in _dimmableGraphics)
            {
                if (g == null) continue;
                var c = g.color; c.a = a; g.color = c;
            }
        }
    }
}
