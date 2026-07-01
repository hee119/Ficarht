using UnityEngine;

public class BardSkillLogic : MonoBehaviour, ISkillLogicBase
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
        if (prefabInfo == null || targetStat == null) return;

        switch (skillType)
        {
            // TODO: 바드 스킬 로직 구현
        }
    }
}
