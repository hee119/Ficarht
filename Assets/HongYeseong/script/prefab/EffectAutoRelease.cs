using UnityEngine;

public class EffectAutoRelease : MonoBehaviour
{
    private string id;

    public void Awake()
    {
        id = GetComponent<PrefabInfo>().skillData.skillId;
    }

    public void ReleaseToPool()
    {
        PoolManager.Instance.Release(id, gameObject);
    }
}
