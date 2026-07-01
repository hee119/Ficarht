using Mirror;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Trap_Card : MonoBehaviour
{
    public static Trap_Card Instance { get; private set; }

    [Header("공통 쿨타임")]
    public float defaultCooldown = 5f;

    [Header("골절")]
    public float fractureMaxHealthDamageRate = 0.03f;

    [Header("무거운 발걸음")]
    public float heavyStepSlowDuration = 3f;
    public float heavyStepSpeedMultiplier = 0.5f;

    private readonly List<TrapID> activeTraps = new List<TrapID>();
    private readonly Dictionary<TrapID, float> nextReadyTimes = new Dictionary<TrapID, float>();
    private readonly List<PlayerNetwork> players = new List<PlayerNetwork>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public static Trap_Card GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        GameObject trapObject = new GameObject("Trap_Card");
        return trapObject.AddComponent<Trap_Card>();
    }

    public void InitializeFromPlayers(IEnumerable<PlayerNetwork> battlePlayers)
    {
        players.Clear();
        activeTraps.Clear();
        nextReadyTimes.Clear();

        if (battlePlayers == null)
            return;

        foreach (PlayerNetwork player in battlePlayers)
        {
            if (player == null)
                continue;

            if (!players.Contains(player))
                players.Add(player);

            foreach (TrapID trapId in player.GetRegisteredTraps())
            {
                if (trapId == TrapID.None || activeTraps.Contains(trapId))
                    continue;

                activeTraps.Add(trapId);
                nextReadyTimes[trapId] = 0f;
            }
        }

        Debug.Log(
            $"[CARD TEST][TRAP] 전투 씬 활성 함정 초기화: " +
            $"players={players.Count}, active={activeTraps.Count}, " +
            $"traps={string.Join(", ", activeTraps)}"
        );
    }

    public void NotifyJump(PlayerNetwork actor)
    {
        if (actor == null)
            return;

        Debug.Log($"[CARD TEST][TRAP] 행동 감지: Jump actor={actor.netId}");
        TryActivate(TrapID.Fracture, actor);
        TryActivate(TrapID.NaturalDisaster, actor);
    }

    public void NotifyRunStarted(PlayerNetwork actor)
    {
        if (actor == null)
            return;

        Debug.Log($"[CARD TEST][TRAP] 행동 감지: RunStarted actor={actor.netId}");
        TryActivate(TrapID.HeavyStep, actor);
        TryActivate(TrapID.LackOfFocus, actor);
        TryActivate(TrapID.Anxiety, actor);
    }

    public void NotifyAttack(PlayerNetwork actor)
    {
        if (actor == null)
            return;

        Debug.Log($"[CARD TEST][TRAP] 행동 감지: Attack actor={actor.netId}");
        TryActivate(TrapID.ThornArmor, actor);
        TryActivate(TrapID.Coward, actor);
        TryActivate(TrapID.NoViolence, actor);
        TryActivate(TrapID.LastResistance, actor);
        TryActivate(TrapID.FairWorld, actor);
        TryActivate(TrapID.Whatever, actor);
    }

    public void NotifySkillUsed(PlayerNetwork actor)
    {
        if (actor == null)
            return;

        Debug.Log($"[CARD TEST][TRAP] 행동 감지: SkillUsed actor={actor.netId}");
        TryActivate(TrapID.PositionSwap, actor);
    }

    private bool TryActivate(TrapID trapId, PlayerNetwork actor)
    {
        if (!activeTraps.Contains(trapId))
            return false;

        if (!IsReady(trapId))
        {
            Debug.Log($"[CARD TEST][TRAP] 쿨타임 중: {trapId}");
            return false;
        }

        Debug.Log($"[CARD TEST][TRAP] 발동 시도: {trapId} actor={actor.netId}");

        switch (trapId)
        {
            case TrapID.Fracture:
                ApplyFractureTrap(actor);
                break;

            case TrapID.HeavyStep:
                ApplyHeavyStepTrap(actor);
                break;

            case TrapID.Coward:
                ApplyCowardTrap(actor);
                break;

            case TrapID.ThornArmor:
                ApplyThornArmorTrap(actor);
                break;

            case TrapID.NaturalDisaster:
                ApplyNaturalDisasterTrap(actor);
                break;

            case TrapID.LastResistance:
                ApplyLastResistanceTrap(actor);
                break;

            case TrapID.NoViolence:
                ApplyNoViolenceTrap(actor);
                break;

            case TrapID.FairWorld:
                ApplyFairWorldTrap(actor);
                break;

            case TrapID.LackOfFocus:
                ApplyLackOfFocusTrap(actor);
                break;

            case TrapID.PositionSwap:
                ApplyPositionSwapTrap(actor);
                break;

            case TrapID.Anxiety:
                ApplyAnxietyTrap(actor);
                break;

            case TrapID.Whatever:
                ApplyWhateverTrap(actor);
                break;

            default:
                return false;
        }

        StartCooldown(trapId);
        Debug.Log($"[CARD TEST][TRAP] 발동 완료: {trapId}");
        return true;
    }

    private bool IsReady(TrapID trapId)
    {
        if (!nextReadyTimes.TryGetValue(trapId, out float readyTime))
            return true;

        return Time.time >= readyTime;
    }

    private void StartCooldown(TrapID trapId)
    {
        nextReadyTimes[trapId] = Time.time + GetCooldown(trapId);
    }

    private float GetCooldown(TrapID trapId)
    {
        switch (trapId)
        {
            case TrapID.Fracture:
                return 2f;

            case TrapID.HeavyStep:
                return 5f;

            case TrapID.PositionSwap:
                return 10f;

            default:
                return defaultCooldown;
        }
    }

    public void ApplyFractureTrap(PlayerNetwork actor)
    {
        float damage = actor.maxHealth * fractureMaxHealthDamageRate;
        ApplyTrueDamage(actor, damage);
    }

    public void ApplyHeavyStepTrap(PlayerNetwork actor)
    {
        ApplySlow(actor, heavyStepSlowDuration, heavyStepSpeedMultiplier);
    }

    public void ApplyCowardTrap(PlayerNetwork actor)
    {
        ApplySlow(actor, 2f, 0.7f);
    }

    public void ApplyThornArmorTrap(PlayerNetwork actor)
    {
        ApplyTrueDamage(actor, actor.maxHealth * 0.02f);
    }

    public void ApplyNaturalDisasterTrap(PlayerNetwork actor)
    {
        foreach (PlayerNetwork player in players)
        {
            if (player == null)
                continue;

            ApplyTrueDamage(player, player.maxHealth * 0.015f);
        }
    }

    public void ApplyLastResistanceTrap(PlayerNetwork actor)
    {
        if (actor.health <= actor.maxHealth * 0.3f)
            ApplyStun(actor, 1f);
    }

    public void ApplyNoViolenceTrap(PlayerNetwork actor)
    {
        ApplyStun(actor, 1f);
    }

    public void ApplyFairWorldTrap(PlayerNetwork actor)
    {
        foreach (PlayerNetwork player in players)
        {
            ApplySlow(player, 1.5f, 0.8f);
        }
    }

    public void ApplyLackOfFocusTrap(PlayerNetwork actor)
    {
        ApplyTrueDamage(actor, actor.maxHealth * 0.01f);
    }

    public void ApplyPositionSwapTrap(PlayerNetwork actor)
    {
        if (players.Count < 2)
            return;

        Transform first = players[0]?.currentCharacter != null
            ? players[0].currentCharacter.transform
            : players[0]?.transform;

        Transform second = players[1]?.currentCharacter != null
            ? players[1].currentCharacter.transform
            : players[1]?.transform;

        if (first == null || second == null)
            return;

        Vector3 tempPosition = first.position;
        first.position = second.position;
        second.position = tempPosition;
    }

    public void ApplyAnxietyTrap(PlayerNetwork actor)
    {
        ApplySlow(actor, 2f, 0.75f);
    }

    public void ApplyWhateverTrap(PlayerNetwork actor)
    {
        int randomEffect = Random.Range(0, 3);

        switch (randomEffect)
        {
            case 0:
                ApplyTrueDamage(actor, actor.maxHealth * 0.015f);
                break;

            case 1:
                ApplySlow(actor, 2f, 0.75f);
                break;

            case 2:
                ApplyStun(actor, 0.5f);
                break;
        }
    }

    private void ApplyTrueDamage(PlayerNetwork actor, float damage)
    {
        if (actor == null || actor.isDead)
            return;

        if (NetworkServer.active)
        {
            actor.TakeTrueDamage(damage);
            return;
        }

        if (NetworkClient.active)
            return;

        actor.health = Mathf.Max(0f, actor.health - damage);
        SyncLocalCharaHealth(actor);

        if (actor.health <= 0f)
            actor.isDead = true;

        Debug.Log($"[CARD TEST][SINGLE][TRAP] 고정 피해 {damage:F1} / 남은 HP {actor.health:F1}");
    }

    private void ApplySlow(PlayerNetwork actor, float duration, float multiplier)
    {
        if (actor == null || actor.isDead)
            return;

        if (NetworkServer.active)
        {
            actor.ApplySlow(duration, multiplier);
            return;
        }

        if (NetworkClient.active)
            return;

        actor.currentState = PlayerStateType.Slow;
        GetLocalController(actor)?.ApplyTemporarySpeedMultiplier(multiplier, duration);
        StartCoroutine(ClearLocalStateAfter(actor, duration, PlayerStateType.Slow));

        Debug.Log($"[CARD TEST][SINGLE][TRAP] 슬로우 적용: {duration:F1}s x{multiplier:F2}");
    }

    private void ApplyStun(PlayerNetwork actor, float duration)
    {
        if (actor == null || actor.isDead)
            return;

        if (NetworkServer.active)
        {
            actor.ApplyStun(duration);
            return;
        }

        if (NetworkClient.active)
            return;

        actor.currentState = PlayerStateType.Stun;
        CharaStat charaStat = GetLocalCharaStat(actor);

        if (charaStat != null && charaStat.playerInput != null)
            charaStat.playerInput.enabled = false;

        StartCoroutine(ClearLocalStateAfter(actor, duration, PlayerStateType.Stun));

        Debug.Log($"[CARD TEST][SINGLE][TRAP] 스턴 적용: {duration:F1}s");
    }

    private IEnumerator ClearLocalStateAfter(
        PlayerNetwork actor,
        float duration,
        PlayerStateType state
    )
    {
        yield return new WaitForSeconds(duration);

        if (actor == null || actor.currentState != state)
            yield break;

        actor.currentState = PlayerStateType.Normal;

        CharaStat charaStat = GetLocalCharaStat(actor);

        if (charaStat != null && charaStat.playerInput != null)
            charaStat.playerInput.enabled = true;
    }

    private void SyncLocalCharaHealth(PlayerNetwork actor)
    {
        CharaStat charaStat = GetLocalCharaStat(actor);

        if (charaStat == null)
            return;

        charaStat.maxHealth = Mathf.Max(actor.maxHealth, 1f);
        charaStat.health = Mathf.Clamp(actor.health, 0f, charaStat.maxHealth);

        if (charaStat.healthBar != null)
        {
            charaStat.healthBar.maxValue = charaStat.maxHealth;
            charaStat.healthBar.value = charaStat.health;
        }
    }

    private CharaStat GetLocalCharaStat(PlayerNetwork actor)
    {
        if (actor == null)
            return null;

        CharaStat charaStat = actor.GetComponent<CharaStat>();

        if (charaStat == null && actor.currentCharacter != null)
            charaStat = actor.currentCharacter.GetComponent<CharaStat>();

        return charaStat;
    }

    private PlayerController GetLocalController(PlayerNetwork actor)
    {
        if (actor == null)
            return null;

        PlayerController controller = actor.GetComponent<PlayerController>();

        if (controller == null && actor.currentCharacter != null)
            controller = actor.currentCharacter.GetComponent<PlayerController>();

        return controller;
    }
}
