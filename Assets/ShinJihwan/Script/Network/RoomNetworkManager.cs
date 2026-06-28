using System.Collections;
using System.Net;
using System.Net.Sockets;
using Mirror;
using TinyGiantStudio.Text;
using UnityEngine;

/// <summary>
/// Mirror 기반 방 생성/참가 매니저.
///
/// 흐름:
///   Host: Make Room 클릭 → StartHost → 방 코드 생성 → UI에 코드 표시
///   Client: Join 클릭 → 코드 입력값으로 Host IP 연결 → 자동 입장
///
/// Inspector 연결:
///   - hostIdText       : "Host id :" 옆 M3D Text (코드 표시용)
///   - player1Text      : Player1 M3D Text
///   - player2Text      : Player2 M3D Text
///   - codeInputField   : Input Field (M3D) - 참가 코드 입력
///   - connectedPanel   : __________UI_커넥티드__________ 오브젝트
/// </summary>
public class RoomNetworkManager : MonoBehaviour
{
    public static RoomNetworkManager Instance { get; private set; }

    [Header("커넥티드 UI 텍스트")]
    public Modular3DText hostIdText;     // "Host id : XXXXXX" 표시
    public Modular3DText player1Text;    // Player1 상태
    public Modular3DText player2Text;    // Player2 상태

    [Header("입력 필드")]
    public InputField codeInputField;    // 방 코드 입력 (Join용)

    [Header("패널")]
    public GameObject connectedPanel;    // __________UI_커넥티드__________

    // 현재 방 코드 (Host만 보유)
    private string currentRoomCode = "";

    private void Awake()
    {
        Instance = this;
    }

    // ─────────────────────────────────────────────
    // 방 만들기 (Make Room 버튼 → MenuController.CreateRoom 에서 호출)
    // ─────────────────────────────────────────────
    public void OnRoomCreated()
    {
        // Host 시작 후 0.5초 대기 → PlayerNetwork 스폰 확인 후 코드 생성
        StartCoroutine(RequestCreateRoomDelayed());
    }

    private IEnumerator RequestCreateRoomDelayed()
    {
        yield return new WaitForSeconds(0.5f);

        PlayerNetwork pn = GetLocalPlayerNetwork();
        if (pn == null)
        {
            Debug.LogWarning("[RoomNetworkManager] PlayerNetwork 없음 - playerPrefab 확인");
            yield break;
        }

        pn.CmdCreateRoom();
        Debug.Log("[RoomNetworkManager] 방 코드 생성 요청");
    }

    // PlayerNetwork.TargetReceiveCode → LobbyManager3D.ShowMyCode에서 여기로도 연결
    public void ShowRoomCode(string code)
    {
        currentRoomCode = code;

        if (hostIdText != null)
            hostIdText.UpdateText("Host id : " + code);

        // 커넥티드 패널 열기
        if (connectedPanel != null)
            connectedPanel.SetActive(true);

        // Player1 (본인) 표시
        if (player1Text != null)
            player1Text.UpdateText("■ Player1");
        if (player2Text != null)
            player2Text.UpdateText("□ Player2");

        Debug.Log($"[RoomNetworkManager] 방 코드: {code}");
    }

    // ─────────────────────────────────────────────
    // 방 참가 (Join 버튼 → MenuController.JoinRoom 에서 호출)
    // ─────────────────────────────────────────────
    public void OnJoinRoom()
    {
        string code = "";
        if (codeInputField != null)
            code = codeInputField.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("[RoomNetworkManager] 방 코드를 입력하세요");
            return;
        }

        // 같은 LAN: 코드 = IP 마지막 부분 or 그냥 localhost (테스트용)
        // 실제 배포시 코드 → IP 매핑 서버 필요
        // 현재는 localhost로 연결 후 코드 검증
        NetworkManager.singleton.networkAddress = "localhost";
        NetworkManager.singleton.StartClient();

        StartCoroutine(JoinRoomAfterConnect(code));
    }

    private IEnumerator JoinRoomAfterConnect(string code)
    {
        float timeout = 5f;
        while (!NetworkClient.isConnected && timeout > 0f)
        {
            yield return new WaitForSeconds(0.1f);
            timeout -= 0.1f;
        }

        if (!NetworkClient.isConnected)
        {
            Debug.LogWarning("[RoomNetworkManager] 연결 실패");
            yield break;
        }

        yield return new WaitForSeconds(0.3f); // PlayerNetwork 스폰 대기

        PlayerNetwork pn = GetLocalPlayerNetwork();
        if (pn != null)
        {
            pn.CmdJoinRoom(code);

            // 커넥티드 패널 열기
            if (connectedPanel != null)
                connectedPanel.SetActive(true);

            if (hostIdText != null)
                hostIdText.UpdateText("Host id : " + code);
            if (player2Text != null)
                player2Text.UpdateText("■ Player2");

            Debug.Log($"[RoomNetworkManager] 코드 [{code}] 방 참가");
        }
        else
        {
            Debug.LogWarning("[RoomNetworkManager] PlayerNetwork 없음 - 참가 실패");
        }
    }

    // ─────────────────────────────────────────────
    // 두 번째 플레이어 접속 감지 → Player2 UI 업데이트 (Host측)
    // GameNetworkManager.OnServerAddPlayer 에서 호출
    // ─────────────────────────────────────────────
    public void OnSecondPlayerConnected()
    {
        if (player2Text != null)
            player2Text.UpdateText("■ Player2");
        Debug.Log("[RoomNetworkManager] Player2 접속 확인");
    }

    // ─────────────────────────────────────────────
    // 방 나가기 UI 리셋 (MenuController.LeaveRoom에서 호출)
    // ─────────────────────────────────────────────
    public void ResetUI()
    {
        currentRoomCode = "";

        if (connectedPanel != null)
            connectedPanel.SetActive(false);

        if (hostIdText != null)
            hostIdText.UpdateText("Host id :");

        if (player1Text != null)
            player1Text.UpdateText("□ Player1");

        if (player2Text != null)
            player2Text.UpdateText("□ Player2");

        Debug.Log("[RoomNetworkManager] UI 리셋");
    }

    // ─────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────
    private PlayerNetwork GetLocalPlayerNetwork()
    {
        if (NetworkClient.localPlayer == null) return null;
        return NetworkClient.localPlayer.GetComponent<PlayerNetwork>();
    }

    private string GetLocalIP()
    {
        try
        {
            string localIP = "127.0.0.1";
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            localIP = ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
            return localIP;
        }
        catch
        {
            return "127.0.0.1";
        }
    }
}
