using UnityEngine;
using Mirror;

/// <summary>
/// 메인메뉴 버튼 이벤트 연결용.
/// 씬의 MenuController 오브젝트에 붙이고 Inspector 연결.
///
/// [버튼 연결 방법]
/// - Start 버튼         → Press Complete → MenuController.ShowHostUI
/// - Make Room 버튼     → Press Complete → MenuController.CreateRoom
/// - Join 버튼          → Press Complete → MenuController.JoinRoom
/// - Game Start 버튼    → Press Complete → MenuController.GameStart
/// - Leave the room 버튼→ Press Complete → MenuController.LeaveRoom
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

        // 방 코드 생성 및 UI 업데이트
        RoomNetworkManager.Instance?.OnRoomCreated();
    }

    // ─────────────────────────────────────────────
    // Join 버튼 → Press Complete에 연결
    // ─────────────────────────────────────────────
    public void JoinRoom()
    {
        if (NetworkClient.isConnected)
        {
            Debug.Log("[MenuController] 이미 연결 중");
            return;
        }

        // RoomNetworkManager가 Input Field에서 코드 읽어서 처리
        RoomNetworkManager.Instance?.OnJoinRoom();
    }

    // ─────────────────────────────────────────────
    // Game Start 버튼 → Press Complete에 연결
    // Host만 실행 가능, 2명 접속 시에만 동작
    // ─────────────────────────────────────────────
    public void GameStart()
    {
        if (!NetworkServer.active)
        {
            Debug.Log("[MenuController] Host만 Game Start 가능");
            return;
        }

        if (NetworkServer.connections.Count < 2)
        {
            Debug.Log($"[MenuController] 플레이어 부족 ({NetworkServer.connections.Count}/2)");
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
        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();
        else if (NetworkClient.isConnected)
            NetworkManager.singleton.StopClient();

        Debug.Log("[MenuController] 방 나가기");
    }
}
