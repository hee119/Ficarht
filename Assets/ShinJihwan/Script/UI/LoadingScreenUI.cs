using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using System.Collections;

[RequireComponent(typeof(UIDocument))]
public class LoadingScreenUI : MonoBehaviour
{
    public static LoadingScreenUI Instance { get; private set; }

    [Header("점 하나 줄어드는 간격 (초)")]
    public float dotInterval = 0.4f;

    [Header("최소 로딩 화면 표시 시간 (초)")]
    public float minDisplayDuration = 3f;

    private VisualElement _root;
    private Label _loadingLabel;
    private Coroutine _dotCoroutine;
    private Coroutine _hideCoroutine;
    private float _showTime = -1f;

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        var doc = GetComponent<UIDocument>();
        _root         = doc.rootVisualElement.Q<VisualElement>("root");
        _loadingLabel = doc.rootVisualElement.Q<Label>("loading-label");

        // Mirror 콜백이 불안정한 경우 SceneManager 이벤트를 백업으로 사용
        SceneManager.sceneLoaded += OnSceneLoaded;

        HideImmediate();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // 씬 로드 완료 시 SceneManager가 호출 (Mirror 콜백 대신 사용)
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 로딩 화면이 켜져 있을 때만 처리
        if (_showTime < 0f) return;
        Hide();
    }

    // -------------------------------------------------------

    public void Show()
    {
        if (_hideCoroutine != null)
        {
            StopCoroutine(_hideCoroutine);
            _hideCoroutine = null;
        }

        _showTime = Time.realtimeSinceStartup;

        if (_root != null)
            _root.style.display = DisplayStyle.Flex;

        if (_dotCoroutine != null)
            StopCoroutine(_dotCoroutine);
        _dotCoroutine = StartCoroutine(AnimateDots());
    }

    // 씬 로드 완료 후 호출 — 최소 시간 미달 시 남은 시간만큼 대기
    public void Hide()
    {
        if (_showTime < 0f)
        {
            HideImmediate();
            return;
        }

        float elapsed   = Time.realtimeSinceStartup - _showTime;
        float remaining = minDisplayDuration - elapsed;

        if (remaining <= 0f)
        {
            HideImmediate();
        }
        else
        {
            if (_hideCoroutine != null)
                StopCoroutine(_hideCoroutine);
            _hideCoroutine = StartCoroutine(HideAfterDelay(remaining));
        }
    }

    // -------------------------------------------------------

    private void HideImmediate()
    {
        if (_root != null)
            _root.style.display = DisplayStyle.None;

        if (_dotCoroutine != null)
        {
            StopCoroutine(_dotCoroutine);
            _dotCoroutine = null;
        }

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
