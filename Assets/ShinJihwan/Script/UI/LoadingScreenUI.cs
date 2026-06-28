using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 전투 맵 씬 도착 직후 검은 로딩 화면을 표시한다.
/// DontDestroyOnLoad → 씬 이동 후에도 유지.
///
/// 트리거: SceneManager.sceneLoaded → excludeScenes에 없는 씬이면 자동 Show
/// 숨김:   minDisplayDuration 경과 후 자동 Hide
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class LoadingScreenUI : MonoBehaviour
{
    public static LoadingScreenUI Instance { get; private set; }

    [Header("로딩 화면을 띄우지 않을 씬 이름 목록")]
    public string[] excludeScenes = { "CardMap", "CardMap_MainDesplay", "MainMenu", "Lobby" };

    [Header("점 하나 줄어드는 간격 (초)")]
    public float dotInterval = 0.4f;

    [Header("최소 표시 시간 (초)")]
    public float minDisplayDuration = 3f;

    private VisualElement _root;
    private Label         _loadingLabel;
    private Coroutine     _dotCoroutine;
    private Coroutine     _hideCoroutine;
    private float         _showTime = -1f;

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        var doc = GetComponent<UIDocument>();
        _root         = doc.rootVisualElement.Q<VisualElement>("root");
        _loadingLabel = doc.rootVisualElement.Q<Label>("loading-label");

        SceneManager.sceneLoaded += OnSceneLoaded;
        HideImmediate();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // -------------------------------------------------------
    // 씬 로드 완료 시 자동 호출
    // -------------------------------------------------------

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 제외 씬이면 무시
        foreach (string ex in excludeScenes)
            if (scene.name == ex) return;

        // 전투 맵 씬 도착 → 즉시 표시
        Show();
    }

    // -------------------------------------------------------

    public void Show()
    {
        if (_hideCoroutine != null) { StopCoroutine(_hideCoroutine); _hideCoroutine = null; }
        _showTime = Time.realtimeSinceStartup;

        if (_root != null)
            _root.style.display = DisplayStyle.Flex;

        if (_dotCoroutine != null) StopCoroutine(_dotCoroutine);
        _dotCoroutine = StartCoroutine(AnimateDots());

        // minDisplayDuration 후 자동 숨김
        _hideCoroutine = StartCoroutine(HideAfterDelay(minDisplayDuration));
    }

    public void Hide()
    {
        if (_showTime < 0f) { HideImmediate(); return; }

        float remaining = minDisplayDuration - (Time.realtimeSinceStartup - _showTime);
        if (remaining <= 0f)
            HideImmediate();
        else
        {
            if (_hideCoroutine != null) StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideAfterDelay(remaining));
        }
    }

    // -------------------------------------------------------

    private void HideImmediate()
    {
        if (_root != null) _root.style.display = DisplayStyle.None;
        if (_dotCoroutine != null) { StopCoroutine(_dotCoroutine); _dotCoroutine = null; }
        _showTime = -1f;
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        HideImmediate();
        _hideCoroutine = null;
    }

    private IEnumerator AnimateDots()
    {
        int dots = 3;
        while (true)
        {
            if (_loadingLabel != null)
                _loadingLabel.text = "Loading" + new string('.', dots);
            yield return new WaitForSecondsRealtime(dotInterval);
            dots--;
            if (dots < 0) dots = 3;
        }
    }
}
