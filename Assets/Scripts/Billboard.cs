using UnityEngine;

// Keeps a world-space transform facing the main camera. Used for the
// "DESTROYED" tower label so it stays readable from any camera angle.
public class Billboard : MonoBehaviour
{
    [SerializeField] bool   _lockY     = true;
    [SerializeField] float  _zRotation = -18f; // slight slant for the destroyed label
    Camera _cam;

    void LateUpdate()
    {
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return;

        Vector3 fwd = _cam.transform.forward;
        if (_lockY) fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.001f) return;

        var look = Quaternion.LookRotation(fwd);
        // Apply slant in screen-space Z so the text reads slanted, not yawed.
        transform.rotation = look * Quaternion.Euler(0f, 0f, _zRotation);
    }
}
