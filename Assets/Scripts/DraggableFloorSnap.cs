using UnityEngine;

/// <summary>
/// 允许 Floor 在固定高度平面上拖动，并吸附到：
///
///     (2a, planeY, 2b)
///
/// 其中 a、b 为整数。
/// </summary>
[RequireComponent(typeof(Collider))]
public class DraggableFloorSnap : MonoBehaviour
{
    [Tooltip("实际被拖动的根物体。为空时拖动当前物体。")]
    [SerializeField] private Transform dragTarget;

    [Header("Drag")]

    [Tooltip("用于发射鼠标射线的摄像机。为空时使用 Camera.main。")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("Floor 拖动时所在的固定 Y 坐标。")]
    [SerializeField] private float planeY = 0f;

    [Tooltip("检测 Player 是否站在当前地板组上的向下射线距离。")]
    [SerializeField, Min(0.01f)]
    private float playerStandingCheckDistance = 3f;

    [Header("Grid Snap")]

    [Tooltip("网格间隔。设置为 2 时，坐标为 (2a, n, 2b)。")]
    [HideInInspector, SerializeField, Min(0.01f)]
    private float gridSize = 2f;

    [Tooltip("网格原点的 XZ 偏移。保持为 0 时坐标就是 (2a, n, 2b)。")]
    [SerializeField]
    private Vector2 gridOrigin = Vector2.zero;

    [Tooltip("Floor 距离吸附点多近时，显示全息方块。")]
    [SerializeField, Min(0f)]
    private float snapDistance = 1f;

    [Header("Hologram")]

    [Tooltip("全息预览 Prefab。为空时会复制当前 Floor。")]
    [SerializeField]
    private GameObject hologramPrefab;

    [Tooltip("可选的全息材质。会替换预览物体的所有材质。")]
    public Material hologramMaterial;

    [ColorUsage(true, true)]
    [SerializeField] private Color blockedHologramColor = Color.red;

    [Header("Placement")]

    [Tooltip("松开鼠标后，将当前 Floor 移动到吸附点。")]
    [SerializeField]
    private bool snapOriginalOnRelease = true;

    [Tooltip("松开鼠标后，在吸附点生成一个新的 Floor。")]
    [SerializeField]
    private bool createCopyOnRelease = false;

    [Tooltip("生成的新 Floor Prefab。为空时复制当前物体。")]
    [SerializeField]
    private GameObject placedFloorPrefab;

    [Tooltip("生成新 Floor 后，拖动物体是否返回开始位置。")]
    [SerializeField]
    private bool returnAfterCreatingCopy = true;

    [Header("Gizmos")]

    [SerializeField, Min(0)]
    private int gridPreviewRange = 5;

    private GameObject hologramInstance;

    private bool isDragging;
    private bool canSnap;
    private bool isPlacementBlocked;

    private Vector3 dragOffset;
    private Vector3 dragStartPosition;
    private Quaternion dragStartRotation;
    private Vector3 currentSnapPosition;
    private Color normalHologramColor = new Color(0f, 0.6f, 1f, 1f);
    private Renderer[] hologramRenderers;
    private BoxCollider[] hologramBoxColliders;

    private static readonly int HologramColorId =
        Shader.PropertyToID("_HologramColor");

    private Transform DragTarget =>
        dragTarget != null ? dragTarget : transform;

    /// <summary>
    /// 让子物体负责接收鼠标事件，但移动整个组。
    /// </summary>
    public void SetDragTarget(Transform target)
    {
        dragTarget = target;
    }

    public void SetGridSize(float size)
    {
        gridSize = Mathf.Max(0.01f, size);
    }

    /// <summary>
    /// 当前吸附点的网格坐标 a。
    /// </summary>
    public int CurrentGridA { get; private set; }

    /// <summary>
    /// 当前吸附点的网格坐标 b。
    /// </summary>
    public int CurrentGridB { get; private set; }

