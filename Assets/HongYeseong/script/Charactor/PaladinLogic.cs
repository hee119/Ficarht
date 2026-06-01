using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaladinLogic : MonoBehaviour, ICharacterSkill
{
    CharaStat charaStat;
    public List<GameObject> SkillPrefabs = new List<GameObject>();
    
    void Awake()
    {
        charaStat = GetComponent<CharaStat>();
    }
    public void UseSkill(string skillName, Transform owner)
    {
        PrefabInfo prefabInfo = PoolManager.Instance.GetPrefab(skillName, owner).GetComponent<PrefabInfo>();
        prefabInfo.SkillDataUpdate(charaStat.characterStats.power, charaStat.characterStats.projectileSpeed, charaStat.characterStats.cooldown, charaStat.characterStats.duration);
        switch (skillName)
        {
            case "Defense_Boost":
                Defense_Boost(prefabInfo);
                break;
            case "SlowDown":
                SlowDown(prefabInfo);
                break;
            case "Default_Slash":
                
                break;
        }
    }

    private void Defense_Boost(PrefabInfo prefabInfo)
    {
        charaStat.characterStats.defense += prefabInfo.defense;
    }
    
    private void SlowDown(PrefabInfo prefabInfo)
    {
        
    }   
}
