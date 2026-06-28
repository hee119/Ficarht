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
        // 로컬 플레이어가 생길 때까지 대기
        yield return new WaitUntil(() => NetworkClient.localPlayer != null);

        string mySpawnID = NetworkServer.active ? "spawn_P1" : "spawn_P2";

        if (spawnID == mySpawnID)
        {
            NetworkClient.localPlayer.transform.position = this.transform.position;
        }
    }
}