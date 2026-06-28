using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 슬롯에 배치된 버프카드들을 RuntimeStats에 일괄 적용.
/// CardSystemManager에서 턴 종료 시 호출하세요.
/// </summary>
public static class BuffApplier
{
    /// <summary>
    /// 배치된 버프 슬롯 목록을 읽어 RuntimeStats에 수치 적용.
    /// </summary>
    public static void ApplyAll(List<CardSlot> buffSlots, RuntimeStats targetStats)
    {
        if (targetStats == null)
        {
            Debug.LogError("[BuffApplier] targetStats가 null입니다.");
            return;
        }

        foreach (var slot in buffSlots)
        {
            if (slot == null || slot.currentCard == null) continue;
            if (slot.currentCard.data.cardType != CardType.Buff) continue;

            BuffEffect effect = slot.currentCard.data.buffEffect;
            if (effect == null)
            {
                Debug.LogWarning(
                    $"[BuffApplier] '{slot.currentCard.data.cardName}' 버프 효과가 비어 있습니다."
                );

                continue;
            }

            targetStats.ApplyBuff(effect);

            Debug.Log(
                $"[BuffApplier] '{slot.currentCard.data.cardName}' 적용: {targetStats}"
            );
        }
    }

    /// <summary>
    /// 단일 버프카드 즉시 적용 (테스트용).
    /// </summary>
    public static void ApplySingle(CardData buffCard, RuntimeStats targetStats)
    {
        if (buffCard.cardType != CardType.Buff)
        {
            Debug.LogWarning("[BuffApplier] Buff 타입 카드가 아닙니다.");
            return;
        }
        if (buffCard.buffEffect == null)
        {
            Debug.LogWarning("[BuffApplier] buffEffect가 비어 있습니다.");
            return;
        }

        targetStats.ApplyBuff(buffCard.buffEffect);
    }
}
