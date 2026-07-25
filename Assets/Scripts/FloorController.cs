using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FloorController : MonoBehaviour, IBeatUpdate
{
    [SerializeField] private List<BeatInfo> m_BeatInfo;
    private int currentBeatIndex = 0;
    private int currentBeatCount = 0;

    [Header("Component")]
    private Renderer rend;
    private MaterialPropertyBlock propBlock;


    public void OnBeatUpdate()
    {
        if (currentBeatCount ++ >= m_BeatInfo[currentBeatIndex].beatCount)
        {
            currentBeatIndex = (currentBeatIndex + 1) % m_BeatInfo.Count;
            currentBeatCount = 0;
        }

        transform.DOPunchPosition(
            new Vector3(0, 0.1f, 0), 0.2f, 
            vibrato: 1, elasticity: 0.5f
        );

        // Change the floor status based on the current beat info
        switch (m_BeatInfo[currentBeatIndex].status)
        {
            case 0:
                rend.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", Color.black);
                rend.SetPropertyBlock(propBlock);
                break;
            case 1:
                rend.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", Color.red);
                rend.SetPropertyBlock(propBlock);
                break;
            case 2:
                rend.GetPropertyBlock(propBlock);
                propBlock.SetColor("_BaseColor", Color.green);
                rend.SetPropertyBlock(propBlock);
                break;
            default:
                Debug.LogWarning("Unknown floor status: " + m_BeatInfo[currentBeatIndex].status);
                break;
        }
    }

    private void Awake()
    {
        BeatSystem.beatUpdateObjects.Add(this);
    }

    void Start()
    {
        rend = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
    }

    void Update()
    {
        
    }
}
