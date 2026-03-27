using UnityEngine;
using UnityEngine.UIElements;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Text;
using System.Collections.Generic;

public class LobbyUIController : NetworkBehaviour
{
    private string vpsIP = "203.145.47.241";
    private string currentRoomCode = "";

    // Server dùng để lưu trữ danh sách tên đồng bộ
    private Dictionary<ulong, string> clientNames = new Dictionary<ulong, string>();

    [Header("Cài đặt nhân vật")]
    public GameObject playerPrefab;
    [Header("Cài đặt Quả Bóng")]
    public GameObject ballPrefab;       // Kéo Prefab quả bóng vào đây
    public Transform ballSpawnPoint;    // Kéo vị trí BallSpawnPoint vào đây
    [Header("Cài đặt Enemy")]
    public GameObject enemyPrefab;       // Kéo Prefab quả bóng vào đây
    public Transform enemySpawnPoint;    // Kéo vị trí BallSpawnPoint vào đây

    private VisualElement mainMenuContent, roomPanel, playerListBox, container, boxMenu;
    private Label roomCodeDisplay, errorText, waitingMsg;
    private TextField nameInput, codeInput;
    private Button joinBtn, hostBtn, startBtn;
    private ulong lobbyHostId;
    [Header("Cài đặt Spawn")]
    // Kéo 4 cái Transform từ Scene vào đây
    public Transform[] spawnPoints;

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;

