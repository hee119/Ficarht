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

    public Slider healthBar;
    public Slider staminaBar;
    public UnityEngine.InputSystem.PlayerInput playerInput;
    public PlayerController playerController;
    private Animator animator;
    public GameObject iceObject;
    public GameObject faintingObject;
    public GameObject fireObject;
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
    private Coroutine hitFlashCoroutine;
    private Rigidbody rb;

    
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
    public float staminaRegenRate = 10f;
    public float staminaRegenDelay = 3f; // 스킬 사용 후 재생 대기 시간
    public float staminaDrainRate; // 초당 소비량
    private float _lastStaminaUseTime = -999f;
    [HideInInspector] public Coroutine staminaDrainCoroutine;
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

        playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        playerController = GetComponent<PlayerController>();
        rb = GetComponent<Rigidbody>();

        if (iceObject != null)
            iceObject.SetActive(false);

        if (faintingObject != null)
            faintingObject.SetActive(false);

        if (fireObject != null)
            fireObject.SetActive(false);

        animator = GetComponent<Animator>();

        if (animator == null)
            Debug.LogError($"{name} : Animator가 없습니다.");

        if (playerInput == null)
            Debug.LogError($"{name} : PlayerInput이 없습니다.");

        if (healthBar == null)
            Debug.LogWarning($"{name} : healthBar가 NULL입니다.");

        if (characterStats != null)
            InitializeStats();
        else
            Debug.LogWarning($"{name} : characterStats가 NULL — SpawnCharacters에서 런타임 할당 예정");

        CacheRenderers();
    }

    /// <summary>
    /// ScriptableObject에서 스탯을 읽어 필드에 적용.
    /// Awake 이후 런타임에도 호출 가능 (GameNetworkManager.SpawnCharacters에서 호출됨).
    /// </summary>
    public void InitializeStats()
    {
        if (characterStats == null)
        {
            Debug.LogError($"{name} : InitializeStats 호출됐지만 characterStats가 NULL");
            return;
        }

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
            healthBar.value = health;
        }

        if (staminaBar != null)
        {
            staminaBar.maxValue = characterStats.stamina;
            staminaBar.value = stamina;
        }

        Debug.Log($"[CharaStat] {name} 스탯 초기화 완료: HP={health}, speed={speed}, runSpeed={runSpeed}");
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

        if (health <= 0)
        {
            Die();
        }
        if (healthBar != null)
            healthBar.value = health;

        // StopAllCoroutines()를 쓰면 BurnDamage/StatusTimer 등 진행 중인 상태이상
        // 코루틴까지 전부 죽어서, 화상 데미지 틱마다 Hit()이 호출될 때 상태 해제
        // 타이머가 계속 리셋되어 불(iceObject/fireObject)이 영원히 안 꺼지는 버그가 있었음.
        // HitFlash 코루틴만 따로 추적해서 중복 실행만 막는다.
        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);
        hitFlashCoroutine = StartCoroutine(HitFlash());
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

                // PlayerController를 완전히 비활성화 + Rigidbody를 kinematic으로 고정해야
                // 동상 걸리기 직전 입력값이 남아 계속 미끄러지는 문제 없이 완전히 멈춘다.
                if (playerController != null)
                    playerController.enabled = false;

                if (rb != null)
                {
                    rb.linearVelocity = Vector3.zero;
                    rb.angularVelocity = Vector3.zero;
                    rb.isKinematic = true;
                }

                // PlayerController를 꺼도 마지막으로 세팅된 Speed 값이 남아있어서
                // 블렌드 트리가 계속 걷기 애니메이션을 재생하는 문제 방지
                if (animator != null)
                    animator.SetFloat("Speed", 0f);

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
        fireObject.SetActive(true);
        while (currentStatus == Status.Burn)
        {
            if (burnDamage <= 0)
                Debug.LogError($"{name} : burnDamage가 0입니다.");
            Hit(health / burnDamage);
            yield return new WaitForSeconds(1f);
        }
        fireObject.SetActive(false);
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

                if (playerController != null)
                    playerController.enabled = true;

                if (rb != null)
                    rb.isKinematic = false;

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
            if (stamina < maxStamina && Time.time - _lastStaminaUseTime >= staminaRegenDelay)
            {
                stamina += staminaRegenRate * Time.deltaTime;
                stamina = Mathf.Min(stamina, maxStamina);

                if (staminaBar != null)
                    staminaBar.value = stamina;
            }

            yield return null;
        }
    }
    
    public IEnumerator StaminaDrain()
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
    }

    public bool UseStamina(float cost)
    {
        if (stamina < cost) return false;

        stamina -= cost;
        stamina = Mathf.Max(stamina, 0f);
        if (staminaBar != null) staminaBar.value = stamina;
        _lastStaminaUseTime = Time.time;
        return true;
    }

    public void Die()
    {
        animator.SetTrigger("Die");
        playerInput.enabled = false;
    }

    /// <summary>
    /// 네트워크 데미지 수신 시 피격 플래시 연출만 실행 (데미지 계산 없이).
    /// PlayerNetwork.RpcOnDamageEffect에서 호출.
    /// </summary>
    public void TriggerHitFlash()
    {
        if (hitFlashCoroutine != null)
            StopCoroutine(hitFlashCoroutine);
        hitFlashCoroutine = StartCoroutine(HitFlash());
    }
}