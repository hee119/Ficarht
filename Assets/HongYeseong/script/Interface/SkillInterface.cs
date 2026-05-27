using UnityEngine;

public interface ICharacterSkill
{
    void UseSkill(string skillName, Transform owner);
}