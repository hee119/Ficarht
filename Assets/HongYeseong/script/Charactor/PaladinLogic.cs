using UnityEngine;

public class PaladinLogic : MonoBehaviour, ICharacterSkill
{
    CharaStat charaStat;
    
    void Awake()
    {
        charaStat = GetComponent<CharaStat>();
    }
    public void UseSkill(string skillName, Transform owner)
    {
        PrefabInfo prefabInfo = PoolManager.Instance.GetPrefab(skillName, owner).GetComponent<PrefabInfo>();
        prefabInfo.SkillDataUpdate(charaStat.characterStats.power, charaStat.characterStats.projectileSpeed, charaStat.characterStats.cooldown, charaStat.characterStats.duration);
    }
}
