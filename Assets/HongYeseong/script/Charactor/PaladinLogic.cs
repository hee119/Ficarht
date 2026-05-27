using System.Collections;
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
        switch (skillName)
        {
            case "Defense_Boost":
                Defense_Boost(prefabInfo);
                DisableAfterDuration(prefabInfo, prefabInfo.duration, prefabInfo.defense, 0, 0, 0);
                break;
            case "SlowDown":
                DisableAfterDuration(prefabInfo, prefabInfo.duration, 0, 0, prefabInfo.speed, 0 );
                break;
            case "Default_Slash":
                
                break;
        }
    }

    private void Defense_Boost(PrefabInfo prefabInfo)
    {
        charaStat.characterStats.defense += prefabInfo.defense;
    }
    private IEnumerator DisableAfterDuration(PrefabInfo prefabInfo, float duration, float defense, float cooldown, float speed, float power)
    {
        yield return new WaitForSeconds(duration);

        if (prefabInfo != null)
        { 
            charaStat.characterStats.defense -= defense;
            charaStat.characterStats.duration -= duration;
            charaStat.characterStats.cooldown -= cooldown;
            charaStat.characterStats.projectileSpeed += speed;
            charaStat.characterStats.power -= power;
            PoolManager.Instance.Release(prefabInfo.skillData.skillId, prefabInfo.gameObject);
        }
    }
}
