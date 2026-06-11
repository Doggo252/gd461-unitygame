using UnityEngine;

// Per-unit sound: deploy cue, looping engine (separate idle vs moving loops that
// crossfade), shot, hit, and destruction. Reads clips from the global
// AudioService's AudioProfileSO, so the prefab needs no audio references — just
// this component. Everything no-ops safely if no AudioService/profile/clips
// exist, so the game runs without audio.
[RequireComponent(typeof(HealthComponent))]
public class UnitAudio : MonoBehaviour
{
    const float FADE_RATE = 2.5f; // engine crossfade speed (volume units / sec)

    HealthComponent _hp;
    UnitMovement    _mv;
    TankCombatant   _combatant;
    AudioSource     _engineIdle;
    AudioSource     _engineMoving;
    bool            _enginePrimed;

    static AudioProfileSO Profile => AudioService.Instance != null ? AudioService.Instance.Profile : null;

    void Awake()
    {
        _hp        = GetComponent<HealthComponent>();
        _mv        = GetComponent<UnitMovement>();
        _combatant = GetComponent<TankCombatant>();

        _engineIdle   = CreateEngineSource();
        _engineMoving = CreateEngineSource();
    }

    AudioSource CreateEngineSource()
    {
        var src = gameObject.AddComponent<AudioSource>();
        src.loop         = true;
        src.playOnAwake  = false;
        src.spatialBlend = 0.7f;
        src.volume       = 0f;
        src.rolloffMode  = AudioRolloffMode.Linear;
        src.maxDistance  = 70f;
        return src;
    }

    void OnEnable()
    {
        if (_hp != null)        { _hp.OnDamaged += OnHit; _hp.OnDeath += OnDied; }
        if (_combatant != null) _combatant.Fired += OnFire;
    }

    void OnDisable()
    {
        if (_hp != null)        { _hp.OnDamaged -= OnHit; _hp.OnDeath -= OnDied; }
        if (_combatant != null) _combatant.Fired -= OnFire;
    }

    void Start()
    {
        var p = Profile;
        if (p == null) return;
        AudioService.Instance.PlayAt(p.unitSpawn, transform.position);

        var idleClip   = p.engineIdle.Pick();
        var movingClip = p.engineMoving.Pick();
        if (idleClip   != null) _engineIdle.clip   = idleClip;
        if (movingClip != null) _engineMoving.clip = movingClip;
        _enginePrimed = idleClip != null || movingClip != null;
    }

    void Update()
    {
        if (!_enginePrimed) return;
        var p = Profile;
        if (p == null) return;

        bool  moving = _mv != null && _mv.IsMoving;
        float scale  = p.masterVolume * GameSettings.Sfx;

        // Moving loop up while driving; idle loop up while stopped — crossfaded.
        DriveEngine(_engineIdle,   p.engineIdle,   !moving, scale);
        DriveEngine(_engineMoving, p.engineMoving,  moving, scale);
    }

    static void DriveEngine(AudioSource src, AudioProfileSO.Sfx sfx, bool active, float scale)
    {
        if (src == null || src.clip == null) return;
        if (active && !src.isPlaying) src.Play();
        float target = active ? sfx.volume * scale : 0f;
        src.volume = Mathf.MoveTowards(src.volume, target, FADE_RATE * Time.deltaTime);
        if (!active && src.volume <= 0.001f && src.isPlaying) src.Pause();
    }

    void OnFire() { var p = Profile; if (p != null) AudioService.Instance.PlayAt(p.fire, transform.position); }
    void OnHit()  { var p = Profile; if (p != null) AudioService.Instance.PlayAt(p.hit,  transform.position); }

    // The unit is destroyed this frame, so play a detached one-shot that survives.
    void OnDied()
    {
        var p = Profile;
        if (p != null) AudioService.PlayDetached(p, p.tankDestroyed, transform.position);
    }
}
