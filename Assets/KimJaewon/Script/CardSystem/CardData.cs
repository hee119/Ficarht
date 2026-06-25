using UnityEngine;

public enum CardType
{
    Character,
    Buff,
    Skill,
    Trap,
    Map
}

public enum SkillID
{
    HolyAura,
    HolySword,
    HandOfGod,

    LifeForDeath,
    Rage,
    BloodAxe,

    IceMagic,
    Flame,
    MagicCircle,

    JoyfulSong,
    Tuning,
    AggressiveSong
}

[System.Serializable]
public class BuffEffect
{
    public float healthMod;

    public float staminaMod;

    public float powerMod;

    public float defenseMod;

    public float intelligenceMod;
}

[CreateAssetMenu(
    fileName = "NewCard",
    menuName = "Ficarght/CardData"
)]
public class CardData : ScriptableObject
{
    [Header("공통")]
    public int cardID;

    public string cardName;

    public CardType cardType;

    public Sprite cardImage;

    // 모든 카드 공용 프리팹
    public GameObject cardPrefab;

    [Header("캐릭터 카드 전용")]
    public CharacterCardStats characterStats;

    [Header("버프 카드 전용")]
    public BuffEffect buffEffect;

    [Header("스킬 카드 전용")]
    public SkillID skillID;

    [Header("맵 카드 전용")]
    [Tooltip("이동할 전투 씬 이름 (예: Forest, Desert, Snow)")]
    public string mapSceneName;
}