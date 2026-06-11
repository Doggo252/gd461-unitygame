using UnityEngine;

// Slow cinematic drift for the menu-scene camera: a gentle elliptical sway
// around its authored pose, always looking at a fixed focus point. Uses
// unscaled time so it keeps moving regardless of timeScale. Reusable: knows
// nothing about menus — attach to any camera that should idle cinematically.
public class MenuCameraDrift : MonoBehaviour
{
    [Tooltip("World point the camera keeps looking at.")]
    [SerializeField] Vector3 _focusPoint = Vector3.zero;
    [Tooltip("Horizontal sway radius in world units.")]
    [SerializeField] float _swayX = 6f;
    [Tooltip("Forward/back sway radius in world units.")]
    [SerializeField] float _swayZ = 3f;
    [Tooltip("Full sway cycle duration in seconds.")]
    [SerializeField] float _period = 40f;

    Vector3 _homePos;

    void Awake() => _homePos = transform.position;

    void Update()
    {
        float t = Time.unscaledTime * (Mathf.PI * 2f) / Mathf.Max(1f, _period);
        var offset = new Vector3(Mathf.Sin(t) * _swayX, 0f, Mathf.Sin(t * 0.7f) * _swayZ);
        transform.position = _homePos + offset;
        transform.LookAt(_focusPoint);
    }
}
