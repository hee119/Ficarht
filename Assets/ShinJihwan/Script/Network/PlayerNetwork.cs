using Mirror;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PlayerNetwork : NetworkBehaviour
{
    // ─────────────────────────────────────────────
    // 스탯 동기화 (SyncVar → 값이 바뀌면 모든 클라이언트에 자동 전달)
    // ─────────────────────────────────────────────

    [SyncVar(hook = nameof(OnHealthChanged))]
    public float health = 100f;

    [SyncVar] public float maxHealth = 100f;
    [SyncVar] public float stamina = 50f;
    [SyncVar] public float power = 50f;
    [SyncVar] public float defense = 50f;
    [SyncVar] public float intelligence = 50f;

    [SyncVar(hook = nameof(OnStateChanged))]
    public PlayerStateType currentState = PlayerStateType.Normal;

    [SyncVar] public bool isDead = false;

    // ─────────────────────────────────────────────
    // 등록된 스킬 (서버에서만 관리)
    // ─────────────────────────────────────────────

    private List<SkillID> registeredSkills = new List<SkillID>();

    private List<TrapID> registeredTraps = new List<TrapID>();

    // ─────────────────────────────────────────────
    // 방 생성 / 참가 (기존 코드 유지)
    // ─────────────────────────────────────────────

    [SyncVar] public int selectedCharacterId = -1;
    [SyncVar] public string selectedMapScene = "";
    public GameObject currentCharacter;

    [Command]
    public void CmdCreateRoom()
    {
        // RoomManager 없이 직접 코드 생성
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        string code = "";
        for (int i = 0; i < 6; i++)
            code += chars[UnityEngine.Random.Range(0, chars.Length)];

        Debug.Log($"[Server] 방 코드 생성: {code}");
        TargetReceiveCode(connectionToClient, code);
    }

    [Command]
    public void CmdJoinRoom(string code)
    {
        // 코드 검증 생략 (localhost 연결 자체가 인증)
        Debug.Log($"[Server] 방 참가: {code}");
    }

    [TargetRpc]
    void TargetReceiveCode(NetworkConnection target, string code)
    {
        Debug.Log($"내 방 코드: {code}");
        RoomNetworkManager.Instance?.ShowRoomCode(code);
    }

    [Command]
    public void CmdSelectCharacter(int characterId)
    {
        selectedCharacterId = characterId;
    }

    // ─────────────────────────────────────────────
    // 스탯 적용 (NetworkCardBridge에서 카드 선택 완료 후 호출)
    // ─────────────────────────────────────────────

    /// <summary>
    /// 서버에서만 호출. CardSystemManager의 RuntimeStats를 SyncVar에 반영한다.
    /// </summary>
    [Server]
    public void ApplyStats(float hp, float stm, float pwr, float def, float intel)
    {
        maxHealth    = hp;
        health       = hp;
        stamina      = stm;
        power        = pwr;
        defense      = def;
        intelligence = intel;

        ApplyStatsToCharacterComponents(hp, stm, pwr, def, intel);

        if (netId != 0)
            RpcApplyStatsToCharacterComponents(hp, stm, pwr, def, intel);

        Debug.Log($"[Server] {netId} 스탯 적용: HP={hp} STM={stm} PWR={pwr} DEF={def} INT={intel}");
    }

    [ClientRpc]
    private void RpcApplyStatsToCharacterComponents(float hp, float stm, float pwr, float def, float intel)
    {
        ApplyStatsToCharacterComponents(hp, stm, pwr, def, intel);
    }

    private void ApplyStatsToCharacterComponents(float hp, float stm, float pwr, float def, float intel)
    {
        CharaStat charaStat = GetComponent<CharaStat>();

        if (charaStat == null && currentCharacter != null)
            charaStat = currentCharacter.GetComponent<CharaStat>();

        if (charaStat == null)
            return;

        charaStat.maxHealth = Mathf.Max(hp, 1f);
        charaStat.health = charaStat.maxHealth;
        charaStat.maxStamina = Mathf.Max(stm, 0f);
        charaStat.stamina = charaStat.maxStamina;
        charaStat.power = Mathf.Max(pwr, 0f);
        charaStat.defense = Mathf.Max(def, 0f);
        charaStat.intelligence = Mathf.Max(intel, 0f);

        if (charaStat.healthBar != null)
        {
            charaStat.healthBar.maxValue = charaStat.maxHealth;
            charaStat.healthBar.value = charaStat.health;
        }

        if (charaStat.staminaBar != null)
        {
            charaStat.staminaBar.maxValue = charaStat.maxStamina;
            charaStat.staminaBar.value = charaStat.stamina;
        }

        GetComponent<PlayerController>()?.RefreshSpeed();
    }

    [Server]
    public void RegisterSkills(List<SkillID> skills)
    {
        registeredSkills = new List<SkillID>(skills);
        Debug.Log($"[Server] {netId} 스킬 등록: {skills.Count}개");
    }

    [Server]
    public void RegisterTraps(int[] trapInts)
    {
        registeredTraps.Clear();

        if (trapInts == null)
            return;

        foreach (int trapInt in trapInts)
        {
            TrapID trapId = (TrapID)trapInt;

            if (trapId == TrapID.None)
                continue;

            registeredTraps.Add(trapId);
        }

        Debug.Log($"[Server] {netId} 함정 등록: {registeredTraps.Count}개");
    }

    public List<TrapID> GetRegisteredTraps()
    {
        return new List<TrapID>(registeredTraps);
    }

    [Server]
    public void CopyBattleSetupFrom(PlayerNetwork source)
    {
        if (source == null)
            return;

        ApplyStats(
            source.maxHealth,
            source.stamina,
            source.power,
            source.defense,
            source.intelligence
        );

        selectedCharacterId = source.selectedCharacterId;
        selectedMapScene = source.selectedMapScene;
        registeredSkills = new List<SkillID>(source.registeredSkills);
        registeredTraps = new List<TrapID>(source.registeredTraps);
    }

    // ─────────────────────────────────────────────
    // 데미지 처리
    // ─────────────────────────────────────────────

    /// <summary>
    /// 서버에서만 호출. 방어력 계산 후 체력 감소.
    /// </summary>
    [Server]
    public void TakeDamage(float rawDamage)
    {
        if (isDead) return;

        // 방어력 계산: defense 1당 0.5% 감소, 최대 50%
        float reduction  = Mathf.Clamp(defense * 0.005f, 0f, 0.5f);
        float finalDamage = rawDamage * (1f - reduction);

        ApplyDamageValue(finalDamage);

        Debug.Log($"[Server] {netId} 데미지 {rawDamage:F1} → 최종 {finalDamage:F1} / 남은 HP {health:F1}");
    }

    [Server]
    public void TakeTrueDamage(float damage)
    {
        if (isDead) return;

        ApplyDamageValue(damage);

        Debug.Log($"[Server] {netId} 함정 고정 피해 {damage:F1} / 남은 HP {health:F1}");
    }

    [Server]
    private void ApplyDamageValue(float damage)
    {
        health = Mathf.Max(0f, health - damage);

        RpcOnDamageEffect(damage);

        if (health <= 0f)
            ServerDie();
    }

    /// <summary>
    /// PlayerController(공격)에서 서버 판정 요청 시 호출.
    /// 범위 내 상대를 찾아 데미지를 입힌다.
    /// </summary>
    [Server]
    public void ServerRequestAttack()
    {
        float attackRange = 2.5f;
        Collider[] hits = Physics.OverlapSphere(transform.position, attackRange);

        foreach (var hit in hits)
        {
            PlayerNetwork target = hit.GetComponent<PlayerNetwork>();
            if (target == null || target == this) continue;

            target.TakeDamage(power);
            break; // PvP 1:1이므로 첫 번째 대상만
        }
    }

    // ─────────────────────────────────────────────
    // 상태이상
    // ─────────────────────────────────────────────

    [Server]
    public void ApplyBurn(float duration, float dps)
    {
        if (isDead) return;
        currentState = PlayerStateType.Burn;
        StartCoroutine(BurnRoutine(duration, dps));
    }

    [Server]
    public void ApplyStun(float duration)
    {
        if (isDead) return;
        currentState = PlayerStateType.Stun;
        StartCoroutine(ClearStateAfter(duration));
    }

    [Server]
    public void ApplySlow(float duration)
    {
        ApplySlow(duration, 0.5f);
    }

    [Server]
    public void ApplySlow(float duration, float speedMultiplier)
    {
        if (isDead) return;
        currentState = PlayerStateType.Slow;
        TargetApplySpeedMultiplier(connectionToClient, speedMultiplier, duration);
        StartCoroutine(ClearStateAfter(duration));
    }

    [Server]
    public void ApplyFreeze(float duration)
    {
        if (isDead) return;
        currentState = PlayerStateType.Freeze;
        StartCoroutine(ClearStateAfter(duration));
    }

    [TargetRpc]
    private void TargetApplySpeedMultiplier(NetworkConnection target, float multiplier, float duration)
    {
        PlayerController controller = GetComponent<PlayerController>();

        if (controller == null && currentCharacter != null)
            controller = currentCharacter.GetComponent<PlayerController>();

        controller?.ApplyTemporarySpeedMultiplier(multiplier, duration);
    }

    [Server]
    private IEnumerator BurnRoutine(float duration, float dps)
    {
        float elapsed = 0f;
        while (elapsed < duration && !isDead)
        {
            yield return new WaitForSeconds(1f);
            elapsed += 1f;
            TakeDamage(dps);
        }
        if (currentState == PlayerStateType.Burn)
            currentState = PlayerStateType.Normal;
    }

    [Server]
    private IEnumerator ClearStateAfter(float duration)
    {
        yield return new WaitForSeconds(duration);
        currentState = PlayerStateType.Normal;
    }

    // ─────────────────────────────────────────────
    // 사망
    // ─────────────────────────────────────────────

    [Server]
    private void ServerDie()
    {
        isDead       = true;
        currentState = PlayerStateType.Normal;
        Debug.Log($"[Server] {netId} 사망");

        // GameNetworkManager에 사망 알림
        GameNetworkManager netManager = NetworkManager.singleton as GameNetworkManager;
        netManager?.OnPlayerDied(connectionToClient);

        RpcOnDied();
    }

    // ─────────────────────────────────────────────
    // ClientRpc - 연출
    // ─────────────────────────────────────────────

    [ClientRpc]
    void RpcOnDamageEffect(float damage)
    {
        Debug.Log($"[Client] 데미지 이펙트: {damage:F1}");
        // TODO: 히트 이펙트, 데미지 텍스트 UI
    }

    [ClientRpc]
    void RpcOnDied()
    {
        Debug.Log("[Client] 플레이어 사망 처리");
        // TODO: 사망 애니메이션
    }

    // ─────────────────────────────────────────────
    // SyncVar 훅 - 클라이언트 UI / 비주얼 업데이트
    // ─────────────────────────────────────────────

    void OnHealthChanged(float oldVal, float newVal)
    {
        Debug.Log($"[Client] 체력 변경: {oldVal:F1} → {newVal:F1}");
        // HpBarUI는 Update에서 자동 갱신 (SyncVar 변경 후 다음 프레임 반영)
    }

    void OnStateChanged(PlayerStateType oldState, PlayerStateType newState)
    {
        Debug.Log($"[Client] 상태 변경: {oldState} → {newState}");

        // 색상으로 상태이상 표현
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend == null) return;

        switch (newState)
        {
            case PlayerStateType.Burn:   rend.material.color = new Color(1f, 0.4f, 0f);  break; // 주황
            case PlayerStateType.Stun:   rend.material.color = Color.yellow;              break; // 노랑
            case PlayerStateType.Slow:   rend.material.color = new Color(0.6f, 0.6f, 1f); break; // 연보라
            case PlayerStateType.Freeze: rend.material.color = new Color(0.3f, 0.7f, 1f); break; // 파랑
            case PlayerStateType.Normal: rend.material.color = Color.white;               break;
        }
    }

    // ─────────────────────────────────────────────
    // 이동/공격 가능 여부 (PlayerController에서 체크)
    // ─────────────────────────────────────────────

    public bool CanMove()   => !isDead && currentState != PlayerStateType.Stun && currentState != PlayerStateType.Freeze;
    public bool CanAttack() => !isDead && currentState != PlayerStateType.Stun && currentState != PlayerStateType.Freeze;
}

// 상태이상 열거형
public enum PlayerStateType
{
    Normal,
    Burn,
    Slow,
    Stun,
    Freeze
}
