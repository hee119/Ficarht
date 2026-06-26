using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using Mirror;
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

    // -------------------------------------------------------

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

    // -------------------------------------------------------

    public void LoadSelectedScene()
    {
        if (IsCollectButton())
        {
            CardSystemManager.Instance?.CollectPlacedCards();
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("[StartButtonSceneLoader] 이동할 씬 이름이 비어 있습니다.");
            return;
        }

        if (CardSystemManager.Instance != null && !CardSystemManager.Instance.CanMoveToBattleScene())
            return;

        // ── 멀티플레이 ──────────────────────────────────────────
        // 카드 선택 결과를 서버로 제출 → GameNetworkManager가 씬 이동 + RPC로 UI 처리
        if (NetworkClient.isConnected && NetworkCardBridge.LocalInstance != null)
        {
            NetworkCardBridge.LocalInstance.SubmitCardSelection();
            return;
        }

        // ── 싱글(로컬 테스트) ────────────────────────────────────
        // 맵 카드 UI + 로딩 화면을 직접 띄운 뒤 3초 후 씬 이동
        string targetScene = CardSystemManager.Instance?.GetSelectedMapScene();
        if (string.IsNullOrEmpty(targetScene))
            targetScene = sceneName;

        StartCoroutine(SinglePlayerSceneLoad(targetScene));
    }

    private IEnumerator SinglePlayerSceneLoad(string targetScene)
    {
        // 맵 카드 UI 표시 (뒤집기 연출 포함)
        MapCardDisplayUI.Instance?.ShowMapCard(targetScene);

        // 카드 연출이 끝날 때까지 대기
        float wait = MapCardDisplayUI.Instance != null
            ? MapCardDisplayUI.Instance.displayDuration
            : 3f;
        yield return new WaitForSecondsRealtime(wait);

        // 씬 이동 → LoadingScreenUI가 SceneManager.sceneLoaded에서 자동으로 Show
        SceneManager.LoadScene(targetScene);
    }

    // -------------------------------------------------------

    private void UpdateButtonState()
    {
        if (!updateInteractableByCardSelection) return;

        bool isReady = ShouldButtonBeInteractable();
        if (isReady == lastReadyState) return;

        lastReadyState = isReady;

        if (startButton != null)
        {
            if (isReady) startButton.Interactable();
            else         startButton.Uninteractable();
        }
    }

    private bool ShouldButtonBeInteractable()
    {
        if (IsCollectButton()) return true;
        return CardSystemManager.Instance != null && CardSystemManager.Instance.IsSelectionComplete();
    }

    private bool IsCollectButton()
    {
        return gameObject.name == collectButtonName;
    }
}
