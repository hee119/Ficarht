using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SlotGuideManager : MonoBehaviour
{
    public static SlotGuideManager Instance { get; private set; }

    [Header("슬롯 연결")]
    public List<CardSlot> characterSlots = new List<CardSlot>();
    public List<CardSlot> buffSlots = new List<CardSlot>();
    public List<CardSlot> skillSlots = new List<CardSlot>();

    [Header("홀로그램 설정")]
    public float pulseSpeed = 2f;
    public float minAlpha = 0.2f;
    public float maxAlpha = 0.8f;

    [Header("가이드 크기")]
    public Vector3 guideSize = new Vector3(0.8f, 0.01f, 1.2f);

    [Header("가이드 크기 배율")]
    public float guideWidthMultiplier = 1.2f;
    public float guideHeight = 0.01f;
    public float guideLengthMultiplier = 1.2f;

    private Dictionary<CardSlot, GameObject> guideObjects =
        new Dictionary<CardSlot, GameObject>();

    private Dictionary<CardSlot, Renderer> guideRenderers =
        new Dictionary<CardSlot, Renderer>();

    private Dictionary<CardSlot, Coroutine> pulseRoutines =
        new Dictionary<CardSlot, Coroutine>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    // -------------------------------------------------------
    // 표시
    // -------------------------------------------------------
    public void ShowGuides()
    {
        ShowSlotsGuide(characterSlots, CardType.Character);
        ShowSlotsGuide(buffSlots, CardType.Buff);
        ShowSlotsGuide(skillSlots, CardType.Skill);
    }

    private void ShowSlotsGuide(List<CardSlot> slots, CardType type)
    {
        foreach (var slot in slots)
        {
            if (slot == null || slot.currentCard != null)
                continue;

            CreateGuide(slot, type);
        }
    }

    // -------------------------------------------------------
    // 숨기기
    // -------------------------------------------------------
    public void HideGuide(CardSlot slot)
    {
        if (slot == null) return;

        if (pulseRoutines.TryGetValue(slot, out Coroutine routine))
        {
            if (routine != null)
                StopCoroutine(routine);

            pulseRoutines.Remove(slot);
        }

        if (guideObjects.TryGetValue(slot, out GameObject guide))
        {
            Destroy(guide);

            guideObjects.Remove(slot);
            guideRenderers.Remove(slot);
        }
    }

    public void ShowGuidesForType(CardType type)
    {
        HideAllGuides();

        switch (type)
        {
            case CardType.Character:
                ShowSlotsGuide(characterSlots, type);
                break;

            case CardType.Buff:
                ShowSlotsGuide(buffSlots, type);
                break;

            case CardType.Skill:
                ShowSlotsGuide(skillSlots, type);
                break;
        }
    }

    public void HideAllGuides()
    {
        List<CardSlot> slots = new List<CardSlot>(guideObjects.Keys);

        foreach (var slot in slots)
            HideGuide(slot);
    }

    // -------------------------------------------------------
    // 가이드 생성
    // -------------------------------------------------------
    private void CreateGuide(CardSlot slot, CardType type)
    {
        if (guideObjects.ContainsKey(slot))
            return;

        GameObject guide = GameObject.CreatePrimitive(PrimitiveType.Quad);

        guide.name = $"Guide_{slot.name}";

        Collider guideCol = guide.GetComponent<Collider>();

        if (guideCol != null)
            guideCol.enabled = false;

        // 위치
        guide.transform.position =
            slot.transform.position + Vector3.up * 0.05f;

        // 회전
        guide.transform.rotation =
            Quaternion.Euler(90f, 0f, 0f);

        // 슬롯 크기 기준 자동 크기 조절
        Renderer slotRenderer = slot.GetComponent<Renderer>();

        if (slotRenderer != null)
        {
            Bounds bounds = slotRenderer.bounds;

            guide.transform.localScale = new Vector3(
                bounds.size.x * guideWidthMultiplier,
                guideHeight,
                bounds.size.z * guideLengthMultiplier
            );
        }
        else
        {
            guide.transform.localScale = guideSize;
        }

        // 머티리얼
        Renderer rend = guide.GetComponent<Renderer>();

        Material mat =
            new Material(Shader.Find("Transparent/Diffuse"));

        if (mat.shader.name == "Hidden/InternalErrorShader")
        {
            mat = new Material(Shader.Find("Standard"));

            mat.SetFloat("_Mode", 3);

            mat.SetInt(
                "_SrcBlend",
                (int)UnityEngine.Rendering.BlendMode.SrcAlpha
            );

            mat.SetInt(
                "_DstBlend",
                (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
            );

            mat.SetInt("_ZWrite", 0);

            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

            mat.renderQueue = 3000;
        }

        mat.color = GetGuideColor(type);

        rend.material = mat;

        guideObjects[slot] = guide;
        guideRenderers[slot] = rend;

        pulseRoutines[slot] =
            StartCoroutine(PulseGuide(rend, type));
    }

    // -------------------------------------------------------
    // 깜빡임
    // -------------------------------------------------------
    private IEnumerator PulseGuide(Renderer rend, CardType type)
    {
        Color baseColor = GetGuideColor(type);

        while (true)
        {
            float t =
                Mathf.PingPong(Time.time * pulseSpeed, 1f);

            float alpha =
                Mathf.Lerp(minAlpha, maxAlpha, t);

            if (rend != null)
            {
                rend.material.color = new Color(
                    baseColor.r,
                    baseColor.g,
                    baseColor.b,
                    alpha
                );
            }

            yield return null;
        }
    }

    // -------------------------------------------------------
    // 유틸
    // -------------------------------------------------------
    private Color GetGuideColor(CardType type)
    {
        switch (type)
        {
            case CardType.Character:
                return new Color(0f, 1f, 1f, 0.5f);

            case CardType.Buff:
                return new Color(0f, 1f, 0f, 0.5f);

            case CardType.Skill:
                return new Color(1f, 0f, 1f, 0.5f);

            default:
                return new Color(1f, 1f, 1f, 0.5f);
        }
    }
}