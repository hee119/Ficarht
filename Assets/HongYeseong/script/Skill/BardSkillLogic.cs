using UnityEngine;
using Mirror;

public class BardSkillLogic : MonoBehaviour, ISkillLogicBase
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
            Debug.LogError($"{name}: SetOwner 시 prefabInfo가 NULL (PrefabInfo 컴포넌트 확인)");
            return;
        }

        prefabInfo.Init();
    }

    public void OnTriggerEnter(Collider other)
    {
        if (prefabInfo == null || playerStat == null) return;

        // 소유자 클라이언트에서만 처리
        PlayerController ownerPC = playerStat.GetComponent<PlayerController>();
        if (ownerPC != null && !ownerPC.isOwned && NetworkClient.active) return;

        CharaStat hitStat = other.GetComponentInParent<CharaStat>();
        if (hitStat == null || hitStat == playerStat) return;

        PlayerController targetPC = hitStat.GetComponent<PlayerController>();

        switch (skillType)
        {
            // TODO: 바드 스킬 로직 구현
            // 예시: targetPC.CmdNetworkDamage(prefabInfo.power);
        }
    }
}
