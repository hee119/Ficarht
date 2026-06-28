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
        defaultPosRot = GetComponent<DefaultPosRot>(); // 👈 기존 초기값 컴포넌트 가져오기
        prefabInfo = GetComponent<PrefabInfo>();
        
        skillDuration = prefabInfo.duration;

        if (prefabInfo == null)
        {
            Debug.LogError($"{gameObject.name}에 PrefabInfo 없음!");
        }

        if(ps != null)
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

        if(ps != null)
            ps.Play();

        SetAlpha(1f);

        StartCoroutine(DurationCoroutine());
    }

    // DefaultPosRot 컴포넌트를 가져오기 위한 변수 추가
    private DefaultPosRot defaultPosRot;

    private void Update()
    {
        // 버프면 플레이어 위치 + 기존 오프셋, 플레이어 회전 * 기존 오프셋 회전 적용
        if (player != null)
        {
            transform.position = player.TransformPoint(defaultPosRot.defaultLocalPosition);
            transform.rotation = player.rotation * defaultPosRot.defaultLocalRotation;
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

        if(ps != null)
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);

        PoolManager.Instance.Release(
            prefabInfo.skillData.skillId,
            gameObject
        );
    }

    private void SetAlpha(float alpha)
    {
        if (ps != null)
        {
            var col = main.startColor.color;
            col.a = alpha;
            main.startColor = col;
        }
    }
}