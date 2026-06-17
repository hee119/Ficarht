using UnityEngine;
using Mirror;

/// <summary>
/// 메인메뉴 버튼 이벤트 연결용 스크립트.
/// 빈 오브젝트에 붙이고 Inspector에서 패널 연결 후
/// M3D 버튼의 onClick 이벤트에 각 메서드를 등록한다.
/// </summary>
public class MenuController : MonoBehaviour
{
    [Header("패널")]
    [Tooltip("__________UI_Host__________ 오브젝트 드래그")]
    public GameObject hostUIPanel;

    [Header("NetworkLobbyController 연결")]
    public NetworkLobbyController lobbyController;

    // ─── Start 버튼 onClick에 연결 ───
    public void ShowHostUI()
    {
        if (hostUIPanel != null)
            hostUIPanel.SetActive(true);
    }

    // ─── 방 만들기 버튼 onClick에 연결 ───
    public void CreateRoom()
    {
        lobbyController?.CreateRoom();
    }

    // ─── 방 참가 버튼 onClick에 연결 ───
    // Input Field (M3D)의 텍스트를 읽어서 IP로 참가
    public void JoinRoom()
    {
        // M3D Input Field에서 텍스트 읽기
        TinyGiantStudio.Text.InputField inputField =
            FindObjectOfType<TinyGiantStudio.Text.InputField>();

        string ip = inputField != null ? inputField.Text : "localhost";
        lobbyController?.JoinRoom(ip);
    }
}
