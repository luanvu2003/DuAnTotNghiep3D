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

    private VisualElement mainMenuContent, roomPanel, playerListBox, container, boxMenu;
    private Label roomCodeDisplay, errorText, waitingMsg;
    private TextField nameInput, codeInput;
    private Button joinBtn, hostBtn, startBtn;

    void Start()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.NetworkConfig.ConnectionApproval = true;
            NetworkManager.Singleton.ConnectionApprovalCallback = ApprovalCheck;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;

            // Server lắng nghe sự kiện kết nối để cập nhật tên cho tất cả
            NetworkManager.Singleton.OnClientConnectedCallback += OnServerPlayerJoin;
        }

        if (Application.isBatchMode) { NetworkManager.Singleton.StartServer(); return; }
        StartCoroutine(InitUI());
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
        // Mỗi khi có người mới, Server gửi lại toàn bộ danh sách tên cho tất cả Client
        UpdatePlayerListClientRpc(GetPlayerNamesArray());
    }

    private string[] GetPlayerNamesArray()
    {
        List<string> names = new List<string>();
        foreach (var id in NetworkManager.Singleton.ConnectedClientsIds)
        {
            if (clientNames.ContainsKey(id)) names.Add(clientNames[id]);
            else names.Add("Thợ săn đang kết nối...");
        }
        return names.ToArray();
    }

    [ClientRpc]
    private void UpdatePlayerListClientRpc(string[] names)
    {
        playerListBox.Clear();
        foreach (string n in names)
        {
            Label pLabel = new Label("• " + n);
            pLabel.AddToClassList("player-item-label");
            playerListBox.Add(pLabel);
        }
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.CreatePlayerObject = false; // Chặn không cho đẻ nhân vật tự động

        string payload = Encoding.UTF8.GetString(request.Payload);
        string[] data = payload.Split(':');

        if (data.Length < 3) { response.Approved = false; return; }

        string action = data[0];
        string pName = data[1];
        string cCode = data[2];

        if (action == "CREATE") { currentRoomCode = cCode; response.Approved = true; }
        else { response.Approved = (cCode == currentRoomCode); }

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
        if (!IsServer) return;

        // Spawn nhân vật cho từng Client dựa trên ID
        foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
        {
            GameObject player = Instantiate(playerPrefab);
            player.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId);
        }

        // Thông báo cho tất cả Client ẩn UI đi để bắt đầu chơi
        StartGameClientRpc();
    }

    [ClientRpc]
    private void StartGameClientRpc()
    {
        container.style.display = DisplayStyle.None;
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