using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SnowMapManager : MonoBehaviour
{
    [Header("동상 게이지")]
    [Tooltip("게이지가 가득 차는 데 걸리는 시간 (초)")]
    public float freezeGaugeDuration = 30f;

    [Tooltip("동상 지속 시간 (초)")]
    public float freezeDuration = 5f;

    [Header("슬로우")]
    [Tooltip("게이지 몇 % 부터 슬로우 시작 (0~1)")]
    public float slowStartThreshold = 0.5f;

    [Tooltip("최대 슬로우 % (게이지 다 찼을 때)")]
    public float maxSlowPercent = 60f;

    public event System.Action<float> OnGaugeChanged; // 0~1
    public event System.Action OnFreezeStart;
    public event System.Action OnFreezeEnd;

    private readonly List<CharaStat> playersInMap = new List<CharaStat>();

    // 플레이어별 게이지 (0~1)
    private readonly Dictionary<CharaStat, float> freezeGauge = new Dictionary<CharaStat, float>();
    private readonly Dictionary<CharaStat, bool> isFreezing = new Dictionary<CharaStat, bool>();

    private void Update()
    {
        foreach (var stat in playersInMap)
        {
            if (stat == null) continue;
            if (isFreezing.TryGetValue(stat, out bool freezing) && freezing) continue;

            // 게이지 증가
            freezeGauge[stat] += Time.deltaTime / freezeGaugeDuration;
            freezeGauge[stat] = Mathf.Clamp01(freezeGauge[stat]);

            OnGaugeChanged?.Invoke(freezeGauge[stat]);

            // 슬로우 적용 (게이지 50% 이상부터)
            ApplyColdSlow(stat, freezeGauge[stat]);

            // 게이지 가득 차면 동상
            if (freezeGauge[stat] >= 1f)
                StartCoroutine(ApplyFreeze(stat));
        }
    }

    private void ApplyColdSlow(CharaStat stat, float gauge)
    {
        if (gauge < slowStartThreshold) return;

        // 게이지 50%~100% 구간을 0%~maxSlowPercent%로 매핑
        float t = (gauge - slowStartThreshold) / (1f - slowStartThreshold);
        float slowPercent = Mathf.Lerp(0f, maxSlowPercent, t);

        // 매 프레임 speed를 직접 조정 (원본 대비 비율로)
        float targetSpeed   = stat.characterStats.speed    * (1f - slowPercent / 100f);
        float targetRunSpeed = stat.characterStats.runSpeed * (1f - slowPercent / 100f);

        stat.speed    = Mathf.Lerp(stat.speed,    targetSpeed,    Time.deltaTime * 2f);
        stat.runSpeed = Mathf.Lerp(stat.runSpeed, targetRunSpeed, Time.deltaTime * 2f);
        stat.GetComponent<PlayerController>()?.RefreshSpeed();
    }

    private IEnumerator ApplyFreeze(CharaStat stat)
    {
        if (isFreezing.ContainsKey(stat) && isFreezing[stat]) yield break;

        isFreezing[stat] = true;
        freezeGauge[stat] = 0f;
        OnFreezeStart?.Invoke();

        // CharaStat의 Freezing 상태 적용
        stat.Freezing(freezeDuration);

        yield return new WaitForSeconds(freezeDuration);

        // 동상 해제 후 speed 복구
        stat.speed    = stat.characterStats.speed;
        stat.runSpeed = stat.characterStats.runSpeed;
        stat.GetComponent<PlayerController>()?.RefreshSpeed();

        isFreezing[stat] = false;
        OnFreezeEnd?.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        var stat = other.GetComponent<CharaStat>();
        if (stat == null || playersInMap.Contains(stat)) return;

        playersInMap.Add(stat);
        freezeGauge[stat]  = 0f;
        isFreezing[stat]   = false;
    }

    private void OnTriggerExit(Collider other)
    {
        var stat = other.GetComponent<CharaStat>();
        if (stat == null) return;

        // 맵 벗어나면 게이지 천천히 감소
        StartCoroutine(DecreaseGauge(stat));
        playersInMap.Remove(stat);
    }

    /// <summary>맵 밖에서 게이지 서서히 감소</summary>
    private IEnumerator DecreaseGauge(CharaStat stat)
    {
        while (freezeGauge.ContainsKey(stat) && freezeGauge[stat] > 0f)
        {
            freezeGauge[stat] -= Time.deltaTime / (freezeGaugeDuration * 0.5f);
            freezeGauge[stat]  = Mathf.Clamp01(freezeGauge[stat]);
            OnGaugeChanged?.Invoke(freezeGauge[stat]);

            // speed 복구
            if (stat != null)
            {
                stat.speed    = Mathf.Lerp(stat.speed,    stat.characterStats.speed,    Time.deltaTime * 2f);
                stat.runSpeed = Mathf.Lerp(stat.runSpeed, stat.characterStats.runSpeed, Time.deltaTime * 2f);
                stat.GetComponent<PlayerController>()?.RefreshSpeed();
            }

            yield return null;
        }
    }
}