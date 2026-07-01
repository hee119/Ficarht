using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(UIDocument))]
public class GameResultUIController : MonoBehaviour
{
    [Header("씬 이동")]
    public string mainMenuSceneName = "CardMap_MainDesplay";

    private VisualElement quitConfirmOverlay;

    private void Awake()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        Button mainMenuButton = root.Q<Button>("main-menu-button");
        Button quitButton = root.Q<Button>("quit-button");
        Button quitConfirmYesButton = root.Q<Button>("quit-confirm-yes-button");
        Button quitConfirmNoButton = root.Q<Button>("quit-confirm-no-button");
        quitConfirmOverlay = root.Q<VisualElement>("quit-confirm-overlay");

        if (mainMenuButton != null)
            mainMenuButton.clicked += GoToMainMenu;

        if (quitButton != null)
            quitButton.clicked += ShowQuitConfirm;

        if (quitConfirmYesButton != null)
            quitConfirmYesButton.clicked += QuitGame;

        if (quitConfirmNoButton != null)
            quitConfirmNoButton.clicked += HideQuitConfirm;

        HideQuitConfirm();
    }

    private void GoToMainMenu()
    {
        StopNetworkIfNeeded();
        SceneManager.LoadScene(mainMenuSceneName);
    }

    private void ShowQuitConfirm()
    {
        if (quitConfirmOverlay != null)
            quitConfirmOverlay.style.display = DisplayStyle.Flex;
    }

    private void HideQuitConfirm()
    {
        if (quitConfirmOverlay != null)
            quitConfirmOverlay.style.display = DisplayStyle.None;
    }

    private void QuitGame()
    {
        StopNetworkIfNeeded();

#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void StopNetworkIfNeeded()
    {
        if (NetworkManager.singleton == null)
            return;

        if (NetworkServer.active && NetworkClient.isConnected)
            NetworkManager.singleton.StopHost();
        else if (NetworkClient.isConnected)
            NetworkManager.singleton.StopClient();
        else if (NetworkServer.active)
            NetworkManager.singleton.StopServer();
    }
}
