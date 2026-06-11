using UnityEngine;
using UnityEngine.UI;

// Manages difficulty selection. Can live either in its own full-screen panel
// (legacy flow) or inside a settings overlay — controlled by whether
// _factionSelectPanel is wired. If wired, selecting a difficulty also navigates
// to that panel. If not wired, the component just applies + highlights in-place.
public class DifficultySelectUI : MonoBehaviour
{
    [Header("Panels (leave _factionSelectPanel null for settings-overlay mode)")]
    [SerializeField] GameObject _difficultyPanel;
    [SerializeField] GameObject _factionSelectPanel;

    [Header("Buttons")]
    [SerializeField] Button _easyBtn;
    [SerializeField] Button _normalBtn;
    [SerializeField] Button _hardBtn;

    [Header("Active highlight (Image tint for selected button)")]
    [SerializeField] Color _activeColor   = new Color(0.55f, 0.38f, 0.08f, 1f);   // muted amber
    [SerializeField] Color _inactiveColor = new Color(0.18f, 0.22f, 0.14f, 1f);

    [Header("Data")]
    [SerializeField] SelectedDifficultySO _selectedDifficulty;

    // ── Lifecycle ─────────────────────────────────────────────────────────────────

    void OnEnable()
    {
        if (_easyBtn   != null) _easyBtn.onClick.AddListener(SelectEasy);
        if (_normalBtn != null) _normalBtn.onClick.AddListener(SelectNormal);
        if (_hardBtn   != null) _hardBtn.onClick.AddListener(SelectHard);

        RefreshHighlight();
    }

    void OnDisable()
    {
        if (_easyBtn   != null) _easyBtn.onClick.RemoveListener(SelectEasy);
        if (_normalBtn != null) _normalBtn.onClick.RemoveListener(SelectNormal);
        if (_hardBtn   != null) _hardBtn.onClick.RemoveListener(SelectHard);
    }

    // ── Button handlers ──────────────────────────────────────────────────────────

    void SelectEasy()   => Apply(_selectedDifficulty?.Easy);
    void SelectNormal() => Apply(_selectedDifficulty?.Normal);
    void SelectHard()   => Apply(_selectedDifficulty?.Hard);

    void Apply(AIDifficultyConfigSO config)
    {
        if (_selectedDifficulty == null || config == null) return;
        _selectedDifficulty.Active = config;
        RefreshHighlight();
        // Navigate only when a destination panel is wired (standalone-panel flow).
        // In settings-overlay mode _factionSelectPanel is null, so we stay put.
        if (_factionSelectPanel != null)
        {
            if (_difficultyPanel   != null) _difficultyPanel.SetActive(false);
            _factionSelectPanel.SetActive(true);
        }
    }

    // ── Visuals ──────────────────────────────────────────────────────────────────

    void RefreshHighlight()
    {
        if (_selectedDifficulty == null) return;
        var active = _selectedDifficulty.Active;
        TintBtn(_easyBtn,   active == _selectedDifficulty.Easy);
        TintBtn(_normalBtn, active == _selectedDifficulty.Normal);
        TintBtn(_hardBtn,   active == _selectedDifficulty.Hard);
    }

    void TintBtn(Button btn, bool on)
    {
        if (btn == null) return;
        var img = btn.GetComponent<UnityEngine.UI.Image>();
        if (img != null) img.color = on ? _activeColor : _inactiveColor;
    }
}
