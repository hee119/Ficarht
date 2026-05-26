using UnityEngine;

public class HitEffect : MonoBehaviour
{
    PrefabInfo prefabInfo;
    public GameObject hitEffefct;

    private void Awake()
    {
        prefabInfo = GetComponent<PrefabInfo>();
    }
    private void OnTriggerEnter(Collider other)
    {
        hitEffefct?.SetActive(true);
        PoolManager.Instance.Release(prefabInfo.id, gameObject);
    }
}
