using UnityEngine;

public class BerserkerSkillLogic : MonoBehaviour, ISkillLogicBase
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

    void Awake()
    {
        prefabInfo = GetComponent<PrefabInfo>();
    }

    // OnEnable: playerStat 주입 전이므로 아무것도 실행하지 않음.
    // 버프 로직은 SetOwner() 에서 실행됩니다.
    public void OnEnable() { }

    /// <summary>
    /// CoolTime.UseSkill() 이 풀에서 꺼낸 직후 호출합니다.
    /// playerStat 주입 + 즉시 발동 스킬 실행.
    /// </summary>
    public void SetOwner(CharaStat ownerStat)
    {
        playerStat = ownerStat;

        if (prefabInfo == null)
        {
            Debug.LogError($"{name}: SetOwner 시 prefabInfo가 NULL (PrefabInfo 컴포넌트 확인)");
            return;
        }

        // 파워 스케일링 (풀 재사용 시 누적 방지)
        prefabInfo.Init();
        prefabInfo.power += playerStat.power * (prefabInfo.power / 100f);

        switch (skillType)
        {
            case SkillType.BerserkerAttackBuff:
                playerStat.playerController.isAttacking = true;
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
                break;

            case SkillType.BerserkerAttackAndSpeedBuff:
                playerStat.playerController.isAttacking = true;
                playerStat.ApplyBuff(prefabInfo.power, prefabInfo.speed, prefabInfo.defense, prefabInfo.duration);
                break;
        }
    }

    public void OnTriggerEnter(Collider other)
    {
        if (prefabInfo == null || targetStat == null) return;
        if (other.gameObject != target) return;

        switch (skillType)
        {
            case SkillType.BerserkerDefaultSlash:
                playerStat.playerController.isAttacking = true;
                targetStat.Hit(prefabInfo.power);
                break;

            case SkillType.BerserkerBloodyAxeChopping:
                if (playerStat != null) playerStat.playerController.isAttacking = true;
                targetStat.Hit(prefabInfo.power);
                break;
        }
    }
}
