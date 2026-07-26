using Cinemachine;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class PlayerController : MonoBehaviour, IBeatUpdate
{
    [SerializeField, Min(0.01f)]
    private float floorSearchTolerance = 0.25f;
    [SerializeField]
    private Animator playerAnimator;

    private Vector3 originPosition;
    private readonly Dictionary<FloorController, int> visitCounts = new();
    private bool hasRecordedCurrentFloor;
    private Vector3 lastMoveDirection = Vector3.forward;

    public bool canMove { get; private set; }

    private void Awake()
    {
        originPosition = transform.position;
        transform.position = originPosition + Vector3.up * 6f;
        BeatSystem.beatUpdateObjects[0] = this;
        canMove = false;
    }

    public void BeatReset()
    {
        transform.position = originPosition + Vector3.up * 6f;
        visitCounts.Clear();
        hasRecordedCurrentFloor = false;
        lastMoveDirection = Vector3.forward;

        playerAnimator.SetTrigger("Idle");
        canMove = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && canMove)
        {
            FloorGroupSpawner[] spawners =
                FindObjectsOfType<FloorGroupSpawner>();
            foreach (FloorGroupSpawner spawner in spawners)
                spawner.ResetFloorGroups();

            playerAnimator.SetTrigger("Catch");
            SingleSceneManager.Instance.PlayerBeFound(transform.position);
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            BeatSystem.ResetBeat();
            transform.DOMoveY(originPosition.y, 0.3f)
                .SetEase(Ease.InCubic);
            
            canMove = true;
            playerAnimator.SetTrigger("Walk");
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
        Vector3[] searchDirections =
            GetRelativeSearchDirections();

        foreach (Vector3 direction in searchDirections)
        {
            Vector3 targetPosition =
                transform.position + direction * cellSize;

            FloorController targetFloor =
                FindFloorAt(floors, targetPosition, cellSize);

            if (targetFloor != null &&
                targetFloor.GetNextBeatInfo() == '-')
            {
                FaceDirection(direction);
                //transform.DOMove(new Vector3(
                //    targetFloor.transform.position.x,
                //    transform.position.y,
                //    targetFloor.transform.position.z
                //), 0.3f)
                //.SetEase(Ease.OutCubic);
                playerAnimator.SetTrigger("Expo");
                SingleSceneManager.Instance.PlayerReachEnd(targetFloor);
                return;
            }
        }

        foreach (Vector3 direction in searchDirections)
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
            lastMoveDirection =
                bestFloor.transform.position -
                transform.position;
            lastMoveDirection.y = 0f;
            lastMoveDirection.Normalize();

            FaceDirection(lastMoveDirection);
            transform.DOMove(new Vector3(
                bestFloor.transform.position.x,
                transform.position.y,
                bestFloor.transform.position.z
            ), 0.3f)
            .SetEase(Ease.OutCubic);

            bestFloor.WavePeopleShadow();

            return;
        }

        // 被发现
        playerAnimator.SetTrigger("Catch");
        SingleSceneManager.Instance.PlayerBeFound(transform.position);
    }

    private void FaceDirection(Vector3 direction)
    {
        Vector3 flatDirection =
            new Vector3(direction.x, 0f, direction.z).normalized;

        if (flatDirection.sqrMagnitude < 0.01f)
            return;

        transform.DORotateQuaternion(
            Quaternion.LookRotation(flatDirection, Vector3.up),
            0.2f
        ).SetEase(Ease.OutCubic);
    }

    private Vector3[] GetRelativeSearchDirections()
    {
        Vector3 forward = new Vector3(
            lastMoveDirection.x,
            0f,
            lastMoveDirection.z
        ).normalized;

        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;

        Vector3 left =
            Vector3.Cross(forward, Vector3.up);
        Vector3 right =
            Vector3.Cross(Vector3.up, forward);

        return new[]
        {
            forward,
            left,
            right,
            -forward
        };
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
