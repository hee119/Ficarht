using UnityEngine;
using Mirror;

public class BerserkerSkillLogic : MonoBehaviour, ISkillLogicBase
{
    [SerializeField]
    SkillType skillType;

    PrefabInfo prefabInfo;

    public CharaStat playerStat;

    enum SkillType
    {
        BerserkerAttackAndSpeedBuff,
        BerserkerAttackBuff,
        BerserkerBloodyAxeChopping,
        BerserkerDefaultSlash
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
            case SkillType.BerserkerAttackBuff:
            case SkillType.BerserkerAttackAndSpeedBuff:
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
                break;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (prefabInfo == null || playerStat == null) return;

        // 소유자 클라이언트에서만 데미지 처리
        PlayerController ownerPC = playerStat.GetComponent<PlayerController>();
        if (ownerPC != null && !ownerPC.isOwned && NetworkClient.active) return;

        CharaStat hitStat = other.GetComponentInParent<CharaStat>();
        if (hitStat == null || hitStat == playerStat) return;

        PlayerController targetPC = hitStat.GetComponent<PlayerController>();

        switch (skillType)
        {
            case SkillType.BerserkerDefaultSlash:
                NetworkApplyDamage(targetPC, hitStat, prefabInfo.power);
                break;

            case SkillType.BerserkerBloodyAxeChopping:
                playerStat.playerController.isAttacking = true;
                NetworkApplyDamage(targetPC, hitStat, prefabInfo.power);
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
}
