using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

/// <summary>
/// UI Toolkit 기반 맵 카드 표시 UI.
/// Host가 뽑은 맵 카드를 양쪽 플레이어 화면에 오버레이로 표시한다.
///
/// 세팅 방법:
///   1. 씬에 빈 GameObject 생성 → UIDocument 컴포넌트 추가
///   2. UIDocument.Panel Settings 연결 (Project > Create > UI Toolkit > Panel Settings)
///   3. UIDocument.Source Asset = MapCardDisplay.uxml
///   4. 같은 오브젝트에 MapCardDisplayUI.cs 추가
/// </summary>
[RequireComponent(typeof(UIDocument))]
public class MapCardDisplayUI : MonoBehaviour
{
    public static MapCardDisplayUI Instance { get; private set; }

    [Header("표시 지속 시간 (초) — LoadBattleScene 3초보다 짧게")]
    public float displayDuration = 2.5f;

    // UI Toolkit 요소
    private VisualElement _overlay;
    private VisualElement _cardImage;
    private Label _mapNameLabel;
    private Label _sceneNameLabel;

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var doc = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;

        // UXML 요소 쿼리
        _overlay       = root.Q<VisualElement>("overlay");
        _cardImage     = root.Q<VisualElement>("card-image");
        _mapNameLabel  = root.Q<Label>("map-name-label");
        _sceneNameLabel = root.Q<Label>("scene-name-label");

        // 시작 시 숨김
        Hide();
    }

    // -------------------------------------------------------

    /// <summary>
    /// NetworkCardBridge.RpcShowMapCard()에서 호출.
    /// mapSceneName으로 CardSystemManager.mapDeck에서 CardData를 찾아 표시.
    /// </summary>
    public void ShowMapCard(string mapSceneName)
    {
        // CardSystemManager에서 선택된 맵 카드 데이터 직접 가져오기
        var cardName  = CardSystemManager.Instance?.GetSelectedMapCardName();
        var cardImage = CardSystemManager.Instance?.GetSelectedMapCardImage();

        // 카드 이미지
        if (_cardImage != null)
        {
            if (cardImage != null)
                _cardImage.style.backgroundImage = new StyleBackground(cardImage);
            else
                _cardImage.style.backgroundImage = StyleKeyword.None;
        }

        // 맵 이름
        if (_mapNameLabel != null)
            _mapNameLabel.text = !string.IsNullOrEmpty(cardName) ? cardName : mapSceneName;

        // 씬 이름
        if (_sceneNameLabel != null)
            _sceneNameLabel.text = $"이동 씬 : {mapSceneName}";

        // 오버레이 표시
        if (_overlay != null)
            _overlay.style.display = DisplayStyle.Flex;

        Debug.Log($"[MapCardDisplayUI] 맵 표시: {mapSceneName}");
        StartCoroutine(HideAfterDelay(displayDuration));
    }

    // -------------------------------------------------------

    private void Hide()
    {
        if (_overlay != null)
            _overlay.style.display = DisplayStyle.None;
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        Hide();
    }
}