    private void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

    }

    private void Update()
    {
        if (!isDragging || !Input.GetKeyDown(KeyCode.Q))
            return;

        RotateGroupClockwise();
        UpdateDragging();
    }

    private void OnMouseDown()
    {
        //if (HasPlayerStandingOnGroup() || CanPlayerMove())
        if (HasPlayerStandingOnGroup())
            return;

        if (targetCamera == null)
        {
            Debug.LogError("没有找到用于拖动的摄像机。", this);
            return;
        }

        if (!TryGetMousePositionOnPlane(out Vector3 mouseWorldPosition))
            return;

        isDragging = true;
        canSnap = false;

        dragStartPosition = DragTarget.position;
        dragStartRotation = DragTarget.rotation;

        // 保留点击位置与物体中心之间的 XZ 偏移，
        // 防止点击后物体中心瞬间跳到鼠标位置。
        dragOffset = new Vector3(
            DragTarget.position.x - mouseWorldPosition.x,
            0.2f,
            DragTarget.position.z - mouseWorldPosition.z
        );

        CreateHologram();
        HideHologram();
    }

    private bool CanPlayerMove()
    {
        PlayerController player =
            FindObjectOfType<PlayerController>();

        return player != null && player.canMove;
    }

    private bool HasPlayerStandingOnGroup()
    {
        PlayerController[] players =
            FindObjectsOfType<PlayerController>();

        foreach (PlayerController player in players)
        {
            if (player == null)
                continue;

            Vector3 rayOrigin =
                player.transform.position + Vector3.up * 0.1f;

            RaycastHit[] hits = Physics.RaycastAll(
                rayOrigin,
                Vector3.down,
                playerStandingCheckDistance,
                ~0,
                QueryTriggerInteraction.Ignore
            );

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider != null &&
                    hit.collider.transform.IsChildOf(DragTarget))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private void OnMouseDrag()
    {
        if (!isDragging)
            return;

        UpdateDragging();
    }

    private void OnMouseUp()
    {
        if (!isDragging)
            return;

        FinishDragging();
    }

    private void UpdateDragging()
    {
        if (!TryGetMousePositionOnPlane(out Vector3 mouseWorldPosition))
            return;

        Vector3 targetPosition = mouseWorldPosition + dragOffset;

        // Floor 始终处于固定高度。
        targetPosition.y = planeY;

        DragTarget.position = targetPosition;

        FloorGroup floorGroup =
            DragTarget.GetComponent<FloorGroup>();
        if (floorGroup != null &&
            floorGroup.IsInsideSpawnerZone())
        {
            canSnap = false;
            isPlacementBlocked = false;
            HideHologram();
            return;
        }

        currentSnapPosition = CalculateSnapPosition(targetPosition);

        float distanceToSnapPoint = Vector2.Distance(
            new Vector2(targetPosition.x, targetPosition.z),
            new Vector2(currentSnapPosition.x, currentSnapPosition.z)
        );

        canSnap = distanceToSnapPoint <= snapDistance;

        if (canSnap)
        {
            ShowHologram(
                currentSnapPosition,
                DragTarget.rotation
            );

            isPlacementBlocked = CheckHologramCollision();
            SetHologramColor(
                isPlacementBlocked
                    ? blockedHologramColor
                    : normalHologramColor
            );
        }
        else
        {
            isPlacementBlocked = false;
            HideHologram();
        }
    }

    private void FinishDragging()
    {
        isDragging = false;

        FloorGroup floorGroup =
            DragTarget.GetComponent<FloorGroup>();

        if (floorGroup != null &&
            floorGroup.IsInsideSpawnerZone())
        {
            floorGroup.ReturnToSpawner();
            ResetDragState();
            return;
        }

        if (canSnap && !isPlacementBlocked)
        {
            if (createCopyOnRelease)
            {
                CreateFloorAtSnapPoint();

                if (returnAfterCreatingCopy)
                    ReturnToSpawner();
            }
            else if (snapOriginalOnRelease)
            {
                DragTarget.position = currentSnapPosition;
                if (floorGroup != null)
                    floorGroup.LeaveSpawnerLayout();
            }
        }
        else
        {
            ReturnToSpawner();
        }

        ResetDragState();
    }

    private void ResetDragState()
    {
        canSnap = false;
        isPlacementBlocked = false;
        HideHologram();
    }

    private void RotateGroupClockwise()
    {
        FloorGroup floorGroup = DragTarget.GetComponent<FloorGroup>();
        Transform pivot = floorGroup != null
            ? floorGroup.FirstFloor
            : DragTarget;

        FloorController[] floors =
            DragTarget.GetComponentsInChildren<FloorController>(true);
        Quaternion[] shadowWorldRotations =
            new Quaternion[floors.Length];

        for (int i = 0; i < floors.Length; i++)
        {
            Transform peopleShadow =
                floors[i].PeopleShadowTransform;
            if (peopleShadow != null)
                shadowWorldRotations[i] = peopleShadow.rotation;
        }

        DragTarget.RotateAround(
            pivot.position,
            Vector3.up,
            90f
        );

        for (int i = 0; i < floors.Length; i++)
        {
            Transform peopleShadow =
                floors[i].PeopleShadowTransform;
            if (peopleShadow != null)
                peopleShadow.rotation = shadowWorldRotations[i];
        }

        if (TryGetMousePositionOnPlane(out Vector3 mouseWorldPosition))
        {
            dragOffset = new Vector3(
                DragTarget.position.x - mouseWorldPosition.x,
                0f,
                DragTarget.position.z - mouseWorldPosition.z
            );
        }
    }

    private void ReturnToSpawner()
    {
        FloorGroup floorGroup =
            DragTarget.GetComponent<FloorGroup>();
        if (floorGroup != null &&
            floorGroup.OwnerSpawner != null)
        {
            floorGroup.ReturnToSpawner();
            return;
        }

        DragTarget.SetPositionAndRotation(
            dragStartPosition,
            dragStartRotation
        );
    }

    /// <summary>
    /// 计算距离当前位置最近的网格点。
    ///
    /// 当 gridSize = 2、gridOrigin = (0,0) 时：
    /// x = 2a
    /// z = 2b
    /// </summary>
    private Vector3 CalculateSnapPosition(Vector3 worldPosition)
    {
        CurrentGridA = Mathf.RoundToInt(
            (worldPosition.x - gridOrigin.x) / gridSize
        );

        CurrentGridB = Mathf.RoundToInt(
            (worldPosition.z - gridOrigin.y) / gridSize
        );

        float snapX =
            gridOrigin.x +
            CurrentGridA * gridSize;

        float snapZ =
            gridOrigin.y +
            CurrentGridB * gridSize;

        return new Vector3(
            snapX,
            planeY,
            snapZ
        );
    }

    /// <summary>
    /// 将鼠标射线投射到 Y = planeY 的无限平面上。
    /// </summary>
    private bool TryGetMousePositionOnPlane(
        out Vector3 worldPosition)
    {
        Ray ray = targetCamera.ScreenPointToRay(
            Input.mousePosition
        );

        Plane dragPlane = new Plane(
            Vector3.up,
            new Vector3(0f, planeY, 0f)
        );

        if (dragPlane.Raycast(ray, out float enter))
        {
            worldPosition = ray.GetPoint(enter);
            return true;
        }

        worldPosition = default;
        return false;
    }

    private void CreateHologram()
    {
        if (hologramInstance != null)
            return;

        GameObject sourcePrefab =
            hologramPrefab != null
                ? hologramPrefab
                : DragTarget.gameObject;

        hologramInstance = Instantiate(sourcePrefab);

        hologramInstance.name =
            $"{DragTarget.gameObject.name}_Hologram";

        /*
         * 如果使用当前 Floor 作为全息源，
         * 复制出来的物体也会带有本脚本。
         * 必须关闭，否则它还会继续创建自己的全息物体。
         */
        // 预览只保留外观，避免复制出的组继续响应节拍或鼠标事件。
        MonoBehaviour[] behaviours =
            hologramInstance.GetComponentsInChildren<MonoBehaviour>(true);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            behaviour.enabled = false;
            Destroy(behaviour);
        }

        // 全息预览不参与碰撞和鼠标检测。
        Collider[] colliders =
            hologramInstance.GetComponentsInChildren
                <Collider>(true);

        foreach (Collider currentCollider in colliders)
            currentCollider.enabled = false;

        Rigidbody[] rigidbodies =
            hologramInstance.GetComponentsInChildren
                <Rigidbody>(true);

        foreach (Rigidbody currentRigidbody in rigidbodies)
        {
            currentRigidbody.isKinematic = true;
            currentRigidbody.detectCollisions = false;
        }

        SetLayerRecursively(
            hologramInstance,
            LayerMask.NameToLayer("Ignore Raycast")
        );

        ApplyHologramMaterial();
        hologramRenderers =
            hologramInstance.GetComponentsInChildren<Renderer>(true);
        hologramBoxColliders =
            hologramInstance.GetComponentsInChildren<BoxCollider>(true);
        HideHologram();
    }

    private void ApplyHologramMaterial()
    {
        if (hologramInstance == null ||
            hologramMaterial == null)
        {
            return;
        }

        if (hologramMaterial.HasProperty(HologramColorId))
            normalHologramColor =
                hologramMaterial.GetColor(HologramColorId);

        Renderer[] renderers =
            hologramInstance.GetComponentsInChildren
                <Renderer>(true);

        foreach (Renderer currentRenderer in renderers)
        {
            Material[] materials =
                new Material[currentRenderer.sharedMaterials.Length];

            for (int i = 0; i < materials.Length; i++)
                materials[i] = hologramMaterial;

            currentRenderer.sharedMaterials = materials;
        }
    }

    private void SetHologramColor(Color color)
    {
        if (hologramRenderers == null)
            return;

        MaterialPropertyBlock propertyBlock =
            new MaterialPropertyBlock();

        foreach (Renderer currentRenderer in hologramRenderers)
        {
            currentRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(HologramColorId, color);
            currentRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    private bool CheckHologramCollision()
    {
        if (hologramBoxColliders == null)
            return false;

        Physics.SyncTransforms();

        foreach (BoxCollider boxCollider in hologramBoxColliders)
        {
            Transform boxTransform = boxCollider.transform;
            Vector3 scale = boxTransform.lossyScale;
            Vector3 halfExtents = Vector3.Scale(
                boxCollider.size * 0.5f,
                new Vector3(
                    Mathf.Abs(scale.x),
                    Mathf.Abs(scale.y),
                    Mathf.Abs(scale.z)
                )
            );

            Collider[] overlaps = Physics.OverlapBox(
                boxTransform.TransformPoint(boxCollider.center),
                halfExtents * 0.99f,
                boxTransform.rotation,
                1 << LayerMask.NameToLayer("Floor"),
                QueryTriggerInteraction.Ignore
            );

            foreach (Collider overlap in overlaps)
            {
                if (overlap == null ||
                    overlap.transform.IsChildOf(DragTarget))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private void ShowHologram(
        Vector3 position,
        Quaternion rotation)
    {
        if (hologramInstance == null)
            return;

        hologramInstance.SetActive(true);

        hologramInstance.transform.SetPositionAndRotation(
            position,
            rotation
        );

        // 保持预览与原 Floor 尺寸一致。
        hologramInstance.transform.localScale =
            DragTarget.lossyScale;
    }

    private void HideHologram()
    {
        if (hologramInstance != null)
            hologramInstance.SetActive(false);
    }

    private void CreateFloorAtSnapPoint()
    {
        GameObject sourcePrefab =
            placedFloorPrefab != null
                ? placedFloorPrefab
                : DragTarget.gameObject;

        GameObject newFloor = Instantiate(
            sourcePrefab,
            currentSnapPosition,
            DragTarget.rotation
        );

        newFloor.name =
            $"{sourcePrefab.name}_Grid_{CurrentGridA}_{CurrentGridB}";

        FloorGroup sourceGroup =
            DragTarget.GetComponent<FloorGroup>();
        FloorGroup newGroup =
            newFloor.GetComponent<FloorGroup>();
        if (sourceGroup != null && newGroup != null)
            newGroup.SetSpawner(sourceGroup.OwnerSpawner);
    }

    private static void SetLayerRecursively(
        GameObject target,
        int layer)
    {
        if (target == null || layer < 0)
            return;

        target.layer = layer;

        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(
                child.gameObject,
                layer
            );
        }
    }

    private void OnDestroy()
    {
        if (hologramInstance != null)
            Destroy(hologramInstance);
    }

    private void OnDrawGizmosSelected()
    {
        float safeGridSize = Mathf.Max(
            0.01f,
            gridSize
        );

        Gizmos.color = Color.cyan;

        for (int a = -gridPreviewRange;
             a <= gridPreviewRange;
             a++)
        {
            for (int b = -gridPreviewRange;
                 b <= gridPreviewRange;
                 b++)
            {
                Vector3 point = new Vector3(
                    gridOrigin.x + a * safeGridSize,
                    planeY,
                    gridOrigin.y + b * safeGridSize
                );

                Gizmos.DrawSphere(point, 0.06f);
            }
        }

        if (Application.isPlaying && canSnap)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(
                currentSnapPosition,
                snapDistance
            );
        }
    }
}