            // SỬA Ở ĐÂY: Lắng nghe cả khi vào và khi thoát
            NetworkManager.Singleton.OnClientConnectedCallback += OnServerPlayerJoin;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnServerPlayerLeft;
        }

        if (Application.isBatchMode) { NetworkManager.Singleton.StartServer(); return; }
        StartCoroutine(InitUI());
    }

    private void OnServerPlayerLeft(ulong clientId)
    {
        if (!IsServer) return;

        if (clientNames.ContainsKey(clientId)) clientNames.Remove(clientId);

        // Nếu chủ phòng thoát
        if (clientId == lobbyHostId)
        {
            // Nhượng quyền cho người đầu tiên còn lại trong danh sách
            if (NetworkManager.Singleton.ConnectedClientsIds.Count > 0)
            {
                lobbyHostId = NetworkManager.Singleton.ConnectedClientsIds[0];
                Debug.Log($"[SERVER] Chủ phòng thoát. Quyền chủ phòng mới: {lobbyHostId}");
            }
        }

        UpdatePlayerListClientRpc(string.Join("|", GetPlayerNamesArray()));
    }

    System.Collections.IEnumerator InitUI()
    {
        yield return null;
        var root = GetComponent<UIDocument>().rootVisualElement;

        container = root.Q<VisualElement>("container-fullscreen");
        boxMenu = root.Q<VisualElement>("ma-mi-panel");
        nameInput = root.Q<TextField>("input-name");
        codeInput = root.Q<TextField>("input-code");
        errorText = root.Q<Label>("error-text");
        joinBtn = root.Q<Button>("btn-join");
        hostBtn = root.Q<Button>("btn-host");

        mainMenuContent = root.Q<VisualElement>("main-menu-content");
        roomPanel = root.Q<VisualElement>("room-panel");
        playerListBox = root.Q<VisualElement>("player-list-box");
        roomCodeDisplay = root.Q<Label>("room-code-display");
        startBtn = root.Q<Button>("btn-start-game");
        waitingMsg = root.Q<Label>("waiting-msg");

        roomPanel.style.display = DisplayStyle.None;
        joinBtn.clicked += OnJoinClicked;
        hostBtn.clicked += OnHostClicked;
        startBtn.clicked += OnStartGameClicked;

        nameInput.value = PlayerPrefs.GetString("PlayerName", "ThoSan_" + Random.Range(10, 99));
    }

    // --- LOGIC SERVER ---
    private void OnServerPlayerJoin(ulong clientId)
    {
        if (!IsServer) return;
        // Gộp tất cả tên thành 1 chuỗi duy nhất: "Ten1|Ten2|Ten3"
        string combinedNames = string.Join("|", GetPlayerNamesArray());
        UpdatePlayerListClientRpc(combinedNames);
    }

    private string[] GetPlayerNamesArray()
    {
        List<string> names = new List<string>();
        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientNames.ContainsKey(id)) names.Add(clientNames[id]);
            else names.Add("Thợ săn mới");
        }
        return names.ToArray();
    }

    [ClientRpc]
    private void UpdatePlayerListClientRpc(string combinedNames)
    {
        playerListBox.Clear();
        // Chẻ chuỗi ra lại thành mảng
        string[] names = combinedNames.Split('|');
        foreach (string n in names)
        {
            if (string.IsNullOrEmpty(n)) continue;
            Label pLabel = new Label("• " + n);
            pLabel.AddToClassList("player-item-label");
            playerListBox.Add(pLabel);
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.CreatePlayerObject = false;
        string payload = Encoding.UTF8.GetString(request.Payload);
        string[] data = payload.Split(':');

        string action = data[0];
        string pName = data[1];
        string cCode = data[2];

        if (action == "CREATE")
        {
            currentRoomCode = cCode;
            response.Approved = true;
            // Ghi nhớ ID của người tạo phòng
            lobbyHostId = request.ClientNetworkId;
        }
        else
        {
            response.Approved = (cCode == currentRoomCode);
        }

        if (response.Approved)
        {
            clientNames[request.ClientNetworkId] = pName;
        }
    }

    // --- LOGIC ĐIỀU KHIỂN ---
    private void OnHostClicked()
    {
        string code = Random.Range(100000, 999999).ToString();
        ConnectToServer("CREATE", nameInput.value, code);
        SwitchToRoomUI(code, true);
    }

    private void OnJoinClicked()
    {
        string code = codeInput.value.Trim();
        if (code.Length < 6) return;
        ConnectToServer("JOIN", nameInput.value, code);
        SwitchToRoomUI(code, false);
    }

    private void SwitchToRoomUI(string code, bool isHost)
    {
        mainMenuContent.style.display = DisplayStyle.None;
        roomPanel.style.display = DisplayStyle.Flex;
        roomCodeDisplay.text = "MÃ PHÒNG: " + code;
        startBtn.style.display = isHost ? DisplayStyle.Flex : DisplayStyle.None;
        waitingMsg.style.display = isHost ? DisplayStyle.None : DisplayStyle.Flex;
    }

    private void OnStartGameClicked()
    {
        // Máy nào cũng bấm được nút này, nhưng ta sẽ gửi yêu cầu lên Server
        Debug.Log("Đang gửi yêu cầu bắt đầu game lên Server...");
        RequestStartGameServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestStartGameServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;

        // Kiểm tra quyền chủ phòng (như cũ)
        if (rpcParams.Receive.SenderClientId != lobbyHostId) return;

        int index = 0;
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            // Mặc định là vị trí gốc nếu quên gán spawnPoints
            Vector3 spawnPosition = new Vector3(index * 2, 2, 0);
            Quaternion spawnRotation = Quaternion.identity;

            // Nếu đã gán các vị trí spawn trong Inspector
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                int posIndex = index % spawnPoints.Length;
                spawnPosition = spawnPoints[posIndex].position;
                spawnRotation = spawnPoints[posIndex].rotation;
            }

            // --- FIX 1: DỌN NHÂN VẬT CŨ NẾU CHƠI LẠI VÁN MỚI ---
            if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
            {
                // Nếu người này đã có nhân vật từ ván trước, hủy nó đi
                if (client.PlayerObject != null && client.PlayerObject.IsSpawned)
                {
                    client.PlayerObject.Despawn(true);
                }
            }

            // Đẻ nhân vật mới
            GameObject player = Instantiate(playerPrefab, spawnPosition, spawnRotation);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);

            Debug.Log($"[SERVER] Đã spawn thợ săn {clientId} tại vị trí {index + 1}");
            index++;
        }

        // --- FIX 2: SỬA LỖI XÓA BÓNG TỪ VÁN TRƯỚC ---
        GameObject[] oldBalls = GameObject.FindGameObjectsWithTag("Ball");
        foreach (GameObject b in oldBalls)
        {
            var netObj = b.GetComponent<NetworkObject>();

            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true); // Lệnh này tự động Hủy mạng + Xóa (Destroy) vật thể luôn
            }
            else
            {
                Destroy(b); // Chỉ dùng Destroy thủ công nếu nó bị kẹt chưa được lên mạng
            }
        }

        if (ballPrefab != null)
        {
            Vector3 spawnPos = new Vector3(0, 5, 0); // Vị trí dự phòng
            Quaternion spawnRot = Quaternion.identity;

            // Nếu đã gán vị trí BallSpawnPoint trên Inspector thì lấy vị trí đó
            if (ballSpawnPoint != null)
            {
                spawnPos = ballSpawnPoint.position;
                spawnRot = ballSpawnPoint.rotation;
            }

            // Tạo quả bóng và gọi nó ra mạng cho tất cả cùng thấy
            GameObject ball = Instantiate(ballPrefab, spawnPos, spawnRot);
            ball.GetComponent<NetworkObject>().Spawn();

            Debug.Log("[SERVER] Tiếng còi khai cuộc! Đã ném bóng ra sân!");
        }


        GameObject[] oldEnemy = GameObject.FindGameObjectsWithTag("Enemy");
        foreach (GameObject e in oldEnemy)
        {
            var netObj = e.GetComponent<NetworkObject>();

            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true); // Lệnh này tự động Hủy mạng + Xóa (Destroy) vật thể luôn
            }
            else
            {
                Destroy(e); // Chỉ dùng Destroy thủ công nếu nó bị kẹt chưa được lên mạng
            }
        }

        if (enemyPrefab != null)
        {
            Vector3 spawnPos = new Vector3(0, 5, 0); // Vị trí dự phòng
            Quaternion spawnRot = Quaternion.identity;

            // Nếu đã gán vị trí EnemySpawnPoint trên Inspector thì lấy vị trí đó
            if (enemySpawnPoint != null)
            {
                spawnPos = enemySpawnPoint.position;
                spawnRot = enemySpawnPoint.rotation;
            }

            // Tạo quả bóng và gọi nó ra mạng cho tất cả cùng thấy
            GameObject enemy = Instantiate(enemyPrefab, spawnPos, spawnRot);
            enemy.GetComponent<NetworkObject>().Spawn();

            Debug.Log("[SERVER] Tiếng còi khai cuộc! Đã ném bóng ra sân!");
        }

        // Cuối cùng mới gọi ClientRpc để chuyển Scene hoặc tắt UI
        StartGameClientRpc();
    }

    [ClientRpc]
    private void StartGameClientRpc()
    {
        container.style.display = DisplayStyle.None; // Ẩn UI
        Camera.main.gameObject.SetActive(true);      // Bật Camera khi bắt đầu đi săn
    }
    private void ConnectToServer(string action, string playerName, string code)
    {
        PlayerPrefs.SetString("PlayerName", playerName);
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.ConnectionData.Address = vpsIP;
        string payload = $"{action}:{playerName}:{code}";
        NetworkManager.Singleton.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(payload);
        NetworkManager.Singleton.StartClient();
    }

    private void OnClientDisconnect(ulong clientId)
    {
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            // Reset UI nếu bị rớt mạng hoặc sai pass
            mainMenuContent.style.display = DisplayStyle.Flex;
            roomPanel.style.display = DisplayStyle.None;
        }
    }
}