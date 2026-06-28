using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Mirror;

/// <summary>
/// Mirror 멀티플레이 PlayerController.
///
/// isLocalPlayer → isOwned 로 변경.
/// isOwned || !NetworkClient.active → Mirror 없는 싱글 테스트에서도 조작 가능.
/// </summary>
public class PlayerController : NetworkBehaviour
{
    private Rigidbody rb;
    private Animator animator;
    private CharaStat characterStats;

    [Header("Speed")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;

    private Vector3 moveInput;
    private float currentSpeed;
    private bool isRunning;
    private bool isAttacking;

    // Mirror 없는 싱글 테스트에서도 입력 처리
    private bool IsLocallyControlled => isOwned || !NetworkClient.active;

    public float mouseSensitivity = 100f;

    private float mouseInputX = 0;
    private float mouseInputY = 0;

    [Header("Jump")]
    public float jumpForce = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckDistance = 0.4f;
    public LayerMask groundLayer;

    public bool isRoll = false;

    [SyncVar]
    private uint ownerPlayerNetId;

    private Coroutine speedMultiplierRoutine;
    private float speedMultiplier = 1f;
    
    public float rollSpeed = 8f;
    public float rollDuration = 0.7f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        characterStats = GetComponent<CharaStat>();
    }

    void Start()
    {
        if (characterStats != null)
        {
            walkSpeed = characterStats.speed;
            runSpeed = characterStats.runSpeed;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    [Server]
    public void ServerSetOwnerPlayerNetwork(PlayerNetwork owner)
    {
        ownerPlayerNetId = owner != null ? owner.netId : 0;
    }

    // ─────────────────────────────────────────────
    // Mirror: 소유권 없는 클라이언트는 스크립트 비활성화
    // ─────────────────────────────────────────────
    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!isOwned)
            this.enabled = false;
    }

    // ─────────────────────────────────────────────
    // Update / FixedUpdate
    // ─────────────────────────────────────────────
    void Update()
    {
        if (!IsLocallyControlled) return;
        if (isAttacking || isRoll) return;

        PlayerNetwork pn = GetPlayerNetwork();
        if (pn != null && !pn.CanMove()) return;

        if (characterStats != null && characterStats.stamina <= 0)
            isRunning = false;

        currentSpeed = moveInput.magnitude > 0.01f
            ? (isRunning ? runSpeed : walkSpeed)
                * GetEffectiveSpeedMultiplier(pn)
            : 0f;

        animator.SetFloat("MoveX", moveInput.x);
        animator.SetFloat("MoveY", moveInput.z);
        animator.SetFloat("Speed", currentSpeed);

        // Ground Check 레이 보기
        Debug.DrawRay(
            groundCheck.position,
            Vector3.down * groundCheckDistance,
            IsGrounded() ? Color.green : Color.red
        );
    }

    void FixedUpdate()
    {
        if (!IsLocallyControlled) return;
        if (isAttacking || isRoll) return;

        PlayerNetwork pn = GetPlayerNetwork();
        if (pn != null && !pn.CanMove()) return;

        // 카메라 기준 이동 방향 계산
        Vector3 moveDir = Vector3.zero;
        Camera cam = Camera.main;
        if (cam != null && moveInput.magnitude > 0.01f)
        {
            Vector3 camForward = cam.transform.forward; camForward.y = 0f; camForward.Normalize();
            Vector3 camRight   = cam.transform.right;   camRight.y   = 0f; camRight.Normalize();
            moveDir = (camForward * moveInput.z + camRight * moveInput.x).normalized;
        }

        // 이동 방향으로 플레이어 회전 (자연스럽게 Slerp)
        if (moveDir.magnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 10f);
        }

