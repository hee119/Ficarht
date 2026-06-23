using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 카드 은닉 / 공개 시스템.
/// - 준비 단계: 내 카드는 보임, 상대 카드는 숨김
/// - 타이머 종료: 양쪽 카드 모두 공개
/// 멀티 연동 시 신지환에게 IsOwner 조건 전달 필요.
/// </summary>
public class CardRevealSystem : MonoBehaviour
{
    public static CardRevealSystem Instance { get; private set; }

    [Header("카드 뒷면 머티리얼 (상대 카드에 씌울 것)")]
    public Material hiddenMaterial;

    [Header("내 카드 슬롯들")]
    public List<CardSlot> mySlots = new List<CardSlot>();

    [Header("상대 카드 슬롯들")]
    public List<CardSlot> opponentSlots = new List<CardSlot>();

    // 각 카드의 원래 머티리얼 저장용
    private Dictionary<CardObject, Material[]> originalMaterials = new Dictionary<CardObject, Material[]>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // -------------------------------------------------------
    //  준비 단계: 상대 카드 숨기기
    // -------------------------------------------------------

    /// <summary>
    /// 준비 단계 시작 시 호출.
    /// 상대 슬롯의 카드를 뒷면으로 교체.
    /// </summary>
    public void HideOpponentCards()
    {
        foreach (var slot in opponentSlots)
        {
            if (slot?.currentCard == null) continue;
            ApplyHiddenMaterial(slot.currentCard);
        }
    }

    // -------------------------------------------------------
    //  공개: 타이머 종료 시 호출
    // -------------------------------------------------------

    /// <summary>
    /// 타이머 종료 → 모든 카드 공개.
    /// CardSystemManager.OnTimerEnd()에서 호출하세요.
    /// </summary>
    public void RevealAllCards()
    {
        foreach (var slot in mySlots)
            if (slot?.currentCard != null) RestoreOriginalMaterial(slot.currentCard);

        foreach (var slot in opponentSlots)
            if (slot?.currentCard != null) RestoreOriginalMaterial(slot.currentCard);

        originalMaterials.Clear();
    }

    // -------------------------------------------------------
    //  슬롯에 카드가 새로 배치될 때마다 호출 (CardSlot에서 호출)
    // -------------------------------------------------------

    /// <summary>
    /// 상대가 카드를 슬롯에 배치했을 때 즉시 숨김 적용.
    /// CardSlot.TryPlaceCard()에서 isOpponent=true로 호출.
    /// </summary>
    public void OnCardPlacedInSlot(CardObject card, bool isOpponent)
    {
        if (isOpponent) ApplyHiddenMaterial(card);
    }

    // -------------------------------------------------------
    //  내부: 머티리얼 교체 / 복원
    // -------------------------------------------------------

    private void ApplyHiddenMaterial(CardObject card)
    {
        if (hiddenMaterial == null)
        {
            // 머티리얼 없으면 비활성화로 대체
            card.gameObject.SetActive(false);
            return;
        }

        Renderer rend = card.GetComponent<Renderer>();
        if (rend == null) return;

        // 원본 저장
        if (!originalMaterials.ContainsKey(card))
            originalMaterials[card] = rend.materials;

        // 뒷면 머티리얼로 교체
        Material[] hidden = new Material[rend.materials.Length];
        for (int i = 0; i < hidden.Length; i++) hidden[i] = hiddenMaterial;
        rend.materials = hidden;
    }

    private void RestoreOriginalMaterial(CardObject card)
    {
        // 비활성화 방식 복원
        if (!card.gameObject.activeSelf)
        {
            card.gameObject.SetActive(true);
            return;
        }

        if (!originalMaterials.ContainsKey(card)) return;

        Renderer rend = card.GetComponent<Renderer>();
        if (rend != null) rend.materials = originalMaterials[card];
        originalMaterials.Remove(card);
    }
}