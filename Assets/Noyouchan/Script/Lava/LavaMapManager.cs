using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LavaMapManager : MonoBehaviour
{
    [Header("화상 데미지")]
    public float burnDuration = 3f;
    public float burnDamagePerSecond = 10f;

    public event System.Action OnLavaEnter;
    public event System.Action OnLavaExit;

    private readonly Dictionary<CharaStat, Coroutine> burningPlayers = new Dictionary<CharaStat, Coroutine>();

    public void PlayerEnterLava(CharaStat stat)
    {
        if (burningPlayers.ContainsKey(stat)) return;

        var coroutine = StartCoroutine(BurnLoop(stat));
        burningPlayers.Add(stat, coroutine);
        OnLavaEnter?.Invoke();
    }

    public void PlayerExitLava(CharaStat stat)
    {
        if (!burningPlayers.TryGetValue(stat, out var coroutine)) return;

        StopCoroutine(coroutine);
        burningPlayers.Remove(stat);

        // 용암에서 나가도 burn 여운
        stat.Burn(burnDuration, burnDamagePerSecond);

        if (burningPlayers.Count == 0)
            OnLavaExit?.Invoke();
    }

    private IEnumerator BurnLoop(CharaStat stat)
    {
        while (true)
        {
            stat.Burn(burnDuration, burnDamagePerSecond);
            yield return new WaitForSeconds(burnDuration * 0.8f);
        }
    }
}