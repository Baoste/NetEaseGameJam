using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeatSystem : MonoBehaviour
{
    public static float beatTime = 1f;
    public static List<IBeatUpdate> beatUpdateObjects = new();

    private float gameTime = beatTime;

    private void Start()
    {
    }

    private void Update()
    {
        gameTime += Time.deltaTime;

        if (gameTime > beatTime)
        {
            gameTime = 0f;
            foreach (var beatUpdateObject in beatUpdateObjects)
            {
                beatUpdateObject.OnBeatUpdate();
            }
        }
    }
}
