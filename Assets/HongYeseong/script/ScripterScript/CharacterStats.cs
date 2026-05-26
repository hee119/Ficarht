using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterStats", menuName = "Stats/Character Stats")]
public class CharacterStats : ScriptableObject
{
    public string characterName;

    [Header("Stats")] public int health; // 체력
    public int stamina; // 스테미너
    public int power; // 힘
    public int defense; // 방어력
    public int intelligence; // 지식
    public float speed; // 속도
    public float projectileSpeed;
    public float cooldown;
    public float duration;

    [Header("Player")] public GameObject player;
}