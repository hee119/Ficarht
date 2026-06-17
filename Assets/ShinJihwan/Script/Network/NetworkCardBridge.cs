using Mirror;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// CardSystemManager(김재원) ↔ Mirror 멀티플레이(신지환) 연결 브릿지.
///
/// 사용법:
/// 1. 플레이어 프리팹에 이 컴포넌트를 붙인다.
/// 2. CardSystemManager.OnTimerEnd() 끝에 아래 한 줄 추가:
///    NetworkCardBridge.LocalInstance?.SubmitCardSelection();
///
/// 흐름:
/// 카드 선택 완료 → SubmitCardSelection() → CmdSubmitStats() → 서버가 PlayerNetwork에 적용
/// → 양쪽 다 완료되면 GameNetworkManager가 전투 씬 전환
/// </summary>
public class NetworkCardBridge : NetworkBehaviour
{
    // 로컬 플레이어의 브릿지 인스턴스 (CardSystemManager에서 쉽게 접근)
    public static NetworkCardBridge LocalInstance { get; private set; }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        LocalInstance = this;
        Debug.Log("[NetworkCardBridge] 로컬 플레이어 브릿지 준비됨");
    }

    // ─────────────────────────────────────────────
    // 카드 선택 완료 → 서버로 전송
    // ─────────────────────────────────────────────

    /// <summary>
    /// CardSystemManager.OnTimerEnd() 마지막에 호출.
    /// RuntimeStats와 등록된 스킬을 서버로 전달한다.
    /// </summary>
    public void SubmitCardSelection()
    {
        if (!isLocalPlayer) return;

        // CardSystemManager에서 최종 스탯 가져오기
        RuntimeStats stats = CardSystemManager.Instance?.GetFinalStats();
        if (stats == null)
        {
            Debug.LogWarning("[NetworkCardBridge] RuntimeStats가 null - 캐릭터 카드가 배치되지 않았을 수 있음");
            return;
        }

        // 배치된 캐릭터 카드에서 characterId 읽기 (0=Paladin, 1=Bard, 2=Berserker, 3=Mage)
        int characterId = 0;
        var charSlots = CardSystemManager.Instance?.characterSlots;
        if (charSlots != null)
        {
            foreach (var slot in charSlots)
            {
                if (slot?.currentCard?.data?.cardType == CardType.Character &&
                    slot.currentCard.data.characterStats != null)
                {
                    characterId = slot.currentCard.data.characterStats.characterId;
                    break;
                }
            }
        }

        // 등록된 스킬 ID 목록 가져오기
        List<SkillID> skills = SkillRegistry.Instance?.GetSkills() ?? new List<SkillID>();

        // 스킬 ID를 int 배열로 변환 (Mirror는 enum 리스트 직접 전송 불가)
        int[] skillInts = new int[skills.Count];
        for (int i = 0; i < skills.Count; i++)
            skillInts[i] = (int)skills[i];

        CmdSubmitStats(
            stats.maxHealth,
            stats.stamina,
            stats.power,
            stats.defense,
            stats.intelligence,
            skillInts,
            characterId
        );

        Debug.Log($"[NetworkCardBridge] 스탯 전송: HP={stats.maxHealth} PWR={stats.power} DEF={stats.defense} 캐릭터ID={characterId} 스킬={skillInts.Length}개");
    }

    // ─────────────────────────────────────────────
    // 서버 수신 → PlayerNetwork에 적용
    // ─────────────────────────────────────────────

    [Command]
    void CmdSubmitStats(float health, float stamina, float power, float defense, float intelligence, int[] skillInts, int characterId)
    {
        // 이 플레이어의 PlayerNetwork에 스탯 + 캐릭터 ID 적용
        PlayerNetwork playerNetwork = GetComponent<PlayerNetwork>();
        if (playerNetwork != null)
        {
            playerNetwork.ApplyStats(health, stamina, power, defense, intelligence);
            playerNetwork.selectedCharacterId = characterId; // 전투씬 스폰에 사용됨

            // 스킬 등록
            List<SkillID> skills = new List<SkillID>();
            foreach (int id in skillInts)
                skills.Add((SkillID)id);
            playerNetwork.RegisterSkills(skills);
        }

        Debug.Log($"[Server] 플레이어 {netId} 스탯/캐릭터(ID={characterId}) 적용 완료");

        // GameNetworkManager에 카드 선택 완료 알림
        GameNetworkManager netManager = NetworkManager.singleton as GameNetworkManager;
        netManager?.OnPlayerCardReady(connectionToClient);
    }

    // ─────────────────────────────────────────────
    // 상대 카드 공개 이벤트 (타이머 종료 후 서버→모든 클라이언트)
    // ─────────────────────────────────────────────

    /// <summary>
    /// 서버에서 양쪽 카드 선택 완료 후 호출 → 모든 클라이언트에서 카드 공개
    /// </summary>
    [ClientRpc]
    public void RpcRevealCards()
    {
        CardRevealSystem.Instance?.RevealAllCards();
        Debug.Log("[Client] 상대 카드 공개됨");
    }
}
