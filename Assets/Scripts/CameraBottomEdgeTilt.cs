using UnityEngine;

public class CameraBottomEdgeTilt : MonoBehaviour
{
    [Header("Mouse Detection")]
    [Range(0.01f, 1f)]
    [Tooltip("屏幕底部触发区域占屏幕高度的比例，例如0.1表示底部10%")]
    [SerializeField] private float bottomEdgePercent = 0.1f;

    [Header("Camera Rotation")]
    [SerializeField] private float maxTiltAngle = 8f;
    [SerializeField] private float rotationSpeed = 8f;

    private Quaternion initialLocalRotation;

    private void Awake()
    {
        initialLocalRotation = transform.localRotation;
    }

    private void Update()
    {
        // 转换为0~1的屏幕纵向比例。
        float mouseYPercent = Input.mousePosition.y / Screen.height;

        // 鼠标位于底部触发区域时：
        // 最底部为1，离开触发区域时为0。
        float tiltRatio = Mathf.InverseLerp(
            bottomEdgePercent,
            0f,
            mouseYPercent
        );

        float tiltAngle = maxTiltAngle * tiltRatio;

        Quaternion targetRotation =
            initialLocalRotation *
            Quaternion.Euler(tiltAngle, 0f, 0f);

        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }
}