using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class HealthBarCanvasHost : MonoBehaviour
{
    [SerializeField] HealthBarServiceSO _service;
    RectTransform _rt;

    void Awake()     { _rt = (RectTransform)transform; }
    void OnEnable()  { if (_service != null) _service.RegisterCanvas(_rt); }
    void OnDisable() { if (_service != null) _service.UnregisterCanvas(_rt); }
}
