using System;
using UnityEngine;
using UnityEngine.Serialization;

public class PrefabInfo : MonoBehaviour
{
    public SkillData skillData;
    
    public float power;
    public float speed;
    public float cooldown;
    public float coolTime;
    public float defense;
    public float duration;
    public bool isBuff;
    public bool isDebuff;


    public void Awake()
    {
        Init();
    }

    public void OnEnable()
    {
        Init();
    }

    public void Init()
    {
        power = skillData.attack;
        speed = skillData.speed;
        cooldown = skillData.cooldown;
        coolTime = skillData.coolTime;
        defense = skillData.defense;
        duration = skillData.duration;
    }
    public void SkillDataUpdate(float _attack, float _speed, float _cooldown, float _duration)
    {
        power += _attack;
        speed += _speed;
        cooldown += _cooldown;
        duration += _duration;
    }
}