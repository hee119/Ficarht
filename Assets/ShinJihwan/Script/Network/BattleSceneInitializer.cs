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
            yield break;
        }

        yield return null; // SpawnPoint Start() 완료 대기

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

        GameObject prefabToSpawn = FindPrefabByName(prefabs, charName);

        if (prefabToSpawn == null)
        {
            Debug.LogWarning($"[BattleSceneInitializer] '{charName}' 프리팹 없음 → 첫 번째 프리팹 사용");
            prefabToSpawn = prefabs[0];
        }

        Vector3 spawnPos = FindSpawnPosition("spawn_P1");

        GameObject character = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity);

        ApplySinglePlayerCardSelection(character);

        PlayerController controller = character.GetComponent<PlayerController>();
        if (controller != null)
            controller.ServerSetOwnerPlayerNetwork(null);
    }

    private void ApplySinglePlayerCardSelection(GameObject character)
    {
        if (character == null)
            return;

        PlayerNetwork playerNetwork = character.GetComponent<PlayerNetwork>();

        if (PlayerPrefs.HasKey("SinglePlayer_HP"))
        {
            float hp = PlayerPrefs.GetFloat("SinglePlayer_HP", 100f);
            float stamina = PlayerPrefs.GetFloat("SinglePlayer_STM", 50f);
            float power = PlayerPrefs.GetFloat("SinglePlayer_PWR", 50f);
            float defense = PlayerPrefs.GetFloat("SinglePlayer_DEF", 50f);
            float intelligence = PlayerPrefs.GetFloat("SinglePlayer_INT", 50f);

            if (playerNetwork != null)
                playerNetwork.ApplyStatsForLocalTest(hp, stamina, power, defense, intelligence);

            ApplyStatsToCharaStat(character, hp, stamina, power, defense, intelligence);

            Debug.Log(
                $"[CARD TEST][SINGLE][BUFF] 전투 캐릭터에 적용: " +
                $"HP={hp}, STM={stamina}, PWR={power}, DEF={defense}, INT={intelligence}"
            );
        }

        int trapCount = PlayerPrefs.GetInt("SinglePlayer_TrapCount", 0);
        int[] trapIds = new int[trapCount];

        for (int i = 0; i < trapCount; i++)
            trapIds[i] = PlayerPrefs.GetInt($"SinglePlayer_Trap_{i}", 0);

        if (playerNetwork != null)
        {
            playerNetwork.RegisterTrapsForLocalTest(trapIds);
            Trap_Card.GetOrCreate().InitializeFromPlayers(new[] { playerNetwork });
        }

        Debug.Log($"[CARD TEST][SINGLE][TRAP] 전투 캐릭터에 등록: {trapCount}개");
    }

    private void ApplyStatsToCharaStat(
        GameObject character,
        float hp,
        float stamina,
        float power,
        float defense,
        float intelligence
    )
    {
        CharaStat charaStat = character.GetComponent<CharaStat>();

        if (charaStat == null)
        {
            Debug.LogWarning("[CARD TEST][SINGLE][BUFF] CharaStat이 없어 실제 캐릭터 스탯 적용 실패");
            return;
        }

        charaStat.maxHealth = Mathf.Max(hp, 1f);
        charaStat.health = charaStat.maxHealth;
        charaStat.maxStamina = Mathf.Max(stamina, 0f);
        charaStat.stamina = charaStat.maxStamina;
        charaStat.power = Mathf.Max(power, 0f);
        charaStat.defense = Mathf.Max(defense, 0f);
        charaStat.intelligence = Mathf.Max(intelligence, 0f);

        if (charaStat.healthBar != null)
        {
            charaStat.healthBar.maxValue = charaStat.maxHealth;
            charaStat.healthBar.value = charaStat.health;
        }

        if (charaStat.staminaBar != null)
        {
            charaStat.staminaBar.maxValue = charaStat.maxStamina;
            charaStat.staminaBar.value = charaStat.stamina;
        }

        character.GetComponent<PlayerController>()?.RefreshSpeed();

        Debug.Log(
            $"[CARD TEST][SINGLE][BUFF] CharaStat 직접 적용 확인: " +
            $"HP={charaStat.health}/{charaStat.maxHealth}, " +
            $"STM={charaStat.stamina}/{charaStat.maxStamina}, " +
            $"PWR={charaStat.power}, DEF={charaStat.defense}, INT={charaStat.intelligence}"
        );
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
                return sp.transform.position;
            }
        }
        Debug.LogWarning($"[BattleSceneInitializer] SpawnPoint '{spawnID}' 없음 → (0,1,0) 사용");
        return new Vector3(0f, 1f, 0f);
    }
}
