using UnityEngine;
using UnityEngine.UI;

// Per-slot Clash-style affordability indicator.
// Subscribes to CpChangedEventSO and to DeckManager.HandChanged.
// Drives:
//   • When affordable → pulse a yellow glow on _glowBorder
//   • When not affordable → dim the slot to alpha 0.55
[RequireComponent(typeof(RectTransform))]
public class CardSlotAffordabilityGlow : MonoBehaviour
{
    [SerializeField] int               _slotIndex;
    [SerializeField] DeckManager       _deckManager;
    [SerializeField] CommandPointsManager _cpManager;
    [SerializeField] CpChangedEventSO  _cpChangedEvent;

    [Header("UI — wire to slot children")]
    [SerializeField] Graphic _glowBorder;        // overlay image, animated alpha when affordable
    [SerializeField] Graphic[] _dimmableGraphics; // entire slot contents — dimmed when unaffordable

    [Header("Tuning")]
    [SerializeField] Color _glowColor       = new Color(1f, 0.92f, 0.25f, 1f);
    [SerializeField] float _pulseSpeed      = 1.6f;   // calmer cadence
    [SerializeField] float _glowMinAlpha    = 0.08f;  // gentler low
    [SerializeField] float _glowMaxAlpha    = 0.32f;  // gentler high
    [SerializeField] float _unaffordableAlpha = 0.55f;
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

    void Start() => Refresh();

    void Update()
    {
        if (_glowBorder == null) return;
        if (_affordable)
        {
            float k = (Mathf.Sin(Time.time * _pulseSpeed) + 1f) * 0.5f;
            float a = Mathf.Lerp(_glowMinAlpha, _glowMaxAlpha, k);
            var c = _glowColor; c.a = a;
            _glowBorder.color = c;
        }
        else
        {
            var c = _glowColor; c.a = 0f;
            _glowBorder.color = c;
        }
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
