using System;
using UnityEngine;
using System.Collections.Generic;

public class CoolTime : MonoBehaviour, ICharacterSkill
{
    private CharaStat charaStat;

    [Header("Skill CoolTime")]
    public List<SkillCoolTime> skillCoolTimes = new();

    // runtime data
    private Dictionary<string, float> coolTimeTable = new();
    private Dictionary<string, float> lastUseTime = new();

    void Awake()
    {
        charaStat = GetComponent<CharaStat>();

        // 리스트 → 딕셔너리 변환
        foreach (var skill in skillCoolTimes)
        {
            coolTimeTable[skill.animationName] = skill.coolTime;
        }
    }

    // =========================
    // 쿨타임 체크 (핵심)
    // =========================
    public bool CoolTimeCheck(string skillName)
    {
        if (!coolTimeTable.TryGetValue(skillName, out float coolTime))
            return true; // 등록 안된 스킬은 제한 없음

        if (lastUseTime.TryGetValue(skillName, out float lastTime))
        {
            float remain = (lastTime + coolTime) - Time.time;

            if (remain > 0)
            {
                Debug.Log($"{skillName} 쿨타임 남음 : {remain:F1}초");
                return false;
            }
        }

        // 사용 가능 → 시간 갱신
        lastUseTime[skillName] = Time.time;
        return true;
    }

    // =========================
    // 스킬 사용
    // =========================
    public void UseSkill(string skillName, Transform owner)
    {
        PrefabInfo prefabInfo =
            PoolManager.Instance.GetPrefab(skillName, owner)
                .GetComponent<PrefabInfo>();

        prefabInfo.SkillDataUpdate(
            charaStat.power,
            charaStat.projectileSpeed,
            charaStat.cooldown,
            charaStat.duration);
    }
}

[Serializable]
public class SkillCoolTime
{
    public string animationName;
    public float coolTime;
}