using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BeatSystem : MonoBehaviour
{
    [SerializeField] private float beatTimeValue;

    public static bool isStop = false;
    public static float beatTime { get; private set; }
    public static SortedDictionary<int, IBeatUpdate> beatUpdateObjects = new();

    private static float gameTime;

    private void Start()
    {
        gameTime = 0f;
        beatTime = beatTimeValue;
    }

    private void Update()
    {
        if (!isStop)
        {
            gameTime += Time.deltaTime;
        }

        if (gameTime > beatTime)
        {
            gameTime = 0f;
            foreach (var pair in beatUpdateObjects)
            {
                pair.Value.OnBeatUpdate();
            }
        }
    }

    public static void ResetBeat()
    {
        isStop = false;
        foreach (var beatUpdateObject in beatUpdateObjects.Values)
        {
            beatUpdateObject.BeatReset();
        }
    }
}
