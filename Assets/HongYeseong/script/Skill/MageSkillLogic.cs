using UnityEngine;
using Mirror;

public class MageSkillLogic : MonoBehaviour, ISkillLogicBase
{
    [SerializeField]
    SkillType skillType;

    PrefabInfo prefabInfo;

    public CharaStat playerStat;

    enum SkillType
    {
        ice,
        fire,
        buff,
        defaultAttack
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
        prefabInfo.power += playerStat.intelligence * (prefabInfo.power / 100f);

        if (skillType == SkillType.buff)
            playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
    }

    public void OnTriggerEnter(Collider other)
    {
        if (prefabInfo == null || playerStat == null) return;

        // 소유자 클라이언트에서만 데미지 처리
        // 비소유자 클라이언트에서는 AnimEvent RPC로 재생된 시각 효과만 존재
        PlayerController ownerPC = playerStat.GetComponent<PlayerController>();
        if (ownerPC != null && !ownerPC.isOwned && NetworkClient.active) return;

        // 트리거에 닿은 오브젝트에서 동적으로 적 CharaStat 탐색
        CharaStat hitStat = other.GetComponentInParent<CharaStat>();
        if (hitStat == null || hitStat == playerStat) return;

        PlayerController targetPC = hitStat.GetComponent<PlayerController>();

        switch (skillType)
        {
            case SkillType.ice:
<<<<<<< HEAD
                playerStat.playerController.isAttacking = true;
                NetworkApplyDamage(targetPC, hitStat, prefabInfo.power);
                NetworkApplyFreeze(targetPC, hitStat, prefabInfo.duration);
=======
                targetStat.Hit(prefabInfo.power);
                targetStat.Freezing(prefabInfo.duration);
>>>>>>> 1a1a276e33f49843816153acaf894bfbcec09c24
                PoolManager.Instance.Release(prefabInfo.skillData.skillId, gameObject);
                break;

            case SkillType.fire:
<<<<<<< HEAD
                playerStat.playerController.isAttacking = true;
                NetworkApplyDamage(targetPC, hitStat, prefabInfo.power);
                NetworkApplyBurn(targetPC, hitStat, prefabInfo.duration, prefabInfo.burnDamage);
=======
                targetStat.Hit(prefabInfo.power);
                targetStat.Burn(prefabInfo.duration, prefabInfo.burnDamage);
>>>>>>> 1a1a276e33f49843816153acaf894bfbcec09c24
                PoolManager.Instance.Release(prefabInfo.skillData.skillId, gameObject);
                break;

            case SkillType.defaultAttack:
                NetworkApplyDamage(targetPC, hitStat, prefabInfo.power);
                PoolManager.Instance.Release(prefabInfo.skillData.skillId, gameObject);
                break;
        }
    }

    // ── 네트워크 헬퍼 ──────────────────────────────

    static void NetworkApplyDamage(PlayerController targetPC, CharaStat fallback, float damage)
    {
        if (targetPC != null && NetworkClient.active)
            targetPC.CmdNetworkDamage(damage);
        else
            fallback.Hit(damage);
    }

    static void NetworkApplyFreeze(PlayerController targetPC, CharaStat fallback, float duration)
    {
        if (targetPC != null && NetworkClient.active)
            targetPC.CmdNetworkFreeze(duration);
        else
            fallback.Freezing(duration);
    }

    static void NetworkApplyBurn(PlayerController targetPC, CharaStat fallback, float duration, float dps)
    {
        if (targetPC != null && NetworkClient.active)
            targetPC.CmdNetworkBurn(duration, dps);
        else
            fallback.Burn(duration, dps);
    }
}
