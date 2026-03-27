
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Unity.Netcode.Components;
using TMPro;
using UnityEngine.UI;

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
    [Header("Hệ thống Máu")]
    public NetworkVariable<int> currentHP = new NetworkVariable<int>(100, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public int maxHP = 100;

    [Header("UI Máu & Hồi máu")]
    public Slider hpSlider;              // Kéo HPSlider vào đây
    public GameObject healUIContainer;   // Kéo HealUIContainer vào đây
    public Slider healProgressSlider;    // Kéo ProgressSlider vào đây
    public TextMeshProUGUI healText;                // Kéo Text vào đây (Nếu dùng TextMeshPro thì đổi thành TMP_Text)

    [Header("Cài đặt Cứu thương")]
    public float healRange = 3f;         // Khoảng cách cứu (3 mét)
    public float requiredHealTime = 3f;  // Thời gian giữ E (3 giây)
    public int healAmount = 20;          // Lượng máu hồi
    public float healCooldown = 5f;      // Hồi chiêu 5 giây
    private float currentHealTime = 0f;
    private float lastHealTime = -10f;

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
        // Cài đặt thanh máu ban đầu
        if (hpSlider != null)
        {
            hpSlider.maxValue = maxHP;
            hpSlider.value = currentHP.Value;
        }

        // Đăng ký tự động nhảy máu khi bị đánh/hồi máu
        currentHP.OnValueChanged += UpdateHPUI;

        // Giấu cụm UI Hồi máu đi (Chỉ hiện khi đến gần người bị thương)
        if (healUIContainer != null) healUIContainer.SetActive(false);
    }

    public override void OnNetworkDespawn()
    {
        // Hủy đăng ký khi chết/thoát game để tránh lỗi
        currentHP.OnValueChanged -= UpdateHPUI;
    }

    private void UpdateHPUI(int previousValue, int newValue)
    {
        if (hpSlider != null) hpSlider.value = newValue;
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
        HandleHealingTeammate();
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
    private void HandleHealingTeammate()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, healRange);
        PlayerController targetToHeal = null;

        // Quét tìm đồng đội bị thương
        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag("Player") && hit.gameObject != this.gameObject)
            {
                PlayerController teammate = hit.GetComponent<PlayerController>();
                // Chỉ nhắm vào đồng đội có máu < 100
                if (teammate != null && teammate.currentHP.Value < teammate.maxHP)
                {
                    targetToHeal = teammate;
                    break;
                }
            }
        }

        bool isCooldown = (Time.time - lastHealTime) < healCooldown;

        if (targetToHeal != null)
        {
            // Có người bị thương ở gần -> Bật Cụm UI của bạn lên
            if (healUIContainer != null) healUIContainer.SetActive(true);

            if (isCooldown)
            {
                if (healText != null) healText.text = $"Hồi chiêu: {Mathf.CeilToInt(healCooldown - (Time.time - lastHealTime))}s";
                if (healProgressSlider != null) healProgressSlider.gameObject.SetActive(false); // Giấu thanh 3s
                currentHealTime = 0f;
            }
            else
            {
                if (healText != null) healText.text = "Giữ E để cứu";

                if (Input.GetKey(KeyCode.E))
                {
                    // Đang đè E -> Hiện thanh 3s và cho chạy
                    if (healProgressSlider != null) healProgressSlider.gameObject.SetActive(true);
                    currentHealTime += Time.deltaTime;

                    if (healProgressSlider != null)
                    {
                        healProgressSlider.maxValue = requiredHealTime;
                        healProgressSlider.value = currentHealTime;
                    }

                    // Đủ 3 giây -> Hồi máu
                    if (currentHealTime >= requiredHealTime)
                    {
                        HealTeammateServerRpc(targetToHeal.NetworkObjectId, healAmount);
                        currentHealTime = 0f;
                        lastHealTime = Time.time;
                        if (healProgressSlider != null) healProgressSlider.gameObject.SetActive(false);
                    }
                }
                else
                {
                    // Không đè E -> Giấu thanh 3s
                    currentHealTime = 0f;
                    if (healProgressSlider != null) healProgressSlider.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            // Xung quanh không có ai bị thương -> Tắt sạch UI của bạn
            currentHealTime = 0f;
            if (healUIContainer != null) healUIContainer.SetActive(false);
        }
    }

    [ServerRpc]
    private void HealTeammateServerRpc(ulong targetPlayerId, int amount)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetPlayerId, out var targetObj))
        {
            var targetPlayer = targetObj.GetComponent<PlayerController>();
            if (targetPlayer != null)
            {
                targetPlayer.currentHP.Value += amount;
                if (targetPlayer.currentHP.Value > targetPlayer.maxHP)
                    targetPlayer.currentHP.Value = targetPlayer.maxHP;
            }
        }
    }

    // Hàm này để Enemy gọi khi cắn trúng
    public void TakeDamage(int damage)
    {
        if (!IsServer) return;

        currentHP.Value -= damage;
        if (currentHP.Value < 0) currentHP.Value = 0;
    }
}