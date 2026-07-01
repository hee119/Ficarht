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
                // isAttacking = true는 AnimManager.HandleKeyStarted에서 이미 세팅됨.
                // 여기서 중복 세팅하면 비소유 클라이언트에서 AnimManager.SetBool() 리셋 타이밍이
                // 꼬여 애니메이션이 영구 고정되는 버그 발생 → 제거.
                // 소유자의 버프 적용만 처리.
                if (playerStat.playerController.isOwned)
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
