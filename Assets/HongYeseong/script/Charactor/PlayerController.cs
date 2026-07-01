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
    public float     jumpForce   = 8f;
    public LayerMask groundLayer;        // 바닥 레이어 (보조 지면 판정용)

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
    private bool _isAttacking;
    public bool isAttacking
    {
        get => _isAttacking;
        set
        {
            _isAttacking = value;
            if (playerInput != null)
            {
                playerInput.enabled = !value;
                Debug.Log($"[PlayerController] isAttacking={value} → playerInput.enabled={playerInput.enabled}");
            }
            else
            {
                Debug.LogWarning("[PlayerController] playerInput이 NULL입니다.");
            }
        }
    }
    private Vector3 velocity;        // Y축에 중력/점프 누적
    private bool    isGrounded;      // FixedUpdate에서 갱신 — OnJump에서 참조
    private bool    isJumping;       // 점프 직후 ground reset 방지용

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
        if (!IsLocallyControlled || isRoll) return;

        if (characterStats != null && characterStats.stamina <= 0)
            isRunning = false;

        PlayerNetwork pn = GetPlayerNetwork();
        bool canMove = !isAttacking && (pn == null || pn.CanMove());

        currentSpeed = (canMove && moveInput.magnitude > 0.01f)
            ? (isRunning ? runSpeed : walkSpeed) * GetEffectiveSpeedMultiplier(pn)
            : 0f;

        animator.SetFloat("MoveX", canMove ? moveInput.x : 0f);
        animator.SetFloat("MoveY", canMove ? moveInput.z : 0f);
        animator.SetFloat("Speed", currentSpeed);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // FixedUpdate
    // ─────────────────────────────────────────────────────────────────────────
    void FixedUpdate()
    {
        if (cc == null || !cc.enabled) return;
        if (!IsLocallyControlled || isRoll) return;

        PlayerNetwork pn = GetPlayerNetwork();
        bool canMove = !isAttacking && (pn == null || pn.CanMove());

        Vector3 moveDir = canMove ? CalcMoveDir() : Vector3.zero;

        // 이동 방향으로 회전
        if (canMove && moveDir.magnitude > 0.01f)
        {
            Quaternion target = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, Time.fixedDeltaTime * 10f);
        }

        // 접지 판정 (OnJump에서도 이 값을 참조)
        isGrounded = IsGrounded();

        if (isGrounded)
        {
            // 점프 중이 아닐 때만 Y속도 초기화 (점프 직후 덮어쓰기 방지)
            if (!isJumping && velocity.y < 0f)
                velocity.y = -2f;

            // 실제로 공중에 떴다가 착지한 경우 플래그 해제
            if (isJumping && velocity.y <= 0f)
                isJumping = false;
        }
        else
        {
            // 공중에 있으면 플래그 해제
            isJumping = false;
        }

        // 중력 누적
        velocity.y += gravity * Time.fixedDeltaTime;

        // 이동 적용 (수평 이동 + 중력/점프)
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
    // Ground Check
    // ─────────────────────────────────────────────────────────────────────────
    public bool IsGrounded()
    {
        if (cc.isGrounded) return true;
        // cc.isGrounded 가 Update/FixedUpdate 타이밍 차이로 튈 때를 대비한 보조 판정
        Vector3 sphereCenter = transform.position + Vector3.down * (cc.height * 0.5f - cc.radius);
        return Physics.CheckSphere(sphereCenter, cc.radius + 0.05f, groundLayer);
    }

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
        // isGrounded는 FixedUpdate에서 갱신된 캐시값 — Update 타이밍의 cc.isGrounded보다 안정적
        if (!IsLocallyControlled || !context.started || !isGrounded || isJumping || isAttacking) return;

        isJumping    = true;
        velocity.y   = jumpForce;

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
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Roll
    // ─────────────────────────────────────────────────────────────────────────
    public void Roll() => StartCoroutine(IERoll());

    public IEnumerator IERoll()
    {
        isRoll = true;

        // CalcMoveDir()로 카메라 기준 방향 계산 — 없으면 현재 전방
        Vector3 calcDir = CalcMoveDir();
        Vector3 dir = calcDir.magnitude > 0.01f ? calcDir : transform.forward;

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
