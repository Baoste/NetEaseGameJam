using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloorController : MonoBehaviour, IBeatUpdate
{
    public string beatInfo;
    private int currentBeatIndex = 0;

    [Header("Component")]
    [SerializeField] private GameObject peopleShadow;
    [SerializeField] public GameObject bomb;
    private Renderer rend;
    private MaterialPropertyBlock propBlock;

    public Transform PeopleShadowTransform =>
        peopleShadow != null ? peopleShadow.transform : null;

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
        if (peopleShadow != null)
        {
            peopleShadow.SetActive(false);
        }
        //char currentBeatChar = beatInfo[currentBeatIndex];
        //char nextBeatChar = beatInfo[(currentBeatIndex + 1) % beatInfo.Length];
        //SetFloorStatus(currentBeatChar, currentBeatChar);
    }

    public void OnBeatUpdate()
    {
        char currentBeatChar = beatInfo[currentBeatIndex];
        currentBeatIndex = (currentBeatIndex + 1) % beatInfo.Length;
        char nextBeatChar = beatInfo[currentBeatIndex];

        transform.DOPunchPosition(
            new Vector3(0, 0.1f, 0), 0.2f,
            vibrato: 1, elasticity: 0.5f
        );

        SetFloorStatus(currentBeatChar, nextBeatChar);
    }

    private void SetFloorStatus(char currentBeatChar, char nextBeatChar)
    {
        // Change the floor status based on the current beat info
        switch (currentBeatChar)
        {
            case 'o':
                // 白要变黑
                if (nextBeatChar == 'x')
                {
                    peopleShadow.SetActive(false);
                    peopleShadow.transform.localPosition = new Vector3(0f, -0.74f, 0f);
                    DOVirtual.DelayedCall(0.9f * BeatSystem.beatTime, () =>
                    {
                        peopleShadow.SetActive(true);

                        peopleShadow.transform
                            .DOLocalMoveY(0.583f, 0.1f * BeatSystem.beatTime)
                            .SetEase(Ease.InCubic);
                    });
                    rend.GetPropertyBlock(propBlock);
                    propBlock.SetColor("_LightColor", Color.white);
                    propBlock.SetFloat("_LightOn", 0f);
                    rend.SetPropertyBlock(propBlock);
                }
                // 白要变白
                else
                {
                    peopleShadow.SetActive(false);
                    peopleShadow.transform.localPosition = new Vector3(0f, -0.74f, 0f);
                    rend.GetPropertyBlock(propBlock);
                    propBlock.SetColor("_LightColor", Color.white);
                    propBlock.SetFloat("_LightOn", 1f);
                    rend.SetPropertyBlock(propBlock);
                }
                break;
            case 'x':
                // 黑要变黑
                if (nextBeatChar == 'x')
                {
                    peopleShadow.SetActive(true);
                    peopleShadow.transform.localPosition = new Vector3(0f, 0.583f, 0f); 
                    rend.GetPropertyBlock(propBlock);
                    propBlock.SetColor("_LightColor", Color.black);
                    propBlock.SetFloat("_LightOn", 0f);
                    rend.SetPropertyBlock(propBlock);
                }
                // 黑要变白
                else
                {
                    peopleShadow.SetActive(true);
                    peopleShadow.transform.localPosition = new Vector3(0f, 0.583f, 0f);
                    DOVirtual.DelayedCall(0.9f * BeatSystem.beatTime, () =>
                    {
                        peopleShadow.transform
                            .DOLocalMoveY(-0.74f, 0.1f * BeatSystem.beatTime)
                            .SetEase(Ease.InCubic);
                    });
                    rend.GetPropertyBlock(propBlock);
                    propBlock.SetColor("_LightColor", Color.black);
                    propBlock.SetFloat("_LightOn", 1f);
                    rend.SetPropertyBlock(propBlock);
                }
                break;
            default:
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

    public void WavePeopleShadow()
    {
        peopleShadow.transform
            .DOLocalRotate(
                new Vector3(0f, 0f, 12f),
                0.5f,
                RotateMode.LocalAxisAdd
            )
            .SetLoops(2, LoopType.Yoyo)
            .SetEase(Ease.InOutSine);
    }
}
