using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DesertMapManager : MonoBehaviour
{
    [Header("모래폭풍 주기")]
    public float calmDuration = 20f;
    public float stormDuration = 10f;

    [Header("모래폭풍 데미지")]
    public float tickInterval = 1f;
    public float tickDamage = 5f;

    [Header("연출")]
    public GameObject sandstormEffect;
    public float warningBeforeStorm = 5f;

    public bool IsStorming { get; private set; } = false;

    public event System.Action OnWarningStart;
    public event System.Action OnWarningEnd;
    public event System.Action OnStormStart;
    public event System.Action OnStormEnd;

    private readonly List<CharaStat> playersInMap = new List<CharaStat>();

    private void Start()
    {
        if (sandstormEffect != null) sandstormEffect.SetActive(false);
        StartCoroutine(StormCycle());
    }

    private IEnumerator StormCycle()
    {
        while (true)
        {
            // 1. 평온 구간
            float calmWait = calmDuration - warningBeforeStorm;
            if (calmWait < 0f) calmWait = 0f;
            yield return new WaitForSeconds(calmWait);

            // 2. 경고 구간
            OnWarningStart?.Invoke();
            yield return new WaitForSeconds(warningBeforeStorm);
            OnWarningEnd?.Invoke();

            // 3. 폭풍 시작
            StartStorm();
            yield return new WaitForSeconds(stormDuration);

            // 4. 폭풍 종료
            EndStorm();
        }
    }

    private void StartStorm()
    {
        IsStorming = true;
        OnStormStart?.Invoke();
        if (sandstormEffect != null) sandstormEffect.SetActive(true);
        StartCoroutine(StormTickDamage());
        Debug.Log("[DesertMap] 모래폭풍 시작!");
    }

    private void EndStorm()
    {
        IsStorming = false;
        OnStormEnd?.Invoke();
        if (sandstormEffect != null) sandstormEffect.SetActive(false);
        Debug.Log("[DesertMap] 모래폭풍 종료");
    }

    private IEnumerator StormTickDamage()
    {
        while (IsStorming)
        {
            foreach (var stat in playersInMap)
            {
                if (stat == null) continue;
                stat.Hit(tickDamage);
            }
            yield return new WaitForSeconds(tickInterval);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        var stat = other.GetComponent<CharaStat>();
        if (stat != null && !playersInMap.Contains(stat))
            playersInMap.Add(stat);
    }

    private void OnTriggerExit(Collider other)
    {
        var stat = other.GetComponent<CharaStat>();
        if (stat != null)
            playersInMap.Remove(stat);
    }
}