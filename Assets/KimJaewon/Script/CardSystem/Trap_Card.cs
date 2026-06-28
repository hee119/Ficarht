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

    [Server]
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

        Debug.Log($"[Trap_Card] 활성 함정 {activeTraps.Count}개 초기화");
    }

    [Server]
    public void NotifyJump(PlayerNetwork actor)
    {
        if (actor == null)
            return;

        TryActivate(TrapID.Fracture, actor);
        TryActivate(TrapID.NaturalDisaster, actor);
    }

    [Server]
    public void NotifyRunStarted(PlayerNetwork actor)
    {
        if (actor == null)
            return;

        TryActivate(TrapID.HeavyStep, actor);
        TryActivate(TrapID.LackOfFocus, actor);
        TryActivate(TrapID.Anxiety, actor);
    }

    [Server]
    public void NotifyAttack(PlayerNetwork actor)
    {
        if (actor == null)
            return;

        TryActivate(TrapID.ThornArmor, actor);
        TryActivate(TrapID.Coward, actor);
        TryActivate(TrapID.NoViolence, actor);
        TryActivate(TrapID.LastResistance, actor);
        TryActivate(TrapID.FairWorld, actor);
        TryActivate(TrapID.Whatever, actor);
    }

    [Server]
    public void NotifySkillUsed(PlayerNetwork actor)
    {
        if (actor == null)
            return;

        TryActivate(TrapID.PositionSwap, actor);
    }

    [Server]
    private bool TryActivate(TrapID trapId, PlayerNetwork actor)
    {
        if (!activeTraps.Contains(trapId))
            return false;

        if (!IsReady(trapId))
            return false;

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
        return true;
    }

    [Server]
    private bool IsReady(TrapID trapId)
    {
        if (!nextReadyTimes.TryGetValue(trapId, out float readyTime))
            return true;

        return Time.time >= readyTime;
    }

    [Server]
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

    [Server]
    public void ApplyFractureTrap(PlayerNetwork actor)
    {
        float damage = actor.maxHealth * fractureMaxHealthDamageRate;
        actor.TakeTrueDamage(damage);
    }

    [Server]
    public void ApplyHeavyStepTrap(PlayerNetwork actor)
    {
        actor.ApplySlow(heavyStepSlowDuration, heavyStepSpeedMultiplier);
    }

    [Server]
    public void ApplyCowardTrap(PlayerNetwork actor)
    {
        actor.ApplySlow(2f, 0.7f);
    }

    [Server]
    public void ApplyThornArmorTrap(PlayerNetwork actor)
    {
        actor.TakeTrueDamage(actor.maxHealth * 0.02f);
    }

    [Server]
    public void ApplyNaturalDisasterTrap(PlayerNetwork actor)
    {
        foreach (PlayerNetwork player in players)
        {
            player?.TakeTrueDamage(player.maxHealth * 0.015f);
        }
    }

    [Server]
    public void ApplyLastResistanceTrap(PlayerNetwork actor)
    {
        if (actor.health <= actor.maxHealth * 0.3f)
            actor.ApplyStun(1f);
    }

    [Server]
    public void ApplyNoViolenceTrap(PlayerNetwork actor)
    {
        actor.ApplyStun(1f);
    }

    [Server]
    public void ApplyFairWorldTrap(PlayerNetwork actor)
    {
        foreach (PlayerNetwork player in players)
        {
            player?.ApplySlow(1.5f, 0.8f);
        }
    }

    [Server]
    public void ApplyLackOfFocusTrap(PlayerNetwork actor)
    {
        actor.TakeTrueDamage(actor.maxHealth * 0.01f);
    }

    [Server]
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

    [Server]
    public void ApplyAnxietyTrap(PlayerNetwork actor)
    {
        actor.ApplySlow(2f, 0.75f);
    }

    [Server]
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
