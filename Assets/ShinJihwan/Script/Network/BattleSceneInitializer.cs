using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// 각 전투 씬 루트 오브젝트에 붙인다.
///
/// - 멀티플레이: NetworkServer.active → GameNetworkManager.OnServerSceneChanged가 이미
///               SpawnCharacters()를 호출하므로 여기서는 아무것도 안 함.
/// - 싱글플레이: SceneManager.LoadScene으로 들어온 경우 서버가 없으므로
///               PlayerPrefs에 저장된 characterId로 캐릭터를 직접 스폰.
///
/// [Inspector 연결]
/// - characterPrefabs : GameNetworkManager와 동일한 순서로 캐릭터 프리팹 배열 할당
///                      (없으면 GameNetworkManager.characterPrefabs 자동 참조 시도)
/// </summary>
public class BattleSceneInitializer : MonoBehaviour
{
    [Header("캐릭터 프리팹 (싱글플레이 전용)")]
    [Tooltip("비워두면 GameNetworkManager.characterPrefabs를 자동으로 참조")]
    public GameObject[] characterPrefabs;

    private IEnumerator Start()
    {
        // 멀티플레이 중이면 GameNetworkManager가 처리하므로 종료
        if (NetworkServer.active)
        {
            Debug.Log("[BattleSceneInitializer] 서버 활성 → 멀티플레이 경로, 스킵");
            yield break;
        }

        // 싱글플레이: 1프레임 대기 후 스폰 (SpawnPoint들이 모두 Start() 완료되도록)
        yield return null;

        Debug.Log("[BattleSceneInitializer] 싱글플레이 감지 → 캐릭터 스폰 시작");

        // GameNetworkManager 프리팹 배열 자동 참조
        GameObject[] prefabs = characterPrefabs;
        if (prefabs == null || prefabs.Length == 0)
        {
            GameNetworkManager gnm = NetworkManager.singleton as GameNetworkManager;
            if (gnm != null)
                prefabs = gnm.characterPrefabs;
        }

        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogError("[BattleSceneInitializer] characterPrefabs가 비어 있음! Inspector에서 할당하세요.");
            yield break;
        }

        // 선택된 캐릭터 ID 읽기
        int charId = PlayerPrefs.GetInt("SinglePlayer_CharId", 0);
        if (charId < 0 || charId >= prefabs.Length || prefabs[charId] == null)
        {
            Debug.LogWarning($"[BattleSceneInitializer] characterPrefabs[{charId}] 없음 → index 0 사용");
            charId = 0;
        }

        // SpawnPoint "spawn_P1" 찾기
        Vector3 spawnPos = FindSpawnPosition("spawn_P1");

        // 캐릭터 스폰
        GameObject character = Instantiate(prefabs[charId], spawnPos, Quaternion.identity);
        Debug.Log($"[BattleSceneInitializer] charId={charId} → {prefabs[charId].name} 스폰 @ {spawnPos}");

        // PlayerController 설정 (단일 플레이어이므로 소유자 없음)
        PlayerController controller = character.GetComponent<PlayerController>();
        if (controller != null)
            controller.ServerSetOwnerPlayerNetwork(null);
    }

    /// <summary>씬의 SpawnPoint 중 spawnID가 일치하는 것의 위치 반환. 없으면 폴백.</summary>
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
