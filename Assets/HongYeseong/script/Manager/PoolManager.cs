using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class PoolManager : MonoBehaviour
{
    public static PoolManager Instance;

    // id -> prefab
    private Dictionary<string, GameObject> prefabDictionary = new();

    // id -> object pool
    private Dictionary<string, ObjectPool<GameObject>> poolDictionary = new();

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadPrefabs();
    }

    private void LoadPrefabs()
    {
        // Resources/Prefabs 안의 모든 프리팹 로드
        GameObject[] prefabs = Resources.LoadAll<GameObject>("Prefabs");
        Debug.Log($"찾은 프리팹 개수 : {prefabs.Length}");
        foreach (GameObject prefab in prefabs)
        {
            PrefabInfo info = prefab.GetComponent<PrefabInfo>();

            if (info == null)
            {
                Debug.LogWarning($"{prefab.name} 에 PrefabInfo 없음");
                continue;
            }

            string id = info.skillData.skillId;

            if (string.IsNullOrEmpty(id))
            {
                Debug.LogWarning($"{prefab.name} 의 id 비어있음");
                continue;
            }

            if (prefabDictionary.ContainsKey(id))
            {
                Debug.LogWarning($"중복된 ID : {id}");
                continue;
            }

            prefabDictionary.Add(id, prefab);

            Debug.Log($"프리팹 등록 완료 : {id}");

            ObjectPool<GameObject> pool = new ObjectPool<GameObject>
            (
                createFunc: () =>
                {
                    GameObject obj = Instantiate(prefab);
                    obj.SetActive(false);
                    return obj;
                },

                actionOnGet: (obj) =>
                {
                    obj.SetActive(true);
                },

                actionOnRelease: (obj) =>
                {
                    obj.SetActive(false);
                },

                actionOnDestroy: (obj) =>
                {
                    Destroy(obj);
                },

                collectionCheck: false,
                defaultCapacity: 10,
                maxSize: 100
            );

            poolDictionary.Add(id, pool);
        }

        Debug.Log("===== 등록된 프리팹 ID 목록 =====");

        foreach (string id in prefabDictionary.Keys)
        {
            Debug.Log(id);
        }
    }

    // 오브젝트 가져오기
    public GameObject Get(string id)
    {
        if (!poolDictionary.ContainsKey(id))
        {
            Debug.LogError($"ID {id} 없음");
            return null;
        }

        return poolDictionary[id].Get();
    }

    // 오브젝트 반환
    public void Release(string id, GameObject obj)
    {
        if (!poolDictionary.ContainsKey(id))
        {
            Destroy(obj);
            return;
        }

        poolDictionary[id].Release(obj);
    }

    // 프리팹 자체 반환
    public GameObject GetPrefab(string id, Transform transform)
    {
        GameObject obj = poolDictionary[id].Get();
        obj.transform.position += transform.position;
        return obj;
    }
}