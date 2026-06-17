using UnityEngine;

/// <summary>
/// 메인메뉴 각 3D 버튼 오브젝트에 붙이는 클릭 처리 스크립트.
///
/// 사용법:
///  - Start 3D 오브젝트에 붙이고 Type = Start 설정
///  - Setting 3D 오브젝트에 붙이고 Type = Setting 설정
///  - Exit 3D 오브젝트에 붙이고 Type = Exit 설정
/// </summary>
public class MainMenuController : MonoBehaviour
{
    public enum ButtonType { Start, Setting, Exit }

    [Header("이 버튼의 종류")]
    public ButtonType buttonType;

    [Header("Start 버튼 전용")]
    [Tooltip("방 만들기/참가 UI가 담긴 오브젝트 (LobbyUIManager 붙은 것)")]
    public GameObject lobbyUIObject;

    [Tooltip("메인메뉴 3D 오브젝트 묶음 (Start/Setting/Exit 포함한 부모)")]
    public GameObject mainMenuRoot;

    private void OnMouseDown()
    {
        switch (buttonType)
        {
            case ButtonType.Start:
                OnStartClicked();
                break;

            case ButtonType.Setting:
                Debug.Log("[MainMenu] Setting (미구현)");
                break;

            case ButtonType.Exit:
                OnExitClicked();
                break;
        }
    }

    private void OnStartClicked()
    {
        // 메인메뉴 3D 오브젝트 숨기기
        if (mainMenuRoot != null)
            mainMenuRoot.SetActive(false);

        // 로비 UI 표시
        if (lobbyUIObject != null)
            lobbyUIObject.SetActive(true);
        else
            Debug.LogWarning("[MainMenu] lobbyUIObject가 연결되지 않았습니다.");
    }

    private void OnExitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
