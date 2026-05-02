using System.Collections;
using UnityEngine;

[RequireComponent(typeof(HealthComponent))]
public class HitFlash : MonoBehaviour
{
    [SerializeField] float _duration  = 0.15f;
    [SerializeField] Color _flashColor = new Color(1f, 0.08f, 0.08f, 1f);

    HealthComponent _health;
    Renderer[]      _renderers;
    bool            _flashing;

    void Awake()
    {
        _health    = GetComponent<HealthComponent>();
        _renderers = GetComponentsInChildren<Renderer>();
    }

    void OnEnable()  { if (_health != null) _health.OnDamaged += TriggerFlash; }
    void OnDisable() { if (_health != null) _health.OnDamaged -= TriggerFlash; }

    void TriggerFlash()
    {
        if (!_flashing) StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        _flashing = true;

        var block = new MaterialPropertyBlock();
        block.SetColor("_BaseColor", _flashColor);
        block.SetColor("_Color",     _flashColor);

        foreach (var r in _renderers) r.SetPropertyBlock(block);
        yield return new WaitForSeconds(_duration);
        foreach (var r in _renderers) r.SetPropertyBlock(null);

        _flashing = false;
    }
}
