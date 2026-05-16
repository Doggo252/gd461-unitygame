using System;
using UnityEngine;
using UnityEngine.UI;

// Drives the 4 pre-built card slot panels in the HUD.
// Refreshes whenever the hand changes or CP changes (affordability colours).
// AGENTS §5: all panels are pre-built in the Editor; this script only updates values.
public class CardHandUI : MonoBehaviour
{
    [Serializable]
    public struct CardSlotUI
    {
        public GameObject    panel;        // root — activated/deactivated when slot is empty
        public Text          nameText;     // human-readable tank name
        public Text          typeText;     // enum name fallback
        public Text          cpCostText;
        public Text          hpText;
        public Text          statsText;    // ATK ARM PEN SPD MOV RNG in one block
        public Text          keywordsText;
        public Image         background;   // tinted: selected / normal / unaffordable
        public Image         flagIcon;     // national flag (top-left corner)
        public RawImage      tankModelView;// live 3D preview
        public CardPreviewSlot previewSlot; // drives the RenderTexture update
    }

    [SerializeField] CardSlotUI[]         _slots = new CardSlotUI[4];
    [SerializeField] DeckManager          _deckManager;
    [SerializeField] CommandPointsManager _cpManager;
    [SerializeField] CpChangedEventSO     _cpChangedEvent;

    [Header("Slot Colours")]
    [SerializeField] Color _selectedColor     = new Color(0.9f, 0.8f, 0.1f, 1.0f);
    [SerializeField] Color _normalColor       = new Color(0.15f, 0.15f, 0.15f, 0.85f);
    [SerializeField] Color _unaffordableColor = new Color(0.50f, 0.20f, 0.20f, 0.85f);

    // Named delegates for correct OnDisable unsubscription.
    Action          _onHandChanged;
    Action<CpChangedInfo> _onCpChanged;

    void Awake()
    {
        _onHandChanged = Refresh;
        _onCpChanged   = _ => Refresh();
    }

    void OnEnable()
    {
        if (_deckManager    != null) _deckManager.HandChanged    += _onHandChanged;
        if (_cpChangedEvent != null) _cpChangedEvent.OnRaised    += _onCpChanged;
    }

    void OnDisable()
    {
        if (_deckManager    != null) _deckManager.HandChanged    -= _onHandChanged;
        if (_cpChangedEvent != null) _cpChangedEvent.OnRaised    -= _onCpChanged;
    }

    void Start() => Refresh();

    // ── Public ────────────────────────────────────────────────────────────────

    public void Refresh()
    {
        if (_deckManager == null) return;
        var hand = _deckManager.Hand;
        int sel  = _deckManager.SelectedHandIndex;

        for (int i = 0; i < 4; i++)
        {
            var s = _slots[i];
            var c = hand[i];

            if (s.panel == null) continue;
            s.panel.SetActive(c != null);
            if (c == null) { s.previewSlot?.Clear(); continue; }

            string displayName = !string.IsNullOrEmpty(c.tankName) ? c.tankName
                               : c.tankType.ToString().Replace('_', ' ');
            if (s.nameText     != null) s.nameText.text     = displayName;
            if (s.typeText     != null) s.typeText.text     = displayName;
            if (s.cpCostText   != null) s.cpCostText.text   = $"CP {c.cpCost}";
            if (s.hpText       != null) s.hpText.text       = $"HP {c.maxHp:F0}";
            if (s.statsText    != null)
                s.statsText.text = $"ATK {c.atk:F0}  ARM {c.arm:F0}  PEN {c.pen:F0}\n" +
                                   $"SPD {c.spd:F1}  MOV {c.mov:F1}  RNG {c.rng:F1}";
            if (s.keywordsText != null)
                s.keywordsText.text = c.keywords == 0 ? "" : c.keywords.ToString();
            if (s.flagIcon     != null) s.flagIcon.sprite   = c.flagSprite;
            if (s.previewSlot  != null) s.previewSlot.SetCard(c);

            bool canAfford = _cpManager != null && _cpManager.CurrentCp >= c.cpCost;
            if (s.background != null)
                s.background.color = i == sel    ? _selectedColor
                                   : canAfford   ? _normalColor
                                                 : _unaffordableColor;
        }
    }
}
