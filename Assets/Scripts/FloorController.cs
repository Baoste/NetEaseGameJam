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


    public void OnBeatUpdate()
    {
        char currentBeatChar = beatInfo[currentBeatIndex];
        currentBeatIndex = (currentBeatIndex + 1) % beatInfo.Length;

        transform.DOPunchPosition(
            new Vector3(0, 0.1f, 0), 0.2f, 
            vibrato: 1, elasticity: 0.5f
        );

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
        BeatSystem.beatUpdateObjects.Add(this);
        rend = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
    }

    void Start()
    {
    }

    private void OnDestroy()
    {
        BeatSystem.beatUpdateObjects.Remove(this);
    }

    void Update()
    {
        
    }
}
