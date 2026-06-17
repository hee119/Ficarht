using UnityEngine;

public class BerserkerLogic : MonoBehaviour, ICharacterSkill
{
    CharaStat charaStat;

    void Awake()
    {
        charaStat = GetComponent<CharaStat>();
    }
    public void UseSkill(string skillName, Transform owner)
    {
        GameObject obj = PoolManager.Instance.GetPrefab(skillName, owner);

        PrefabInfo prefabInfo = obj.GetComponent<PrefabInfo>();

        prefabInfo.SkillDataUpdate(
            charaStat.characterStats.power,
            charaStat.characterStats.projectileSpeed,
            charaStat.characterStats.cooldown,
            charaStat.characterStats.duration
        );
    }
}