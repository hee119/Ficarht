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
    public UnityEngine.InputSystem.PlayerInput playerInput;

    [Header("Speed")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;

    private Vector3 moveInput;
    private float currentSpeed;
    private bool isRunning;
    public bool isAttacking;
    
    [Header("Slope")]
    public float maxSlopeAngle = 45f;
    public float maxClimbHeight = 0.5f;
    public float SLOPE_RAY_DISTANCE = 2f;
    public float slopeStickyForce = 80f;
    private RaycastHit slopeHit;

    [Header("Step Climbing")]
    public float stepCheckDistance = 0.5f;
    public float stepCheckRadius = 0.3f;
    
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
    public float rollDuration = 1f;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        characterStats = GetComponent<CharaStat>();
        playerInput = GetComponent<UnityEngine.InputSystem.PlayerInput>();
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

        // 싱글플레이: 스폰 즉시 카메라에 자신을 등록
        if (!NetworkClient.active)
            RegisterCamera();
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
        {
            this.enabled = false;
            return;
        }
        // 멀티플레이: 내 캐릭터 스폰 즉시 카메라에 등록
        RegisterCamera();
    }

    private void RegisterCamera()
    {
        CameraFollow camFollow = Camera.main?.GetComponent<CameraFollow>();
        if (camFollow != null)
        {
            camFollow.SetTarget(transform);
            Debug.Log($"[PlayerController] 카메라 타겟 등록: {name}");
        }
        else
        {
            Debug.LogWarning("[PlayerController] CameraFollow 없음 — 카메라가 Main Camera 태그인지 확인");
        }
    }

    // ─────────────────────────────────────────────
    // Update / FixedUpdate
    // ─────────────────────────────────────────────
    void Update()
    {
        if (isAttacking)
        {
            playerInput.enabled = false;
        }
        else
        {
            playerInput.enabled = true;
        }
        
        if (!IsLocallyControlled) return;
        if(isRoll) return;

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
        
        // IsGrounded 레이 (초록=땅, 빨강=공중)
        Debug.DrawRay(groundCheck.position, Vector3.down * groundCheckDistance,
            IsGrounded() ? Color.green : Color.red);

// IsOnSlope 레이 (파랑=경사, 흰색=평지)
        Debug.DrawRay(transform.position, Vector3.down * SLOPE_RAY_DISTANCE,
            IsOnSlope() ? Color.blue : Color.white);

// 다음 프레임 위치 레이 (노랑=각도 체크)
        if (moveInput.magnitude > 0.01f && Camera.main != null)
        {
            Vector3 cf = Camera.main.transform.forward; cf.y = 0; cf.Normalize();
            Vector3 cr = Camera.main.transform.right;   cr.y = 0; cr.Normalize();
            Vector3 debugDir = (cf * moveInput.z + cr * moveInput.x).normalized;
            Vector3 nextPos = transform.position + debugDir * currentSpeed * Time.fixedDeltaTime;
            Debug.DrawLine(transform.position, nextPos, Color.cyan);         // 이동 방향
            Debug.DrawRay(nextPos, Vector3.down * SLOPE_RAY_DISTANCE, Color.yellow); // 다음 위치 지형 체크
        }
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

        // 계단 오르기
        if (IsGrounded() && moveDir.magnitude > 0.01f)
            TryStepClimb(moveDir);

        bool isOnSlope = IsOnSlope();
        bool grounded = IsGrounded();

        if (grounded && isOnSlope)
        {
            // 경사면: 중력 항상 비활성화 (정지 시 미끄럼 방지)
            rb.useGravity = false;

            if (moveInput.magnitude > 0.01f)
            {
                if (CanMoveToSlope(moveDir))
                {
                    // 경사 방향으로 이동 + 경사 법선 반대 방향으로 밀착
                    rb.linearVelocity = AdjustDirectionToSlope(moveDir) * currentSpeed;
                    rb.AddForce(-slopeHit.normal * slopeStickyForce, ForceMode.Force);
                }
                else
                {
                    // 진입 불가 경사 (너무 가파름) → 정지
                    rb.linearVelocity = Vector3.zero;
                }
            }
            else
            {
                // 경사면 정지 시 완전 정지 (미끄럼 방지)
                rb.linearVelocity = Vector3.zero;
            }
        }
        else
        {
            rb.useGravity = true;
            // 접지 중이면 경사 이동에서 넘어온 양의 Y 속도를 차단 (공중 부유 방지)
            float yVel = grounded ? Mathf.Min(rb.linearVelocity.y, 0f) : rb.linearVelocity.y;
            rb.linearVelocity = new Vector3(
                moveDir.x * currentSpeed,
                yVel,
                moveDir.z * currentSpeed
            );
        }
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
            else if (!NetworkClient.active)
                Trap_Card.Instance?.NotifyRunStarted(GetPlayerNetwork());
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

        if (grounded)
        {
            rb.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse
            );

            if (NetworkClient.active && isOwned)
                CmdNotifyJump();
            else if (!NetworkClient.active)
                Trap_Card.Instance?.NotifyJump(GetPlayerNetwork());
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
            PlayerNetwork localNetwork = GetComponent<PlayerNetwork>();
            localNetwork?.ServerRequestAttack();
            Trap_Card.Instance?.NotifyAttack(localNetwork);
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
    
    private void TryStepClimb(Vector3 moveDir)
    {
        float skinWidth = 0.05f;
        Vector3 footPos = groundCheck.position;

        // 구체로 발 높이 앞 장애물 감지 (넓은 판정)
        if (!Physics.SphereCast(footPos + Vector3.up * skinWidth, stepCheckRadius, moveDir, out RaycastHit lowerHit, stepCheckDistance))
            return;

        // maxClimbHeight 위에서 앞쪽 공간 확인 (오를 수 있는지)
        if (Physics.SphereCast(new Ray(footPos + Vector3.up * (maxClimbHeight + skinWidth), moveDir), stepCheckRadius, stepCheckDistance))
            return;

        // 계단 윗면 실제 높이 탐색
        Vector3 topOrigin = lowerHit.point + moveDir * 0.05f + Vector3.up * (maxClimbHeight + skinWidth);
        if (!Physics.Raycast(topOrigin, Vector3.down, out RaycastHit topHit, maxClimbHeight + 0.1f))
            return;

        float climbAmount = topHit.point.y - footPos.y;
        if (climbAmount <= 0f || climbAmount > maxClimbHeight) return;

        rb.position += Vector3.up * climbAmount;
    }

    private bool IsOnSlope()
    {
        // groundCheck 기준으로 레이캐스트 (발 위치에서 더 정확한 감지)
        Ray ray = new Ray(groundCheck.position, Vector3.down);
        if (Physics.Raycast(ray, out slopeHit, SLOPE_RAY_DISTANCE, groundLayer))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            return angle != 0f && angle < maxSlopeAngle;
        }
        return false;
    }

    private Vector3 AdjustDirectionToSlope(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }

    private bool CanMoveToSlope(Vector3 moveDir)
    {
        Vector3 origin = groundCheck.position + moveDir.normalized * 0.3f;

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, SLOPE_RAY_DISTANCE, groundLayer))
        {
            float heightDiff = hit.point.y - groundCheck.position.y;

            Debug.DrawRay(origin, Vector3.down * SLOPE_RAY_DISTANCE, Color.yellow);

            return heightDiff <= maxClimbHeight;
        }

        return false;
    }
}
