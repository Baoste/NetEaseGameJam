using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour, IBeatUpdate
{
    [SerializeField, Min(0.01f)]
    private float floorSearchTolerance = 0.25f;

    private Vector3 originPosition;
    private readonly Dictionary<FloorController, int> visitCounts = new();
    private bool hasRecordedCurrentFloor;

    public bool canMove { get; private set; }

    private static readonly Vector3[] SearchDirections =
    {
        Vector3.forward,
        Vector3.right,
        Vector3.back,
        Vector3.left
    };

    private void Awake()
    {
        originPosition = transform.position;
        BeatSystem.beatUpdateObjects[0] = this;
        canMove = false;
    }

    public void BeatReset()
    {
        transform.position = originPosition + Vector3.up * 3f;
        transform.DOMoveY(originPosition.y, 0.3f)
            .SetEase(Ease.InCubic);
        visitCounts.Clear();
        hasRecordedCurrentFloor = false;
        canMove = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            FloorGroupSpawner[] spawners =
                FindObjectsOfType<FloorGroupSpawner>();
            foreach (FloorGroupSpawner spawner in spawners)
                spawner.ResetFloorGroups();

            SingleSceneManager.Instance.PlayerBeFound(transform.position);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            BeatSystem.ResetBeat();
            canMove = true;
        }
    }

public void OnBeatUpdate()
    {
        if (!canMove) return;

        FloorController[] floors =
            FindObjectsOfType<FloorController>();

        float cellSize = GetCellSize(floors);

        if (!hasRecordedCurrentFloor)
        {
            FloorController currentFloor =
                FindFloorAt(floors, transform.position, cellSize);
            if (currentFloor != null)
                RecordVisit(currentFloor);

            hasRecordedCurrentFloor = true;
        }

        FloorController bestFloor = null;
        int lowestVisitCount = int.MaxValue;

        foreach (Vector3 direction in SearchDirections)
        {
            Vector3 targetPosition =
                transform.position + direction * cellSize;

            FloorController targetFloor =
                FindFloorAt(floors, targetPosition, cellSize);

            if (targetFloor != null &&
                targetFloor.GetNextBeatInfo() == '-')
            {
                transform.DOMove(new Vector3(
                    targetFloor.transform.position.x,
                    transform.position.y,
                    targetFloor.transform.position.z
                ), 0.3f)
                .SetEase(Ease.OutCubic);

                SingleSceneManager.Instance.PlayerReachEnd();
                return;
            }
        }

        foreach (Vector3 direction in SearchDirections)
        {
            Vector3 targetPosition =
                transform.position + direction * cellSize;

            FloorController targetFloor =
                FindFloorAt(floors, targetPosition, cellSize);

            if (targetFloor == null ||
                targetFloor.GetNextBeatInfo() != 'x')
            {
                continue;
            }

            int visitCount = GetVisitCount(targetFloor);
            if (visitCount < lowestVisitCount)
            {
                lowestVisitCount = visitCount;
                bestFloor = targetFloor;
            }
        }

        if (bestFloor != null)
        {
            RecordVisit(bestFloor);

            transform.DOMove(new Vector3(
                bestFloor.transform.position.x,
                transform.position.y,
                bestFloor.transform.position.z
            ), 0.3f)
            .SetEase(Ease.OutCubic);

            return;
        }

        // 被发现
        SingleSceneManager.Instance.PlayerBeFound(transform.position);
    }

    private int GetVisitCount(FloorController floor)
    {
        return visitCounts.TryGetValue(floor, out int count)
            ? count
            : 0;
    }

    private void RecordVisit(FloorController floor)
    {
        visitCounts[floor] = GetVisitCount(floor) + 1;
    }

    private float GetCellSize(FloorController[] floors)
    {
        FloorController closestFloor = null;
        float closestSqrDistance = float.MaxValue;

        foreach (FloorController floor in floors)
        {
            if (floor == null)
                continue;

            Vector2 offset = new Vector2(
                floor.transform.position.x - transform.position.x,
                floor.transform.position.z - transform.position.z
            );
            float sqrDistance = offset.sqrMagnitude;

            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closestFloor = floor;
            }
        }

        return closestFloor != null
            ? closestFloor.GetCellSize()
            : 1f;
    }

    private FloorController FindFloorAt(
        FloorController[] floors,
        Vector3 targetPosition,
        float cellSize)
    {
        float tolerance =
            Mathf.Max(0.01f, floorSearchTolerance * cellSize);
        float toleranceSqr = tolerance * tolerance;

        foreach (FloorController floor in floors)
        {
            if (floor == null)
                continue;

            float deltaX =
                floor.transform.position.x - targetPosition.x;
            float deltaZ =
                floor.transform.position.z - targetPosition.z;

            if (deltaX * deltaX + deltaZ * deltaZ <= toleranceSqr)
                return floor;
        }

        return null;
    }

    private void OnDestroy()
    {
        BeatSystem.beatUpdateObjects.Remove(0);
    }
}
