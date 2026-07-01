using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mirror;

/// <summary>
/// 로비 UI (방 만들기 / 방 참가).
/// Canvas 오브젝트에 붙이고 Inspector에서 버튼/인풋 연결.
///
/// 씬 세팅:
///  Canvas
///    └─ LobbyPanel (이 스크립트 붙이는 곳)
///         ├─ Btn_Create   (Button)
///         ├─ Btn_Join     (Button)
///         ├─ Input_Code   (TMP_InputField) ← 방 코드 입력
///         └─ Text_Code    (TextMeshProUGUI) ← 내 방 코드 표시
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("버튼")]
    public Button createButton;
    public Button joinButton;

    [Header("방 코드 입력 (참가용)")]
    public TMP_InputField codeInputField;

    [Header("내 방 코드 표시 (생성 후)")]
    public TextMeshProUGUI myCodeText;

    [Header("상태 메시지")]
    public TextMeshProUGUI statusText;

    private void Awake()
    {
        if (createButton != null)
            createButton.onClick.AddListener(OnCreateClicked);

        if (joinButton != null)
            joinButton.onClick.AddListener(OnJoinClicked);

        if (myCodeText != null)
            myCodeText.text = "";

        SetStatus("방을 만들거나 코드를 입력해 참가하세요.");
    }

    // ─────────────────────────────────────────────
    // 방 만들기
    // ─────────────────────────────────────────────
    private void OnCreateClicked()
    {
        if (NetworkServer.active || NetworkClient.isConnected)
        {
            SetStatus("이미 연결 중입니다.");
            return;
        }

        // Host로 시작 (서버 + 클라이언트 동시)
        NetworkManager.singleton.StartHost();
        SetStatus("방을 만들었습니다. 상대방에게 코드를 알려주세요.");

        // 로컬 PlayerNetwork에서 방 코드 생성 요청
        // (Host 시작 직후 플레이어가 스폰되면 자동 호출됨)
        Invoke(nameof(RequestCreateRoom), 0.5f);
    }

    private void RequestCreateRoom()
    {
        PlayerNetwork pn = GetLocalPlayerNetwork();
        if (pn != null)
        {
            pn.CmdCreateRoom(GetLocalIP());
        }
        else
        {
            Debug.LogWarning("[LobbyUI] PlayerNetwork를 찾지 못했습니다.");
            SetStatus("방 코드 생성 실패 - 잠시 후 다시 시도하세요.");
        }
    }

    // ─────────────────────────────────────────────
    // 방 참가
    // ─────────────────────────────────────────────
    private void OnJoinClicked()
    {
        if (NetworkClient.isConnected)
        {
            SetStatus("이미 연결 중입니다.");
            return;
        }

        string code = codeInputField != null ? codeInputField.text.Trim() : "";

        if (string.IsNullOrEmpty(code))
        {
            SetStatus("방 코드를 입력하세요.");
            return;
        }

        // Client로 먼저 연결 (Host의 localhost 주소로)
        NetworkManager.singleton.networkAddress = "localhost";
        NetworkManager.singleton.StartClient();
        SetStatus($"코드 [{code}] 로 참가 시도 중...");

        // 연결 완료 후 방 참가 요청
        StartCoroutine(JoinAfterConnect(code));
    }

    private System.Collections.IEnumerator JoinAfterConnect(string code)
    {
        // 연결 완료까지 최대 5초 대기
        float timeout = 5f;
        while (!NetworkClient.isConnected && timeout > 0f)
        {
            yield return new UnityEngine.WaitForSeconds(0.1f);
            timeout -= 0.1f;
        }

        if (!NetworkClient.isConnected)
        {
            SetStatus("연결 실패. 코드를 확인하거나 Host가 먼저 방을 만들어야 합니다.");
            yield break;
        }

        PlayerNetwork pn = GetLocalPlayerNetwork();
        if (pn != null)
        {
            pn.CmdJoinRoom(code);
            SetStatus($"[{code}] 방 참가 완료!");
        }
        else
        {
            SetStatus("참가 실패: 플레이어를 찾을 수 없습니다.");
        }
    }

    // ─────────────────────────────────────────────
    // 방 코드 표시 (외부에서 호출)
    // ─────────────────────────────────────────────
    public void ShowMyCode(string code)
    {
        if (myCodeText != null)
            myCodeText.text = $"내 방 코드: {code}";
        SetStatus("상대방이 이 코드를 입력하면 접속됩니다.");
    }

    // ─────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────
    private PlayerNetwork GetLocalPlayerNetwork()
    {
        if (NetworkClient.localPlayer == null) return null;
        return NetworkClient.localPlayer.GetComponent<PlayerNetwork>();
    }

    private void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
        Debug.Log($"[LobbyUI] {msg}");
    }

    private string GetLocalIP()
    {
        try
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                foreach (UnicastIPAddressInformation addr in ni.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    string ip = addr.Address.ToString();
                    if (ip.StartsWith("192.168.") || ip.StartsWith("10."))
                        return ip;
                }
            }
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            return ((IPEndPoint)socket.LocalEndPoint).Address.ToString();
        }
        catch { return "127.0.0.1"; }
    }
}
