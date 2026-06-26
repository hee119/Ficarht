using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

[RequireComponent(typeof(UIDocument))]
public class MapCardDisplayUI : MonoBehaviour
{
    public static MapCardDisplayUI Instance { get; private set; }

    [Header("전체 표시 시간 (초) — 플립 포함")]
    public float displayDuration = 2.8f;

    [Header("플립 한 방향 소요 시간 (초)")]
    public float flipDuration = 0.35f;

    private VisualElement _overlay;
    private VisualElement _card;

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var root = GetComponent<UIDocument>().rootVisualElement;
        _overlay = root.Q<VisualElement>("overlay");
        _card    = root.Q<VisualElement>("card");

        HideImmediate();
    }

    // -------------------------------------------------------
    // NetworkCardBridge.RpcShowMapCard() 및 StartButtonSceneLoader에서 호출
    // -------------------------------------------------------

    public void ShowMapCard(string mapSceneName)
    {
        // mapDeck에서 씬 이름으로 카드 검색 (Host·Client 모두 mapDeck 데이터 있음)
        CardData found = null;
        if (CardSystemManager.Instance != null)
        {
            foreach (var c in CardSystemManager.Instance.mapDeck)
            {
                if (c != null && c.mapSceneName == mapSceneName)
                {
                    found = c;
                    break;
                }
            }
        }

        if (_overlay != null)
            _overlay.style.display = DisplayStyle.Flex;

        StopAllCoroutines();
        StartCoroutine(FlipAnimation(found?.cardImage));
        StartCoroutine(HideAfterDelay(displayDuration));
    }

    // -------------------------------------------------------

    private void HideImmediate()
    {
        if (_overlay != null)
            _overlay.style.display = DisplayStyle.None;

        // 카드 스케일 초기화
        if (_card != null)
            _card.transform.scale = Vector3.one;
    }

    private IEnumerator HideAfterDelay(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        HideImmediate();
    }

    // -------------------------------------------------------
    // 카드 플립: 뒷면(어두운 배경) → X스케일 1→0 → 이미지 주입 → X스케일 0→1
    // -------------------------------------------------------

    private IEnumerator FlipAnimation(Sprite cardSprite)
    {
        if (_card == null) yield break;

        // 초기: 뒷면 배경, 이미지 없음
        _card.style.backgroundImage = StyleKeyword.None;
        _card.style.backgroundColor = new StyleColor(new Color(0.07f, 0.05f, 0.15f));
        _card.transform.scale = Vector3.one;

        // Phase 1 — X 1→0 (카드 뒤집히며 사라짐)
        yield return AnimateScaleX(1f, 0f, flipDuration);

        // 플립 중간 — 카드 이미지 적용
        if (cardSprite != null)
        {
            _card.style.backgroundImage  = new StyleBackground(cardSprite);
            _card.style.backgroundColor  = StyleKeyword.Null; // 이미지가 있으면 배경색 제거
        }

        // Phase 2 — X 0→1 (카드 앞면 드러남)
        yield return AnimateScaleX(0f, 1f, flipDuration);
        _card.transform.scale = Vector3.one;
    }

    private IEnumerator AnimateScaleX(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float sx = Mathf.Lerp(from, to, t);
            _card.transform.scale = new Vector3(sx, 1f, 1f);
            yield return null;
        }
    }
}
