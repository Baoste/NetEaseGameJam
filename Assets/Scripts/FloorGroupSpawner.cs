using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloorGroupSpawner : MonoBehaviour
{
    [Header("Floor")]
    [SerializeField] private GameObject floorGroupPrefab;
    [SerializeField] private List<GameObject> floorGroupInstances = new();

    [Header("Layout")]
    [SerializeField, Min(1)] private int generateCount = 5;

    // Floor 本身的尺寸
    [SerializeField] private Vector2 floorSize = new Vector2(1f, 1f);

    // Floor 之间的额外间隔
    [SerializeField] private Vector2 floorSpacing = new Vector2(0.1f, 0.1f);

    [SerializeField] private Vector3 floorEuler;

    [Header("Zone")]
    [SerializeField] private float zoneWidth = 6f;
    [SerializeField] private float zoneHeight = 4f;

    [Header("Spawn")]
    [SerializeField] private Vector3 instantiatePosition;


    private void Start()
    {
        GenerateFloorGroup(
            new string[] { 
                "xoo", "oox",
                "ooo", "",
            },
            2
        );

        GenerateFloorGroup(
            new string[] {
                "oxo", "oox",
                "xoo",    "",
            },
            2
        );

        GenerateFloorGroup(
            new string[] {
                "oxo", "xoo", "oxo",
            },
            3
        );
    }

    public void GenerateFloorGroup(string[] beatInfo, int columnCount, float duration = 0.5f)
    {
        GameObject floorGroup = Instantiate(
            floorGroupPrefab,
            instantiatePosition,
            Quaternion.identity
        );

        FloorGroup floorGroupController = floorGroup.GetComponent<FloorGroup>();
        if (floorGroupController == null)
        {
            Debug.LogError("floorGroupPrefab 缺少 FloorGroup 组件。", floorGroup);
            Destroy(floorGroup);
            return;
        }

        floorGroupController.GenerateGroup(beatInfo, columnCount);

        floorGroupInstances.Add(floorGroup);
        StartCoroutine(UpdateFloorPositions(duration));
    }

    public IEnumerator UpdateFloorPositions(float duration)
    {
        floorGroupInstances.RemoveAll(floor => floor == null);

        if (floorGroupInstances.Count == 0)
            yield break;

        int columns = Mathf.Max(1, generateCount);
        int rows = Mathf.CeilToInt((float)floorGroupInstances.Count / columns);

        // 每两个 Floor 中心之间的距离
        float stepX = floorSize.x + floorSpacing.x;
        float stepZ = floorSize.y + floorSpacing.y;

        // 防止整体排列超出区域
        if (columns > 1)
        {
            float maxStepX = zoneWidth / (columns - 1);
            stepX = Mathf.Min(stepX, maxStepX);
        }

        if (rows > 1)
        {
            float maxStepZ = zoneHeight / (rows - 1);
            stepZ = Mathf.Min(stepZ, maxStepZ);
        }

        float totalHeight = (rows - 1) * stepZ;

        for (int i = 0; i < floorGroupInstances.Count; i++)
        {
            GameObject floor = floorGroupInstances[i];

            if (floor == null)
                continue;

            int row = i / columns;
            int column = i % columns;

            // 最后一行不足 columns 时，最后一行单独居中
            int currentRowCount = Mathf.Min(
                columns,
                floorGroupInstances.Count - row * columns
            );

            float currentRowWidth = (currentRowCount - 1) * stepX;

            float localX =
                -currentRowWidth * 0.5f +
                column * stepX;

            float localZ =
                totalHeight * 0.5f -
                row * stepZ;

            // Plane 使用局部 XZ 平面
            Vector3 localPosition = new Vector3(
                localX,
                0f,
                localZ
            );

            Vector3 targetPosition =
                transform.TransformPoint(localPosition);

            Quaternion targetRotation =
                transform.rotation *
                Quaternion.Euler(floorEuler);

            floor.transform.DOMove(
                targetPosition,
                duration
            );

            floor.transform.DORotateQuaternion(
                targetRotation,
                duration
            );
        }

        yield return new WaitForSeconds(duration);
    }

    public void RemoveFloor(GameObject floor, float duration = 0.3f)
    {
        if (floor == null)
            return;

        floorGroupInstances.Remove(floor);
        Destroy(floor);

        StartCoroutine(UpdateFloorPositions(duration));
    }

    public void ClearFloors()
    {
        foreach (GameObject floor in floorGroupInstances)
        {
            if (floor != null)
                Destroy(floor);
        }

        floorGroupInstances.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;

        DrawRect(
            transform.position,
            transform.rotation,
            zoneWidth,
            zoneHeight
        );

        Gizmos.color = Color.gray;
        Gizmos.DrawSphere(instantiatePosition, 0.05f);

        // 预览每个 Floor 的目标位置
        DrawFloorPreview();
    }

    private void DrawFloorPreview()
    {
        if (floorGroupInstances == null ||
            floorGroupInstances.Count == 0)
        {
            return;
        }

        int columns = Mathf.Max(1, generateCount);
        int rows = Mathf.CeilToInt(
            (float)floorGroupInstances.Count / columns
        );

        float stepX = floorSize.x + floorSpacing.x;
        float stepZ = floorSize.y + floorSpacing.y;

        if (columns > 1)
            stepX = Mathf.Min(
                stepX,
                zoneWidth / (columns - 1)
            );

        if (rows > 1)
            stepZ = Mathf.Min(
                stepZ,
                zoneHeight / (rows - 1)
            );

        float totalHeight = (rows - 1) * stepZ;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < floorGroupInstances.Count; i++)
        {
            int row = i / columns;
            int column = i % columns;

            int currentRowCount = Mathf.Min(
                columns,
                floorGroupInstances.Count - row * columns
            );

            float currentRowWidth =
                (currentRowCount - 1) * stepX;

            float localX =
                -currentRowWidth * 0.5f +
                column * stepX;

            float localZ =
                totalHeight * 0.5f -
                row * stepZ;

            Vector3 position = transform.TransformPoint(
                new Vector3(localX, 0f, localZ)
            );

            DrawRect(
                position,
                transform.rotation,
                floorSize.x,
                floorSize.y
            );
        }
    }

    private void DrawRect(
        Vector3 center,
        Quaternion rotation,
        float width,
        float height)
    {
        Vector3 a = center + rotation * new Vector3(
            -width * 0.5f,
            0f,
            -height * 0.5f
        );

        Vector3 b = center + rotation * new Vector3(
            width * 0.5f,
            0f,
            -height * 0.5f
        );

        Vector3 c = center + rotation * new Vector3(
            width * 0.5f,
            0f,
            height * 0.5f
        );

        Vector3 d = center + rotation * new Vector3(
            -width * 0.5f,
            0f,
            height * 0.5f
        );

        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, d);
        Gizmos.DrawLine(d, a);
    }
}
