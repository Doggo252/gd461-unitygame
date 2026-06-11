using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Cinemachine;

// Drives camera shake through the CinemachineCamera's noise component
// (CinemachineBasicMultiChannelPerlin): events spike the amplitude, which then
// decays to zero. Strengths:
//   • structure destroyed  → heavy thud
//   • any unit killed      → short kick
//   • unit deployed        → tiny bump
// Subscribes to the existing event channels (CombatFeedback, TankCombatant,
// objective health events via the registry) — nothing calls it directly.
public class ScreenShakeService : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] UnitRegistrySO _registry;

    [Header("Cinemachine noise (on the CinemachineCamera)")]
    [SerializeField] CinemachineBasicMultiChannelPerlin _noise;

    // The 6D Shake profile maps amplitude→camera offset at roughly 0.012 world
    // units per amplitude unit (measured on this ortho-17 camera), so the
    // strengths below are calibrated in profile-amplitude terms.
    [Header("Strengths (noise amplitude)")]
    [SerializeField] float _towerDestroyed = 22f;   // ≈ 0.26 wu ≈ 8 px thud
    [SerializeField] float _unitKilled     = 6f;
    [SerializeField] float _unitSpawn      = 2f;
    [Tooltip("Seconds for a shake to decay to zero.")]
    [SerializeField] float _duration       = 0.30f;

    float _amplitude;
    float _decayRate;

    readonly Dictionary<ObjectiveTarget, Action> _deathHandlers = new();

    void OnEnable()
    {
        CombatFeedback.OnHit     += OnHit;
        TankCombatant.AnySpawned += OnSpawn;
        if (_noise != null) _noise.AmplitudeGain = 0f;
    }

    void OnDisable()
    {
        CombatFeedback.OnHit     -= OnHit;
        TankCombatant.AnySpawned -= OnSpawn;
        foreach (var kv in _deathHandlers)
            if (kv.Key != null && kv.Key.Health != null) kv.Key.Health.OnDeath -= kv.Value;
        _deathHandlers.Clear();
        if (_noise != null) _noise.AmplitudeGain = 0f;
    }

    // Objectives register in their own OnEnable — subscribe after, in Start.
    void Start()
    {
        if (_registry == null) return;
        foreach (var o in _registry.Objectives)
        {
            if (o == null || o.Health == null || _deathHandlers.ContainsKey(o)) continue;
            Action death = () => Shake(_towerDestroyed);
            o.Health.OnDeath += death;
            _deathHandlers[o] = death;
        }
    }

    void OnHit(CombatFeedback.Hit hit)
    {
        if (hit.lethal) Shake(_unitKilled);
    }

    void OnSpawn(Vector3 _) => Shake(_unitSpawn);

    public void Shake(float magnitude)
    {
        if (magnitude <= _amplitude) return;
        _amplitude = magnitude;
        _decayRate = magnitude / Mathf.Max(0.05f, _duration);
    }

    // Unscaled time so the tower-destroyed thud still lands when the kill also
    // freezes the game (HQ destruction → game over at timeScale 0).
    void Update()
    {
        if (_noise == null) return;
        _amplitude = Mathf.MoveTowards(_amplitude, 0f, _decayRate * Time.unscaledDeltaTime);
        _noise.AmplitudeGain = _amplitude;
    }
}
