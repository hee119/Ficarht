using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SandstormUI : MonoBehaviour
{
    [Header("연결")]
    public DesertMapManager desertManager;

    [Header("오버레이 색상")]
    public Color stormColor = new Color(0.78f, 0.55f, 0.15f, 0.38f);

    [Tooltip("페이드 속도")]
    public float fadeSpeed = 2f;

    private VisualElement overlay;
    private VisualElement warningPanel;
    private VisualElement stormStatus;

    private Coroutine blinkCoroutine;
    private float currentAlpha = 0f;

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        overlay      = root.Q<VisualElement>("sandstorm-overlay");
        warningPanel = root.Q<VisualElement>("warning-panel");
        stormStatus  = root.Q<VisualElement>("storm-status");

        SetWarning(false);
        SetStormStatus(false);
        SetOverlayAlpha(0f);

        if (desertManager != null)
        {
            desertManager.OnWarningStart += HandleWarningStart;
            desertManager.OnWarningEnd   += HandleWarningEnd;
            desertManager.OnStormStart   += HandleStormStart;
            desertManager.OnStormEnd     += HandleStormEnd;
        }
    }

    private void OnDisable()
    {
        if (desertManager != null)
        {
            desertManager.OnWarningStart -= HandleWarningStart;
            desertManager.OnWarningEnd   -= HandleWarningEnd;
            desertManager.OnStormStart   -= HandleStormStart;
            desertManager.OnStormEnd     -= HandleStormEnd;
        }
    }

    private void HandleWarningStart() => SetWarning(true);
    private void HandleWarningEnd()   => SetWarning(false);
    private void HandleStormStart()   => SetStormStatus(true);
    private void HandleStormEnd()     => SetStormStatus(false);

    private void Update()
    {
        if (desertManager == null) return;

        float targetAlpha = desertManager.IsStorming ? stormColor.a : 0f;
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
        SetOverlayAlpha(currentAlpha);
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (overlay == null) return;
        overlay.style.backgroundColor = new StyleColor(
            new Color(stormColor.r, stormColor.g, stormColor.b, alpha)
        );
    }

    private void SetWarning(bool visible)
    {
        if (warningPanel == null) return;
        warningPanel.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void SetStormStatus(bool visible)
    {
        if (stormStatus == null) return;
        stormStatus.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}