using System;
using UnityEngine;

public class BardSkillLogic : MonoBehaviour
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
    
    

    public void OnTriggerEnter(Collider other)
    {
        switch (skillType)
        {
            
        }
    }
}
