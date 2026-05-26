using UnityEngine;

[CreateAssetMenu(fileName = "SkillData", menuName = "Game/Skill Data")]
public class SkillData : ScriptableObject
{
    public string skillId;

    public float attack;
    public float speed;
    public float cooldown;
    public float coolTime;
    public float defense;
    public float duration;
}