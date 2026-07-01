using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class GameNetworkManager : NetworkManager
{
    public GameObject testCharacterPrefab;

    public GameObject[] characterPrefabs;

    // 카드 선택 완료 플레이어 추적
    private HashSet<NetworkConnectionToClient> cardReadyPlayers = new HashSet<NetworkConnectionToClient>();

    // OnServerAddPlayer 중복 호출 방어 (씬 전환 후 Mirror가 재호출할 수 있음)
    private HashSet<int> _lobbyPlayerSpawned = new HashSet<int>();

    // SpawnCharacters 중복 호출 방지 (OnServerSceneChanged가 여러 번 발화할 경우 대비)
    private bool _spawnedThisScene = false;


    // Host가 선택한 맵 씬 이름 (LoadBattleScene에서 사용)
    private string _pendingMapScene = "";

    // characterPrefabs[i]의 CharaStat.characterStats를 캐싱.
    // Awake에서 prefab에서 읽고, SpawnCharacters에서 인스턴스에 보장 적용.
    private CharacterStats[] _cachedCharaStats = new CharacterStats[4];

    // ─────────────────────────────────────────────
    // characterPrefabs가 Inspector에서 비어있으면
    // spawnPrefabs 이름으로 자동 매핑 (0=Paladin, 1=Bard, 2=Berserker, 3=Mage)
    // + 각 prefab에서 CharacterStats SO를 캐싱
    // ─────────────────────────────────────────────
    public override void Awake()
    {
        base.Awake();
        AutoPopulateCharacterPrefabs();
        CacheCharacterStats();
    }

    private void AutoPopulateCharacterPrefabs()
    {
        bool needsPopulate = characterPrefabs == null || characterPrefabs.Length == 0;
        if (!needsPopulate)
        {
            foreach (var p in characterPrefabs)
                if (p == null) { needsPopulate = true; break; }
        }
        if (!needsPopulate) return;

        // characterId 기준 이름 매핑
        var nameToId = new Dictionary<string, int>
        {
            { "paladin",   0 },
            { "bard",      1 },
            { "berserker", 2 },
            { "mage",      3 }
        };

        characterPrefabs = new GameObject[4];
        foreach (var prefab in spawnPrefabs)
        {
            if (prefab == null) continue;
            string lname = prefab.name.ToLower();
            foreach (var kv in nameToId)
            {
                if (lname.Contains(kv.Key))
                {
                    characterPrefabs[kv.Value] = prefab;
                    break;
                }
            }
        }
        Debug.Log($"[GameNetworkManager] characterPrefabs 자동 설정: " +
                  $"0={characterPrefabs[0]?.name}, 1={characterPrefabs[1]?.name}, " +
                  $"2={characterPrefabs[2]?.name}, 3={characterPrefabs[3]?.name}");
    }

    // 각 characterPrefab의 CharaStat.characterStats SO를 미리 캐싱.
    // 런타임 spawn 후 참조가 null이어도 여기서 강제 할당 가능.
    private void CacheCharacterStats()
    {
        _cachedCharaStats = new CharacterStats[4];
        if (characterPrefabs == null) return;
        for (int i = 0; i < characterPrefabs.Length && i < 4; i++)
        {
            if (characterPrefabs[i] == null) continue;
            CharaStat cs = characterPrefabs[i].GetComponent<CharaStat>();
            if (cs != null && cs.characterStats != null)
            {
                _cachedCharaStats[i] = cs.characterStats;
                Debug.Log($"[GameNetworkManager] _cachedCharaStats[{i}] = {cs.characterStats.name}");
            }
            else
            {
                Debug.LogWarning($"[GameNetworkManager] characterPrefabs[{i}] ({characterPrefabs[i].name}) 의 CharaStat.characterStats가 null — Inspector에서 연결 필요");
            }
        }
    }

    // ─────────────────────────────────────────────
    // OnServerAddPlayer
    // - DontDestroyOnLoad: 씬 전환 시 playerPrefab 보존 → conn.identity 유지
    //   → 카드맵에서 선택한 selectedCharacterId가 전투씬까지 살아있음
    // - conn.identity != null 가드: DontDestroyOnLoad 덕분에 씬 재진입 시
    //   Mirror가 다시 호출해도 playerPrefab 중복 생성 방지
    // ─────────────────────────────────────────────
    public override void OnServerConnect(NetworkConnectionToClient conn)
    {
        base.OnServerConnect(conn);
        Debug.Log($"[Server] 클라이언트 연결: connId={conn.connectionId}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        // 연결 해제 시 추적 목록에서 제거 → 재접속 시 정상 처리
        _lobbyPlayerSpawned.Remove(conn.connectionId);
        base.OnServerDisconnect(conn);
        Debug.Log($"[Server] 클라이언트 연결 해제: connId={conn.connectionId}");
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // ① 최대 2명 제한 — 이미 2명 스폰됐으면 새 접속 강제 차단
        if (_lobbyPlayerSpawned.Count >= 2)
        {
            Debug.LogWarning($"[Server] 최대 2명 초과 — conn {conn.connectionId} 거부");
            conn.Disconnect();
            return;
        }

        // ② HashSet 기반 중복 방어 (씬 전환 후 Mirror 재호출 포함)
        if (_lobbyPlayerSpawned.Contains(conn.connectionId))
        {
            Debug.Log($"[Server] conn {conn.connectionId} 이미 로비 플레이어 있음 — 스킵");
            NetworkServer.SetClientReady(conn);
            return;
        }
        // ③ Mirror 내부 상태도 이중 확인
        if (conn.identity != null)
        {
            _lobbyPlayerSpawned.Add(conn.connectionId);
            Debug.Log($"[Server] conn {conn.connectionId} identity 존재 — 스킵");
            NetworkServer.SetClientReady(conn);
            return;
        }

        Debug.Log($"[Server] OnServerAddPlayer: conn {conn.connectionId}");

        GameObject player = Instantiate(playerPrefab);
        // 씬 전환 후에도 playerPrefab이 살아있어야 selectedCharacterId 등 데이터 보존
        DontDestroyOnLoad(player);
        _lobbyPlayerSpawned.Add(conn.connectionId);
        NetworkServer.AddPlayerForConnection(conn, player);
    }

    public override void OnClientDisconnect()
    {
        base.OnClientDisconnect();
        // 조인 중에 끊겼으면 (코드 검증 실패/타임아웃) UI 복구
        RoomNetworkManager.Instance?.OnDisconnected();
    }


    // 전투 씬이 아닌 씬 목록 (이 목록에 없으면 스폰)
    private static readonly HashSet<string> nonBattleScenes = new HashSet<string>
    {
        "CardMap", "CardMap_MainDesplay", "MainMenu", "Lobby", "SampleScene"
    };

    public override void OnServerSceneChanged(string sceneName)
    {
        Debug.Log($"[Server] OnServerSceneChanged: '{sceneName}'");
        _spawnedThisScene = false; // 씬이 바뀌면 플래그 초기화
        if (!nonBattleScenes.Contains(sceneName))
        {
            Debug.Log($"[Server] 전투 씬 감지 ({sceneName}) → 캐릭터 스폰");
            SpawnCharacters();
        }
        else
        {
            Debug.Log($"[Server] '{sceneName}' 은 비전투 씬 — 스폰 스킵");
        }
    }
    
    // SpawnPoint spawnID 순서 (연결 순서 = P1→P2)
    private static readonly string[] spawnIDs = { "spawn_P1", "spawn_P2" };

    // SpawnPoint가 없는 맵을 위한 기본 폴백 위치
    private static readonly Vector3[] fallbackSpawnPositions = {
        new Vector3(-4f, 1f, 0f),
        new Vector3( 4f, 1f, 0f)
    };

    [Server]
    void SpawnCharacters()
    {
        // 중복 호출 방지
        if (_spawnedThisScene)
        {
            Debug.LogWarning("[Server] SpawnCharacters 중복 호출 감지 — 스킵");
            return;
        }
        _spawnedThisScene = true;

        // 씬의 SpawnPoint 수집 (spawnID → world position)
        var spawnPointMap = new Dictionary<string, Vector3>();
        foreach (SpawnPoint sp in FindObjectsOfType<SpawnPoint>())
        {
            if (!string.IsNullOrEmpty(sp.spawnID))
                spawnPointMap[sp.spawnID] = sp.transform.position;
        }
        Debug.Log($"[Server] 씬 SpawnPoint {spawnPointMap.Count}개 발견: {string.Join(", ", spawnPointMap.Keys)}");

        int spawnIndex = 0;
        List<PlayerNetwork> battlePlayers = new List<PlayerNetwork>();

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity == null)
            {
                Debug.LogWarning($"[Server] conn {conn.connectionId} identity 없음 — 스킵");
                continue;
            }

            PlayerNetwork playerNet = conn.identity.GetComponent<PlayerNetwork>();
            int charId = playerNet != null ? playerNet.selectedCharacterId : 0;

            // 캐릭터 프리팹 결정
            GameObject prefabToSpawn = null;
            if (characterPrefabs != null && charId >= 0 && charId < characterPrefabs.Length && characterPrefabs[charId] != null)
                prefabToSpawn = characterPrefabs[charId];
            else if (testCharacterPrefab != null)
            {
                Debug.LogWarning($"[Server] characterPrefabs[{charId}] 없음 → testCharacterPrefab 사용");
                prefabToSpawn = testCharacterPrefab;
            }
            else
            {
                Debug.LogError($"[Server] 스폰할 프리팹 없음! characterPrefabs와 testCharacterPrefab 모두 null");
                spawnIndex++;
                continue;
            }

            // 스폰 위치: 씬의 SpawnPoint 우선, 없으면 폴백
            string targetSpawnID = spawnIDs[spawnIndex % spawnIDs.Length];
            Vector3 spawnPos = spawnPointMap.ContainsKey(targetSpawnID)
                ? spawnPointMap[targetSpawnID]
                : fallbackSpawnPositions[spawnIndex % fallbackSpawnPositions.Length];

            // 스폰 Y 보정: SpawnPoint 바로 아래 1.5m만 검사
            // ※ 이전에 3m 위에서 8m 아래로 쏘면 발판 위 스폰포인트에서
            //   발판 아래의 바닥(용암 등)을 잘못 감지해 캐릭터가 낙사하는 버그가 있었음
            Vector3 adjustedPos = spawnPos;
            if (Physics.Raycast(spawnPos + Vector3.up * 0.5f, Vector3.down, out RaycastHit groundHit, 1.5f))
            {
                adjustedPos.y = groundHit.point.y + 0.05f;
                Debug.Log($"[Server] 스폰 Y 보정: {spawnPos.y:F2} → {adjustedPos.y:F2} (바닥 {groundHit.point.y:F2})");
            }
            else
            {
                // 바닥을 못 찾으면 SpawnPoint 위치 그대로 사용 (발판 끝이나 허공일 경우 SpawnPoint를 수정해야 함)
                Debug.Log($"[Server] Y 보정 없음 — SpawnPoint 위치 그대로 사용: {spawnPos}");
            }

            GameObject character = Instantiate(prefabToSpawn, adjustedPos, Quaternion.identity);

            // CharaStat.characterStats가 null이면 캐싱된 SO로 강제 할당 후 초기화.
            // prefab 참조가 런타임에 유실되는 경우(guid 충돌 등)를 방어한다.
            CharaStat charaStat = character.GetComponent<CharaStat>();
            if (charaStat != null)
            {
                if (charaStat.characterStats == null && charId < _cachedCharaStats.Length && _cachedCharaStats[charId] != null)
                {
                    charaStat.characterStats = _cachedCharaStats[charId];
                    Debug.Log($"[Server] CharaStat.characterStats 런타임 할당: {charaStat.characterStats.name}");
                }
                // prefab에서 이미 읽었거나 방금 할당한 SO로 스탯 재초기화 (speed/runSpeed 보장)
                charaStat.InitializeStats();
            }
            else
            {
                Debug.LogError($"[Server] {character.name} 에 CharaStat 컴포넌트 없음!");
            }

            PlayerNetwork characterNet = character.GetComponent<PlayerNetwork>();

            PlayerController controller = character.GetComponent<PlayerController>();

            // 그냥 Spawn(character, conn)만 하면 캐릭터는 conn이 소유(authority)만 하는
            // 별개의 오브젝트가 되고, conn.identity(=NetworkClient.localPlayer)는 계속
            // 로비 PlayerNetwork(TargetEnterBattleMode로 y=-9999에 숨겨진 오브젝트)로 남는다.
            // → SkyMapManager 등이 NetworkClient.localPlayer/isLocalPlayer로 "내 플레이어"를
            //   찾으면 숨겨진 로비 오브젝트를 찾게 되어 낙사 리스폰이 실제 캐릭터에 적용되지 않음.
            // ReplacePlayerForConnection으로 conn에 "이미 등록된 플레이어" 자리를 전투 캐릭터로
            // 교체해야 NetworkClient.localPlayer/isLocalPlayer가 실제 캐릭터를 가리키게 된다.
            // KeepActive: 기존 로비 오브젝트는 파괴하지 않고 유지(비활성 권한)한다.
            NetworkServer.ReplacePlayerForConnection(conn, character, ReplacePlayerOptions.KeepActive);

            if (characterNet != null && playerNet != null && characterNet != playerNet)
                characterNet.CopyBattleSetupFrom(playerNet);

            if (controller != null)
                controller.ServerSetOwnerPlayerNetwork(playerNet);

            if (playerNet != null)
            {
                playerNet.currentCharacter = character;
                battlePlayers.Add(characterNet != null ? characterNet : playerNet);

                // 로비용 DontDestroyOnLoad PlayerNetwork 오브젝트의 시각/물리/입력 비활성화
                // → 맵 밖에서 떨어지거나 카메라가 잘못 고정되는 문제 방지
                playerNet.TargetEnterBattleMode(conn);
            }

            Debug.Log($"[Server] P{spawnIndex + 1} (conn={conn.connectionId}) charId={charId} → {targetSpawnID} {spawnPos}");
            spawnIndex++;
        }

        Trap_Card.GetOrCreate().InitializeFromPlayers(battlePlayers);
    }


    // ─────────────────────────────────────────────
    // 카드 선택 완료 처리 (NetworkCardBridge에서 호출)
    // ─────────────────────────────────────────────

    /// <summary>
    /// 플레이어 한 명이 카드 선택을 완료했을 때 호출.
    /// 양쪽 다 완료되면 전투 씬으로 이동한다.
    /// </summary>
    [Server]
    public void OnPlayerCardReady(NetworkConnectionToClient conn)
    {
        cardReadyPlayers.Add(conn);
        int total = NetworkServer.connections.Count;
        Debug.Log($"[Server] 카드 선택 완료: {cardReadyPlayers.Count}/{total}");

        // 접속한 모든 플레이어가 제출하면 진행 (1인 테스트도 동작)
        if (cardReadyPlayers.Count >= total)
        {
            cardReadyPlayers.Clear();

            // Host(첫 번째 연결)의 selectedMapScene 수집
            _pendingMapScene = "";
            foreach (var c in NetworkServer.connections.Values)
            {
                PlayerNetwork pn = c.identity?.GetComponent<PlayerNetwork>();
                if (pn != null && !string.IsNullOrEmpty(pn.selectedMapScene))
                {
                    _pendingMapScene = pn.selectedMapScene;
                    break;
                }
            }
            if (string.IsNullOrEmpty(_pendingMapScene))
                _pendingMapScene = "BattleScene_01";

            Debug.Log($"[Server] 선택된 맵: {_pendingMapScene}");

            // 카드 공개 + 맵 카드 UI 브로드캐스트
            foreach (var c in NetworkServer.connections.Values)
            {
                NetworkCardBridge bridge = c.identity?.GetComponent<NetworkCardBridge>();
                bridge?.RpcRevealCards();
                bridge?.RpcShowMapCard(_pendingMapScene);
            }

            // 3초 후 전투 씬 이동 (맵 UI 표시 시간 확보)
            Invoke(nameof(LoadBattleScene), 3f);
        }
    }

    [Server]
    private void LoadBattleScene()
    {
        Debug.Log($"[Server] 전투 씬 이동: {_pendingMapScene}");
        ServerChangeScene(_pendingMapScene);
    }

    // ─────────────────────────────────────────────
    // 사망 처리 (PlayerNetwork에서 호출)
    // ─────────────────────────────────────────────

    /// <summary>
    /// 플레이어가 사망했을 때 호출. 상대를 승자로 처리한다.
    /// </summary>
    [Server]
    public void OnPlayerDied(NetworkConnectionToClient loserConn)
    {
        Debug.Log($"[Server] 패배자 결정: {loserConn.connectionId}");

        // 승자 찾기 (사망하지 않은 플레이어)
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn == loserConn) continue;

            PlayerNetwork winner = conn.identity?.GetComponent<PlayerNetwork>();
            if (winner != null && !winner.isDead)
            {
                Debug.Log($"[Server] 승자: {conn.connectionId}");
                // TODO: 결과 UI 표시 RPC 추가
            }
        }
    }

    // [Server]
    // void SpawnCharacters()
    // {
    //     SpawnPoint[] spawnPoints = FindObjectsOfType<SpawnPoint>();
    //
    //     int index = 0;
    //
    //     foreach (var conn in NetworkServer.connections.Values)
    //     {
    //         if (conn.identity == null) continue;
    //
    //         PlayerNetwork player = conn.identity.GetComponent<PlayerNetwork>();
    //
    //         int charId = player.selectedCharacterId;
    //
    //         if (charId < 0 || charId >= characterPrefabs.Length)
    //         {
    //             Debug.LogError("캐릭터 선택 안됨");
    //             continue;
    //         }
    //
    //         GameObject character = Instantiate(characterPrefabs[charId]);
    //
    //         character.transform.position = spawnPoints[index % spawnPoints.Length].transform.position;
    //
    //         NetworkServer.Spawn(character, conn);
    //
    //         player.currentCharacter = character;
    //
    //         index++;
    //     }
    // }
}
