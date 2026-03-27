
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Unity.Netcode.Components;

// BẮT BUỘC: Nhân vật cần có NetworkObject, NetworkTransform và NetworkAnimator
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkObject))]
public class PlayerController : NetworkBehaviour
{
    [Header("Animator Settings")]
    public Animator animator;

    [Header("Movement Settings")]
    public float slowWalkSpeed = 2f;    // Tốc độ đi bộ (Khi giữ Left Ctrl)
    public float mediumRunSpeed = 5f;   // Tốc độ chạy vừa (Mặc định)
    public float sprintSpeed = 8f;      // Tốc độ chạy nhanh (Khi giữ Sprint)
    public float rotationSpeed = 10f;

    [Header("Jump & Gravity Settings")]
    public float jumpHeight = 2f;
    public float gravity = -15f;
    private Vector3 velocity;
    private bool isGrounded;

    [Header("Input Settings")]
    public InputActionReference moveInput;
    public InputActionReference jumpInput;
    public InputActionReference attackInput;
    public InputActionReference sprintInput;
    public InputActionReference walkInput;

    [Header("Camera Reference")]
    public Transform mainCamera;
    [Header("Physics Settings")]
    public float pushPower = 2.0f; // Độ mạnh khi đẩy vật
    public float kickCooldown = 0.5f; // Thời gian chờ giữa 2 lần đá
    private float lastKickTime = 0f;  // Lưu lại thời điểm đá cuối cùng

    private CharacterController characterController;
    private NetworkAnimator networkAnimator;

