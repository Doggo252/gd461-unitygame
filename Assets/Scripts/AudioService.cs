using System;
using System.Collections.Generic;
using UnityEngine;

// Central sound service (service-locator pattern allowed by AGENTS.md §2).
// Owns a small pool of AudioSources for positional one-shots and a 2D source
// for UI/global cues. Per-unit SFX are driven by UnitAudio; this service handles
// global/structure cues (structure destroyed, low-HP siren, CP full) by
// subscribing to the existing event channels — no per-structure wiring needed.
public class AudioService : MonoBehaviour
{
    public static AudioService Instance { get; private set; }

    [Header("Data")]
    [SerializeField] AudioProfileSO   _profile;
    [SerializeField] UnitRegistrySO   _registry;   // structures: death + low-HP
    [SerializeField] CpChangedEventSO _cpEvent;     // player CP changes

    [Header("Settings")]
    [SerializeField] int   _playerTeam      = 0;
    [SerializeField] int   _poolSize        = 16;
    [SerializeField] float _lowHpFraction   = 0.25f;

    public AudioProfileSO Profile => _profile;

    AudioSource[] _pool;
    int           _next;
    AudioSource   _src2D;
    int           _lastCp;

    readonly Dictionary<ObjectiveTarget, Action>               _deathHandlers = new();
    readonly Dictionary<ObjectiveTarget, Action<float, float>> _hpHandlers    = new();
    readonly HashSet<ObjectiveTarget>                          _sirened       = new();

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _pool = new AudioSource[Mathf.Max(4, _poolSize)];
        for (int i = 0; i < _pool.Length; i++)
        {
            var go = new GameObject($"sfx_{i}");
            go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>();
            a.playOnAwake   = false;
            a.spatialBlend  = 0.7f;
            a.rolloffMode   = AudioRolloffMode.Linear;
            a.maxDistance   = 70f;
            _pool[i] = a;
        }
        var ui = new GameObject("sfx_2d");
        ui.transform.SetParent(transform, false);
        _src2D = ui.AddComponent<AudioSource>();
        _src2D.playOnAwake  = false;
        _src2D.spatialBlend = 0f;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    void OnEnable()
    {
        if (_cpEvent != null) _cpEvent.OnRaised += OnCp;
    }

    void OnDisable()
    {
        if (_cpEvent != null) _cpEvent.OnRaised -= OnCp;
        UnsubscribeStructures();
    }

    // Objectives register during their own OnEnable, so wait until Start.
    void Start() => SubscribeStructures();

    // ── Public play API ────────────────────────────────────────────────────────

    public void PlayAt(AudioProfileSO.Sfx sfx, Vector3 pos)
    {
        if (_profile == null || sfx == null || !sfx.HasClips) return;
        var clip = sfx.Pick();
        if (clip == null) return;
        var src = _pool[_next];
        _next = (_next + 1) % _pool.Length;
        src.transform.position = pos;
        src.clip         = clip;
        src.volume       = sfx.volume * _profile.masterVolume * GameSettings.Sfx;
        src.pitch        = 1f + UnityEngine.Random.Range(-sfx.pitchJitter, sfx.pitchJitter);
        src.spatialBlend = 0.7f;
        src.Play();
    }

    public void Play2D(AudioProfileSO.Sfx sfx)
    {
        if (_profile == null || sfx == null || !sfx.HasClips) return;
        var clip = sfx.Pick();
        if (clip == null) return;
        _src2D.pitch = 1f + UnityEngine.Random.Range(-sfx.pitchJitter, sfx.pitchJitter);
        _src2D.PlayOneShot(clip, sfx.volume * _profile.masterVolume * GameSettings.Sfx);
    }

    // One-shot that outlives the caller (used for a unit's own death sound, since
    // the unit GameObject is destroyed the same frame).
    public static void PlayDetached(AudioProfileSO profile, AudioProfileSO.Sfx sfx, Vector3 pos)
    {
        if (profile == null || sfx == null || !sfx.HasClips) return;
        var clip = sfx.Pick();
        if (clip == null) return;
        AudioSource.PlayClipAtPoint(clip, pos, sfx.volume * profile.masterVolume * GameSettings.Sfx);
    }

    // Convenience for UI buttons: AudioService.Instance?.PlayClick();
    public void PlayClick() { if (_profile != null) Play2D(_profile.uiClick); }

    // ── Structure subscriptions ──────────────────────────────────────────────────

    void SubscribeStructures()
    {
        if (_registry == null) return;
        foreach (var o in _registry.Objectives)
        {
            if (o == null || o.Health == null || _deathHandlers.ContainsKey(o)) continue;
            var obj = o;
            Action               death = () => OnStructureDeath(obj);
            Action<float, float> hp    = (c, m) => OnStructureHp(obj, c, m);
            obj.Health.OnDeath         += death;
            obj.Health.OnHealthChanged += hp;
            _deathHandlers[obj] = death;
            _hpHandlers[obj]    = hp;
        }
    }

    void UnsubscribeStructures()
    {
        foreach (var kv in _deathHandlers)
            if (kv.Key != null && kv.Key.Health != null) kv.Key.Health.OnDeath -= kv.Value;
        foreach (var kv in _hpHandlers)
            if (kv.Key != null && kv.Key.Health != null) kv.Key.Health.OnHealthChanged -= kv.Value;
        _deathHandlers.Clear();
        _hpHandlers.Clear();
    }

    void OnStructureDeath(ObjectiveTarget o)
    {
        if (o == null || _profile == null) return;
        // Layered positional + 2D playback: AudioSource volume caps at 1.0, so
        // doubling the sources is how this cue gets to be LOUDER than the rest.
        PlayAt(_profile.structureDestroyed, o.transform.position);
        Play2D(_profile.structureDestroyed);
    }

    void OnStructureHp(ObjectiveTarget o, float current, float max)
    {
        if (o == null || o.Team != _playerTeam || max <= 0f) return;
        if (current / max <= _lowHpFraction && current > 0f && !_sirened.Contains(o))
        {
            _sirened.Add(o);
            if (_profile != null) Play2D(_profile.lowHpSiren);
        }
    }

    void OnCp(CpChangedInfo info)
    {
        if (info.team != _playerTeam) return;
        // Chime on every CP gained (not just at capacity).
        if (info.currentCp > _lastCp && _profile != null)
            Play2D(_profile.cpGain);
        _lastCp = info.currentCp;
    }
}
