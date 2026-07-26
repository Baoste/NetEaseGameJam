using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloorController : MonoBehaviour, IBeatUpdate
{
    public string beatInfo;
    private int currentBeatIndex = 0;

    [Header("Component")]
    private Renderer rend;
    private MaterialPropertyBlock propBlock;

    private int sortIndex;
    public const int SORTLAYER = 1000;

    public int CurrentBeatIndex => currentBeatIndex;

    public char GetNextBeatInfo()
    {
        if (string.IsNullOrEmpty(beatInfo))
            return 'o';

        int nextIndex =
            currentBeatIndex % beatInfo.Length;
        return beatInfo[nextIndex];
    }

    public float GetCellSize()
    {
        FloorGroup floorGroup =
            GetComponentInParent<FloorGroup>();
        return floorGroup != null
            ? floorGroup.CellSize
            : 1f;
    }

    public void BeatReset()
    {
        currentBeatIndex = 0;
        char currentBeatChar = beatInfo[currentBeatIndex];
        SetFloorStatus(currentBeatChar);
    }

    public void OnBeatUpdate()
    {
        char currentBeatChar = beatInfo[currentBeatIndex];
        currentBeatIndex = (currentBeatIndex + 1) % beatInfo.Length;

        transform.DOPunchPosition(
            new Vector3(0, 0.1f, 0), 0.2f,
            vibrato: 1, elasticity: 0.5f
        );

        SetFloorStatus(currentBeatChar);
    }

    private void SetFloorStatus(char currentBeatChar)
    {
        // Change the floor status based on the current beat info
        switch (currentBeatChar)
        {
            case 'o':
                rend.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", Color.white);
                rend.SetPropertyBlock(propBlock);
                break;
            case 'x':
                rend.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", Color.black);
                rend.SetPropertyBlock(propBlock);
                break;
            default:
                Debug.LogWarning("Unknown floor status: " + currentBeatChar);
                break;
        }
    }

    private void Awake()
    {
        sortIndex = SORTLAYER + BeatSystem.beatUpdateObjects.Count;
        BeatSystem.beatUpdateObjects[sortIndex] = this;
        rend = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
    }

    void Start()
    {
    }

    private void OnDestroy()
    {
        BeatSystem.beatUpdateObjects.Remove(sortIndex);
    }

    void Update()
    {
        
    }
}
