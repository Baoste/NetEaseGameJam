using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines one local directional-light volume and sends at most two volumes
/// to every target Renderer that uses Custom/URP/BoxUnlitVolume.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class BoxUnlitVolumeController : MonoBehaviour
{
    [Header("Target Renderers")]
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private bool autoCollectRenderers;
    [SerializeField] private Transform searchRoot;
    [SerializeField] private bool includeInactive = true;

    [Header("Volume Light")]
    [Tooltip("Light travel direction. With Local Direction enabled it follows this Box's rotation.")]
    [SerializeField] private Vector3 lightDirection = new Vector3(0f, -1f, 0f);
    [SerializeField] private bool localDirection = true;
    [ColorUsage(false, true)]
    [SerializeField] private Color lightColor = Color.white;
    [Min(0f)]
    [SerializeField] private float lightIntensity = 1f;
    [Tooltip("Direction-independent light inside this volume.")]
    [Range(0f, 1f)]
    [SerializeField] private float ambientLighting = 0.1f;
    [Min(0f)]
    [SerializeField] private float boxFadeWidth;
    [Tooltip("Lower values fill the first shader slot when more than two volumes target the same Renderer.")]
    [SerializeField] private int priority;

    [Header("Update")]
    [SerializeField] private bool updateEveryFrame = true;

    [Header("Debug")]
    [SerializeField] private bool drawSelectedGizmo = true;
    [SerializeField] private bool drawAlwaysGizmo;

    private BoxCollider boxCollider;
    private static readonly HashSet<BoxUnlitVolumeController> ActiveVolumes = new HashSet<BoxUnlitVolumeController>();
    private static readonly HashSet<Renderer> DirtyRenderers = new HashSet<Renderer>();
    private static readonly List<BoxUnlitVolumeController> MatchingVolumes = new List<BoxUnlitVolumeController>(2);
    private static MaterialPropertyBlock propertyBlock;

    private static readonly int VolumeCountId = Shader.PropertyToID("_LightVolumeCount");
    private static readonly int WorldToLocal0Id = Shader.PropertyToID("_LightBoxWorldToLocal0");
    private static readonly int WorldToLocal1Id = Shader.PropertyToID("_LightBoxWorldToLocal1");
    private static readonly int Center0Id = Shader.PropertyToID("_LightBoxCenter0");
    private static readonly int Center1Id = Shader.PropertyToID("_LightBoxCenter1");
    private static readonly int HalfSize0Id = Shader.PropertyToID("_LightBoxHalfSize0");
    private static readonly int HalfSize1Id = Shader.PropertyToID("_LightBoxHalfSize1");
    private static readonly int Direction0Id = Shader.PropertyToID("_BoxLightDirection0");
    private static readonly int Direction1Id = Shader.PropertyToID("_BoxLightDirection1");
    private static readonly int ColorIntensity0Id = Shader.PropertyToID("_BoxLightColorIntensity0");
    private static readonly int ColorIntensity1Id = Shader.PropertyToID("_BoxLightColorIntensity1");
    private static readonly int Settings0Id = Shader.PropertyToID("_BoxLightSettings0");
    private static readonly int Settings1Id = Shader.PropertyToID("_BoxLightSettings1");

    private void Reset()
    {
        CacheComponents();
        boxCollider.isTrigger = true;
        if (searchRoot == null) searchRoot = transform;
        CollectRenderers();
    }

    private void OnEnable()
    {
        CacheComponents();
        if (autoCollectRenderers) CollectRenderers();
        ActiveVolumes.Add(this);
        UpdateAllShaderParameters();
    }

    private void LateUpdate()
    {
        if (updateEveryFrame) UpdateAllShaderParameters();
    }

    private void OnValidate()
    {
        lightIntensity = Mathf.Max(0f, lightIntensity);
        ambientLighting = Mathf.Clamp01(ambientLighting);
        boxFadeWidth = Mathf.Max(0f, boxFadeWidth);
        CacheComponents();
        if (autoCollectRenderers) CollectRenderers();
        if (isActiveAndEnabled) ActiveVolumes.Add(this);
        UpdateAllShaderParameters();
    }

    private void OnDisable()
    {
        AddTargetsToDirty(this);
        ActiveVolumes.Remove(this);
        RebuildDirtyRenderers();
    }

    [ContextMenu("Collect Target Renderers")]
    public void CollectRenderers()
    {
        Transform root = searchRoot != null ? searchRoot : transform;
        Renderer[] previousTargets = targetRenderers;
        targetRenderers = root.GetComponentsInChildren<Renderer>(includeInactive);
        UpdateAllShaderParameters(previousTargets);
    }

    [ContextMenu("Update Shader Parameters")]
    public void UpdateShaderParameters() => UpdateAllShaderParameters();

    public void SetTargetRenderers(Renderer[] renderers, bool updateImmediately = true)
    {
        Renderer[] previousTargets = targetRenderers;
        targetRenderers = renderers;
        if (updateImmediately) UpdateAllShaderParameters(previousTargets);
    }

    public void SetLight(Vector3 direction, Color color, float intensity, bool updateImmediately = true)
    {
        lightDirection = direction;
        lightColor = color;
        lightIntensity = Mathf.Max(0f, intensity);
        if (updateImmediately) UpdateAllShaderParameters();
    }

    private static void UpdateAllShaderParameters(Renderer[] previousTargets = null)
    {
        DirtyRenderers.Clear();
        foreach (BoxUnlitVolumeController volume in ActiveVolumes)
            AddTargetsToDirty(volume);
        if (previousTargets != null)
        {
            foreach (Renderer target in previousTargets)
                if (target != null) DirtyRenderers.Add(target);
        }
        RebuildDirtyRenderers();
    }

    private static void AddTargetsToDirty(BoxUnlitVolumeController volume)
    {
        if (volume == null || volume.targetRenderers == null) return;
        foreach (Renderer target in volume.targetRenderers)
            if (target != null) DirtyRenderers.Add(target);
    }

    private static void RebuildDirtyRenderers()
    {
        foreach (Renderer target in DirtyRenderers)
            ApplyVolumes(target);
        DirtyRenderers.Clear();
    }

    private static void ApplyVolumes(Renderer target)
    {
        if (propertyBlock == null)
            propertyBlock = new MaterialPropertyBlock();

        MatchingVolumes.Clear();
        foreach (BoxUnlitVolumeController volume in ActiveVolumes)
        {
            if (volume != null && volume.isActiveAndEnabled && volume.Targets(target))
                MatchingVolumes.Add(volume);
        }
        MatchingVolumes.Sort((a, b) =>
        {
            int result = a.priority.CompareTo(b.priority);
            return result != 0 ? result : a.GetInstanceID().CompareTo(b.GetInstanceID());
        });

        int count = Mathf.Min(2, MatchingVolumes.Count);
        target.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(VolumeCountId, count);
        if (count > 0) WriteSlot(propertyBlock, MatchingVolumes[0], 0);
        if (count > 1) WriteSlot(propertyBlock, MatchingVolumes[1], 1);
        target.SetPropertyBlock(propertyBlock);
    }

    private bool Targets(Renderer target)
    {
        if (targetRenderers == null) return false;
        for (int i = 0; i < targetRenderers.Length; i++)
            if (targetRenderers[i] == target) return true;
        return false;
    }

    private static void WriteSlot(MaterialPropertyBlock block, BoxUnlitVolumeController volume, int slot)
    {
        volume.CacheComponents();
        if (volume.boxCollider == null) return;

        Vector3 direction = volume.lightDirection.sqrMagnitude > 0.000001f
            ? volume.lightDirection.normalized
            : Vector3.down;
        if (volume.localDirection)
            direction = volume.transform.TransformDirection(direction).normalized;

        BoxCollider box = volume.boxCollider;
        Vector4 colorIntensity = new Vector4(
            volume.lightColor.r, volume.lightColor.g, volume.lightColor.b, volume.lightIntensity);
        Vector4 settings = new Vector4(volume.ambientLighting, volume.boxFadeWidth, 0f, 0f);

        block.SetMatrix(slot == 0 ? WorldToLocal0Id : WorldToLocal1Id, box.transform.worldToLocalMatrix);
        block.SetVector(slot == 0 ? Center0Id : Center1Id, box.center);
        block.SetVector(slot == 0 ? HalfSize0Id : HalfSize1Id, box.size * 0.5f);
        block.SetVector(slot == 0 ? Direction0Id : Direction1Id, direction);
        block.SetVector(slot == 0 ? ColorIntensity0Id : ColorIntensity1Id, colorIntensity);
        block.SetVector(slot == 0 ? Settings0Id : Settings1Id, settings);
    }

    private void CacheComponents()
    {
        if (boxCollider == null) boxCollider = GetComponent<BoxCollider>();
    }

    private void OnDrawGizmos() { if (drawAlwaysGizmo) DrawBoxGizmo(); }
    private void OnDrawGizmosSelected() { if (drawSelectedGizmo && !drawAlwaysGizmo) DrawBoxGizmo(); }

    private void DrawBoxGizmo()
    {
        CacheComponents();
        if (boxCollider == null) return;
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = boxCollider.transform.localToWorldMatrix;
        Gizmos.color = lightColor;
        Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);
        Gizmos.matrix = previous;
    }
}
