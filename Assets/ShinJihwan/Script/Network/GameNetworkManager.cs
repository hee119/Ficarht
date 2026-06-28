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

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        Debug.Log("🔥 OnServerAddPlayer 호출됨");

        GameObject player = Instantiate(playerPrefab);
        NetworkServer.AddPlayerForConnection(conn, player);

        // 두 번째 플레이어 접속 시 Host 클라이언트 UI 업데이트
        // (GameNetworkManager는 NetworkBehaviour가 아니므로 직접 호출)
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

            PlayerNetwork characterNet = character.GetComponent<PlayerNetwork>();
            if (characterNet != null && playerNet != null && characterNet != playerNet)
                characterNet.CopyBattleSetupFrom(playerNet);

            PlayerController controller = character.GetComponent<PlayerController>();
            if (controller != null)
                controller.ServerSetOwnerPlayerNetwork(playerNet);

            NetworkServer.Spawn(character, conn);

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