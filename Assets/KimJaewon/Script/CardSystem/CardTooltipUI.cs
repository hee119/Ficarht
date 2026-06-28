using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class CardTooltipUI : MonoBehaviour
{
    public static CardTooltipUI Instance { get; private set; }

    private RectTransform panelRect;
    private Text titleText;
    private Text typeText;
    private Text bodyText;
    private CanvasGroup canvasGroup;
    private Font tooltipFont;
    private const float PanelWidth = 380f;
    private const float MinPanelHeight = 180f;
    private const float MaxPanelHeight = 460f;

    public static CardTooltipUI GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject("CardTooltipUI");
        return root.AddComponent<CardTooltipUI>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        BuildUI();
        Hide();
    }

    private void Update()
    {
        if (canvasGroup == null || canvasGroup.alpha <= 0f)
            return;

        FollowMouse();
    }

    public void Show(CardData cardData)
    {
        if (cardData == null)
        {
            Hide();
            return;
        }

        titleText.text = string.IsNullOrWhiteSpace(cardData.cardName)
            ? "이름 없는 카드"
            : cardData.cardName;

        typeText.text = GetTypeLabel(cardData.cardType);
        bodyText.text = BuildDescription(cardData);
        ResizePanelToText();

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = false;
        FollowMouse();
    }

    public void Hide()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    private void BuildUI()
    {
        tooltipFont = CreateKoreanCapableFont();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();

        GameObject panel = new GameObject("Panel");
        panel.transform.SetParent(transform, false);

        panelRect = panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0f, 1f);
        panelRect.anchorMax = new Vector2(0f, 1f);
        panelRect.pivot = new Vector2(0f, 1f);
        panelRect.sizeDelta = new Vector2(PanelWidth, MinPanelHeight);

        Image background = panel.AddComponent<Image>();
        background.color = new Color(0.05f, 0.045f, 0.04f, 0.92f);

        VerticalLayoutGroup layout = panel.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 14);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        titleText = CreateText("Title", 26, FontStyle.Bold, new Color(1f, 0.86f, 0.42f));
        typeText = CreateText("Type", 18, FontStyle.Bold, new Color(0.78f, 0.7f, 1f));
        bodyText = CreateText("Body", 18, FontStyle.Normal, Color.white);
    }

    private Font CreateKoreanCapableFont()
    {
        string[] preferredFonts =
        {
            "Apple SD Gothic Neo",
            "AppleGothic",
            "NanumGothic",
            "Malgun Gothic",
            "Noto Sans CJK KR",
            "Noto Sans KR",
            "Arial Unicode MS"
        };

        Font font = Font.CreateDynamicFontFromOSFont(preferredFonts, 18);

        if (font != null)
            return font;

        return Resources.GetBuiltinResource<Font>("Arial.ttf");
    }

    private Text CreateText(string objectName, int fontSize, FontStyle style, Color color)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(panelRect, false);

        Text text = textObject.AddComponent<Text>();
        text.font = tooltipFont;
        text.fontSize = fontSize;
        text.resizeTextForBestFit = style == FontStyle.Normal;
        text.resizeTextMinSize = 14;
        text.resizeTextMaxSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.alignment = TextAnchor.UpperLeft;
        text.lineSpacing = 1.05f;

        RectTransform textRect =
            text.GetComponent<RectTransform>();

        textRect.sizeDelta = new Vector2(
            PanelWidth - 36f,
            fontSize + 8f
        );

        LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = fontSize + 8f;
        layoutElement.preferredHeight = fontSize + 8f;

        return text;
    }

    private void ResizePanelToText()
    {
        float textWidth = PanelWidth - 36f;
        float titleHeight = titleText.preferredHeight;
        float typeHeight = typeText.preferredHeight;
        float bodyHeight = bodyText.cachedTextGeneratorForLayout.GetPreferredHeight(
            bodyText.text,
            bodyText.GetGenerationSettings(
                new Vector2(textWidth, 0f)
            )
        ) / bodyText.pixelsPerUnit;

        float panelHeight = 14f
            + titleHeight
            + 6f
            + typeHeight
            + 6f
            + bodyHeight
            + 14f;

        LayoutElement titleLayout =
            titleText.GetComponent<LayoutElement>();

        if (titleLayout != null)
            titleLayout.preferredHeight = titleHeight;

        LayoutElement typeLayout =
            typeText.GetComponent<LayoutElement>();

        if (typeLayout != null)
            typeLayout.preferredHeight = typeHeight;

        panelHeight = Mathf.Clamp(
            panelHeight,
            MinPanelHeight,
            MaxPanelHeight
        );

        panelRect.sizeDelta = new Vector2(
            PanelWidth,
            panelHeight
        );

        LayoutElement bodyLayout =
            bodyText.GetComponent<LayoutElement>();

        if (bodyLayout != null)
        {
            bodyLayout.preferredHeight = Mathf.Max(
                bodyText.fontSize + 8f,
                bodyHeight
            );
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    }

    private void FollowMouse()
    {
        Vector2 mousePosition = Input.mousePosition;
        Vector2 targetPosition = mousePosition + new Vector2(28f, -28f);

        float maxX = Screen.width
            - panelRect.sizeDelta.x
            - 16f;

        float minY = panelRect.sizeDelta.y + 16f;

        targetPosition.x = Mathf.Clamp(targetPosition.x, 16f, maxX);
        targetPosition.y = Mathf.Clamp(targetPosition.y, minY, Screen.height - 16f);

        panelRect.position = targetPosition;
    }

    private string BuildDescription(CardData cardData)
    {
        if (!string.IsNullOrWhiteSpace(cardData.cardDescription))
            return cardData.cardDescription;

        switch (cardData.cardType)
        {
            case CardType.Character:
                return BuildCharacterDescription(cardData);

            case CardType.Buff:
                return BuildBuffDescription(cardData);

            case CardType.Trap:
                return BuildTrapDescription(cardData.GetTrapID());

            case CardType.Map:
                return string.IsNullOrWhiteSpace(cardData.mapSceneName)
                    ? "전투 맵 후보 카드입니다."
                    : $"전투 씬: {cardData.mapSceneName}";

            default:
                return "카드 효과 정보가 아직 입력되지 않았습니다.";
        }
    }

    private string BuildCharacterDescription(CardData cardData)
    {
        CharacterCardStats stats = cardData.characterStats;

        if (stats == null)
            return "캐릭터 스탯 정보가 없습니다.";

        return $"캐릭터: {stats.characterName}\n" +
            $"체력 {stats.health} / 스태미나 {stats.stamina}\n" +
            $"힘 {stats.power} / 방어 {stats.defense} / 지식 {stats.intelligence}";
    }

    private string BuildBuffDescription(CardData cardData)
    {
        BuffEffect effect = cardData.buffEffect;

        if (effect == null)
            return "버프 효과 정보가 없습니다.";

        StringBuilder builder = new StringBuilder();

        string buffSummary = GetBuffSummary(cardData);

        if (!string.IsNullOrWhiteSpace(buffSummary))
            builder.AppendLine(buffSummary);

        AppendStat(builder, "체력", effect.healthMod);
        AppendStat(builder, "스태미나", effect.staminaMod);
        AppendStat(builder, "힘", effect.powerMod);
        AppendStat(builder, "방어", effect.defenseMod);
        AppendStat(builder, "지식", effect.intelligenceMod);

        return builder.Length > 0
            ? builder.ToString().TrimEnd()
            : "스탯 변화가 없는 버프 카드입니다.";
    }

    private string GetBuffSummary(CardData cardData)
    {
        string cardName = cardData.cardName != null
            ? cardData.cardName.ToLowerInvariant()
            : "";

        switch (cardData.cardID)
        {
            case 1:
                return "운동 버프: 힘 또는 체력 계열을 강화합니다.";

            case 2:
                return "기도 버프: 지식 또는 회복 계열을 강화합니다.";

            case 3:
                return "커피 버프: 스태미나 계열을 강화합니다.";

            case 4:
                return "결의 버프: 전투 능력을 강화합니다.";

            case 5:
                return "수리 버프: 체력 회복 또는 생존력을 보강합니다.";

            case 6:
                return "철갑 버프: 방어력을 강화합니다.";

            case 7:
                return "아침 버프: 전반적인 컨디션을 끌어올립니다.";

            case 8:
                return "달리기 버프: 스태미나 또는 기동력을 강화합니다.";
        }

        if (cardName.Contains("exercise"))
            return "운동 버프: 힘 또는 체력 계열을 강화합니다.";

        if (cardName.Contains("prayer"))
            return "기도 버프: 지식 또는 회복 계열을 강화합니다.";

        if (cardName.Contains("coffee"))
            return "커피 버프: 스태미나 계열을 강화합니다.";

        if (cardName.Contains("determination"))
            return "결의 버프: 전투 능력을 강화합니다.";

        if (cardName.Contains("repair"))
            return "수리 버프: 체력 회복 또는 생존력을 보강합니다.";

        if (cardName.Contains("armor"))
            return "철갑 버프: 방어력을 강화합니다.";

        if (
            cardName.Contains("morning") ||
            cardName.Contains("moring")
        )
        {
            return "아침 버프: 전반적인 컨디션을 끌어올립니다.";
        }

        if (cardName.Contains("running"))
            return "달리기 버프: 스태미나 또는 기동력을 강화합니다.";

        return "버프 카드: 캐릭터 능력치를 강화합니다.";
    }

    private void AppendStat(StringBuilder builder, string label, float value)
    {
        if (Mathf.Approximately(value, 0f))
            return;

        string sign = value > 0f ? "+" : "";
        builder.AppendLine($"{label} {sign}{value}");
    }

    private string BuildTrapDescription(TrapID trapID)
    {
        switch (trapID)
        {
            case TrapID.Fracture:
                return "조건: 누군가 점프\n효과: 점프한 플레이어가 최대 체력의 3% 피해";

            case TrapID.HeavyStep:
                return "조건: 누군가 달리기 시작\n효과: 해당 플레이어 이동속도 감소";

            case TrapID.ThornArmor:
                return "조건: 누군가 공격\n효과: 공격한 플레이어가 피해";

            case TrapID.Coward:
                return "조건: 누군가 공격\n효과: 공격한 플레이어 이동속도 감소";

            case TrapID.NoViolence:
                return "조건: 누군가 공격\n효과: 공격한 플레이어 잠시 기절";

            case TrapID.LastResistance:
                return "조건: 체력이 낮은 상태에서 공격\n효과: 공격한 플레이어 기절";

            case TrapID.LackOfFocus:
                return "조건: 누군가 달리기 시작\n효과: 해당 플레이어가 피해";

            case TrapID.PositionSwap:
                return "조건: 스킬 사용\n효과: 두 플레이어의 위치 교환";

            case TrapID.Anxiety:
                return "조건: 누군가 달리기 시작\n효과: 해당 플레이어 이동속도 감소";

            case TrapID.Whatever:
                return "조건: 누군가 공격\n효과: 무작위 방해 효과 발동";

            case TrapID.NaturalDisaster:
                return "조건: 누군가 점프\n효과: 모든 플레이어가 피해";

            case TrapID.FairWorld:
                return "조건: 누군가 공격\n효과: 모든 플레이어 이동속도 감소";

            default:
                return "함정 효과 정보가 아직 입력되지 않았습니다.";
        }
    }

    private string GetTypeLabel(CardType cardType)
    {
        switch (cardType)
        {
            case CardType.Character:
                return "캐릭터 카드";

            case CardType.Buff:
                return "버프 카드";

            case CardType.Trap:
                return "함정 카드";

            case CardType.Map:
                return "맵 카드";

            default:
                return "카드";
        }
    }
}
