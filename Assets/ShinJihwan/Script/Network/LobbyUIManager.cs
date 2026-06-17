using UnityEngine;
using UnityEngine.UIElements;

public class LobbyUIManager : MonoBehaviour
{
    [SerializeField] private NetworkLobbyController lobbyController;
    
    private UIDocument uiDocument;
    private Button createButton;
    private Button joinButton;
    private TextField ipTextField;

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;

        // UXML에서 엘리먼트 가져오기 (이름은 본인의 UXML에 맞게 수정)
        createButton = root.Q<Button>("btn-create");
        joinButton = root.Q<Button>("btn-join");
        ipTextField = root.Q<TextField>("input-ip");

        // 버튼 이벤트 리스너 등록
        if (createButton != null)
            createButton.clicked += OnCreateButtonClicked;

        if (joinButton != null)
            joinButton.clicked += OnJoinButtonClicked;
    }

    private void OnDisable()
    {
        if (createButton != null) createButton.clicked -= OnCreateButtonClicked;
        if (joinButton != null) joinButton.clicked -= OnJoinButtonClicked;
    }

    private void OnCreateButtonClicked()
    {
        lobbyController.CreateRoom();
    }

    private void OnJoinButtonClicked()
    {
        // 입력창에 적힌 IP 주소를 가져와서 참여
        string ip = ipTextField != null ? ipTextField.value : "localhost";
        lobbyController.JoinRoom(ip);
    }
}