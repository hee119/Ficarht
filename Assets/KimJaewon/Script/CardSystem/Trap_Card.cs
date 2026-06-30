using Mirror;
using UnityEngine;
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
        InGameUIController.Instance?.PlayTrapUsed(actor);
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
        actor.TakeTrueDamage(damage);
    }

    public void ApplyHeavyStepTrap(PlayerNetwork actor)
    {
        actor.ApplySlow(heavyStepSlowDuration, heavyStepSpeedMultiplier);
    }

    public void ApplyCowardTrap(PlayerNetwork actor)
    {
        actor.ApplySlow(2f, 0.7f);
    }

    public void ApplyThornArmorTrap(PlayerNetwork actor)
    {
        actor.TakeTrueDamage(actor.maxHealth * 0.02f);
    }

    public void ApplyNaturalDisasterTrap(PlayerNetwork actor)
    {
        foreach (PlayerNetwork player in players)
        {
            player?.TakeTrueDamage(player.maxHealth * 0.015f);
        }
    }

    public void ApplyLastResistanceTrap(PlayerNetwork actor)
    {
        if (actor.health <= actor.maxHealth * 0.3f)
            actor.ApplyStun(1f);
    }

    public void ApplyNoViolenceTrap(PlayerNetwork actor)
    {
        actor.ApplyStun(1f);
    }

    public void ApplyFairWorldTrap(PlayerNetwork actor)
    {
        foreach (PlayerNetwork player in players)
        {
            player?.ApplySlow(1.5f, 0.8f);
        }
    }

    public void ApplyLackOfFocusTrap(PlayerNetwork actor)
    {
        actor.TakeTrueDamage(actor.maxHealth * 0.01f);
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
        actor.ApplySlow(2f, 0.75f);
    }

    public void ApplyWhateverTrap(PlayerNetwork actor)
    {
        int randomEffect = Random.Range(0, 3);

        switch (randomEffect)
        {
            case 0:
                actor.TakeTrueDamage(actor.maxHealth * 0.015f);
                break;

            case 1:
                actor.ApplySlow(2f, 0.75f);
                break;

            case 2:
                actor.ApplyStun(0.5f);
                break;
        }
    }
}
