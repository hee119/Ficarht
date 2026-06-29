using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SnowUI : MonoBehaviour
{
    [Header("연결")]
    public SnowMapManager snowManager;

    [Header("오버레이 색상")]
    public Color freezeColor = new Color(0.4f, 0.7f, 1f, 0.4f);

    public float fadeSpeed = 2f;

    private VisualElement overlay;
    private VisualElement gaugeFill;
    private VisualElement gaugeRoot;

    private float currentAlpha = 0f;
    private float currentGauge = 0f;

    private void Start()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        overlay   = root.Q<VisualElement>("snow-overlay");
        gaugeFill = root.Q<VisualElement>("gauge-fill");
        gaugeRoot = root.Q<VisualElement>("freeze-gauge-root");

        SetGauge(0f);
        SetOverlayAlpha(0f);

        if (snowManager != null)
        {
            snowManager.OnGaugeChanged += HandleGaugeChanged;
            snowManager.OnFreezeStart  += HandleFreezeStart;
            snowManager.OnFreezeEnd    += HandleFreezeEnd;
        }
    }

    private void OnDisable()
    {
        if (snowManager != null)
        {
            snowManager.OnGaugeChanged -= HandleGaugeChanged;
            snowManager.OnFreezeStart  -= HandleFreezeStart;
            snowManager.OnFreezeEnd    -= HandleFreezeEnd;
        }
    }

    private void HandleGaugeChanged(float gauge)
    {
        currentGauge = gauge;
        SetGauge(gauge);
    }

    private void HandleFreezeStart() => SetOverlayAlpha(freezeColor.a);
    private void HandleFreezeEnd()   => currentGauge = 0f;

    private void Update()
    {
        float targetAlpha = currentGauge > 0.5f ? freezeColor.a * ((currentGauge - 0.5f) / 0.5f) : 0f;
        currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, Time.deltaTime * fadeSpeed);
        SetOverlayAlpha(currentAlpha);
    }

    private void SetGauge(float ratio) // 0~1
    {
        if (gaugeFill == null) return;
        gaugeFill.style.width = new StyleLength(new Length(ratio * 100f, LengthUnit.Percent));
    }

    private void SetOverlayAlpha(float alpha)
    {
        if (overlay == null) return;
        overlay.style.backgroundColor = new StyleColor(
            new Color(freezeColor.r, freezeColor.g, freezeColor.b, alpha)
        );
    }
}