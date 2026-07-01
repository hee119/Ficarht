using UnityEngine;
using Mirror;
using System.Collections;

public class SpawnPoint : MonoBehaviour
{
    public string spawnID;

    void Start()
    {
        StartCoroutine(WaitAndSpawn());
    }

    private IEnumerator WaitAndSpawn()
    {
        // 멀티플레이: GameNetworkManager.SpawnCharacters가 FindObjectsOfType<SpawnPoint>()로
        // 이 컴포넌트를 직접 읽어 서버에서 스폰 위치를 결정한다.
        // 클라이언트에서 NetworkClient.localPlayer를 이동시키면
        // 로비 오브젝트나 배틀 캐릭터 위치를 덮어써 스폰 위치가 어긋나므로 즉시 종료.
        if (NetworkServer.active || NetworkClient.active) yield break;

        // 싱글플레이 전용: 로컬 플레이어 대기 후 이 위치로 이동
        yield return new WaitUntil(() => NetworkClient.localPlayer != null);

        string mySpawnID = NetworkServer.active ? "spawn_P1" : "spawn_P2";

        if (spawnID == mySpawnID)
        {
            NetworkClient.localPlayer.transform.position = this.transform.position;
        }
    }
}