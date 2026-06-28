using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.InputSystem;
public class CharaStat : MonoBehaviour
{
    public CharacterStats characterStats;

    private Rigidbody charactorRb;
    public Slider healthBar;
    public Slider staminaBar;
    public UnityEngine.InputSystem.PlayerInput playerInput;
    private PlayerController playerController;
    private Animator animator;
    public GameObject iceObject;
    public GameObject faintingObject;
    public string faintingAnimationName = "Stun";
    private float burnDamage;
    private float slowAmount;
    private float restoreStatPowerDebuff;
    private float restoreStatSpeedDebuff;
    private float restoreStatDefenseDebuff;
    private float restoreStatPowerBuff;
    private float restoreStatSpeedBuff;
    private float restoreStatDefenseBuff;
    private float restoreStatRunSpeedDebuff;
    private float restoreStatRunSpeedBuff;
    private float shieldHp;
    public bool isShield = false;
    public GameObject shieldObject;
    
    private Renderer[] allRenderers;
    private Dictionary<Renderer, Color[]> originalColors = new Dictionary<Renderer, Color[]>();

    
    [Header("Hit Flash")]
    public float flashDuration = 0.1f;
    public Color flashColor = Color.white;
    
    [Header("Block")]
    public bool isBlocking = false;
    public float blockDamageReduction = 50f; // %
    public float blockStaminaPerSecond = 10f;
    
    [Header("Stats")]
    public float maxHealth;
    public float health;
    public float maxStamina;
    public float stamina;
    public float staminaRegenRate;
    public float staminaDrainRate; // 초당 소비량
    public float power;
    public float defense;
    public float intelligence;
    public float speed;
    public float runSpeed;
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
        if (characterStats == null)
            Debug.LogError($"{name} : characterStats가 NULL입니다.");

        charactorRb = GetComponent<Rigidbody>();
        playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        playerController = GetComponent<PlayerController>();
        
        if (iceObject != null)
            iceObject.SetActive(false);
        
        if (faintingObject != null)
            faintingObject.SetActive(false);
        
        animator = GetComponent<Animator>();

        if (animator == null)
            Debug.LogError($"{name} : Animator가 없습니다.");
        
        

        if (charactorRb == null)
            Debug.LogError($"{name} : Rigidbody가 없습니다.");

        if (playerInput == null)
            Debug.LogError($"{name} : PlayerController가 없습니다.");

        if (healthBar == null)
            Debug.LogError($"{name} : healthBar가 NULL입니다.");

        maxHealth = characterStats.health;
        health = characterStats.health;
        maxStamina = characterStats.stamina;
        stamina = characterStats.stamina;
        power = characterStats.power;
        defense = characterStats.defense;
        intelligence = characterStats.intelligence;
        speed = characterStats.speed;
        runSpeed = characterStats.runSpeed;
        projectileSpeed = characterStats.projectileSpeed;
        cooldown = characterStats.cooldown;
        duration = characterStats.duration;

        if (healthBar != null)
        {
            healthBar.maxValue = characterStats.health;
        }
        
        if (staminaBar != null)
        {
            staminaBar.maxValue = characterStats.stamina;
        }
        
