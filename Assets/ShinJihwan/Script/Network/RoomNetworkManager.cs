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

    private void Start()
    {
        // 시작 시 커넥티드 패널 숨기기
        if (connectedPanel != null)
            connectedPanel.SetActive(false);
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
        StartCoroutine(ShowRoomCodeDelayed(code));
    }

    private IEnumerator ShowRoomCodeDelayed(string code)
    {
        // 커넥티드 패널 열기
        if (connectedPanel != null)
            connectedPanel.SetActive(true);

        // M3D가 패널 활성화 후 렌더링 준비하도록 1프레임 대기
        yield return null;

        if (hostIdText != null)
            hostIdText.UpdateText("Host id : " + code);
        if (player1Text != null)
            player1Text.UpdateText("■ Player1");
        if (player2Text != null)
            player2Text.UpdateText("□ Player2");

        Debug.Log($"[RoomNetworkManager] 방 코드: {code}");
    }

    // ─────────────────────────────────────────────
    // 방 참가 (Join 버튼 → MenuController.JoinRoom 에서 호출)
    // ─────────────────────────────────────────────
    // 조인 진행 중 여부 (OnClientDisconnect에서 구분용)
    private bool _isJoining = false;

    public void OnJoinRoom()
    {
        string code = "";
        if (codeInputField != null)
            code = codeInputField.Text?.Trim() ?? "";

        if (string.IsNullOrEmpty(code))
        {
            Debug.LogWarning("[RoomNetworkManager] 방 코드를 입력하세요");
            OnJoinFailed();
            return;
        }

        _isJoining = true;
        NetworkManager.singleton.networkAddress = "localhost";
        NetworkManager.singleton.StartClient();

        StartCoroutine(JoinRoomAfterConnect(code));
    }

    /// <summary>GameNetworkManager.OnClientDisconnect에서 호출 — 조인 중이었으면 UI 복구.</summary>
    public void OnDisconnected()
    {
        if (_isJoining)
        {
            Debug.Log("[RoomNetworkManager] 조인 중 연결 끊김 → UI 복구");
            OnJoinFailed();
        }
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
            Debug.LogWarning("[RoomNetworkManager] 연결 실패 (서버 없음 또는 잘못된 코드)");
            NetworkManager.singleton.StopClient();
            OnJoinFailed(); // 커넥티드 패널 숨기고 UI_Host 복구
            yield break;
        }

        yield return new WaitForSeconds(0.3f); // PlayerNetwork 스폰 대기

        PlayerNetwork pn = GetLocalPlayerNetwork();
        if (pn != null)
        {
            pn.CmdJoinRoom(code);
            // UI 표시는 서버 검증 후 TargetJoinSuccess/TargetJoinFailed RPC에서 처리
            Debug.Log($"[RoomNetworkManager] 코드 [{code}] 방 참가 요청");
        }
        else
        {
            // PlayerNetwork 스폰 실패 = 연결은 됐지만 코드 검증 불가 → 강제 차단
            Debug.LogWarning("[RoomNetworkManager] PlayerNetwork 없음 - 연결 차단");
            NetworkManager.singleton.StopClient();
            OnJoinFailed();
        }
    }

    // ─────────────────────────────────────────────
    // 방 참가 성공 (PlayerNetwork.TargetJoinSuccess에서 호출)
    // ─────────────────────────────────────────────
    public void OnJoinSuccess(string code)
    {
        _isJoining = false;
        StartCoroutine(ShowClientJoinedUI(code));
    }

    private IEnumerator ShowClientJoinedUI(string code)
    {
        if (connectedPanel != null)
            connectedPanel.SetActive(true);

        yield return null; // M3D 렌더링 대기

        if (hostIdText != null)
            hostIdText.UpdateText("Host id : " + code);
        if (player1Text != null)
            player1Text.UpdateText("■ Player1");
        if (player2Text != null)
            player2Text.UpdateText("■ Player2");
    }

    // ─────────────────────────────────────────────
    // 방 참가 실패 (PlayerNetwork.TargetJoinFailed에서 호출)
    // ─────────────────────────────────────────────
    public void OnJoinFailed()
    {
        _isJoining = false;
        Debug.LogWarning("[RoomNetworkManager] 방 코드가 올바르지 않습니다.");

        // 커넥티드 패널 숨기기
        if (connectedPanel != null)
            connectedPanel.SetActive(false);

        // Next_UI()가 UI_Host를 비활성화했으므로 inactive 포함 탐색으로 복구
        GameObject hostUI = FindInactiveByName("__________UI_Host__________");
        if (hostUI != null)
            hostUI.SetActive(true);
    }

    // 비활성 오브젝트를 포함해 씬 전체에서 이름으로 탐색
    private static GameObject FindInactiveByName(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == name && go.scene.isLoaded)
                return go;
        return null;
    }

    // ─────────────────────────────────────────────
    // 두 번째 플레이어 접속 감지 → Player2 UI 업데이트 (Host측)
    // GameNetworkManager.OnServerAddPlayer 에서 호출
    // ─────────────────────────────────────────────
    public void OnSecondPlayerConnected()
    {
        StartCoroutine(OnSecondPlayerConnectedDelayed());
    }

    private IEnumerator OnSecondPlayerConnectedDelayed()
    {
        // 패널이 닫혀있으면 열기
        if (connectedPanel != null && !connectedPanel.activeSelf)
            connectedPanel.SetActive(true);

        yield return null; // M3D 렌더링 대기

        if (player1Text != null)
            player1Text.UpdateText("■ Player1");
        if (player2Text != null)
            player2Text.UpdateText("■ Player2");

        Debug.Log("[RoomNetworkManager] Player2 접속 UI 갱신");
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
