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
    
    void Awake()
    {
        prefabInfo = GetComponent<PrefabInfo>();
    }

    void Start()
    {
        if (prefabInfo == null)
        {
            Debug.LogWarning($"{name} : prefabInfo가 NULL입니다.");
            return;
        }

        if (playerStat == null)
        {
            Debug.LogWarning($"{name} : playerStat이 NULL입니다.");
            return;
        }

        prefabInfo.power += playerStat.power * (prefabInfo.power / 100f);
    }

    public void OnEnable()
    {
        if (player != null)
            playerStat = player.GetComponent<CharaStat>();
    }

    public void Activate()
    {
        if (prefabInfo == null)
        {
            Debug.LogWarning($"{name} : prefabInfo가 NULL입니다.");
            return;
        }

        if (playerStat == null)
        {
            Debug.LogWarning($"{name} : playerStat이 NULL입니다.");
            return;
        }

        switch (skillType)
        {
            case SkillType.PaladinDefenseBuff:
                playerStat.playerController.isAttacking  = true;
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
                break;

            case SkillType.PaladinDivineProtection:
                playerStat.playerController.isAttacking  = true;
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
                break;

            case SkillType.PaladinHolySword:
                playerStat.playerController.isAttacking  = true;
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
                break;

            case SkillType.PaladinShield:
                playerStat.playerController.isAttacking  = true;
                playerStat.ApplyShield(prefabInfo.defense, prefabInfo.duration, gameObject);
                break;

            case SkillType.PaladinHandOfGod:
                playerStat.playerController.isAttacking  = true;
                if (targetStat == null) return;
                targetStat.Slowdown(prefabInfo.duration, prefabInfo.speed);
                break;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (prefabInfo == null)
        {
            Debug.LogWarning($"{name} : prefabInfo가 NULL입니다.");
            return;
        }

        switch (skillType)
        {
            case SkillType.PaladinDefaultAttack:
                CharaStat hitStat = other.GetComponent<CharaStat>();
                if (hitStat != null)
                    hitStat.Hit(prefabInfo.power);
                break;
        }
    }
}