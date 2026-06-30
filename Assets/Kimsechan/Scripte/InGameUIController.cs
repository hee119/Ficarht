using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(UIDocument))]
public class InGameUIController : MonoBehaviour
{
    public static InGameUIController Instance { get; private set; }

    private const int MaxTrapCards = 4;

    [Header("카드 이미지")]
    public Sprite enemyRedCardBackSprite;
    public List<CardData> trapCardDatabase = new List<CardData>();

    [Header("카드 사용 애니메이션")]
    public float usedYOffset = 18f;
    public float animationDuration = 0.16f;
    public float resetDelay = 0.35f;

    [Header("HP 바")]
    public float lowBarDelay = 0.25f;
    public float lowBarFollowSpeed = 80f;

    private readonly List<VisualElement> myTrapSlots = new List<VisualElement>();
    private readonly List<VisualElement> myTrapCards = new List<VisualElement>();
    private readonly List<VisualElement> enemyTrapSlots = new List<VisualElement>();
    private readonly List<VisualElement> enemyTrapCards = new List<VisualElement>();
    private readonly float[] myTrapOffsets = new float[MaxTrapCards];
    private readonly float[] enemyTrapOffsets = new float[MaxTrapCards];
    private readonly Coroutine[] myMoveRoutines = new Coroutine[MaxTrapCards];
    private readonly Coroutine[] enemyMoveRoutines = new Coroutine[MaxTrapCards];
    private readonly Dictionary<int, Sprite> trapSpriteLookup = new Dictionary<int, Sprite>();

    private Label playerName;
    private Label playerHpText;
    private VisualElement playerHpFill;
    private VisualElement playerHpLowFill;
    private VisualElement playerHpDangerFill;
    private PlayerNetwork myNetworkPlayer;
    private PlayerController singlePlayer;
    private float searchTimer;
    private int myPulseIndex;
    private int enemyPulseIndex;
    private float displayedHpPercent = 100f;
    private float lowHpPercent = 100f;
    private float lowBarDelayTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        playerName = root.Q<Label>("player-name");
        playerHpText = root.Q<Label>("player-hp-text");
        playerHpFill = root.Q<VisualElement>("player-hp-fill");
        playerHpLowFill = root.Q<VisualElement>("player-hp-low-fill");
        playerHpDangerFill = root.Q<VisualElement>("player-hp-danger-fill");

        for (int i = 0; i < MaxTrapCards; i++)
        {
            myTrapSlots.Add(root.Q<VisualElement>($"my-trap-slot-{i}"));
            myTrapCards.Add(root.Q<VisualElement>($"my-trap-card-{i}"));
            enemyTrapSlots.Add(root.Q<VisualElement>($"enemy-trap-slot-{i}"));
            enemyTrapCards.Add(root.Q<VisualElement>($"enemy-trap-card-{i}"));
        }

