using DG.Tweening;
using System.Collections;
using UnityEngine;

public class GlitchController : MonoBehaviour
{
    [Header("Material")]
    [SerializeField] private Material glitchMaterial;

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private void Awake()
    {
        // Make sure the glitch is disabled by default.
        if (glitchMaterial != null)
        {
            glitchMaterial.SetFloat(IntensityId, 0f);
        }
    }

    public void TriggerGlitch(float intensity, float time)
    {
        if (glitchMaterial == null) return;

        DOTween.To(
            () => glitchMaterial.GetFloat(IntensityId),
            value => glitchMaterial.SetFloat(IntensityId, value),
            intensity,
            time
        ).SetEase(Ease.OutCubic);
    }

}