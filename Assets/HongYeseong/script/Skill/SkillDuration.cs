using System.Collections;
using UnityEngine;

public class SkillDuration : MonoBehaviour
{
    public float skillDuration = 2f;
    public float fadeDuration = 1f;

    private ParticleSystem ps;
    private PrefabInfo prefabInfo;
    private ParticleSystem.MainModule main;

    private Transform player;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();

        prefabInfo = GetComponent<PrefabInfo>();
        
        skillDuration = prefabInfo.duration;

        if (prefabInfo == null)
        {
            Debug.LogError($"{gameObject.name}에 PrefabInfo 없음!");
        }

        main = ps.main;
    }

    private void OnEnable()
    {
        StopAllCoroutines();

        // 버프면 플레이어 찾기
        if (prefabInfo.skillData.isBuff)
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }
        else
        {
            player = null;
        }

        ps.Play();

        SetAlpha(1f);

        StartCoroutine(DurationCoroutine());
    }

    private void Update()
    {
        // 버프면 플레이어 따라가기
        if (player != null)
        {
            transform.position = player.position;
        }
    }

    private IEnumerator DurationCoroutine()
    {
        yield return new WaitForSeconds(skillDuration);

        float t = 0f;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;

            float alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            SetAlpha(alpha);

            yield return null;
        }

        SetAlpha(0f);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        PoolManager.Instance.Release(
            prefabInfo.skillData.skillId,
            gameObject
        );
    }

    private void SetAlpha(float alpha)
    {
        var col = main.startColor.color;
        col.a = alpha;
        main.startColor = col;
    }
}