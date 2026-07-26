using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BeatSystem : MonoBehaviour
{
    public static float beatTime = 1f;
    public static SortedDictionary<int, IBeatUpdate> beatUpdateObjects = new();

    private static float gameTime;

    private void Start()
    {
        gameTime = 0f;
    }

    private void Update()
    {
        gameTime += Time.deltaTime;

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
        gameTime = 0f;
        beatTime = 1f;
        foreach (var beatUpdateObject in beatUpdateObjects.Values)
        {
            beatUpdateObject.BeatReset();
        }
    }
}
