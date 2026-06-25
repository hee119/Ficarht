using Mirror;
using UnityEngine;
using System.Collections.Generic;

public class GameNetworkManager : NetworkManager
{
    public GameObject testCharacterPrefab;

    public GameObject[] characterPrefabs;

    // 카드 선택 완료 플레이어 추적
    private HashSet<NetworkConnectionToClient> cardReadyPlayers = new HashSet<NetworkConnectionToClient>();

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


    public override void OnServerSceneChanged(string sceneName)
    {
        if (sceneName.Contains("BattleScene"))
        {
            SpawnCharacters();
        }
    }
    
    // 플레이어 1, 2 스폰 위치
    private static readonly Vector3[] spawnPoints = {
        new Vector3(-4f, 0f, 0f),
        new Vector3( 4f, 0f, 0f)
    };

    [Server]
    void SpawnCharacters()
    {
        int spawnIndex = 0;

        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn.identity == null) continue;

            PlayerNetwork playerNet = conn.identity.GetComponent<PlayerNetwork>();
            int charId = playerNet != null ? playerNet.selectedCharacterId : 0;

            // characterPrefabs 배열에서 선택된 캐릭터 프리팹 결정
            // characterPrefabs[0]=Paladin, [1]=Bard, [2]=Berserker, [3]=Mage
            GameObject prefabToSpawn = testCharacterPrefab; // 기본값 (미선택 시 폴백)
            if (characterPrefabs != null && charId >= 0 && charId < characterPrefabs.Length && characterPrefabs[charId] != null)
                prefabToSpawn = characterPrefabs[charId];
            else
                Debug.LogWarning($"[Server] characterPrefabs[{charId}] 없음 → testCharacterPrefab 사용");

            GameObject character = Instantiate(prefabToSpawn);
            character.transform.position = spawnPoints[spawnIndex % spawnPoints.Length];

            NetworkServer.Spawn(character, conn);

            // 플레이어 오브젝트에 현재 캐릭터 참조 저장
            if (playerNet != null)
                playerNet.currentCharacter = character;

            Debug.Log($"[Server] 플레이어 {conn.connectionId} → 캐릭터ID={charId} 스폰 위치={character.transform.position}");
            spawnIndex++;
        }
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
        Debug.Log($"[Server] 카드 선택 완료: {cardReadyPlayers.Count}/2");

        if (cardReadyPlayers.Count >= 2)
        {
            cardReadyPlayers.Clear();

            // 카드 공개 (모든 클라이언트에서 RevealAllCards 호출)
            foreach (var c in NetworkServer.connections.Values)
            {
                NetworkCardBridge bridge = c.identity?.GetComponent<NetworkCardBridge>();
                bridge?.RpcRevealCards();
            }

            // 잠시 후 전투 씬 전환
            Invoke(nameof(LoadBattleScene), 2f);
        }
    }

    [Server]
    private void LoadBattleScene()
    {
        // 플레이어 맵 카드 씬 이름 수집 (Host 우선)
        string selectedMap = "";
        foreach (var conn in NetworkServer.connections.Values)
        {
            PlayerNetwork pn = conn.identity?.GetComponent<PlayerNetwork>();
            if (pn != null && !string.IsNullOrEmpty(pn.selectedMapScene))
            {
                selectedMap = pn.selectedMapScene;
                break;
            }
        }

        if (string.IsNullOrEmpty(selectedMap))
        {
            Debug.LogWarning("[Server] 맵 카드 미선택 → BattleScene_01 사용");
            selectedMap = "BattleScene_01";
        }

        Debug.Log($"[Server] 전투 씬 이동: {selectedMap}");
        ServerChangeScene(selectedMap);
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