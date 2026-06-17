using UnityEngine;

/// <summary>
/// 캐릭터 기본 스탯 ScriptableObject.
/// 수치는 인스펙터에서 직접 입력하세요.
/// </summary>
[CreateAssetMenu(fileName = "NewCharacterStats", menuName = "Ficarght/CharacterStats")]
public class CharacterCardStats : ScriptableObject
{
    [Header("캐릭터 정보")]
    public string characterName;

    [Header("캐릭터 프리팹 인덱스 (GameNetworkManager.characterPrefabs 순서)")]
    [Tooltip("0=Paladin, 1=Bard, 2=Berserker, 3=Mage")]
    public int characterId;

    [Header("기본 스탯 (0 ~ 100)")]
    [Range(0, 100)] public float health;       // 체력
    [Range(0, 100)] public float stamina;      // 스테미너
    [Range(0, 100)] public float power;        // 힘
    [Range(0, 100)] public float defense;      // 방어력
    [Range(0, 100)] public float intelligence; // 지식

    /// <summary>
    /// 전투용 가변 복사본 생성 (원본 SO는 절대 수정 안 함)
    /// </summary>
    public RuntimeStats CreateRuntime() => new RuntimeStats(this);
}