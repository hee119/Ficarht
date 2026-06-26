using System.Collections;
using UnityEngine;
using UnityEngine.UI;
public class CharaStat : MonoBehaviour
{
    public CharacterStats characterStats;

    private Rigidbody charactorRb;
    private AnimManager animManager;
    public Slider healthBar;
    
    private float burnDamage;
    private float slowAmount;
    private bool isSlow;

    [Header("Stats")] 
    public float health; // 체력
    public float stamina; // 스테미너
    public float power; // 힘
    public float defense; // 방어력
    public float intelligence; // 지식
    public float speed; // 속도
    public float projectileSpeed;
    public float cooldown;
    public float duration;

    public enum Status
    {
        Default = 0,
        Burn = 1,
        Slowdown = 2,
        Fainting = 3,
        Freezing = 4
    }

    public Status currentStatus = Status.Default;

    private Coroutine statusCoroutine;

    void Awake()
    {
        charactorRb = GetComponent<Rigidbody>();
        animManager = GetComponent<AnimManager>();

        health = characterStats.health;
        stamina = characterStats.stamina;
        power = characterStats.power;
        defense = characterStats.defense;
        intelligence = characterStats.intelligence;
        speed = characterStats.speed;
        projectileSpeed = characterStats.projectileSpeed;
        cooldown = characterStats.cooldown;
        duration = characterStats.duration;
        
        healthBar.maxValue = characterStats.health;
    }

    public void Hit(float damage)
    {
        health -= damage;
        healthBar.value = health;
    }

    public void Burn(float duration, float damagePerSecond)
    {
        burnDamage = damagePerSecond;
        ApplyStatus(Status.Burn, duration);
    }

    public void Slowdown(float duration, float slowPercent)
    {
        slowAmount = slowPercent;
        ApplyStatus(Status.Slowdown, duration);
    }

    public void Fainting(float duration)
    {
        ApplyStatus(Status.Fainting, duration);
    }

    public void Freezing(float duration)
    {
        ApplyStatus(Status.Freezing, duration);
    }

    private void ApplyStatus(Status newStatus, float duration)
    {
        // 우선순위가 낮으면 무시
        if ((int)newStatus < (int)currentStatus)
            return;

        if (statusCoroutine != null)
            StopCoroutine(statusCoroutine);

        currentStatus = newStatus;

        switch (newStatus)
        {
            case Status.Burn:
                StartCoroutine(BurnDamage());
                break;

            case Status.Slowdown:
                ApplyDebuff(0, slowAmount, 0);
                isSlow = true;
                break;

            case Status.Fainting:
                animManager.enabled = false;
                break;

            case Status.Freezing:
                speed = 0;
                animManager.enabled = false;
                break;
        }
        statusCoroutine = StartCoroutine(StatusTimer(duration));
    }
    
    private IEnumerator BurnDamage()
    {
        while (currentStatus == Status.Burn)
        {
            Hit(burnDamage);
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator StatusTimer(float duration)
    {
        yield return new WaitForSeconds(duration);

        currentStatus = Status.Default;
        statusCoroutine = null;
        if (isSlow)
        {
            ApplyBuff(0, slowAmount,0);
        }
    }

    public void ApplyBuff(float power, float speed, float defense)
    {
        if (power   != 0) this.power   += this.power   * power;
        if (speed   != 0) this.speed   += this.speed   * speed;
        if (defense != 0) this.defense += this.defense * defense;
    }

    public void ApplyDebuff(float power, float speed, float defense)
    {
        if (power   != 0) this.power   -= this.power   * power;
        if (speed   != 0) this.speed   -= this.speed   * speed;
        if (defense != 0) this.defense -= this.defense * defense;
    }
}