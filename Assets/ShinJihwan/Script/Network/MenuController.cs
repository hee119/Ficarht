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

    [Tooltip("__________UI_Basic__________ 오브젝트 드래그 (Leave 후 돌아올 초기 패널)")]
    public GameObject basicUIPanel;

    [Header("M3D Input Field (IP/코드 입력창)")]
    [Tooltip("씬의 Input Field (M3D) 오브젝트 드래그")]
    public TinyGiantStudio.Text.InputField m3dInputField;

    [Header("카드 씬 이름")]
    public string cardSceneName = "CardMap";

    private void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log("[MenuController] 씬 로드 완료 - 상태 초기화");
    }

    // M3D InputField가 포커스 된 채 비활성화되면
    // RaycastSelector가 Focus(false) coroutine을 시도해 에러가 발생한다.
    // 패널 전환 전에 명시적으로 포커스를 해제해 이를 방지한다.
    private void SafeUnfocusInputField()
    {
        if (m3dInputField != null && m3dInputField.gameObject.activeInHierarchy)
            m3dInputField.Focus(false);
    }

    // 로비 UI를 초기 상태(Basic 패널)로 되돌린다.
    // Inspector 연결이 없어도 이름으로 자동 탐색.
    // ※ GameObject.Find는 비활성 오브젝트를 못 찾으므로 FindIncludingInactive 사용.
    private void ResetToBasicUI()
    {
        // Host 패널 숨기기
        GameObject host = hostUIPanel != null
            ? hostUIPanel
            : FindIncludingInactive("__________UI_Host__________");
        if (host != null) host.SetActive(false);

        // Basic 패널 보이기
        GameObject basic = basicUIPanel != null
            ? basicUIPanel
            : FindIncludingInactive("__________UI_Basic__________");
        if (basic != null)
        {
            basic.SetActive(true);
            basicUIPanel = basic; // 다음 호출을 위해 캐시
        }
        else
            Debug.LogWarning("[MenuController] UI_Basic 패널을 찾을 수 없습니다. Inspector에서 basicUIPanel을 연결하세요.");
    }

    // 비활성 오브젝트를 포함해 씬에서 이름으로 GameObject를 찾는다.
    private static GameObject FindIncludingInactive(string name)
    {
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            if (go.name == name && go.scene.isLoaded)
                return go;
        return null;
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

        // 패널 전환 전 InputField 포커스 해제 (M3D coroutine 에러 방지)
        SafeUnfocusInputField();

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

        SafeUnfocusInputField();

        // 코드 미입력 검증
        string code = "";
        if (m3dInputField != null)
            code = m3dInputField.Text?.Trim().ToUpper() ?? "";

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
        SafeUnfocusInputField();

        if (!NetworkServer.active && !NetworkClient.isConnected)
        {
            Debug.Log("[MenuController] 연결 상태가 아님 → UI만 리셋");
            RoomNetworkManager.Instance?.ResetUI();
            ResetToBasicUI();
            return;
        }

        Debug.Log("[MenuController] 방 나가기");

        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();
        else if (NetworkClient.isConnected)
            NetworkManager.singleton.StopClient();

        RoomNetworkManager.Instance?.ResetUI();
        ResetToBasicUI();
        // ⚠️ SceneManager.LoadScene() 제거:
        //    씬 리로드 시 복제 NetworkManager가 생성되고 즉시 파괴되는데,
        //    씬 안의 버튼들이 파괴된 MenuController를 레퍼런스해
        //    다음 CreateRoom() 호출이 무시되는 버그가 발생한다.
    }
}
