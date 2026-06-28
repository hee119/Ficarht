using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterStats", menuName = "Stats/Character Stats")]
public class CharacterStats : ScriptableObject
{
    public string characterName;

    [Header("Stats")] public int health; // 체력
    public float stamina; // 스테미너
    public float power; // 힘
    public float defense; // 방어력
    public float intelligence; // 지식
    public float speed; // 속도
    public float runSpeed; // 속도
    public float projectileSpeed;
    public float cooldown;
    public float duration;

    [Header("Player")] public GameObject player;
}