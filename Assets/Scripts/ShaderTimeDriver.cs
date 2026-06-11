using UnityEngine;

// Feeds an unscaled, pause-proof, speed-scaled time value into a per-renderer
// material vector every frame. Time-driven shaders (e.g. flowing water) read
// this (.x) instead of the built-in _Time, so they keep animating even when
// Time.timeScale == 0 (pre-battle hold, pause menu, game-over screen).
//
// Two deliberate choices work around SRP Batcher behaviour observed in this
// project (URP, Unity 6):
//   * A Vector property (set via Material.SetVector) is used rather than a
//     float. A runtime Material.SetFloat does not reliably refresh an
//     SRP-batched material's UnityPerMaterial buffer; SetVector (float4) does.
//   * The flow speed is multiplied in here, so the shader's animation depends
//     only on this reliably-bound vector, not on a separate scalar uniform.
//
// Written to the renderer's material instance (cloned on first access in play)
// so the shared asset is never mutated. Reusable: knows nothing about rivers.
[RequireComponent(typeof(Renderer))]
public class ShaderTimeDriver : MonoBehaviour
{
    [Tooltip("Per-material Vector the shader reads (.x) for pause-proof animation.")]
    [SerializeField] string _propertyName = "_FlowTimeVec";

    [Tooltip("Flow-speed multiplier applied to unscaled time before it is written.")]
    [SerializeField] float _speed = 1.4f;

    Renderer _renderer;
    int      _id;
    float    _accumulated;   // animation time; advances only while unpaused

    // Freezes every driver at once (the game-over screen wants the river still).
    // Reset in OnEnable so a fresh match always animates.
    public static bool GlobalPaused;

    void OnEnable()
    {
        _renderer    = GetComponent<Renderer>();
        _id          = Shader.PropertyToID(_propertyName);
        GlobalPaused = false;
    }

    // Update runs every rendered frame regardless of Time.timeScale, so the
    // value keeps advancing while gameplay is frozen (pre-battle hold, pause) —
    // unless GlobalPaused freezes it deliberately.
    void Update()
    {
        if (!Application.isPlaying || _renderer == null) return;
        if (!GlobalPaused) _accumulated += Time.unscaledDeltaTime;
        _renderer.material.SetVector(_id, new Vector4(_accumulated * _speed, 0f, 0f, 0f));
    }
}
