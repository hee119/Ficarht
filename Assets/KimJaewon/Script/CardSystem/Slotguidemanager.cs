using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SlotGuideManager : MonoBehaviour
{
    public static SlotGuideManager Instance { get; private set; }

    [Header("슬롯 연결")]
    public List<CardSlot> characterSlots = new List<CardSlot>();
    public List<CardSlot> buffSlots = new List<CardSlot>();
    public List<CardSlot> trapSlots = new List<CardSlot>();

    [Header("홀로그램 설정")]
    public float pulseSpeed = 2f;
    public float minAlpha = 0.2f;
    public float maxAlpha = 0.8f;

    [Header("가이드 크기")]
    public Vector2 guideSize = new Vector2(0.8f, 1.2f);

    [Header("가이드 크기 배율")]
    public float guideWidthMultiplier = 1f;
    public float guideLengthMultiplier = 1f;

    [Header("가이드 위치")]
    public float guideYOffset = 0.06f;

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

    public static SlotGuideManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        GameObject managerObject = new GameObject("SlotGuideManager");

        return managerObject.AddComponent<SlotGuideManager>();
    }

    private void SyncSlotsFromCardSystem()
    {
        if (CardSystemManager.Instance == null)
            return;

        if (characterSlots.Count == 0)
        {
            characterSlots = CardSystemManager.Instance.characterSlots;
        }

        if (buffSlots.Count == 0)
        {
            buffSlots = CardSystemManager.Instance.buffSlots;
        }

        if (trapSlots.Count == 0)
        {
            trapSlots = CardSystemManager.Instance.trapSlots;
        }
    }

    // -------------------------------------------------------
    // 표시
    // -------------------------------------------------------
    public void ShowGuides()
    {
        SyncSlotsFromCardSystem();

        ShowSlotsGuide(characterSlots, CardType.Character);
        ShowSlotsGuide(buffSlots, CardType.Buff);
        ShowSlotsGuide(trapSlots, CardType.Trap);
    }

    private void ShowSlotsGuide(List<CardSlot> slots, CardType type)
    {
        ShowSlotsGuide(slots, type, null);
    }

    private void ShowSlotsGuide(
        List<CardSlot> slots,
        CardType type,
        CardObject card
    )
    {
        foreach (var slot in slots)
        {
            if (slot == null || slot.currentCard != null)
                continue;

            CreateGuide(slot, type, card);
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
        ShowGuidesForCardType(type, null);
    }

    public void ShowGuidesForCard(CardObject card)
    {
        if (card == null || card.data == null)
            return;

        ShowGuidesForCardType(card.data.cardType, card);
    }

    private void ShowGuidesForCardType(CardType type, CardObject card)
    {
        SyncSlotsFromCardSystem();
        HideAllGuides();

        List<CardSlot> targetSlots = GetSlotsByType(type);

        if (card != null)
        {
            ShowFirstAvailableSlotGuide(targetSlots, type, card);
            return;
        }

        ShowSlotsGuide(targetSlots, type, null);
    }

    private List<CardSlot> GetSlotsByType(CardType type)
    {
        switch (type)
        {
            case CardType.Character:
                return characterSlots;

            case CardType.Buff:
                return buffSlots;

            case CardType.Trap:
                return trapSlots;

            default:
                return null;
        }
    }

    private void ShowFirstAvailableSlotGuide(
        List<CardSlot> slots,
        CardType type,
        CardObject card
    )
    {
        if (slots == null)
            return;

        foreach (var slot in slots)
        {
            if (slot == null || slot.currentCard != null)
                continue;

            CreateGuide(slot, type, card);
            return;
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
        CreateGuide(slot, type, null);
    }

    private void CreateGuide(
        CardSlot slot,
        CardType type,
        CardObject card
    )
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
            slot.transform.position + Vector3.up * guideYOffset;

        // 회전
        guide.transform.rotation =
            Quaternion.Euler(90f, 0f, 0f);

        Vector2 size =
            card != null
                ? card.GetPlacedGuideSize()
                : GetSlotGuideSize(slot);

        guide.transform.localScale = new Vector3(
            size.x * guideWidthMultiplier,
            size.y * guideLengthMultiplier,
            1f
        );

        // 머티리얼
        Renderer rend = guide.GetComponent<Renderer>();

        Material mat =
            CreateGuideMaterial(type);

        rend.material = mat;

        guideObjects[slot] = guide;
        guideRenderers[slot] = rend;

        pulseRoutines[slot] =
            StartCoroutine(PulseGuide(rend, type));
    }

    private Vector2 GetSlotGuideSize(CardSlot slot)
    {
        Renderer slotRenderer = slot.GetComponent<Renderer>();

        if (slotRenderer == null)
            return guideSize;

        Bounds bounds = slotRenderer.bounds;

        return new Vector2(
            bounds.size.x,
            bounds.size.z
        );
    }

    private Material CreateGuideMaterial(CardType type)
    {
        Shader shader =
            Shader.Find("Unlit/Transparent");

        if (shader == null)
        {
            shader = Shader.Find("Sprites/Default");
        }

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material mat = new Material(shader);

        mat.color = GetGuideColor(type);
        mat.renderQueue = 3000;

        if (mat.HasProperty("_Mode"))
        {
            mat.SetFloat("_Mode", 3);
        }

        if (mat.HasProperty("_SrcBlend"))
        {
            mat.SetInt(
                "_SrcBlend",
                (int)UnityEngine.Rendering.BlendMode.SrcAlpha
            );
        }

        if (mat.HasProperty("_DstBlend"))
        {
            mat.SetInt(
                "_DstBlend",
                (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha
            );
        }

        if (mat.HasProperty("_ZWrite"))
        {
            mat.SetInt("_ZWrite", 0);
        }

        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        return mat;
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

            case CardType.Trap:
                return new Color(1f, 0f, 0f, 0.5f);

            default:
                return new Color(1f, 1f, 1f, 0.5f);
        }
    }
}