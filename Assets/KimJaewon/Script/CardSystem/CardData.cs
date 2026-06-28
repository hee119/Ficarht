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

public enum TrapID
{
    None = 0,
    Fracture = 1,
    HeavyStep = 2,
    ThornArmor = 3,
    Coward = 4,
    NoViolence = 5,
    LastResistance = 6,
    LackOfFocus = 7,
    PositionSwap = 8,
    Anxiety = 9,
    Whatever = 10,
    NaturalDisaster = 11,
    FairWorld = 12
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

    [TextArea(3, 6)]
    public string cardDescription;

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

    [Header("함정 카드 전용")]
    public TrapID trapID;

    [Header("맵 카드 전용")]
    [Tooltip("이동할 전투 씬 이름 (예: Forest, Desert, Snow)")]
    public string mapSceneName;

    public TrapID GetTrapID()
    {
        if (trapID != TrapID.None)
            return trapID;

        switch (cardID)
        {
            case 1:
                return TrapID.Fracture;

            case 2:
                return TrapID.HeavyStep;

            case 3:
                return TrapID.ThornArmor;

            case 4:
                return TrapID.Coward;

            case 5:
                return TrapID.NoViolence;

            case 6:
                return TrapID.LastResistance;

            case 7:
                return TrapID.LackOfFocus;

            case 8:
                return TrapID.PositionSwap;

            case 9:
                return TrapID.Anxiety;

            case 10:
                return TrapID.Whatever;

            case 11:
                return TrapID.NaturalDisaster;

            case 12:
                return TrapID.FairWorld;

            default:
                return TrapID.None;
        }
    }
}
