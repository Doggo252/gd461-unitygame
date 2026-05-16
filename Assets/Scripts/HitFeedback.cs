using System.Collections;
using UnityEngine;

// Standalone hit-feedback component. Attach to any GameObject that has a
// HealthComponent. Wire _health and _profile in the Inspector.
// Subscribes to HealthComponent.OnDamaged (fires on damage only, not on
// initialization or healing) — never calls damage scripts directly.
public class HitFeedback : MonoBehaviour
{
    [SerializeField] HitEffectProfileSO _profile;
    [SerializeField] HealthComponent    _health;

    Renderer[]            _renderers;
    MaterialPropertyBlock _flashMpb;
    MaterialPropertyBlock _clearMpb; // empty — clears overrides so team-color material instance shows through
    Vector3               _originalScale;
    Coroutine             _flashRoutine;
    Coroutine             _squashRoutine;

    static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
    static readonly int BaseColorID     = Shader.PropertyToID("_BaseColor");

    void Awake()
    {
        _renderers     = GetComponentsInChildren<Renderer>(true);
        _flashMpb      = new MaterialPropertyBlock();
        _clearMpb      = new MaterialPropertyBlock(); // intentionally empty
        _originalScale = transform.localScale;

        // Fallback: if _health not wired in Inspector, look on same GameObject
        if (_health == null) _health = GetComponent<HealthComponent>();
    }

    void OnEnable()
    {
        if (_health != null) _health.OnDamaged += OnDamaged;
    }

    void OnDisable()
    {
        if (_health != null) _health.OnDamaged -= OnDamaged;
        // Safety: ensure flash is fully cleared if component is disabled mid-flash
        ClearFlash();
    }

    void OnDamaged()
    {
        if (_profile == null) return;
        if (_flashRoutine  != null) StopCoroutine(_flashRoutine);
        if (_squashRoutine != null) StopCoroutine(_squashRoutine);
        _flashRoutine  = StartCoroutine(FlashRoutine());
        _squashRoutine = StartCoroutine(SquashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        // Override _BaseColor to white (works on all URP Lit renderers regardless
        // of _EMISSION keyword state) + HDR emission for parts that support it.
        _flashMpb.SetColor(BaseColorID,     _profile.flashColor);
        _flashMpb.SetColor(EmissionColorID, _profile.emissionFlashColor);
        foreach (var r in _renderers)
            r.SetPropertyBlock(_flashMpb);

        yield return new WaitForSeconds(_profile.flashDuration);

        ClearFlash();
    }

    IEnumerator SquashRoutine()
    {
        float   t        = 0f;
        Vector3 squashed = new Vector3(
            _originalScale.x * _profile.stretchScaleXZ,
            _originalScale.y * _profile.squashScaleY,
            _originalScale.z * _profile.stretchScaleXZ);

        while (t < _profile.squashDuration)
        {
            transform.localScale = Vector3.Lerp(_originalScale, squashed, t / _profile.squashDuration);
            t += Time.deltaTime;
            yield return null;
        }

        t = 0f;
        while (t < _profile.recoverDuration)
        {
            transform.localScale = Vector3.Lerp(squashed, _originalScale, t / _profile.recoverDuration);
            t += Time.deltaTime;
            yield return null;
        }
        transform.localScale = _originalScale;
    }

    void ClearFlash()
    {
        // Passing an empty PropertyBlock removes all overrides so the
        // material instance's team color (_BaseColor set by TankCombatant) shows through.
        foreach (var r in _renderers)
            r.SetPropertyBlock(_clearMpb);
    }
}
