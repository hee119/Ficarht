using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class LavaUI : MonoBehaviour
{
    [Header("연결")]
    public LavaMapManager lavaManager;

    [Header("오버레이 색상")]
    public Color lavaColor = new Color(0.9f, 0.2f, 0.0f, 0.3f);

    [Tooltip("페이드 속도")]
    public float fadeSpeed = 3f;

    private VisualElement overlay;
    private VisualElement warningPanel;

    private float currentAlpha = 0f;
    private bool isInLava = false;

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        overlay      = root.Q<VisualElement>("lava-overlay");
        warningPanel = root.Q<VisualElement>("lava-warning-panel");

        SetWarning(false);
        SetOverlayAlpha(0f);

        if (lavaManager != null)
        {
            lavaManager.OnLavaEnter += HandleLavaEnter;
            lavaManager.OnLavaExit  += HandleLavaExit;
        }
    }

    private void OnDisable()
    {
        if (lavaManager != null)
        {
            lavaManager.OnLavaEnter -= HandleLavaEnter;
            lavaManager.OnLavaExit  -= HandleLavaExit;
        }
    }

    private void HandleLavaEnter()
    {
        isInLava = true;
        SetWarning(true);
    }

    private void HandleLavaExit()
    {
        isInLava = false;
        SetWarning(false);
    }

    private void Update()
    {
        float targetAlpha = isInLava ? lavaColor.a : 0f;
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
        SetOverlayAlpha(currentAlpha);
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (overlay == null) return;
        overlay.style.backgroundColor = new StyleColor(
            new Color(lavaColor.r, lavaColor.g, lavaColor.b, alpha)
        );
    }

    private void SetWarning(bool visible)
    {
        if (warningPanel == null) return;
        warningPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}