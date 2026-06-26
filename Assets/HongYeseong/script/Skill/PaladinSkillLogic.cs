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

    public void OnEnable()
    {
        switch (skillType)
        {
            case SkillType.PaladinDefenseBuff:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense);
                break;
            case SkillType.PaladinDivineProtection:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense);
                break;
            case SkillType.PaladinHolySword:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense);
                break;
            case SkillType.PaladinShield:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense);
                break;
            case SkillType.PaladinHandOfGod:
                targetStat.Slowdown(prefabInfo.duration, prefabInfo.speed);
                break;
        }
    }


    public void OnTriggerEnter(Collider other)
    {
        switch (skillType)
        {
            case SkillType.PaladinDefaultAttack:
                targetStat.Hit(prefabInfo.power);
                break;
        }
    }
}
