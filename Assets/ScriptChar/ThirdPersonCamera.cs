using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public float distance = 4f;

    [Header("Camera Controls")]
    public InputActionReference lookInput;
    public float mouseSensitivity = 0.2f;
    public float pitchMin = -20f;
    public float pitchMax = 60f;

    private float yaw = 0f;
    private float pitch = 0f;

    // Biến lưu trạng thái hiện tại của chuột
    private bool isCursorLocked = true;

    private void Start()
    {
        // Khởi tạo ban đầu: Khóa và ẩn chuột để chơi game
        SetCursorState(true);
    }

    private void OnEnable()
    {
        lookInput.action.Enable();
    }

    private void OnDisable()
    {
        lookInput.action.Disable();
    }

    private void Update()
    {
        // Nhận diện phím Left Alt bằng New Input System
        if (Keyboard.current != null && Keyboard.current.leftAltKey.wasPressedThisFrame)
        {
            isCursorLocked = !isCursorLocked; // Đảo ngược trạng thái (Đang khóa -> Mở, Đang mở -> Khóa)
            SetCursorState(isCursorLocked);
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // CHỈ KHÓA VIỆC XOAY CAMERA KHI CHUỘT ĐANG HIỆN
        if (isCursorLocked)
        {
            Vector2 lookDelta = lookInput.action.ReadValue<Vector2>();

            yaw += lookDelta.x * mouseSensitivity;
            pitch -= lookDelta.y * mouseSensitivity;

            pitch = Mathf.Clamp(pitch, pitchMin, pitchMax);
        }

        // VẪN CHO PHÉP CAMERA ĐI THEO VỊ TRÍ NHÂN VẬT DÙ CÓ ĐANG HIỆN CHUỘT HAY KHÔNG
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 position = target.position - (rotation * Vector3.forward * distance) + (Vector3.up * 1.5f);

        transform.position = position;
        transform.rotation = rotation;
    }

    // Hàm hỗ trợ bật/tắt chuột
    private void SetCursorState(bool locked)
    {
        if (locked)
        {
            Cursor.lockState = CursorLockMode.Locked; // Khóa chuột vào giữa màn hình
            Cursor.visible = false;                   // Ẩn con trỏ chuột
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;   // Giải phóng chuột
            Cursor.visible = true;                    // Hiện con trỏ chuột
        }
    }
}