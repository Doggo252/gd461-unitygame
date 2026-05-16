using UnityEngine;

[CreateAssetMenu(fileName = "HitEffectProfile", menuName = "Tank Royale/VFX/Hit Effect Profile")]
public class HitEffectProfileSO : ScriptableObject
{
    [Header("Flash")]
    public float flashDuration = 0.1f;
    public Color flashColor    = Color.white;
    [ColorUsage(true, true)]
    public Color emissionFlashColor = new Color(3f, 3f, 3f, 1f); // HDR white

    [Header("Squash & Stretch")]
    public float squashDuration  = 0.06f;
    public float squashScaleY    = 0.75f;
    public float stretchScaleXZ  = 1.15f;
    public float recoverDuration = 0.08f;
}
