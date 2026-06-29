using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LavaMapManager : MonoBehaviour
{
    [Header("화상 데미지")]
    [Tooltip("Burn 지속 시간 (초)")]
    public float burnDuration = 3f;
    [Tooltip("초당 데미지")]
    public float burnDamagePerSecond = 10f;

    public event System.Action OnLavaEnter;
    public event System.Action OnLavaExit;

    // 용암 안에 있는 플레이어 추적
    private readonly Dictionary<CharaStat, Coroutine> burningPlayers = new Dictionary<CharaStat, Coroutine>();

    private void OnTriggerEnter(Collider other)
    {
        var stat = other.GetComponent<CharaStat>();
        if (stat == null) return;

        if (!burningPlayers.ContainsKey(stat))
        {
            var coroutine = StartCoroutine(BurnLoop(stat));
            burningPlayers.Add(stat, coroutine);
            OnLavaEnter?.Invoke();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var stat = other.GetComponent<CharaStat>();
        if (stat == null) return;

        if (burningPlayers.TryGetValue(stat, out var coroutine))
        {
            StopCoroutine(coroutine);
            burningPlayers.Remove(stat);

            // 용암에서 나가도 burn 상태는 duration 동안 유지
            stat.Burn(burnDuration, burnDamagePerSecond);
        }

        if (burningPlayers.Count == 0)
            OnLavaExit?.Invoke();
    }

    /// <summary>용암 위에 있는 동안 계속 Burn 갱신</summary>
    private IEnumerator BurnLoop(CharaStat stat)
    {
        while (true)
        {
            stat.Burn(burnDuration, burnDamagePerSecond);
            yield return new WaitForSeconds(burnDuration * 0.8f); // 끊기지 않게 조금 일찍 갱신
        }
    }
}