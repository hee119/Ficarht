using Mirror;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// CardSystemManager ↔ Mirror 연결 브릿지.
///
/// 네트워크 카드 페이즈 흐름:
///   CardBox 클릭 → CmdRequestStartCards() → 서버 → RpcStartCards() → 양쪽 카드 드로우 시작
///   서버 60초 타이머 → RpcForceEndCards() → 양쪽 강제 제출
///   둘 다 제출 → GameNetworkManager → 전투씬 이동
/// </summary>
public class NetworkCardBridge : NetworkBehaviour
{
    public static NetworkCardBridge LocalInstance { get; private set; }

    // 서버 전용 카드 페이즈 상태
    private static bool _phaseActive = false;

    [Header("카드 페이즈 타이머 (초)")]
    public float cardPhaseDuration = 60f;

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        LocalInstance = this;
        Debug.Log("[NetworkCardBridge] 로컬 브릿지 준비");
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        // 서버 종료/씬 전환 시 static 상태 리셋 → 재접속 후 카드팩 클릭 정상 동작
        _phaseActive = false;
        Debug.Log("[NetworkCardBridge] _phaseActive 리셋 (서버 종료)");
    }

    // ─────────────────────────────────────────────
    // 카드 페이즈 시작 (CardBox 클릭 시 호출)
    // ─────────────────────────────────────────────

    [Command]
    public void CmdRequestStartCards()
    {
        if (_phaseActive) return;
        _phaseActive = true;

        Debug.Log("[Server] 카드 페이즈 시작");

        CardSystemManager csm = CardSystemManager.Instance;
        if (csm == null) { Debug.LogError("[Server] CardSystemManager 없음"); return; }

        // 덱 전체를 한 번 셔플 → P1이 앞 N장, P2가 그 다음 N장
        int[] charOrder = ShuffleIndices(csm.characterDeck.Count);
        int[] buffOrder = ShuffleIndices(csm.buffDeck.Count);
        int[] trapOrder = ShuffleIndices(csm.trapDeck.Count);

        int playerIdx = 0;
        foreach (var conn in NetworkServer.connections.Values)
        {
            NetworkCardBridge bridge = conn.identity?.GetComponent<NetworkCardBridge>();
            if (bridge == null) continue;

            bool drawMap = (conn == NetworkServer.localConnection);

            int[] myChar = SliceIndices(charOrder, playerIdx * csm.drawCharacterCards, csm.drawCharacterCards);
            int[] myBuff = SliceIndices(buffOrder, playerIdx * csm.drawBuffCards,      csm.drawBuffCards);
            int[] myTrap = SliceIndices(trapOrder, playerIdx * csm.drawTrapCards,      csm.drawTrapCards);

            bridge.TargetStartCards(conn, myChar, myBuff, myTrap, drawMap);

            Debug.Log($"[Server] P{playerIdx + 1} 카드 인덱스 — char:{string.Join(",", myChar)} buff:{string.Join(",", myBuff)} trap:{string.Join(",", myTrap)}");
            playerIdx++;
        }

        StartCoroutine(ServerCardTimer());
    }

    // 0~count-1 배열을 Fisher-Yates로 셔플
    private static int[] ShuffleIndices(int count)
    {
        int[] idx = new int[count];
        for (int i = 0; i < count; i++) idx[i] = i;
        for (int i = count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (idx[i], idx[j]) = (idx[j], idx[i]);
        }
        return idx;
    }

    // 배열에서 start 위치부터 len개 잘라 반환 (부족하면 앞에서 재사용)
    private static int[] SliceIndices(int[] src, int start, int len)
    {
        int[] result = new int[len];
        for (int i = 0; i < len; i++)
            result[i] = src.Length > 0 ? src[(start + i) % src.Length] : 0;
        return result;
    }

    [TargetRpc]
    void TargetStartCards(NetworkConnection target, int[] charIdx, int[] buffIdx, int[] trapIdx, bool drawMapCard)
    {
        Debug.Log($"[Client] 카드 드로우 시작 — char:{charIdx.Length}장 buff:{buffIdx.Length}장 trap:{trapIdx.Length}장");
        CardSystemManager.Instance?.SetDrawMapCard(drawMapCard);
        CardSystemManager.Instance?.StartGameWithIndices(charIdx, buffIdx, trapIdx);
    }

    // ─────────────────────────────────────────────
    // 서버 타이머 (5초마다 클라이언트 타이머 보정)
    // ─────────────────────────────────────────────

    [Server]
    IEnumerator ServerCardTimer()
    {
        float remaining = cardPhaseDuration;

        while (remaining > 0f)
        {
            yield return new WaitForSeconds(5f);
            remaining -= 5f;
            if (remaining > 0f)
                RpcSyncTimer(remaining);
        }

        Debug.Log("[Server] 카드 페이즈 종료 → 강제 제출");
        RpcForceEndCards();
        _phaseActive = false;
    }

    [ClientRpc]
    void RpcSyncTimer(float remaining)
    {
        CardSystemManager.Instance?.SyncTimerFromServer(remaining);
    }

    [ClientRpc]
    void RpcForceEndCards()
    {
        Debug.Log("[Client] 서버 강제 제출");
        CardSystemManager.Instance?.ForceEndTurn();
    }

    // ─────────────────────────────────────────────
    // 카드 선택 완료 → 서버로 스탯 전송
    // CardSystemManager.OnTimerEnd() 끝에서 자동 호출됨
    // ─────────────────────────────────────────────

    public void SubmitCardSelection()
    {
        if (!isLocalPlayer) return;

        RuntimeStats stats = CardSystemManager.Instance?.GetFinalStats();
        if (stats == null)
        {
            Debug.LogWarning("[NetworkCardBridge] RuntimeStats null - 기본값으로 제출");
            CmdSubmitStats(100f, 50f, 50f, 50f, 50f, new int[0], new int[0], 0, "");
            return;
        }

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

        List<SkillID> skills = SkillRegistry.Instance?.GetSkills() ?? new List<SkillID>();
        int[] skillInts = new int[skills.Count];
        for (int i = 0; i < skills.Count; i++)
            skillInts[i] = (int)skills[i];

        int[] trapInts =
            CardSystemManager.Instance?.GetSelectedTrapIds()
            ?? new int[0];

        // 맵 카드에서 씬 이름 추출
        string mapScene = CardSystemManager.Instance?.GetSelectedMapScene() ?? "";

        CmdSubmitStats(stats.maxHealth, stats.stamina, stats.power,
            stats.defense, stats.intelligence, skillInts, trapInts, characterId, mapScene);

        Debug.Log(
            $"[CARD TEST][SUBMIT] 카드 선택 제출: " +
            $"HP={stats.maxHealth}, STM={stats.stamina}, PWR={stats.power}, " +
            $"DEF={stats.defense}, INT={stats.intelligence}, " +
            $"캐릭터ID={characterId}, 맵={mapScene}, 함정={trapInts.Length}개"
        );
    }

    // ─────────────────────────────────────────────
    // 서버: 스탯 수신 → PlayerNetwork 적용
    // ─────────────────────────────────────────────

    [Command]
    void CmdSubmitStats(float health, float stamina, float power,
        float defense, float intelligence, int[] skillInts, int[] trapInts, int characterId, string mapScene)
    {
        PlayerNetwork playerNetwork = GetComponent<PlayerNetwork>();
        if (playerNetwork != null)
        {
            playerNetwork.ApplyStats(health, stamina, power, defense, intelligence);
            playerNetwork.selectedCharacterId = characterId;
            playerNetwork.selectedMapScene = mapScene;

            List<SkillID> skills = new List<SkillID>();
            foreach (int id in skillInts)
                skills.Add((SkillID)id);
            playerNetwork.RegisterSkills(skills);
            playerNetwork.RegisterTraps(trapInts);
        }

        Debug.Log($"[Server] 플레이어 {netId} 스탯 적용 (캐릭터ID={characterId} 맵={mapScene})");

        GameNetworkManager netManager = NetworkManager.singleton as GameNetworkManager;
        netManager?.OnPlayerCardReady(connectionToClient);
    }

    // ─────────────────────────────────────────────
    // 카드 공개 (GameNetworkManager.OnPlayerCardReady에서 호출)
    // ─────────────────────────────────────────────

    [ClientRpc]
    public void RpcRevealCards()
    {
        CardRevealSystem.Instance?.RevealAllCards();
        Debug.Log("[Client] 카드 공개");
    }

    // ─────────────────────────────────────────────
    // 맵 카드 UI 표시 (양쪽 클라이언트에 선택된 맵 표시)
    // ─────────────────────────────────────────────

    [ClientRpc]
    public void RpcShowMapCard(string mapSceneName)
    {
        Debug.Log($"[Client] 선택된 전투 맵: {mapSceneName}");
        MapCardDisplayUI.Instance?.ShowMapCard(mapSceneName);
    }
}
