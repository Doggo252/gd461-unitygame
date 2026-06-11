using System.Collections;
using UnityEngine;

// Attach to any tank prefab. On death, fades all renderers out over 1 second
// using URP Lit material transparency, then destroys the GameObject.
[RequireComponent(typeof(HealthComponent))]
public class TankDeathFader : MonoBehaviour
{
    [SerializeField] float _fadeDuration = 1f;

    static readonly int SurfaceProp = Shader.PropertyToID("_Surface");
    static readonly int BlendProp   = Shader.PropertyToID("_Blend");
    static readonly int ZWriteProp  = Shader.PropertyToID("_ZWrite");
    static readonly int BaseColor   = Shader.PropertyToID("_BaseColor");

    HealthComponent _health;

    void Awake()  => _health = GetComponent<HealthComponent>();
    void OnEnable()  => _health.OnDeath += StartFade;
    void OnDisable() => _health.OnDeath -= StartFade;

    void StartFade() => StartCoroutine(FadeOut());

    IEnumerator FadeOut()
    {
        var renderers = GetComponentsInChildren<Renderer>();

        // Switch all materials to URP transparent mode
        foreach (var rend in renderers)
        {
            foreach (var mat in rend.materials)
            {
                mat.SetFloat(SurfaceProp, 1f); // 1 = Transparent
                mat.SetFloat(BlendProp,   0f); // 0 = Alpha
                mat.SetFloat(ZWriteProp,  0f);
                mat.renderQueue = 3000;
            }
        }

        float elapsed = 0f;
        while (elapsed < _fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(1f - elapsed / _fadeDuration);
            foreach (var rend in renderers)
            {
                foreach (var mat in rend.materials)
                {
                    var c = mat.GetColor(BaseColor);
                    c.a = alpha;
                    mat.SetColor(BaseColor, c);
                }
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}
