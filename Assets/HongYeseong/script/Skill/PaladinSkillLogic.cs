using System;
using UnityEngine;

public class PaladinSkillLogic : MonoBehaviour
{
    [SerializeField]
    SkillType skillType;

    PrefabInfo prefabInfo;

    public GameObject target;
    public GameObject player;
    public CharaStat targetStat;
    public CharaStat playerStat;

    enum SkillType
    {
        PaladinDefaultAttack,
        PaladinDefenseBuff,
        PaladinDivineProtection,
        PaladinHandOfGod,
        PaladinHolySword,
        PaladinShield
    }
    
    void Start()
    {
        if (prefabInfo == null)
        {
            Debug.LogError($"{name} : prefabInfo가 NULL입니다.");
            return;
        }

        if (playerStat == null)
        {
            Debug.LogError($"{name} : playerStat이 NULL입니다.");
            return;
        }

        prefabInfo.power += playerStat.power * (prefabInfo.power / 100f);
    }
    public void OnEnable()
    {
        if (prefabInfo == null)
            Debug.LogError($"{name} : prefabInfo가 NULL입니다.");

        if (playerStat == null)
            Debug.LogError($"{name} : playerStat이 NULL입니다.");

        if (targetStat == null)
            Debug.LogError($"{name} : targetStat이 NULL입니다.");

        switch (skillType)
        {
            case SkillType.PaladinDefenseBuff:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
                break;

            case SkillType.PaladinDivineProtection:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
                break;

            case SkillType.PaladinHolySword:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
                break;

            case SkillType.PaladinShield:
                playerStat.ApplyShield(prefabInfo.defense, prefabInfo.duration, gameObject);
                break;

            case SkillType.PaladinHandOfGod:
                targetStat.Slowdown(prefabInfo.duration, prefabInfo.speed);
                break;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (prefabInfo == null)
            Debug.LogError($"{name} : prefabInfo가 NULL입니다.");

        if (targetStat == null)
            Debug.LogError($"{name} : targetStat이 NULL입니다.");

        switch (skillType)
        {
            case SkillType.PaladinDefaultAttack:
                targetStat.Hit(prefabInfo.power);
                break;
        }
    }
}