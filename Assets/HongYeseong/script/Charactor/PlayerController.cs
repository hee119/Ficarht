using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Mirror;

public class PlayerController : NetworkBehaviour
{
    // ─── Components ───────────────────────────────────────────────────────────
    private CharacterController cc;
    private Animator  animator;
    private CharaStat characterStats;
    public  UnityEngine.InputSystem.PlayerInput playerInput;

    // ─── Speed ────────────────────────────────────────────────────────────────
    [Header("Speed")]
    public float walkSpeed = 3f;
    public float runSpeed  = 6f;

    // ─── Jump / Gravity ───────────────────────────────────────────────────────
    [Header("Jump")]
    public float jumpForce = 5f;

    [Header("Gravity")]
    public float gravity = -20f;

    // ─── Roll ─────────────────────────────────────────────────────────────────
    [Header("Roll")]
    public float rollSpeed    = 8f;
    public float rollDuration = 1f;
    public bool  isRoll       = false;

    // ─── Runtime State ────────────────────────────────────────────────────────
    private Vector3 moveInput;
    private float   currentSpeed;
    private bool    isRunning;
    public  bool    isAttacking;
    private Vector3 velocity;        // Y축에 중력/점프 누적

    private Coroutine speedMultiplierRoutine;
    private float     speedMultiplier = 1f;

    [SyncVar] private uint ownerPlayerNetId;

    private bool IsLocallyControlled => isOwned || !NetworkClient.active;

    // ─────────────────────────────────────────────────────────────────────────
    // Lifecycle
    // ─────────────────────────────────────────────────────────────────────────
    void Awake()
    {
        cc             = GetComponent<CharacterController>();
        animator       = GetComponent<Animator>();
        characterStats = GetComponent<CharaStat>();
        playerInput    = GetComponent<UnityEngine.InputSystem.PlayerInput>();
    }

    void Start()
    {
        if (characterStats != null)
        {
            // CharaStat.Awake()가 SO를 읽지 못해 speed=0인 경우 재초기화
            if (characterStats.speed == 0f)
                characterStats.InitializeStats();

            walkSpeed = characterStats.speed;
            runSpeed  = characterStats.runSpeed;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        if (!NetworkClient.active)
            RegisterCamera();
    }

    [Server]
    public void ServerSetOwnerPlayerNetwork(PlayerNetwork owner)
    {
        ownerPlayerNetId = owner != null ? owner.netId : 0;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isOwned) { this.enabled = false; return; }
        RegisterCamera();
    }

