using System.Collections;
using Mirror;
using UnityEngine;

/// <summary>
/// 각 전투 씬 루트 오브젝트에 붙인다.
///
/// - 멀티플레이: NetworkServer.active → GameNetworkManager.OnServerSceneChanged가 처리
/// - 싱글플레이: PlayerPrefs["SinglePlayer_CharName"] (캐릭터 이름)으로 프리팹을 이름 매칭해 스폰.
///              배열 순서에 의존하지 않으므로 Inspector 순서가 달라도 정상 작동.
///
/// [Inspector 연결]
/// - characterPrefabs : 비워두면 GameNetworkManager.characterPrefabs 자동 참조
/// </summary>
public class BattleSceneInitializer : MonoBehaviour
{
    [Header("캐릭터 프리팹 (싱글플레이 전용)")]
    [Tooltip("비워두면 GameNetworkManager.characterPrefabs를 자동으로 참조")]
    public GameObject[] characterPrefabs;

    private IEnumerator Start()
    {
        if (NetworkServer.active)
        {
            Debug.Log("[BattleSceneInitializer] 서버 활성 → 멀티플레이 경로, 스킵");
            yield break;
        }

        yield return null; // SpawnPoint Start() 완료 대기

        Debug.Log("[BattleSceneInitializer] 싱글플레이 감지 → 캐릭터 스폰 시작");

        GameObject[] prefabs = characterPrefabs;
        if (prefabs == null || prefabs.Length == 0)
        {
            GameNetworkManager gnm = NetworkManager.singleton as GameNetworkManager;
            if (gnm != null) prefabs = gnm.characterPrefabs;
        }

        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogError("[BattleSceneInitializer] characterPrefabs가 비어 있음! Inspector에서 할당하세요.");
            yield break;
        }

        // 이름으로 프리팹 탐색 (배열 순서 무관)
        string charName = PlayerPrefs.GetString("SinglePlayer_CharName", "");
        Debug.Log($"[BattleSceneInitializer] 저장된 캐릭터 이름: '{charName}'");

        GameObject prefabToSpawn = FindPrefabByName(prefabs, charName);

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[BattleSceneInitializer] '{charName}' 프리팹 없음 → 첫 번째 프리팹 사용");
            prefabToSpawn = prefabs[0];
        }

        Vector3 spawnPos = FindSpawnPosition("spawn_P1");

        GameObject character = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);
        Debug.Log($"[BattleSceneInitializer] '{prefabToSpawn.name}' 스폰 @ {spawnPos}");

        PlayerController controller = character.GetComponent<PlayerController>();
        if (controller != null)
            controller.ServerSetOwnerPlayerNetwork(null);
    }

    /// <summary>prefab 이름이 charName을 포함하는 것을 반환 (대소문자 무시).</summary>
    private GameObject FindPrefabByName(GameObject[] prefabs, string charName)
    {
        if (string.IsNullOrEmpty(charName)) return null;

        string lower = charName.ToLower();
        foreach (var p in prefabs)
        {
            if (p != null && p.name.ToLower().Contains(lower))
                return p;
        }
        return null;
    }

    private Vector3 FindSpawnPosition(string spawnID)
    {
        foreach (SpawnPoint sp in FindObjectsOfType<SpawnPoint>())
        {
            if (sp.spawnID == spawnID)
            {
                Debug.Log($"[BattleSceneInitializer] SpawnPoint '{spawnID}' @ {sp.transform.position}");
                return sp.transform.position;
            }
        }
        Debug.LogWarning($"[BattleSceneInitializer] SpawnPoint '{spawnID}' 없음 → (0,1,0) 사용");
        return new Vector3(0f, 1f, 0f);
    }
}