        Vector3 velocity = moveDir * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
    }

    // ─────────────────────────────────────────────
    // Input 콜백 (PlayerInput → Invoke Unity Events)
    // ─────────────────────────────────────────────
    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        Vector2 input = context.ReadValue<Vector2>();

        moveInput = new Vector3(
            input.x,
            0f,
            input.y
        ).normalized;
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;

        if (context.started)
        {
            isRunning = true;

            if (characterStats != null)
                characterStats.staminaDrainCoroutine = StartCoroutine(characterStats.StaminaDrain());

            if (NetworkClient.active && isOwned)
                CmdNotifyRunStarted();
        }

        if (context.canceled)
        {
            isRunning = false;

            if (characterStats != null && characterStats.staminaDrainCoroutine != null)
                StopCoroutine(characterStats.staminaDrainCoroutine);
        }
    }

    // =========================
    // JUMP INPUT
    // =========================
    public void OnJump(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        if (!context.started) return;

        bool grounded = IsGrounded();

        Debug.Log($"Grounded : {grounded}");

        if (grounded)
        {
            rb.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse
            );

            Debug.Log("점프!");

            if (NetworkClient.active && isOwned)
                CmdNotifyJump();
        }
    }

    // =========================
    // MOUSE LOOK → 카메라 회전 (플레이어 회전 없음)
    // =========================
    public void OnMouseLook(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        Vector2 mouseInput = context.ReadValue<Vector2>();

        // 카메라에 마우스 델타 전달
        CameraFollow camFollow = Camera.main?.GetComponent<CameraFollow>();
        camFollow?.AddYawDelta(mouseInput.x);
    }

    // =========================
    // ATTACK INPUT
    // =========================
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        if (!context.started || isAttacking) return;

        PlayerNetwork pn = GetPlayerNetwork();
        if (pn != null && !pn.CanAttack()) return;

        if (NetworkClient.active)
        {
            // 멀티: Command로 서버에 전달
            CmdRequestAttack();
        }
        else
        {
            // 싱글: 직접 호출
            GetComponent<PlayerNetwork>()?.ServerRequestAttack();
            StartCoroutine(AttackAnimation());
        }
    }

    // ─────────────────────────────────────────────
    // Network
    // ─────────────────────────────────────────────
    [Command]
    void CmdNotifyJump()
    {
        Trap_Card.Instance?.NotifyJump(GetPlayerNetwork());
    }

    [Command]
    void CmdNotifyRunStarted()
    {
        Trap_Card.Instance?.NotifyRunStarted(GetPlayerNetwork());
    }

    [Command]
    void CmdRequestAttack()
    {
        PlayerNetwork pn = GetPlayerNetwork();
        pn?.ServerRequestAttack();
        Trap_Card.Instance?.NotifyAttack(pn);
        RpcPlayAttackAnimation();
    }

    [ClientRpc]
    void RpcPlayAttackAnimation()
    {
        StartCoroutine(AttackAnimation());
    }

    IEnumerator AttackAnimation()
    {
        isAttacking = true;
        if (animator != null)
            animator.SetTrigger("Attack");
        yield return new WaitForSeconds(1f);
        isAttacking = false;
    }

    public bool IsGrounded()
    {
        bool isGrounded = Physics.Raycast(
            groundCheck.position,
            Vector3.down,
            groundCheckDistance,
            groundLayer
        );

        return isGrounded;
    }

    public void Roll()
    {
        Debug.Log("aaaa");
        StartCoroutine(IERoll());
    }

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

            // Y축 속도는 유지해서 점프/낙하가 자연스럽게 되도록
            rb.linearVelocity = new Vector3(
                dir.x * rollSpeed,
                rb.linearVelocity.y,
                dir.z * rollSpeed
            );

            yield return new WaitForFixedUpdate();
        }

        // 구르기 종료
        rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);
        isRoll = false;
    }

    public void RefreshSpeed()
    {
        if (characterStats == null)
            characterStats = GetComponent<CharaStat>();

        if (characterStats == null)
            return;

        walkSpeed = characterStats.speed;
        runSpeed = characterStats.runSpeed;
    }

    public void ApplyTemporarySpeedMultiplier(float multiplier, float duration)
    {
        if (speedMultiplierRoutine != null)
            StopCoroutine(speedMultiplierRoutine);

        speedMultiplierRoutine = StartCoroutine(
            SpeedMultiplierRoutine(multiplier, duration)
        );
    }

    private IEnumerator SpeedMultiplierRoutine(float multiplier, float duration)
    {
        speedMultiplier = multiplier;
        yield return new WaitForSeconds(duration);
        speedMultiplier = 1f;
        speedMultiplierRoutine = null;
    }

    private float GetEffectiveSpeedMultiplier(PlayerNetwork playerNetwork)
    {
        if (speedMultiplier < 0.99f)
            return speedMultiplier;

        if (playerNetwork != null && playerNetwork.currentState == PlayerStateType.Slow)
            return 0.5f;

        return 1f;
    }

    private PlayerNetwork GetPlayerNetwork()
    {
        PlayerNetwork selfNetwork = GetComponent<PlayerNetwork>();

        if (selfNetwork != null)
            return selfNetwork;

        if (ownerPlayerNetId == 0)
            return null;

        NetworkIdentity identity = null;

        if (NetworkServer.active)
        {
            NetworkServer.spawned.TryGetValue(ownerPlayerNetId, out identity);
        }
        else if (NetworkClient.active)
        {
            NetworkClient.spawned.TryGetValue(ownerPlayerNetId, out identity);
        }

        return identity != null
            ? identity.GetComponent<PlayerNetwork>()
            : null;
    }
}
