using Mirror;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class PlayerHpBarUIController : MonoBehaviour
{
    [Header("타겟 탐색")]
    public float searchInterval = 0.5f;

    [Header("HP 연출")]
    public float lowBarDelay = 0.25f;
    public float lowBarFollowSpeed = 80f;
    public float dangerPercent = 22f;

    private Label playerName;
    private Label playerHpText;
    private VisualElement playerHpFill;
    private VisualElement playerHpLowFill;
    private VisualElement playerHpDangerFill;

    private PlayerNetwork myNetworkPlayer;
    private CharaStat localCharaStat;
    private float searchTimer;
    private float displayedHpPercent = 100f;
    private float lowHpPercent = 100f;
    private float lowBarDelayTimer;

    private void Awake()
    {
        VisualElement root = GetComponent<UIDocument>().rootVisualElement;

        playerName = root.Q<Label>("player-name");
        playerHpText = root.Q<Label>("player-hp-text");
        playerHpFill = root.Q<VisualElement>("player-hp-fill");
        playerHpLowFill = root.Q<VisualElement>("player-hp-low-fill");
        playerHpDangerFill = root.Q<VisualElement>("player-hp-danger-fill");

        SetHp(100f, 100f);
    }

    private void Update()
    {
        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            searchTimer = Mathf.Max(searchInterval, 0.05f);
            FindMyPlayer();
        }

        RefreshHpBar();
    }

    public void SetTarget(PlayerNetwork player)
    {
        myNetworkPlayer = player;
        localCharaStat = player != null ? player.GetComponent<CharaStat>() : null;
        RefreshName();
        RefreshHpBar();
    }

    private void FindMyPlayer()
    {
        if (NetworkClient.active)
        {
            foreach (PlayerNetwork player in FindObjectsByType<PlayerNetwork>(FindObjectsSortMode.None))
            {
                if (!player.isOwned)
                    continue;

                SetTarget(player);
                return;
            }

            myNetworkPlayer = null;
            localCharaStat = null;
            return;
        }

        PlayerController singlePlayer = FindAnyObjectByType<PlayerController>();
        localCharaStat = singlePlayer != null ? singlePlayer.GetComponent<CharaStat>() : null;
        myNetworkPlayer = localCharaStat != null ? localCharaStat.GetComponent<PlayerNetwork>() : null;
        RefreshName();
    }

    private void RefreshHpBar()
    {
        if (NetworkClient.active)
        {
            if (myNetworkPlayer == null)
                return;

            RefreshName();
            SetHp(myNetworkPlayer.health, myNetworkPlayer.maxHealth);
            return;
        }

        if (localCharaStat == null)
            return;

        RefreshName();
        SetHp(localCharaStat.health, localCharaStat.maxHealth);
    }

    private void RefreshName()
    {
        if (playerName == null)
            return;

        string displayName = GetDisplayName();
        playerName.text = string.IsNullOrEmpty(displayName) ? "PLAYER" : displayName;
    }

    private string GetDisplayName()
    {
        CharaStat stat = null;

        if (myNetworkPlayer != null)
        {
            if (myNetworkPlayer.currentCharacter != null)
                stat = myNetworkPlayer.currentCharacter.GetComponent<CharaStat>();

            if (stat == null)
                stat = myNetworkPlayer.GetComponent<CharaStat>();

            if (stat != null && stat.characterStats != null)
                return stat.characterStats.characterName;

            return myNetworkPlayer.name;
        }

        if (localCharaStat != null)
        {
            if (localCharaStat.characterStats != null)
                return localCharaStat.characterStats.characterName;

            return localCharaStat.name;
        }

        return "PLAYER";
    }

    private void SetHp(float current, float max)
    {
        float safeMax = Mathf.Max(max, 1f);
        float safeCurrent = Mathf.Clamp(current, 0f, safeMax);
        float percent = Mathf.Clamp01(safeCurrent / safeMax) * 100f;

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
            playerHpDangerFill.style.display = percent <= dangerPercent ? DisplayStyle.Flex : DisplayStyle.None;

        UpdateLowHpBar(percent);

        if (playerHpText != null)
            playerHpText.text = $"{Mathf.CeilToInt(safeCurrent)} / {Mathf.CeilToInt(safeMax)}";
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
