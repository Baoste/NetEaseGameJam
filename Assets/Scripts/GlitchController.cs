using DG.Tweening;
using System.Collections;
using UnityEngine;

public class GlitchController : MonoBehaviour
{
    [Header("Material")]
    [SerializeField] private Material glitchMaterial;

    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
    private static readonly int WhiteBloomId = Shader.PropertyToID("_WhiteBloom");

    private void Awake()
    {
        // Make sure the glitch is disabled by default.
        //if (glitchMaterial != null)
        //{
        //    glitchMaterial.SetFloat(IntensityId, 0f);
        //}
    }

    public void TriggerGlitch(float intensity, float time)
    {
        if (glitchMaterial == null) return;

        Vector2 startValue = new Vector2(
            glitchMaterial.GetFloat(IntensityId),
            glitchMaterial.GetFloat(WhiteBloomId)
        );

        DOTween.To(
            () => startValue,
            value =>
            {
                startValue = value;

                glitchMaterial.SetFloat(IntensityId, value.x);
                glitchMaterial.SetFloat(WhiteBloomId, value.y * 1.2f);
            },
            new Vector2(intensity, intensity),
            time
        );
    }

}