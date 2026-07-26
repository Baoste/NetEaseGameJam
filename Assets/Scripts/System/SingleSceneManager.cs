using System.Collections;
using UnityEngine;

public class SingleSceneManager : MonoBehaviour
{
    public static SingleSceneManager Instance { get; private set; }

    [Header("Spot Light")]
    [SerializeField] private GameObject spotLight;

    private void Awake()
    {
        // 场景中只允许存在一个实例
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (spotLight != null)
        {
            spotLight.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        // 避免场景销毁后仍然引用旧对象
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void PlayerBeFound(Vector3 playerPosition)
    {
        BeatSystem.beatTime = 10000f;
        StartCoroutine(PlayerBeFoundCoroutine(playerPosition));
    }

    private IEnumerator PlayerBeFoundCoroutine(Vector3 playerPosition)
    {
        if (spotLight == null)
        {
            Debug.LogWarning("SingleSceneManager：未设置 Spot Light。");
            yield break;
        }

        spotLight.transform.position = new Vector3(
            playerPosition.x,
            8f,
            playerPosition.z
        );

        spotLight.SetActive(true);

        yield return new WaitForSeconds(1f);

        spotLight.SetActive(false);
        BeatSystem.ResetBeat();
    }
}