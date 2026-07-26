using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class RendererTextureOverride : MonoBehaviour
{
    [SerializeField] private Texture texture;

    [Tooltip("URP Lit / Shader Graph 通常为 _BaseMap")]
    [SerializeField] private string textureProperty = "_BaseMap";
    [SerializeField] private float intensity = 1.0f;

    [SerializeField] private int materialIndex = 0;

    private Renderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;

    private void OnEnable()
    {
        ApplyTexture();
    }

    private void OnValidate()
    {
        ApplyTexture();
    }

    public void ApplyTexture()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<Renderer>();

        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        targetRenderer.GetPropertyBlock(propertyBlock, materialIndex);

        propertyBlock.SetTexture(
            Shader.PropertyToID(textureProperty),
            texture
        );
        propertyBlock.SetTexture(
            Shader.PropertyToID("_EmissionMap"),
            texture
        );
        propertyBlock.SetFloat(
            Shader.PropertyToID("_Intensity"),
            intensity
        );

        targetRenderer.SetPropertyBlock(
            propertyBlock,
            materialIndex
        );
    }

    public void ClearTextureOverride()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        targetRenderer.SetPropertyBlock(null, materialIndex);
    }
}