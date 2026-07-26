using System.Collections.Generic;
using UnityEngine;

public class FloorGroup : MonoBehaviour
{
    [Header("Floor")]
    [SerializeField] private GameObject floorPrefab;
    [SerializeField] private List<GameObject> floorInstances = new();
    [SerializeField] private Material hologramMaterial;

    [Header("Grid")]
    [Tooltip("统一控制 Floor 的 X/Z 尺寸、组内排列间距和拖拽吸附网格。")]
    [SerializeField, Min(0.5f)] private float cellSize = 1f;

    private FloorGroupSpawner ownerSpawner;

    public float CellSize => cellSize;
    public FloorGroupSpawner OwnerSpawner => ownerSpawner;

    public Transform FirstFloor =>
        floorInstances.Count > 0 && floorInstances[0] != null
            ? floorInstances[0].transform
            : transform;

    public void SetSpawner(FloorGroupSpawner spawner)
    {
        ownerSpawner = spawner;
        if (ownerSpawner != null)
            ownerSpawner.RegisterFloorGroup(gameObject);
    }

    public bool IsInsideSpawnerZone()
    {
        return ownerSpawner != null &&
               ownerSpawner.IsInsideZone(transform.position);
    }

    public void ReturnToSpawner(float duration = 0.3f)
    {
        if (ownerSpawner != null)
            ownerSpawner.ReturnFloorGroup(gameObject, duration);
    }

    public void LeaveSpawnerLayout(float duration = 0.3f)
    {
        if (ownerSpawner != null)
            ownerSpawner.RemoveFloorGroupFromLayout(gameObject, duration);
    }

    public void GenerateGroup(string[] beatInfos, int columnCount)
    {
        if (beatInfos == null || beatInfos.Length == 0)
            return;

        int columns = Mathf.Max(1, columnCount);

        for (int i = 0; i < beatInfos.Length; i++)
        {
            if (string.IsNullOrEmpty(beatInfos[i]))
                continue;

            int row = i / columns;
            int column = i % columns;

            GenerateFloor(
                beatInfos[i],
                new Vector2(column * cellSize, row * cellSize)
            );
        }
    }

    private void GenerateFloor(string beatInfo, Vector2 localPosition)
    {
        GameObject floor = Instantiate(floorPrefab, transform);
        floor.transform.localPosition =
            new Vector3(localPosition.x, 0f, -localPosition.y);
        floor.transform.localRotation = Quaternion.identity;
        Vector3 floorScale = floor.transform.localScale;
        floor.transform.localScale =
            new Vector3(cellSize, floorScale.y, cellSize);

        FloorController floorController =
            floor.GetComponent<FloorController>();
        if (floorController != null)
            floorController.beatInfo = beatInfo;

        DraggableFloorSnap draggableFloor =
            floor.GetComponent<DraggableFloorSnap>();
        if (draggableFloor == null)
            draggableFloor = floor.AddComponent<DraggableFloorSnap>();

        draggableFloor.SetDragTarget(transform);
        draggableFloor.SetGridSize(cellSize);
        draggableFloor.hologramMaterial = hologramMaterial;
        floorInstances.Add(floor);
    }
}
