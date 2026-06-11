using UnityEngine;

// Persistent user audio settings (PlayerPrefs). Master drives the global
// AudioListener volume; SFX scales gameplay sound effects (AudioService /
// UnitAudio multiply by it); Music is stored for the future music system.
// These are user preferences, not game-balance data, so PlayerPrefs is the
// right home (not a balancing ScriptableObject).
public static class GameSettings
{
    const string KMaster = "opt_master";
    const string KSfx    = "opt_sfx";
    const string KMusic  = "opt_music";

    static bool  _loaded;
    static float _master = 1f, _sfx = 1f, _music = 0.7f;

    public static float Master { get { Load(); return _master; } }
    public static float Sfx    { get { Load(); return _sfx;    } }
    public static float Music  { get { Load(); return _music;  } }

    static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        _master = PlayerPrefs.GetFloat(KMaster, 1f);
        _sfx    = PlayerPrefs.GetFloat(KSfx,    1f);
        _music  = PlayerPrefs.GetFloat(KMusic,  0.7f);
        ApplyMaster();
    }

    public static void SetMaster(float v) { Load(); _master = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KMaster, _master); ApplyMaster(); }
    public static void SetSfx(float v)    { Load(); _sfx    = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KSfx,    _sfx);   }
    public static void SetMusic(float v)  { Load(); _music  = Mathf.Clamp01(v); PlayerPrefs.SetFloat(KMusic,  _music); }

    public static void Save() => PlayerPrefs.Save();

    static void ApplyMaster() => AudioListener.volume = _master;
}