        ApplyEnemyCardBacks();
        BuildTrapSpriteLookup();
        RefreshMyTrapCardsFromSelection();
    }

    private void Update()
    {
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            searchTimer = 0.5f;
            FindPlayerTarget();
        }

        RefreshMyTrapCardsFromSelection();
        RefreshPlayerUI();
    }

    public void RefreshMyTrapCardsFromSelection()
    {
        List<Sprite> selectedSprites = FindSelectedTrapSprites();

        for (int i = 0; i < MaxTrapCards; i++)
        {
            VisualElement slot = myTrapSlots[i];
            VisualElement card = myTrapCards[i];
            bool hasCard = i < selectedSprites.Count && selectedSprites[i] != null;

            if (slot != null)
                slot.style.display = hasCard ? DisplayStyle.Flex : DisplayStyle.None;

            if (card != null && hasCard)
                card.style.backgroundImage = new StyleBackground(selectedSprites[i]);
        }
    }

    public void PlayTrapUsed(PlayerNetwork actor)
    {
        bool actorIsMine = actor != null && actor.isOwned;

        if (actorIsMine)
            PulseEnemyTrap();
        else
            PulseMyTrap();
    }

    public void PulseMyTrap()
    {
        int index = GetNextVisibleIndex(myTrapSlots, ref myPulseIndex);
        PulseTrap(myTrapSlots, myTrapOffsets, myMoveRoutines, index);
    }

    public void PulseEnemyTrap()
    {
        int index = GetNextVisibleIndex(enemyTrapSlots, ref enemyPulseIndex);
        PulseTrap(enemyTrapSlots, enemyTrapOffsets, enemyMoveRoutines, index);
    }

    private void ApplyEnemyCardBacks()
    {
        for (int i = 0; i < MaxTrapCards; i++)
        {
            VisualElement slot = enemyTrapSlots[i];
            VisualElement card = enemyTrapCards[i];

            if (slot != null)
                slot.style.display = DisplayStyle.Flex;

            if (card != null && enemyRedCardBackSprite != null)
                card.style.backgroundImage = new StyleBackground(enemyRedCardBackSprite);
        }
    }

    private List<Sprite> FindSelectedTrapSprites()
    {
        List<Sprite> sprites = new List<Sprite>();
        List<int> trapIds = FindPlayerTrapIds();

        foreach (int trapId in trapIds)
        {
            Sprite sprite = FindTrapSpriteById(trapId);
            if (sprite != null)
                sprites.Add(sprite);

            if (sprites.Count >= MaxTrapCards)
                return sprites;
        }

        CardSystemManager manager = CardSystemManager.Instance;

        if (manager != null)
        {
            foreach (CardSlot slot in manager.trapSlots)
            {
                CardData data = slot?.currentCard?.data;
                if (data == null || data.cardType != CardType.Trap || data.cardImage == null)
                    continue;

                sprites.Add(data.cardImage);
                if (sprites.Count >= MaxTrapCards)
                    return sprites;
            }
        }

        bool hasLocalNetworkTraps = NetworkClient.active && PlayerPrefs.GetInt("LocalPlayer_TrapCount", 0) > 0;
        string prefsPrefix = hasLocalNetworkTraps ? "LocalPlayer" : "SinglePlayer";

        int trapCount = Mathf.Min(PlayerPrefs.GetInt($"{prefsPrefix}_TrapCount", 0), MaxTrapCards);
        for (int i = 0; i < trapCount; i++)
        {
            int trapId = PlayerPrefs.GetInt($"{prefsPrefix}_Trap_{i}", 0);
            Sprite sprite = FindTrapSpriteById(trapId);

            if (sprite != null)
                sprites.Add(sprite);
        }

        return sprites;
    }

    private List<int> FindPlayerTrapIds()
    {
        List<int> trapIds = new List<int>();

        if (myNetworkPlayer != null)
        {
            foreach (TrapID trapId in myNetworkPlayer.GetRegisteredTraps())
            {
                if (trapId != TrapID.None)
                    trapIds.Add((int)trapId);

                if (trapIds.Count >= MaxTrapCards)
                    return trapIds;
            }
        }

        return trapIds;
    }

    private Sprite FindTrapSpriteById(int trapId)
    {
        if (trapId == 0)
            return null;

        if (trapSpriteLookup.TryGetValue(trapId, out Sprite sprite) && sprite != null)
            return sprite;

        BuildTrapSpriteLookup();

        if (trapSpriteLookup.TryGetValue(trapId, out sprite) && sprite != null)
            return sprite;

        return null;
    }

    private void BuildTrapSpriteLookup()
    {
        trapSpriteLookup.Clear();

        AddTrapCardsToLookup(trapCardDatabase);

        if (CardSystemManager.Instance != null)
            AddTrapCardsToLookup(CardSystemManager.Instance.trapDeck);

        AddTrapCardsToLookup(Resources.FindObjectsOfTypeAll<CardData>());

#if UNITY_EDITOR
        foreach (string guid in AssetDatabase.FindAssets("t:CardData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            CardData data = AssetDatabase.LoadAssetAtPath<CardData>(path);
            AddTrapCardToLookup(data);
        }
#endif
    }

    private void AddTrapCardsToLookup(IEnumerable<CardData> cards)
    {
        if (cards == null)
            return;

        foreach (CardData data in cards)
            AddTrapCardToLookup(data);
    }

    private void AddTrapCardToLookup(CardData data)
    {
        if (data == null || data.cardType != CardType.Trap || data.cardImage == null)
            return;

        TrapID trapId = data.GetTrapID();
        if (trapId == TrapID.None)
            return;

        int key = (int)trapId;
        if (!trapSpriteLookup.ContainsKey(key))
            trapSpriteLookup.Add(key, data.cardImage);
    }

    private int GetNextVisibleIndex(List<VisualElement> slots, ref int cursor)
    {
        for (int i = 0; i < MaxTrapCards; i++)
        {
            int index = (cursor + i) % MaxTrapCards;
            VisualElement slot = slots[index];

            if (slot != null && slot.resolvedStyle.display != DisplayStyle.None)
            {
                cursor = (index + 1) % MaxTrapCards;
                return index;
            }
        }

        return 0;
    }

    private void PulseTrap(
        List<VisualElement> slots,
        float[] offsets,
        Coroutine[] routines,
        int index
    )
    {
        if (index < 0 || index >= MaxTrapCards || slots[index] == null)
            return;

        if (routines[index] != null)
            StopCoroutine(routines[index]);

        routines[index] = StartCoroutine(PulseTrapRoutine(slots[index], offsets, routines, index));
    }

    private IEnumerator PulseTrapRoutine(
        VisualElement slot,
        float[] offsets,
        Coroutine[] routines,
        int index
    )
    {
        yield return AnimateTrapOffset(slot, offsets, index, usedYOffset);
        yield return new WaitForSeconds(resetDelay);
        yield return AnimateTrapOffset(slot, offsets, index, 0f);

        routines[index] = null;
    }

    private IEnumerator AnimateTrapOffset(
        VisualElement slot,
        float[] offsets,
        int index,
        float target
    )
    {
        float start = offsets[index];
        float elapsed = 0f;
        float duration = Mathf.Max(animationDuration, 0.01f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            float value = Mathf.Lerp(start, target, eased);

            offsets[index] = value;
            slot.style.translate = new Translate(0f, value, 0f);

            yield return null;
        }

        offsets[index] = target;
        slot.style.translate = new Translate(0f, target, 0f);
    }

    private void FindPlayerTarget()
    {
        if (NetworkClient.active)
        {
            foreach (PlayerNetwork player in FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None))
            {
                if (player.isOwned)
                {
                    myNetworkPlayer = player;
                    break;
                }
            }
        }
        else if (singlePlayer == null)
        {
            singlePlayer = FindAnyObjectByType<PlayerController>();
            if (singlePlayer != null)
                myNetworkPlayer = singlePlayer.GetComponent<PlayerNetwork>();
        }
    }

    private void RefreshPlayerUI()
    {
        if (NetworkClient.active && myNetworkPlayer != null)
        {
            SetPlayerName(myNetworkPlayer.name);
            SetHp(myNetworkPlayer.health, myNetworkPlayer.maxHealth);
            return;
        }

        if (singlePlayer == null)
            return;

        CharaStat stat = singlePlayer.GetComponent<CharaStat>();
        if (stat == null)
            return;

        string displayName = stat.characterStats != null
            ? stat.characterStats.characterName
            : singlePlayer.name;

        SetPlayerName(displayName);
        SetHp(stat.health, stat.maxHealth);
    }

    private void SetPlayerName(string displayName)
    {
        if (playerName != null)
            playerName.text = string.IsNullOrEmpty(displayName) ? "PLAYER" : displayName;
    }

    private void SetHp(float current, float max)
    {
        float safeMax = Mathf.Max(max, 1f);
        float percent = Mathf.Clamp01(current / safeMax) * 100f;

        if (percent < displayedHpPercent)
        {
            lowBarDelayTimer = lowBarDelay;
        }
        else if (percent > lowHpPercent)
        {
            lowHpPercent = percent;
        }

        displayedHpPercent = percent;

        if (playerHpFill != null)
            playerHpFill.style.width = new StyleLength(new Length(percent, LengthUnit.Percent));

        if (playerHpDangerFill != null)
            playerHpDangerFill.style.display = percent <= 22f ? DisplayStyle.Flex : DisplayStyle.None;

        UpdateLowHpBar(percent);

        if (playerHpText != null)
            playerHpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(safeMax)}";
    }

    private void UpdateLowHpBar(float targetPercent)
    {
        if (playerHpLowFill == null)
            return;

        if (lowBarDelayTimer > 0f)
        {
            lowBarDelayTimer -= Time.deltaTime;
        }
        else
        {
            lowHpPercent = Mathf.MoveTowards(
                lowHpPercent,
                targetPercent,
                lowBarFollowSpeed * Time.deltaTime
            );
        }

        if (lowHpPercent < targetPercent)
            lowHpPercent = targetPercent;

        playerHpLowFill.style.width = new StyleLength(new Length(lowHpPercent, LengthUnit.Percent));
    }
}