    // Biến mạng lưu tốc độ hiện tại để đồng bộ Animator cho tất cả mọi người cùng xem
    private NetworkVariable<float> netAnimSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
    private NetworkVariable<bool> netIsGrounded = new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponentInChildren<Animator>();
        networkAnimator = GetComponent<NetworkAnimator>();
    }

    private void OnEnable()
    {
        moveInput.action.Enable();
        jumpInput.action.Enable();
        attackInput.action.Enable();
        sprintInput.action.Enable();
        walkInput.action.Enable();
    }

    private void OnDisable()
    {
        moveInput.action.Disable();
        jumpInput.action.Disable();
        attackInput.action.Disable();
        sprintInput.action.Disable();
        walkInput.action.Disable();
    }

    // Khi nhân vật được sinh ra trên mạng (Spawn)
    public override void OnNetworkSpawn()
    {
        if (characterController == null) characterController = GetComponent<CharacterController>();

        if (IsOwner)
        {
            if (characterController != null)
            {
                characterController.enabled = true;
                Debug.Log("CharacterController đã được kích hoạt!");
            }

            // TỰ ĐỘNG TÌM CAMERA VÀ GÁN MỤC TIÊU
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                // Quan trọng: Gán transform camera vào biến mainCamera để không bị lỗi null
                mainCamera = mainCam.transform;

                ThirdPersonCamera camScript = mainCam.GetComponent<ThirdPersonCamera>();
                if (camScript != null)
                {
                    camScript.target = this.transform;
                }
            }
        }
        else
        {
            // Bật cho cả người chơi khác để đồng bộ mượt hơn
            if (characterController != null) characterController.enabled = true;
        }
    }

    public void Update()
    {
        // 1. Nếu không phải mình thì chỉ cập nhật hoạt ảnh rồi thoát
        if (!IsOwner)
        {
            UpdateAnimatorForOthers();
            return;
        }

        // 2. Nếu CharacterController chưa bật thì không làm gì cả
        if (characterController != null && characterController.enabled == false) return;

        // 3. LUÔN LUÔN chạy trọng lực (Đã đưa lên trước để không bị Camera chặn)
        ApplyGravityAndJumping();

        // 4. Chỉ xử lý di chuyển khi đã tìm thấy Camera
        if (mainCamera != null)
        {
            HandleMovement();
        }
        else
        {
            // Nếu lúc đầu chưa tìm thấy thì tìm lại
            if (Camera.main != null) mainCamera = Camera.main.transform;
        }
    }
    private void HandleMovement()
    {
        Vector2 moveInputValue = moveInput.action.ReadValue<Vector2>();

        Vector3 camForward = mainCamera.forward;
        Vector3 camRight = mainCamera.right;

        camForward.y = 0f;
        camRight.y = 0f;
        camForward.Normalize();
        camRight.Normalize();

        Vector3 moveDirection = (camForward * moveInputValue.y) + (camRight * moveInputValue.x);

        bool isSprinting = sprintInput.action.IsPressed();
        bool isWalking = walkInput.action.IsPressed();

        float currentMoveSpeed = mediumRunSpeed;
        float targetAnimSpeed = 0.5f;

        if (isWalking)
        {
            currentMoveSpeed = slowWalkSpeed;
            targetAnimSpeed = 0.2f;
        }
        else if (isSprinting)
        {
            currentMoveSpeed = sprintSpeed;
            targetAnimSpeed = 1f;
        }

        if (moveDirection.magnitude == 0)
        {
            targetAnimSpeed = 0f;
        }

        if (moveDirection.magnitude >= 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

            characterController.Move(moveDirection * currentMoveSpeed * Time.deltaTime);
        }

        // Cập nhật giá trị vào biến mạng (NetworkVariable) để mọi người cùng biết tốc độ của mình
        netAnimSpeed.Value = Mathf.Lerp(netAnimSpeed.Value, targetAnimSpeed, Time.deltaTime * 10f);

        // Cập nhật Animator cho chính mình xem
        if (animator != null)
        {
            animator.SetFloat("Speed", netAnimSpeed.Value);
        }
    }

    private void ApplyGravityAndJumping()
    {
        isGrounded = characterController.isGrounded;

        // Cập nhật biến mạng IsGrounded
        netIsGrounded.Value = isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
            if (animator != null) animator.SetBool("IsGrounded", true);
        }
        else
        {
            if (animator != null) animator.SetBool("IsGrounded", false);
        }

        if (jumpInput.action.triggered && isGrounded)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

            // Dùng NetworkAnimator để đồng bộ Trigger "Jump" cho tất cả mọi người
            if (networkAnimator != null) networkAnimator.SetTrigger("Jump");
        }

        velocity.y += gravity * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    // Hàm này chỉ dành cho máy của MÌNH dùng để vẽ hoạt ảnh cho NGƯỜI KHÁC
    private void UpdateAnimatorForOthers()
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", netAnimSpeed.Value);
            animator.SetBool("IsGrounded", netIsGrounded.Value);
        }
    }

    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (Time.time - lastKickTime < kickCooldown) return;

        Rigidbody body = hit.collider.attachedRigidbody;

        // CHÚ Ý: ĐÃ XÓA "|| body.isKinematic"
        // Vì NetworkRigidbody trên Client sẽ tự động làm quả bóng thành Kinematic
        if (body == null) return;

        if (hit.moveDirection.y < -0.3f) return;

        var netObj = hit.collider.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            lastKickTime = Time.time;

            // TÍNH HƯỚNG SÚT: Từ tâm người chơi xuyên qua tâm quả bóng (chuẩn nhất)
            Vector3 pushDir = hit.collider.transform.position - transform.position;
            pushDir.y = 0; // Bỏ trục Y để lực đẩy chỉ bay ngang trên mặt đất
            pushDir.Normalize(); // Chuẩn hóa về độ dài 1

            // Gọi Server đá hộ. PushPower để khoảng 20 là bay rất xa rồi nhé!
            PushBallServerRpc(netObj.NetworkObjectId, pushDir * pushPower);
        }
    }

    [ServerRpc]
    private void PushBallServerRpc(ulong targetId, Vector3 force)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetId, out var netObj))
        {
            Rigidbody rb = netObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Bật dòng Log này lên để chắc chắn Server đã nhận được lệnh
                Debug.Log($"[SERVER] Đang sút bóng với lực: {force.magnitude}");
                rb.AddForce(force, ForceMode.Impulse);
            }
        }
    }
}