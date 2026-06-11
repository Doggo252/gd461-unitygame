using UnityEngine;
using UnityEngine.UI;

// Binds the (editor-built) options sliders to GameSettings. Holds references to
// pre-existing UI only — no procedural UI construction (AGENTS.md §5).
public class SettingsPanelUI : MonoBehaviour
{
    [Header("Sliders (0..1)")]
    [SerializeField] Slider _master;
    [SerializeField] Slider _sfx;
    [SerializeField] Slider _music;

    [Header("Optional % labels")]
    [SerializeField] Text _masterPct;
    [SerializeField] Text _sfxPct;
    [SerializeField] Text _musicPct;

    void OnEnable()
    {
        Bind(_master, GameSettings.Master, OnMaster);
        Bind(_sfx,    GameSettings.Sfx,    OnSfx);
        Bind(_music,  GameSettings.Music,  OnMusic);
        Refresh();
    }

    void OnDisable()
    {
        if (_master != null) _master.onValueChanged.RemoveListener(OnMaster);
        if (_sfx    != null) _sfx.onValueChanged.RemoveListener(OnSfx);
        if (_music  != null) _music.onValueChanged.RemoveListener(OnMusic);
        GameSettings.Save();
    }

    static void Bind(Slider s, float value, UnityEngine.Events.UnityAction<float> cb)
    {
        if (s == null) return;
        s.minValue = 0f; s.maxValue = 1f;
        s.SetValueWithoutNotify(value);
        s.onValueChanged.RemoveListener(cb);
        s.onValueChanged.AddListener(cb);
    }

    void OnMaster(float v) { GameSettings.SetMaster(v); Refresh(); }
    void OnSfx(float v)    { GameSettings.SetSfx(v);    Refresh(); }
    void OnMusic(float v)  { GameSettings.SetMusic(v);  Refresh(); }

    void Refresh()
    {
        if (_masterPct != null) _masterPct.text = Mathf.RoundToInt(GameSettings.Master * 100f) + "%";
        if (_sfxPct    != null) _sfxPct.text    = Mathf.RoundToInt(GameSettings.Sfx    * 100f) + "%";
        if (_musicPct  != null) _musicPct.text  = Mathf.RoundToInt(GameSettings.Music  * 100f) + "%";
    }
}
