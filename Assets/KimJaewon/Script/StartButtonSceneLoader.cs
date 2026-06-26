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
        // 맵 카드 UI 표시 (싱글에서는 CardSystemManager가 이미 mapCard 선택해뒀음)
        MapCardDisplayUI.Instance?.ShowMapCard(targetScene);

        // 로딩 화면 표시
        LoadingScreenUI.Instance?.Show();

        // 로딩 화면이 최소 3초 보이도록 대기 (minDisplayDuration과 동기화)
        float wait = LoadingScreenUI.Instance != null
            ? LoadingScreenUI.Instance.minDisplayDuration
            : 3f;
        yield return new WaitForSeconds(wait);

        SceneManager.LoadScene(targetScene);
        // LoadingScreenUI는 SceneManager.sceneLoaded 이벤트에서 자동으로 Hide 처리됨
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
