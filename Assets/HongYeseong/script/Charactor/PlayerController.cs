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
    public float SLOPE_RAY_DISTANCE = 2f;
    private RaycastHit slopeHit;
    
    [SerializeField] private Transform slopeCheck;
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
            // CharaStat.Awake()가 SO를 읽지 못해 speed=0인 경우 재초기화
            if (characterStats.speed == 0f)
                characterStats.InitializeStats();

            walkSpeed = characterStats.speed;
            runSpeed = characterStats.runSpeed;
            Debug.Log($"[PlayerController] {name} walkSpeed={walkSpeed}, runSpeed={runSpeed}");
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

        // 이동 방향으로 플레이어 회전
        if (moveDir.magnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(moveDir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.fixedDeltaTime * 10f);
        }

        bool grounded = IsGrounded();
        
        // [수정] 발밑이 경사면이거나, 혹은 내가 '갈 곳'이 경사면일 때 모두 체크하도록 변경
        // CanMoveToSlope에서 slopeHit 정보를 갱신하도록 함수를 연결해주는 것이 좋습니다.
        bool isOnSlope = CheckSlope(moveDir); 

        // 키보드 입력이 있는지 확인
        bool hasInput = moveInput.magnitude > 0.01f;

        if (grounded && isOnSlope)
        {
            if (hasInput)
            {
                // 이동 중일 때는 경사면에 맞게 벡터를 꺾어서 이동
                rb.useGravity = false;
                rb.linearVelocity = AdjustDirectionToSlope(moveDir) * currentSpeed;
            }
            else
            {
                // [꿀팁] 경사면에서 멈췄을 때 미끄러짐 방지 (속도를 완전히 0으로 잡고 중력 끄기)
                rb.useGravity = false;
                rb.linearVelocity = new Vector3(0f, 0f, 0f);
            }
        }
        else
        {
            // 평지 or 공중
            rb.useGravity = true;
            rb.linearVelocity = new Vector3(
                moveDir.x * currentSpeed,
                rb.linearVelocity.y,  // 점프/낙하 Y값 유지
                moveDir.z * currentSpeed
            );
        }
    }

    // [기존 IsOnSlope와 CanMoveToSlope를 하나로 통합한 깔끔한 지형 체크 함수]
    private bool CheckSlope(Vector3 moveDir)
    {
        // 1. 우선 순위: 내가 이동할 앞방향 체크 (Look-Ahead)
        // 입력이 있을 때는 앞쪽을 먼저 레이캐스트 쳐서 부드럽게 진입하게 함
        Vector3 origin = slopeCheck.position + moveDir.normalized * 0.3f;
        if (moveDir.magnitude > 0.01f && Physics.Raycast(origin, Vector3.down, out slopeHit, SLOPE_RAY_DISTANCE, groundLayer))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            if (angle > 0.1f && angle <= maxSlopeAngle) return true;
        }

        // 2. 입력이 없거나 앞쪽에 걸리는 게 없다면 현재 발밑 체크
        if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, SLOPE_RAY_DISTANCE, groundLayer))
        {
            float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
            if (angle > 0.1f && angle <= maxSlopeAngle) return true;
        }

        return false;
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

        // CharaStat.Awake()가 SO를 읽지 못해 speed=0인 경우 재초기화
        if (characterStats.speed == 0f)
            characterStats.InitializeStats();

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

    private Vector3 AdjustDirectionToSlope(Vector3 direction)
    {
        return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
    }
}
