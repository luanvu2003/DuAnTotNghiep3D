using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class RandomCubeController : NetworkBehaviour
{
    [Header("Cài đặt UI")]
    public TextMeshProUGUI statusText;
    public float interactRange = 3f;

    // Trạng thái của Cube: 0 = Đợi (Idle), 1 = Đang nhảy số (Rolling), 2 = Hiện kết quả (Result)
    private NetworkVariable<int> cubeState = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    // Số đang hiển thị
    private NetworkVariable<int> currentNum = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        // Đăng ký sự kiện thay đổi để cập nhật UI ngay lập tức cho tất cả mọi người
        cubeState.OnValueChanged += OnStateChanged;
        currentNum.OnValueChanged += OnNumberChanged;

        // Cập nhật giao diện lúc mới vào game
        RefreshUI();
    }

    public override void OnNetworkDespawn()
    {
        cubeState.OnValueChanged -= OnStateChanged;
        currentNum.OnValueChanged -= OnNumberChanged;
    }

    void Update()
    {
        // LỚP BẢO VỆ: Đảm bảo game đã chạy và nhân vật của bạn đã xuất hiện trên sân
        if (!IsSpawned || NetworkManager.Singleton.LocalClient == null || NetworkManager.Singleton.LocalClient.PlayerObject == null)
            return;

        // Bấm phím F và Cube đang ở trạng thái rảnh (0)
        if (Input.GetKeyDown(KeyCode.F) && cubeState.Value == 0)
        {
            // Lấy vị trí nhân vật của chính bạn
            GameObject localPlayer = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;

            // Đo khoảng cách từ người chơi đến Cube
            float distance = Vector3.Distance(transform.position, localPlayer.transform.position);

            if (distance <= interactRange)
            {
                Debug.Log("Đã bấm F thành công! Gửi lệnh quay số lên Server...");
                StartRandomServerRpc();
            }
            else
            {
                // Báo lỗi trên Console nếu bạn đứng quá xa
                Debug.Log($"Đứng quá xa Cube! Khoảng cách của bạn là {distance}, cần phải nhỏ hơn {interactRange}");
            }
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void StartRandomServerRpc()
    {
        // Nếu Server đang bận quay số rồi thì không nhận lệnh nữa (Anti-spam)
        if (cubeState.Value != 0) return;

        // Bắt đầu quy trình 5s nhảy số + 5s hiện kết quả
        StartCoroutine(RandomSequenceRoutine());
    }

    private IEnumerator RandomSequenceRoutine()
    {
        // BƯỚC 1: NHẢY SỐ TRONG 5 GIÂY
        cubeState.Value = 1; // Chuyển sang trạng thái Rolling
        float timer = 0f;
        while (timer < 5f)
        {
            currentNum.Value = Random.Range(0, 101); // Nhảy số random liên tục
            yield return new WaitForSeconds(0.1f);  // Mỗi 0.1s nhảy 1 lần
            timer += 0.1f;
        }

        // BƯỚC 2: CHỐT SỐ VÀ HIỆN KẾT QUẢ TRONG 5 GIÂY
        currentNum.Value = Random.Range(0, 101); // Số cuối cùng
        cubeState.Value = 2; // Chuyển sang trạng thái Result
        yield return new WaitForSeconds(5f);

        // BƯỚC 3: RESET VỀ BAN ĐẦU
        cubeState.Value = 0;
    }

    // --- CẬP NHẬT UI ĐỒNG BỘ CHO TẤT CẢ CLIENT ---

    private void OnStateChanged(int oldVal, int newVal) => RefreshUI();
    private void OnNumberChanged(int oldVal, int newVal) => RefreshUI();

    private void RefreshUI()
    {
        if (statusText == null) return;

        switch (cubeState.Value)
        {
            case 0: // Idle
                statusText.text = "Ấn [F] để Random";
                statusText.color = Color.white;
                break;
            case 1: // Rolling
                statusText.text = "Đang quay: " + currentNum.Value;
                statusText.color = Color.yellow;
                break;
            case 2: // Result
                statusText.text = "KẾT QUẢ: " + currentNum.Value;
                statusText.color = Color.green;
                break;
        }
    }
}