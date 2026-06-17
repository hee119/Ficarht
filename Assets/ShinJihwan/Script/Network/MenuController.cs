using UnityEngine;
using Mirror;

/// <summary>
/// 메인메뉴 버튼 이벤트 연결용.
/// 씬의 MenuController 오브젝트에 붙이고 Inspector 연결.
///
/// [버튼 연결 방법]
/// - Start 버튼       → Press Complete → MenuController.ShowHostUI
/// - Make Room 버튼   → Press Complete → MenuController.CreateRoom
/// - Join 버튼        → Press Complete → MenuController.JoinRoom
/// </summary>
public class MenuController : MonoBehaviour
{
    [Header("UI 패널")]
    [Tooltip("__________UI_Host__________ 오브젝트 드래그")]
    public GameObject hostUIPanel;

    [Header("M3D Input Field (IP/코드 입력창)")]
    [Tooltip("씬의 Input Field (M3D) 오브젝트 드래그")]
    public TinyGiantStudio.Text.InputField m3dInputField;

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

        // M3D Input Field에서 IP 읽기 (비어있으면 localhost)
        string ip = "localhost";
        if (m3dInputField != null && !string.IsNullOrEmpty(m3dInputField.Text))
            ip = m3dInputField.Text.Trim();

        NetworkManager.singleton.networkAddress = ip;
        NetworkManager.singleton.StartClient();
        Debug.Log($"[MenuController] Client 시작 → {ip}");
    }
}
