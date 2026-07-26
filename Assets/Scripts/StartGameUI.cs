using DG.Tweening;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StartGameUI : MonoBehaviour
{
    public RawImage texture;
    public GameObject canvas;

    public void StartGame()
    {
        texture.DOFade(0f, 1f)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                BeatSystem.isStop = false;
                canvas.SetActive(false);
            });
    }
}
