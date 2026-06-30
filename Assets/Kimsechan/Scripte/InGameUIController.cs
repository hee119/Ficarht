using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class InGameUIController : MonoBehaviour
{
    public static InGameUIController Instance { get; private set; }

    [Header("카드 이미지")]
    public Sprite enemyRedCardBackSprite;
    public List<CardData> trapCardDatabase = new List<CardData>();

    [Header("포스트잇 애니메이션")]
    public float usedYOffset = 18f;
    public float animationDuration = 0.16f;

    private VisualElement myTrapNote;
    private VisualElement enemyTrapNote;
    private VisualElement myTrapCard;
    private VisualElement enemyTrapCard;
    private Label playerName;
    private Label playerHpText;
    private VisualElement playerHpFill;

    private PlayerNetwork myNetworkPlayer;
    private PlayerController singlePlayer;
    private float searchTimer;
    private bool myTrapUsed;
    private bool enemyTrapUsed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        VisualElement root = GetComponent<UIDocument>().rootVisualElement;
        myTrapNote = root.Q<VisualElement>("my-trap-note");
        enemyTrapNote = root.Q<VisualElement>("enemy-trap-note");
        myTrapCard = root.Q<VisualElement>("my-trap-card");
        enemyTrapCard = root.Q<VisualElement>("enemy-trap-card");
        playerName = root.Q<Label>("player-name");
        playerHpText = root.Q<Label>("player-hp-text");
        playerHpFill = root.Q<VisualElement>("player-hp-fill");

        ApplyEnemyCardBack();
        RefreshMyTrapCardFromSelection();
        SetMyTrapUsed(false);
        SetEnemyTrapUsed(false);
    }

    private void Update()
    {
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            searchTimer = 0.5f;
            FindPlayerTarget();
        }

        RefreshPlayerUI();
    }

    public void RefreshMyTrapCardFromSelection()
    {
        Sprite selectedSprite = FindSelectedTrapSprite();

        if (selectedSprite != null && myTrapCard != null)
            myTrapCard.style.backgroundImage = new StyleBackground(selectedSprite);
    }

    public void SetMyTrapUsed(bool used)
    {
        myTrapUsed = used;
        AnimateTrapNote(myTrapNote, used);
    }

    public void SetEnemyTrapUsed(bool used)
    {
        enemyTrapUsed = used;
        AnimateTrapNote(enemyTrapNote, used);
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
        StopCoroutine(nameof(ResetMyTrapRoutine));
        SetMyTrapUsed(true);
        StartCoroutine(nameof(ResetMyTrapRoutine));
    }

    public void PulseEnemyTrap()
    {
        StopCoroutine(nameof(ResetEnemyTrapRoutine));
        SetEnemyTrapUsed(true);
        StartCoroutine(nameof(ResetEnemyTrapRoutine));
    }

    private System.Collections.IEnumerator ResetMyTrapRoutine()
    {
        yield return new WaitForSeconds(0.35f);
        SetMyTrapUsed(false);
    }

    private System.Collections.IEnumerator ResetEnemyTrapRoutine()
    {
        yield return new WaitForSeconds(0.35f);
        SetEnemyTrapUsed(false);
    }

    private void ApplyEnemyCardBack()
    {
        if (enemyTrapCard == null)
            return;

        if (enemyRedCardBackSprite != null)
            enemyTrapCard.style.backgroundImage = new StyleBackground(enemyRedCardBackSprite);
    }

    private Sprite FindSelectedTrapSprite()
    {
        CardSystemManager manager = CardSystemManager.Instance;

        if (manager != null)
        {
            foreach (CardSlot slot in manager.trapSlots)
            {
                CardData data = slot?.currentCard?.data;
                if (data != null && data.cardType == CardType.Trap && data.cardImage != null)
                    return data.cardImage;
            }
        }

        int trapId = PlayerPrefs.GetInt("SinglePlayer_Trap_0", 0);
        if (trapId == 0)
            return null;

        foreach (CardData data in trapCardDatabase)
        {
            if (data != null && data.GetTrapID() == (TrapID)trapId)
                return data.cardImage;
        }

        return null;
    }

    private void AnimateTrapNote(VisualElement note, bool used)
    {
        if (note == null)
            return;

        note.style.transitionDuration = new List<TimeValue> { new TimeValue(animationDuration) };
        note.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("translate") };
        note.style.translate = new Translate(0f, used ? usedYOffset : 0f, 0f);
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

        if (playerHpFill != null)
            playerHpFill.style.width = new StyleLength(new Length(percent, LengthUnit.Percent));

        if (playerHpText != null)
            playerHpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(safeMax)}";
    }
}