    private void RegisterCamera()
    {
        CameraFollow cam = Camera.main?.GetComponent<CameraFollow>();
        if (cam != null)
            cam.SetTarget(transform);
        else
            Debug.LogWarning("[PlayerController] CameraFollow 없음 — Main Camera 태그 확인");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────────────────────────────────
    void Update()
    {
        playerInput.enabled = !isAttacking;

        if (!IsLocallyControlled || isRoll) return;

        PlayerNetwork pn = GetPlayerNetwork();
        if (pn != null && !pn.CanMove()) return;

        if (characterStats != null && characterStats.stamina <= 0)
            isRunning = false;

        currentSpeed = moveInput.magnitude > 0.01f
            ? (isRunning ? runSpeed : walkSpeed) * GetEffectiveSpeedMultiplier(pn)
            : 0f;

        animator.SetFloat("MoveX", moveInput.x);
        animator.SetFloat("MoveY", moveInput.z);
        animator.SetFloat("Speed", currentSpeed);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FixedUpdate
    // ─────────────────────────────────────────────────────────────────────────
    void FixedUpdate()
    {
        if (cc == null || !cc.enabled) return;
        if (!IsLocallyControlled || isAttacking || isRoll) return;

        PlayerNetwork pn = GetPlayerNetwork();
        if (pn != null && !pn.CanMove()) return;

        Vector3 moveDir = CalcMoveDir();

        // 이동 방향으로 회전
        if (moveDir.magnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.fixedDeltaTime * 10f);
        }

        // 접지 시 Y속도 초기화 (살짝 아래로 유지해야 cc.isGrounded 안정적)
        if (cc.isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        // 중력 누적
        velocity.y += gravity * Time.fixedDeltaTime;

        // 이동 적용 (수평 이동 + 중력)
        Vector3 move = moveDir * currentSpeed + Vector3.up * velocity.y;
        cc.Move(move * Time.fixedDeltaTime);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Movement Helper
    // ─────────────────────────────────────────────────────────────────────────
    private Vector3 CalcMoveDir()
    {
        if (moveInput.magnitude <= 0.01f || Camera.main == null)
            return Vector3.zero;

        Vector3 fwd = Camera.main.transform.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 rgt = Camera.main.transform.right;   rgt.y = 0f; rgt.Normalize();
        return (fwd * moveInput.z + rgt * moveInput.x).normalized;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Ground Check  (외부 스크립트 호환용)
    // ─────────────────────────────────────────────────────────────────────────
    public bool IsGrounded() => cc.isGrounded;

    // ─────────────────────────────────────────────────────────────────────────
    // Input Callbacks
    // ─────────────────────────────────────────────────────────────────────────
    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        Vector2 v = context.ReadValue<Vector2>();
        moveInput = new Vector3(v.x, 0f, v.y).normalized;
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;

        if (context.started)
        {
            isRunning = true;
            if (characterStats != null)
                characterStats.staminaDrainCoroutine = StartCoroutine(characterStats.StaminaDrain());
            if (NetworkClient.active && isOwned) CmdNotifyRunStarted();
            else if (!NetworkClient.active) Trap_Card.Instance?.NotifyRunStarted(GetPlayerNetwork());
        }
        else if (context.canceled)
        {
            isRunning = false;
            if (characterStats?.staminaDrainCoroutine != null)
                StopCoroutine(characterStats.staminaDrainCoroutine);
        }
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled || !context.started || !cc.isGrounded) return;

        velocity.y = jumpForce;

        if (NetworkClient.active && isOwned) CmdNotifyJump();
        else if (!NetworkClient.active) Trap_Card.Instance?.NotifyJump(GetPlayerNetwork());
    }

    public void OnMouseLook(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        Camera.main?.GetComponent<CameraFollow>()?.AddYawDelta(context.ReadValue<Vector2>().x);
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled || !context.started || isAttacking) return;

        PlayerNetwork pn = GetPlayerNetwork();
        if (pn != null && !pn.CanAttack()) return;

        if (NetworkClient.active)
        {
            CmdRequestAttack();
        }
        else
        {
            PlayerNetwork local = GetComponent<PlayerNetwork>();
            local?.ServerRequestAttack();
            Trap_Card.Instance?.NotifyAttack(local);
            StartCoroutine(AttackAnimation());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Roll
    // ─────────────────────────────────────────────────────────────────────────
    public void Roll() => StartCoroutine(IERoll());

    public IEnumerator IERoll()
    {
        isRoll = true;

        Vector3 dir = moveInput != Vector3.zero
            ? transform.TransformDirection(moveInput).normalized
            : transform.forward;

        float elapsed = 0f;
        while (elapsed < rollDuration)
        {
            elapsed += Time.deltaTime;

            if (cc.isGrounded && velocity.y < 0f) velocity.y = -2f;
            velocity.y += gravity * Time.deltaTime;

            cc.Move((dir * rollSpeed + Vector3.up * velocity.y) * Time.fixedDeltaTime);
            yield return new WaitForFixedUpdate();
        }

        isRoll = false;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Speed Utilities
    // ─────────────────────────────────────────────────────────────────────────
    public void RefreshSpeed()
    {
        characterStats ??= GetComponent<CharaStat>();
        if (characterStats == null) return;
        walkSpeed = characterStats.speed;
        runSpeed  = characterStats.runSpeed;
    }

    public void ApplyTemporarySpeedMultiplier(float multiplier, float duration)
    {
        if (speedMultiplierRoutine != null) StopCoroutine(speedMultiplierRoutine);
        speedMultiplierRoutine = StartCoroutine(SpeedMultiplierRoutine(multiplier, duration));
    }

    private IEnumerator SpeedMultiplierRoutine(float multiplier, float duration)
    {
        speedMultiplier = multiplier;
        yield return new WaitForSeconds(duration);
        speedMultiplier = 1f;
        speedMultiplierRoutine = null;
    }

    private float GetEffectiveSpeedMultiplier(PlayerNetwork pn)
    {
        if (speedMultiplier < 0.99f) return speedMultiplier;
        if (pn != null && pn.currentState == PlayerStateType.Slow) return 0.5f;
        return 1f;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Network
    // ─────────────────────────────────────────────────────────────────────────
    [Command] void CmdNotifyJump()       => Trap_Card.Instance?.NotifyJump(GetPlayerNetwork());
    [Command] void CmdNotifyRunStarted() => Trap_Card.Instance?.NotifyRunStarted(GetPlayerNetwork());

    [Command]
    void CmdRequestAttack()
    {
        PlayerNetwork pn = GetPlayerNetwork();
        pn?.ServerRequestAttack();
        Trap_Card.Instance?.NotifyAttack(pn);
        RpcPlayAttackAnimation();
    }

    [ClientRpc] void RpcPlayAttackAnimation() => StartCoroutine(AttackAnimation());

    IEnumerator AttackAnimation()
    {
        isAttacking = true;
        animator?.SetTrigger("Attack");
        yield return new WaitForSeconds(1f);
        isAttacking = false;
    }

    private PlayerNetwork GetPlayerNetwork()
    {
        PlayerNetwork self = GetComponent<PlayerNetwork>();
        if (self != null) return self;
        if (ownerPlayerNetId == 0) return null;

        NetworkIdentity id = null;
        if (NetworkServer.active)
            NetworkServer.spawned.TryGetValue(ownerPlayerNetId, out id);
        else if (NetworkClient.active)
            NetworkClient.spawned.TryGetValue(ownerPlayerNetId, out id);

        return id?.GetComponent<PlayerNetwork>();
    }
}
