using Cinemachine;
using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SingleSceneManager : MonoBehaviour
{
    public static SingleSceneManager Instance { get; private set; }

    [SerializeField] private GlitchController glitchController;
    private CinemachineImpulseSource impulseSource;

    [Header("GameObject")]
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
        if (glitchController == null)
            glitchController = FindAnyObjectByType<GlitchController>();
        impulseSource = GetComponent<CinemachineImpulseSource>();
        glitchController.TriggerGlitch(0, 2f);
        DOVirtual.DelayedCall(2f, () => BeatSystem.isStop = false);

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

    public void CameraShake(
        float force = 0.2f,
        float duration = 0.2f,
        CinemachineImpulseDefinition.ImpulseShapes shape = CinemachineImpulseDefinition.ImpulseShapes.Bump)
    {
        impulseSource.m_ImpulseDefinition.m_ImpulseShape = shape;
        impulseSource.m_ImpulseDefinition.m_ImpulseDuration = duration;

        impulseSource.GenerateImpulseWithForce(force);
    }

    public void PlayerReachEnd(FloorController fc)
    {
        BeatSystem.isStop = true;
        StartCoroutine(playerReachEndCoroutin(fc));
    }

    private IEnumerator playerReachEndCoroutin(FloorController fc)
    {
        CinemachineVirtualCamera virtualCamera = fc.GetComponentInChildren<CinemachineVirtualCamera>();
        virtualCamera.Priority = 99;
        yield return new WaitForSeconds(2f);

        // targetFloor boomb
        CameraShake(1.2f, 0.4f, CinemachineImpulseDefinition.ImpulseShapes.Explosion);
        MeshDestroy[] meshes = fc.GetComponentsInChildren<MeshDestroy>();
        foreach (MeshDestroy m in meshes)
        {
            m.DestroyMesh(3);
            yield return null;
        }


        yield return new WaitForSeconds(2f);

        StartCoroutine(LoadNextSceneCoroutine());
        
        //glitchController.TriggerGlitch(0.8f, 0.8f);
        //yield return new WaitForSeconds(1f);
        
        //BeatSystem.beatUpdateObjects.Clear();
        //int currentIndex = SceneManager.GetActiveScene().buildIndex;
        //int nextIndex = currentIndex + 1;
        //if (nextIndex < SceneManager.sceneCountInBuildSettings)
        //{
        //    BeatSystem.isStop = false;
        //    SceneManager.LoadScene(nextIndex);
        //}
    }

    private IEnumerator LoadNextSceneCoroutine()
    {
        BeatSystem.isStop = true;

        int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;

        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            // BeatSystem.isStop = false;
            yield break;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(nextIndex);
        operation.allowSceneActivation = false;

        // progress 到 0.9 表示场景已经加载完成，等待激活
        while (operation.progress < 0.9f)
            yield return null;

        glitchController.TriggerGlitch(0.8f, 0.8f);
        yield return new WaitForSeconds(1f);

        BeatSystem.beatUpdateObjects.Clear();
        // BeatSystem.isStop = false;

        // 正式切换到新场景
        operation.allowSceneActivation = true;
    }


    public void PlayerBeFound(Vector3 playerPosition)
    {
        BeatSystem.isStop = true;
        StartCoroutine(playerBeFoundCoroutine(playerPosition));
    }

    private IEnumerator playerBeFoundCoroutine(Vector3 playerPosition)
    {
        if (spotLight == null)
        {
            Debug.LogWarning("SingleSceneManager：未设置 Spot Light。");
            yield break;
        }

        CameraShake();
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