        CacheRenderers();

    }
    
    private void OnEnable()
    {
        StartCoroutine(StaminaRegen());
    }
    
    private void CacheRenderers()
    {
        allRenderers = GetComponentsInChildren<Renderer>(true);

        foreach (var rend in allRenderers)
        {
            // 각 렌더러의 머티리얼 색 저장
            var mats = rend.materials;
            Color[] cols = new Color[mats.Length];

            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].HasProperty("_Color"))
                    cols[i] = mats[i].color;
                else
                    cols[i] = Color.white;
            }

            originalColors[rend] = cols;
        }
    }

    public void Hit(float damage)
    {
        if (isShield)
        {
            shieldHp -= damage;

            if (shieldHp <= 0)
            {
                shieldHp = 0;
                PoolManager.Instance.Release("PaladinShield" ,shieldObject);
                isShield = false;
            }
            
            return;
        }
        if (healthBar == null)
            Debug.LogWarning($"{name} : healthBar가 NULL입니다.");

        // 1. 블록 적용
        if (isBlocking)
        {
            damage *= (100f - blockDamageReduction) / 100f;
        }

        // 2. 방어력 적용 (핵심)
        float defenseFactor = 100f / (100f + defense);
        damage *= defenseFactor;

        // 3. 최소 데미지 보장 (0 방지)
        if (damage < 1f)
            damage = 1f;

        health -= damage;

        if (healthBar != null)
            healthBar.value = health;

        StopAllCoroutines();
        StartCoroutine(HitFlash());
    }
    
    private IEnumerator HitFlash()
    {
        SetFlashColor(flashColor);

        yield return new WaitForSeconds(flashDuration);

        RestoreOriginalColors();
    }
    
    private void RestoreOriginalColors()
    {
        foreach (var rend in allRenderers)
        {
            if (!originalColors.ContainsKey(rend)) continue;

            var mats = rend.materials;
            var cols = originalColors[rend];

            for (int i = 0; i < mats.Length && i < cols.Length; i++)
            {
                if (mats[i].HasProperty("_Color"))
                    mats[i].color = cols[i];
            }
        }
    }
    private void SetFlashColor(Color c)
    {
        foreach (var rend in allRenderers)
        {
            var mats = rend.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i].HasProperty("_Color"))
                    mats[i].color = c;
            }
        }
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
        if (playerInput == null)
            Debug.LogError($"{name} : PlayerController가 NULL입니다.");

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
                break;

            case Status.Fainting:
                playerInput.enabled = false;

                if (animator != null)
                    animator.Play(faintingAnimationName);

                if (faintingObject != null)
                    faintingObject.SetActive(true);
                else
                    Debug.LogError($"{name} : faintingObject가 NULL입니다.");

                break;

            case Status.Freezing:
                playerInput.enabled = false;

                if (iceObject != null)
                    iceObject.SetActive(true);
                else
                    Debug.LogError($"{name} : iceObject가 NULL입니다.");

                break;
        }

        statusCoroutine = StartCoroutine(StatusTimer(duration));
    }

    private IEnumerator BurnDamage()
    {
        while (currentStatus == Status.Burn)
        {
            if (burnDamage <= 0)
                Debug.LogError($"{name} : burnDamage가 0입니다.");
            Hit(health / burnDamage);
            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator StatusTimer(float duration)
    {
        if (playerInput == null)
            Debug.LogError($"{name} : PlayerController가 NULL입니다.");

        yield return new WaitForSeconds(duration);

        switch (currentStatus)
        {
            case Status.Burn:
                break;

            case Status.Slowdown:
                break;

            case Status.Fainting:
                playerInput.enabled = true;

                if (faintingObject != null)
                    faintingObject.SetActive(false);

                break;

            case Status.Freezing:
                playerInput.enabled = true;

                if (iceObject != null)
                    iceObject.SetActive(false);

                break;
        }

        power += restoreStatPowerDebuff;
        speed += restoreStatSpeedDebuff;
        runSpeed += restoreStatRunSpeedDebuff;
        defense += restoreStatDefenseDebuff;

        playerController.RefreshSpeed();
        currentStatus = Status.Default;
        statusCoroutine = null;
    }
    
    private IEnumerator BuffTimer(float duration)
    {
        yield return new WaitForSeconds(duration);

        power += restoreStatPowerBuff;
        speed += restoreStatSpeedBuff;
        runSpeed += restoreStatRunSpeedBuff;
        defense += restoreStatDefenseBuff;

        playerController.RefreshSpeed();
    }

    public void ApplyBuff(float power, float speed, float defense, float duration)
    {
        restoreStatPowerBuff = this.power * (power / 100f);
        restoreStatSpeedBuff = this.speed * (speed / 100f);
        restoreStatRunSpeedBuff = this.runSpeed * (speed / 100f);
        restoreStatDefenseBuff = this.defense * (defense / 100f);

        if (power != 0)
            this.power += this.power * (power / 100f);

        if (speed != 0)
        {
            this.speed += this.speed * (speed / 100f);
            this.runSpeed += this.runSpeed * (speed / 100f);
        }

        if (defense != 0)
            this.defense += this.defense * (defense / 100f);

        playerController.RefreshSpeed();

        StartCoroutine(BuffTimer(duration));
    }

    public void ApplyDebuff(float power, float speed, float defense)
    {
        restoreStatPowerDebuff = this.power * (power / 100f);
        restoreStatSpeedDebuff = this.speed * (speed / 100f);
        restoreStatRunSpeedDebuff = this.runSpeed * (speed / 100f);
        restoreStatDefenseDebuff = this.defense * (defense / 100f);

        if (power != 0)
            this.power -= this.power * (power / 100f);

        if (speed != 0)
        {
            this.speed -= this.speed * (speed / 100f);
            this.runSpeed -= this.runSpeed * (speed / 100f);
        }

        if (defense != 0)
            this.defense -= this.defense * (defense / 100f);

        playerController.RefreshSpeed();
    }

    public IEnumerator ApplyShield(float defense, float duration, GameObject shield)
    {
        isShield = true;
        shieldObject = shield;
        shieldHp = defense;
        yield return new WaitForSeconds(duration);
        isShield = false;
    }
    
    private IEnumerator StaminaRegen()
    {
        while (true)
        {
            if (stamina < maxStamina)
            {
                stamina += staminaRegenRate * Time.deltaTime;
                stamina = Mathf.Min(stamina, maxStamina);

                if (staminaBar != null) 
                    staminaBar.value = stamina;
            }

            yield return null;
        }
    }
    
    private IEnumerator StaminaDrain()
    {
        while (true)
        {
            if (stamina > 0f)
            {
                stamina -= staminaDrainRate * Time.deltaTime;
                stamina = Mathf.Max(stamina, 0f);

                if (staminaBar != null)
                    staminaBar.value = stamina;
            }

            yield return null;
        }
    }
    
    public void StaminaDrain(float drainRate)
    {
        if (stamina > 0f)
        {
            stamina -= drainRate * Time.deltaTime;
            stamina = Mathf.Max(stamina, 0f);

            if (staminaBar != null)
                staminaBar.value = stamina;
        }
    }[
    =]
}