using UnityEngine;
using UnityEngine.SceneManagement;
using MTextButton = TinyGiantStudio.Text.Button;

public class StartButtonSceneLoader : MonoBehaviour
{
    [Header("이동할 씬 이름")]
    public string sceneName = "Forest";

    [Header("버튼 활성화 표시")]
    public bool updateInteractableByCardSelection = true;

    [Header("Collect 버튼 이름")]
    public string collectButtonName = "Collect_Button";

    private MTextButton startButton;

    private bool lastReadyState;

    private void Awake()
    {
        startButton = GetComponent<MTextButton>();

        lastReadyState = !ShouldButtonBeInteractable();

        UpdateButtonState();
    }

    private void Update()
    {
        UpdateButtonState();
    }

    public void LoadSelectedScene()
    {
        if (IsCollectButton())
        {
            CardSystemManager.Instance
                ?.CollectPlacedCards();

            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[StartButtonSceneLoader] 이동할 씬 이름이 비어 있습니다.");
            return;
        }

        if (
            CardSystemManager.Instance != null &&
            !CardSystemManager.Instance.CanMoveToBattleScene()
        )
        {
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    private void UpdateButtonState()
    {
        if (!updateInteractableByCardSelection)
            return;

        bool isReady = ShouldButtonBeInteractable();

        if (isReady == lastReadyState)
            return;

        lastReadyState = isReady;

        if (startButton != null)
        {
            if (isReady)
            {
                startButton.Interactable();
            }
            else
            {
                startButton.Uninteractable();
            }
        }
    }

    private bool ShouldButtonBeInteractable()
    {
        if (IsCollectButton())
            return true;

        return
            CardSystemManager.Instance != null &&
            CardSystemManager.Instance.IsSelectionComplete();
    }

    private bool IsCollectButton()
    {
        return gameObject.name == collectButtonName;
    }
}
