using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCameraController : MonoBehaviour
{
    [Header("Target")]
    public Transform playerTransform;
    public Vector3 targetOffset = new Vector3(0f, 1.5f, 0f);
    public float distance = 4.0f;

    [Header("Sensitivity & Limits")]
    public float mouseSensitivity = 15f;
    public float yMinLimit = -20f;
    public float yMaxLimit = 70f;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        if (playerTransform != null)
            rotationY = playerTransform.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (playerTransform == null) return;

        // ✅ PlayerInput 이벤트 없이 직접 읽기
        if (Mouse.current != null)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            rotationY += delta.x * mouseSensitivity * 0.01f;
            rotationX -= delta.y * mouseSensitivity * 0.01f;
            rotationX = Mathf.Clamp(rotationX, yMinLimit, yMaxLimit);
        }

        Quaternion cameraRotation = Quaternion.Euler(rotationX, rotationY, 0f);
        Vector3 targetPosition = playerTransform.position + targetOffset;
        Vector3 cameraPosition = (cameraRotation * new Vector3(0f, 0f, -distance)) + targetPosition;

        transform.rotation = cameraRotation;
        transform.position = cameraPosition;

        // 플레이어는 Y축 회전만 동기화
        playerTransform.rotation = Quaternion.Euler(0f, rotationY, 0f);
    }
}