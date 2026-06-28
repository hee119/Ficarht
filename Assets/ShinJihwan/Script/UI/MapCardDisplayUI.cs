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

    [Header("카드 뒷면 스프라이트")]
    public Sprite cardBackSprite;

    private VisualElement _overlay;
    private VisualElement _card;

    // -------------------------------------------------------

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var doc  = GetComponent<UIDocument>();
        var root = doc.rootVisualElement;
        _overlay = root.Q<VisualElement>("overlay");
        _card    = root.Q<VisualElement>("card");

        Debug.Log($"[MapCardDisplayUI] Awake — overlay={_overlay != null}, card={_card != null}");
        HideImmediate();
    }

    // -------------------------------------------------------
    // NetworkCardBridge.RpcShowMapCard() 및 StartButtonSceneLoader에서 호출
    // -------------------------------------------------------

    public void ShowMapCard(string mapSceneName)
    {
        Debug.Log($"[MapCardDisplayUI] ShowMapCard({mapSceneName}) — overlay={_overlay != null}");

        // 이미지 취득 순서
        // 1순위: 이미 선택된 카드 데이터 (Host / 단독 플레이어)
        Sprite image = CardSystemManager.Instance?.GetSelectedMapCardImage();

        // 2순위: 씬 이름으로 mapDeck 검색 (Client — _selectedMapCardData 없음)
        if (image == null && CardSystemManager.Instance != null)
        {
            foreach (var c in CardSystemManager.Instance.mapDeck)
            {
                if (c != null && c.mapSceneName == mapSceneName)
                {
                    image = c.cardImage;
                    break;
                }
            }
        }

        Debug.Log($"[MapCardDisplayUI] 이미지={image != null}");

        if (_overlay != null)
            _overlay.style.display = DisplayStyle.Flex;

        StopAllCoroutines();
        StartCoroutine(FlipAnimation(image));
        StartCoroutine(HideAfterDelay(displayDuration));
    }

    // -------------------------------------------------------

    private void HideImmediate()
    {
        if (_overlay != null)
            _overlay.style.display = DisplayStyle.None;

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

        // 뒷면: 카드 뒷면 스프라이트 (없으면 어두운 배경)
        if (cardBackSprite != null)
        {
            _card.style.backgroundImage = new StyleBackground(cardBackSprite);
            _card.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
        }
        else
        {
            _card.style.backgroundImage = StyleKeyword.None;
            _card.style.backgroundColor = new StyleColor(new Color(0.07f, 0.05f, 0.15f));
        }
        _card.transform.scale = Vector3.one;

        // Phase 1 — X 1→0
        yield return AnimateScaleX(1f, 0f, flipDuration);

        // 플립 중간: 앞면 이미지 주입
        if (cardSprite != null)
        {
            _card.style.backgroundImage = new StyleBackground(cardSprite);
            _card.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f)); // 투명
        }

        // Phase 2 — X 0→1
        yield return AnimateScaleX(0f, 1f, flipDuration);
        _card.transform.scale = Vector3.one;
    }

    private IEnumerator AnimateScaleX(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t  = Mathf.Clamp01(elapsed / duration);
            _card.transform.scale = new Vector3(Mathf.Lerp(from, to, t), 1f, 1f);
            yield return null;
        }
    }
}
