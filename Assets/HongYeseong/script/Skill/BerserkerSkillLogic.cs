using System;
using UnityEngine;

public class BerserkerSkillLogic : MonoBehaviour
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
        BerserkerAttackAndSpeedBuff,
        BerserkerAttackBuff,
        BerserkerBloodyAxeChopping,
        BerserkerDefaultSlash
    }

    public void OnEnable()
    {
        switch (skillType)
        {
            case SkillType.BerserkerAttackBuff:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense);
                break;
            case SkillType.BerserkerAttackAndSpeedBuff:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense);
                break;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        switch (skillType)
        {
            case SkillType.BerserkerDefaultSlash:
                targetStat.Hit(prefabInfo.power);
                break;
            case SkillType.BerserkerBloodyAxeChopping:
                targetStat.Hit(prefabInfo.power);
                break;
        }
    }
}
