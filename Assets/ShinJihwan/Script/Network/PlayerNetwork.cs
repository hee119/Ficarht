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

    // ─────────────────────────────────────────────
    // 방 생성 / 참가 (기존 코드 유지)
    // ─────────────────────────────────────────────

    [SyncVar] public int selectedCharacterId = -1;
    public GameObject currentCharacter;

    [Command]
    public void CmdCreateRoom()
    {
        string code = RoomManager.Instance.CreateRoom(connectionToClient);
        TargetReceiveCode(connectionToClient, code);
    }

    [Command]
    public void CmdJoinRoom(string code)
    {
        RoomManager.Instance.JoinRoom(code, connectionToClient);
    }

    [TargetRpc]
    void TargetReceiveCode(NetworkConnection target, string code)
    {
        Debug.Log($"내 방 코드: {code}");
        LobbyManager3D.Instance?.ShowMyCode(code);
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

        Debug.Log($"[Server] {netId} 스탯 적용: HP={hp} STM={stm} PWR={pwr} DEF={def} INT={intel}");
    }

    [Server]
    public void RegisterSkills(List<SkillID> skills)
    {
        registeredSkills = new List<SkillID>(skills);
        Debug.Log($"[Server] {netId} 스킬 등록: {skills.Count}개");
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

        health = Mathf.Max(0f, health - finalDamage);

        Debug.Log($"[Server] {netId} 데미지 {rawDamage:F1} → 최종 {finalDamage:F1} / 남은 HP {health:F1}");

        RpcOnDamageEffect(finalDamage);

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
        if (isDead) return;
        currentState = PlayerStateType.Slow;
        StartCoroutine(ClearStateAfter(duration));
    }

    [Server]
    public void ApplyFreeze(float duration)
    {
        if (isDead) return;
        currentState = PlayerStateType.Freeze;
        StartCoroutine(ClearStateAfter(duration));
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
        // TODO: 체력 UI 업데이트
        Debug.Log($"[Client] 체력 변경: {oldVal:F1} → {newVal:F1}");
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
