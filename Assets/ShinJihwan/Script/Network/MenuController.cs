using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

/// <summary>
/// 메인메뉴 버튼 이벤트 연결용.
/// NetworkManager와 같은 오브젝트에 붙음 (DontDestroyOnLoad).
///
/// [버튼 연결 방법]
/// - Start 버튼         → Press Complete → MenuController.ShowHostUI
/// - Make Room 버튼     → Press Complete → MenuController.CreateRoom
/// - Join 버튼          → Press Complete → MenuController.JoinRoom
/// - Game Start 버튼    → Press Complete → MenuController.GameStart
/// - Leave the room 버튼→ Press Complete → MenuController.LeaveRoom
///
/// [Inspector 추가 연결]
/// - joinedPanel : __________UI_HMIE__________ (연결된 방 패널)
/// - basicPanel  : __________UI_Basic__________ 또는 UI_Host (Join 실패 시 되돌릴 패널)
/// </summary>
public class MenuController : MonoBehaviour
{
    [Header("UI 패널")]
    [Tooltip("__________UI_Host__________ 오브젝트 드래그")]
    public GameObject hostUIPanel;

    [Header("M3D Input Field (IP/코드 입력창)")]
    [Tooltip("씬의 Input Field (M3D) 오브젝트 드래그")]
    public TinyGiantStudio.Text.InputField m3dInputField;

    [Header("카드 씬 이름")]
    public string cardSceneName = "CardMap";

    private bool _isLeaving = false;

    private void Awake()
    {
        // DontDestroyOnLoad 오브젝트는 씬 리로드 후에도 살아있으므로
        // 씬 로드 이벤트로 _isLeaving 초기화
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _isLeaving = false;
        Debug.Log("[MenuController] 씬 로드 완료 - 상태 초기화");
    }

    // ─────────────────────────────────────────────
    // Start 버튼 → Press Complete에 연결
    // ─────────────────────────────────────────────
    public void ShowHostUI()
    {
        if (hostUIPanel != null)
            hostUIPanel.SetActive(true);
    }

    // ─────────────────────────────────────────────
    // Make Room 버튼 → Press Complete에 연결
    // ─────────────────────────────────────────────
    public void CreateRoom()
    {
        if (NetworkServer.active || NetworkClient.isConnected)
        {
            Debug.Log("[MenuController] 이미 연결 중");
            return;
        }

        NetworkManager.singleton.StartHost();
        Debug.Log("[MenuController] Host 시작 (방 만들기)");

        RoomNetworkManager.Instance?.OnRoomCreated();
    }

    // ─────────────────────────────────────────────
    // Join 버튼 → Press Complete에 연결
    // NextUI_Animation.Next_UI()가 먼저 실행된 뒤 이 메서드가 호출됨.
    // 코드가 없으면 패널을 되돌린다.
    // ─────────────────────────────────────────────
    public void JoinRoom()
    {
        if (NetworkClient.isConnected)
        {
            Debug.Log("[MenuController] 이미 연결 중");
            return;
        }

        // 코드 미입력 검증
        string code = "";
        if (m3dInputField != null)
            code = m3dInputField.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("[MenuController] 방 코드를 입력하세요");
            // Next_UI()가 이미 커넥티드 패널을 열었으므로 되돌리기
            RoomNetworkManager.Instance?.ResetUI();
            if (hostUIPanel != null) hostUIPanel.SetActive(true);
            return;
        }

        RoomNetworkManager.Instance?.OnJoinRoom();
    }

    // ─────────────────────────────────────────────
    // Game Start 버튼 → Press Complete에 연결
    // ─────────────────────────────────────────────
    public void GameStart()
    {
        if (!NetworkServer.active)
        {
            Debug.LogWarning("[MenuController] Host만 Game Start 가능 (현재 Client)");
            return;
        }

        if (NetworkServer.connections.Count < 2)
        {
            Debug.LogWarning($"[MenuController] 플레이어 부족 ({NetworkServer.connections.Count}/2) - 상대방이 참가해야 시작 가능");
            return;
        }

        Debug.Log($"[MenuController] 카드 씬으로 이동: {cardSceneName}");
        NetworkManager.singleton.ServerChangeScene(cardSceneName);
    }

    // ─────────────────────────────────────────────
    // Leave the room 버튼 → Press Complete에 연결
    // ─────────────────────────────────────────────
    public void LeaveRoom()
    {
        if (_isLeaving)
        {
            Debug.Log("[MenuController] 이미 나가는 중");
            return;
        }

        if (!NetworkServer.active && !NetworkClient.isConnected)
        {
            Debug.Log("[MenuController] 연결 상태가 아님 → UI만 리셋");
            RoomNetworkManager.Instance?.ResetUI();
            return;
        }

        _isLeaving = true;
        Debug.Log("[MenuController] 방 나가기");

        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();
        else if (NetworkClient.isConnected)
            NetworkManager.singleton.StopClient();

        RoomNetworkManager.Instance?.ResetUI();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
