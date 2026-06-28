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

        switch (skillType)
        {
            case SkillType.BerserkerAttackBuff:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
                break;

            case SkillType.BerserkerAttackAndSpeedBuff:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
                break;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (prefabInfo == null)
            Debug.LogError($"{name} : prefabInfo가 NULL입니다.");

        if (targetStat == null)
            Debug.LogError($"{name} : targetStat이 NULL입니다.");

        if (other.gameObject == target)
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
}