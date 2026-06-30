using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class GameNetworkManager : NetworkManager
{
    public GameObject testCharacterPrefab;

    public GameObject[] characterPrefabs;

    // 카드 선택 완료 플레이어 추적
    private HashSet<NetworkConnectionToClient> cardReadyPlayers = new HashSet<NetworkConnectionToClient>();

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
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // 이미 player 오브젝트가 있으면 중복 생성 방지 (씬 전환 후 재호출 방어)
        if (conn.identity != null)
        {
            Debug.Log($"[Server] conn {conn.connectionId} 이미 플레이어 있음 — OnServerAddPlayer 스킵");
            NetworkServer.SetClientReady(conn);
            return;
        }

        Debug.Log("🔥 OnServerAddPlayer 호출됨");

        GameObject player = Instantiate(playerPrefab);
        // 씬 전환 후에도 playerPrefab이 살아있어야 selectedCharacterId 등 데이터 보존
        DontDestroyOnLoad(player);
        NetworkServer.AddPlayerForConnection(conn, player);

        // 두 번째 플레이어 접속 시 Host 클라이언트 UI 업데이트
        if (NetworkServer.connections.Count >= 2)
            RoomNetworkManager.Instance?.OnSecondPlayerConnected();
    }


    // 전투 씬이 아닌 씬 목록 (이 목록에 없으면 스폰)
    private static readonly HashSet<string> nonBattleScenes = new HashSet<string>
    {
        "CardMap", "CardMap_MainDesplay", "MainMenu", "Lobby", "SampleScene"
    };

    public override void OnServerSceneChanged(string sceneName)
    {
        Debug.Log($"[Server] OnServerSceneChanged: '{sceneName}'");
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

            GameObject character = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

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

            NetworkServer.Spawn(character, conn);

            if (characterNet != null && playerNet != null && characterNet != playerNet)
                characterNet.CopyBattleSetupFrom(playerNet);

            if (controller != null)
                controller.ServerSetOwnerPlayerNetwork(playerNet);

            if (playerNet != null)
            {
                playerNet.currentCharacter = character;
                battlePlayers.Add(characterNet != null ? characterNet : playerNet);
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
