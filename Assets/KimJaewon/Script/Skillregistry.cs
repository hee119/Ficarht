using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 슬롯에 배치된 스킬카드를 등록해두고,
/// 전투 중 홍예성(플레이어 시스템)쪽에서 GetSkills()로 가져가는 구조.
/// </summary>
public class SkillRegistry : MonoBehaviour
{
    public static SkillRegistry Instance { get; private set; }

    // 플레이어별 등록된 스킬 목록
    private List<SkillID> registeredSkills = new List<SkillID>();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // -------------------------------------------------------
    //  등록
    // -------------------------------------------------------

    /// <summary>
    /// 배치된 스킬 슬롯들을 읽어 스킬 등록.
    /// CardSystemManager 턴 종료 시 호출.
    /// </summary>
    public void RegisterFromSlots(List<CardSlot> skillSlots)
    {
        registeredSkills.Clear();

        foreach (var slot in skillSlots)
        {
            if (slot == null || slot.currentCard == null) continue;
            if (slot.currentCard.data.cardType != CardType.Skill) continue;

            SkillID id = slot.currentCard.data.skillID;
            registeredSkills.Add(id);
        }

    }

    // -------------------------------------------------------
    //  조회 (플레이어 시스템에서 사용)
    // -------------------------------------------------------

    /// <summary>
    /// 등록된 스킬 목록 반환 (전투 시작 시 플레이어 시스템이 가져감).
    /// </summary>
    public List<SkillID> GetSkills() => new List<SkillID>(registeredSkills);

    /// <summary>
    /// 특정 스킬이 등록되어 있는지 확인.
    /// </summary>
    public bool HasSkill(SkillID id) => registeredSkills.Contains(id);

    // -------------------------------------------------------
    //  사용 처리 (스킬은 1회만 사용 가능)
    // -------------------------------------------------------

    /// <summary>
    /// 스킬 사용 후 목록에서 제거 (재사용 불가 처리).
    /// 전투 중 플레이어 시스템에서 호출.
    /// </summary>
    public bool UseSkill(SkillID id)
    {
        if (!registeredSkills.Contains(id))
        {
            Debug.LogWarning($"[SkillRegistry] {id} 스킬이 없거나 이미 사용됨.");
            return false;
        }

        registeredSkills.Remove(id);
        return true;
    }

    public void Clear() => registeredSkills.Clear();
}