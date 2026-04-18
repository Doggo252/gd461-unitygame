using System;
using UnityEngine;

// Single-responsibility: owns HP, damage, healing, and death notification.
// Does NOT know about tanks, teams, or game rules.
public class HealthComponent : MonoBehaviour
{
    public event Action<float, float> OnHealthChanged; // (current, max)
    public event Action              OnDeath;

    float _current;
    float _max;
    bool  _dead;

    public float Current => _current;
    public float Max     => _max;
    public bool  IsDead  => _dead;

    public void Initialize(float maxHp)
    {
        _max     = maxHp;
        _current = maxHp;
        _dead    = false;
    }

    public void TakeDamage(float amount)
    {
        if (_dead || amount <= 0f) return;
        _current = Mathf.Max(0f, _current - amount);
        OnHealthChanged?.Invoke(_current, _max);
        if (_current <= 0f && !_dead)
        {
            _dead = true;
            OnDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (_dead || amount <= 0f) return;
        _current = Mathf.Min(_max, _current + amount);
        OnHealthChanged?.Invoke(_current, _max);
    }
}
