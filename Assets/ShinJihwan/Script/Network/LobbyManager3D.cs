using System.Net;
using System.Net.Sockets;
using UnityEngine;
using TMPro;
using Mirror;

/// <summary>
/// 3D 오브젝트 기반 로비 UI.
///
/// 씬 세팅:
///   LobbyRoot (이 스크립트 붙이기)
///     ├─ Obj_Create       3D 오브젝트 (CreateRoomButton.cs 붙이기)
///     ├─ Obj_Join         3D 오브젝트 (JoinRoomButton.cs 붙이기)
///     ├─ TextMesh_Code    TextMeshPro(3D) - 입력 중인 방 코드 표시
///     ├─ TextMesh_MyCode  TextMeshPro(3D) - 내 방 코드 표시
///     └─ TextMesh_Status  TextMeshPro(3D) - 상태 메시지 표시
/// </summary>
public class LobbyManager3D : MonoBehaviour
{
    public static LobbyManager3D Instance { get; private set; }

    [Header("3D 텍스트 연결")]
    public TextMeshPro inputCodeText;   // 방 코드 입력 표시
    public TextMeshPro myCodeText;      // 내 방 코드 표시
    public TextMeshPro statusText;      // 상태 메시지

    [Header("입력 모드 안내")]
    public TextMeshPro inputHintText;   // "코드 입력 후 Enter" 안내

    private string typedCode = "";
    private bool isTypingCode = false;  // 방 코드 입력 모드

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetStatus("방을 만들거나 코드를 입력해 참가하세요.");
        UpdateCodeDisplay();
        SetHint("");
    }

    private void Update()
    {
        if (!isTypingCode) return;
        HandleKeyboardInput();
    }

    // ─────────────────────────────────────────────
    // 방 만들기 (CreateRoomButton에서 호출)
    // ─────────────────────────────────────────────
    public void CreateRoom()
    {
        if (NetworkServer.active || NetworkClient.isConnected)
        {
            SetStatus("이미 연결 중입니다.");
            return;
        }

        NetworkManager.singleton.StartHost();
        SetStatus("방 만드는 중...");

        Invoke(nameof(RequestCreateRoom), 0.5f);
    }

    private void RequestCreateRoom()
    {
        if (NetworkClient.localPlayer == null)
        {
            SetStatus("Host 시작 실패");
            return;
        }

        PlayerNetwork pn = NetworkClient.localPlayer.GetComponent<PlayerNetwork>();
        pn?.CmdCreateRoom(GetLocalIP());
    }

    // 방 코드 받으면 표시 (PlayerNetwork.TargetReceiveCode에서 호출)
    public void ShowMyCode(string code)
    {
        if (myCodeText != null)
            myCodeText.text = "방 코드: " + code;
        SetStatus("상대방에게 코드를 알려주세요.");
    }

    // ─────────────────────────────────────────────
    // 방 참가 (JoinRoomButton에서 호출)
    // ─────────────────────────────────────────────
    public void StartJoinMode()
    {
        if (NetworkClient.isConnected)
        {
            SetStatus("이미 연결 중입니다.");
            return;
        }

        isTypingCode = true;
        typedCode = "";
        UpdateCodeDisplay();
        SetStatus("방 코드를 입력하세요.");
        SetHint("숫자 입력 후 Enter로 참가 / ESC로 취소");
    }

    private void HandleKeyboardInput()
    {
        // ESC → 입력 취소
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            isTypingCode = false;
            typedCode = "";
            UpdateCodeDisplay();
            SetStatus("취소됨.");
            SetHint("");
            return;
        }

        // Backspace → 한 글자 삭제
        if (Input.GetKeyDown(KeyCode.Backspace) && typedCode.Length > 0)
        {
            typedCode = typedCode[..^1];
            UpdateCodeDisplay();
            return;
        }

        // Enter → 참가 시도
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (typedCode.Length == 0)
            {
                SetStatus("코드를 입력하세요.");
                return;
            }
            isTypingCode = false;
            SetHint("");
            JoinRoom(typedCode);
            return;
        }

        // 숫자/영문 입력 (방 코드는 최대 6자리)
        foreach (char c in Input.inputString)
        {
            if (typedCode.Length >= 6) break;
            if (char.IsLetterOrDigit(c))
            {
                typedCode += char.ToUpper(c);
                UpdateCodeDisplay();
            }
        }
    }

    private void JoinRoom(string code)
    {
        NetworkManager.singleton.networkAddress = "localhost";
        NetworkManager.singleton.StartClient();
        SetStatus($"[{code}] 접속 중...");

        StartCoroutine(JoinAfterConnect(code));
    }

    private System.Collections.IEnumerator JoinAfterConnect(string code)
    {
        float timeout = 5f;
        while (!NetworkClient.isConnected && timeout > 0f)
        {
            yield return new WaitForSeconds(0.1f);
            timeout -= 0.1f;
        }

        if (!NetworkClient.isConnected)
        {
            SetStatus("연결 실패. Host가 먼저 방을 만들어야 합니다.");
            yield break;
        }

        PlayerNetwork pn = NetworkClient.localPlayer?.GetComponent<PlayerNetwork>();
        if (pn != null)
        {
            pn.CmdJoinRoom(code);
            SetStatus($"[{code}] 참가 완료!");
        }
        else
        {
            SetStatus("참가 실패: 플레이어를 찾을 수 없습니다.");
        }
    }

    // ─────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────
    private void UpdateCodeDisplay()
    {
        if (inputCodeText != null)
            inputCodeText.text = typedCode.Length > 0 ? typedCode : "------";
    }

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
        Debug.Log($"[LobbyManager3D] {msg}");
    }

    private void SetHint(string msg)
    {
        if (inputHintText != null)
            inputHintText.text = msg;
    }

    private string GetLocalIP()
    {
        try
        {
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
        }
        catch { return "127.0.0.1"; }
    }
}
