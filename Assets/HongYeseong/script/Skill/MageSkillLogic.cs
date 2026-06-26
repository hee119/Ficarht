using System;
using UnityEngine;

public class MageSkillLogic : MonoBehaviour
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
        ice,
        fire,
        buff,
        defaultAttack
    }


    public void OnEnable()
    {
        if (skillType == SkillType.buff)
            playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense);
    }

    public void OnTriggerEnter(Collider other)
    {
        switch (skillType)
        {
            case SkillType.ice:
                targetStat.Hit(prefabInfo.power);
                targetStat.Freezing(prefabInfo.duration);
                break;
            case SkillType.fire:
                targetStat.Hit(prefabInfo.power);
                targetStat.Burn(prefabInfo.duration, prefabInfo.power);
                break;
            case SkillType.defaultAttack:
                targetStat.Hit(prefabInfo.power);
                break;
        }
    }
}
