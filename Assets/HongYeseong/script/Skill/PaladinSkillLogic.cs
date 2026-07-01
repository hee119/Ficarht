using UnityEngine;
using Mirror;

public class PaladinSkillLogic : MonoBehaviour, ISkillLogicBase
{
    [SerializeField]
    SkillType skillType;

    PrefabInfo prefabInfo;

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

    public void OnEnable() { }

    public void SetOwner(CharaStat ownerStat)
    {
        playerStat = ownerStat;

        if (prefabInfo == null)
        {
            Debug.LogError($"{name}: SetOwner 시 prefabInfo가 NULL");
            return;
        }

        prefabInfo.Init();
        prefabInfo.power += playerStat.power * (prefabInfo.power / 100f);

        switch (skillType)
        {
            case SkillType.PaladinDefenseBuff:
            case SkillType.PaladinDivineProtection:
            case SkillType.PaladinHolySword:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
                break;

            case SkillType.PaladinShield:
<<<<<<< HEAD
                playerStat.playerController.isAttacking = true;
                playerStat.StartCoroutine(playerStat.ApplyShield(prefabInfo.defense, prefabInfo.duration, gameObject));
                break;

            case SkillType.PaladinHandOfGod:
                if (playerStat != null)
                {
                    playerStat.playerController.isAttacking = true;
                    // HandOfGod: 이미 SetOwner 단계에서 대상이 필요 → OnTriggerEnter에서 처리
                }
=======
                playerStat.ApplyShield(prefabInfo.defense, prefabInfo.duration, gameObject);
                break;

            case SkillType.PaladinHandOfGod:
                if (targetStat != null)
                    targetStat.Slowdown(prefabInfo.duration, prefabInfo.speed);
>>>>>>> 1a1a276e33f49843816153acaf894bfbcec09c24
                break;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
<<<<<<< HEAD
        if (prefabInfo == null || playerStat == null) return;

        // 소유자 클라이언트에서만 데미지/상태이상 처리
        PlayerController ownerPC = playerStat.GetComponent<PlayerController>();
        if (ownerPC != null && !ownerPC.isOwned && NetworkClient.active) return;

        CharaStat hitStat = other.GetComponentInParent<CharaStat>();
        if (hitStat == null || hitStat == playerStat) return;

        PlayerController targetPC = hitStat.GetComponent<PlayerController>();
=======
        if (prefabInfo == null) return;
>>>>>>> 1a1a276e33f49843816153acaf894bfbcec09c24

        switch (skillType)
        {
            case SkillType.PaladinDefaultAttack:
                NetworkApplyDamage(targetPC, hitStat, prefabInfo.power);
                break;

            case SkillType.PaladinHandOfGod:
                NetworkApplySlow(targetPC, hitStat, prefabInfo.duration);
                break;
        }
    }

    static void NetworkApplyDamage(PlayerController targetPC, CharaStat fallback, float damage)
    {
        if (targetPC != null && NetworkClient.active)
            targetPC.CmdNetworkDamage(damage);
        else
            fallback.Hit(damage);
    }

    static void NetworkApplySlow(PlayerController targetPC, CharaStat fallback, float duration)
    {
        if (targetPC != null && NetworkClient.active)
            targetPC.CmdNetworkSlow(duration);
        else
            fallback.Slowdown(duration, 0.5f);
    }
}
