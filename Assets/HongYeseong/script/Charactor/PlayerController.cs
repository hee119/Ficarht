using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Mirror;

/// <summary>
/// Mirror 멀티플레이 PlayerController.
///
/// ■ Rigidbody 또는 CharacterController 자동 감지
/// ■ 소유(Owned)  : 입력 → 카메라 기준 이동 + 20Hz 서버 전송
/// ■ 비소유(Non-owned) : SyncVar → SmoothDamp 위치 보간 + 애니메이터 반영
///   - PlayerInput / CharacterController 비활성화 → 로컬 입력 차단
/// </summary>
public class PlayerController : NetworkBehaviour
{
    // ── 컴포넌트 참조 ──
    private Rigidbody         rb;
    private CharacterController cc;
    private Animator          animator;
    private CharaStat         characterStats;

    [Header("Speed")]
    public float walkSpeed = 3f;
    public float runSpeed  = 6f;

    private Vector3 moveInput;
    private float   currentSpeed;
    private bool    isRunning;
    [HideInInspector] public bool isAttacking;

    private bool IsLocallyControlled => isOwned || !NetworkClient.active;

    [Header("Jump")]
    public float jumpForce = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float     groundCheckDistance = 0.4f;
    public LayerMask groundLayer;

    public bool isRoll = false;

    // CharacterController 전용 중력 누적
    private float _verticalVelocity = 0f;
    private const float Gravity = -20f;

    // ── SyncVar : 서버 → 전체 클라이언트 동기화 ──
    [SyncVar] private Vector3 _syncPos;
    [SyncVar] private float   _syncRotY;
    [SyncVar] private float   _syncMoveX;
    [SyncVar] private float   _syncMoveY;
    [SyncVar] private float   _syncSpeed;

    // 비소유 클라이언트 보간용
    private float   _localMoveX;
    private float   _localMoveY;
    private float   _localSpeed;
    private Vector3 _posVelocity;  // SmoothDamp용

    private float       _syncTimer;
    private const float SyncInterval = 0.05f; // 20 Hz

    private PlayerNetwork _ownerPlayerNetwork;

    // ─────────────────────────────────────────────
    void Awake()
    {
        rb             = GetComponent<Rigidbody>();
        cc             = GetComponent<CharacterController>();
        animator       = GetComponent<Animator>();
        characterStats = GetComponent<CharaStat>();
    }

    void Start()
    {
        if (characterStats != null)
        {
            walkSpeed = characterStats.speed;
            runSpeed  = characterStats.runSpeed;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }

    // ─────────────────────────────────────────────
    // Mirror 콜백
    // ─────────────────────────────────────────────
    public override void OnStartClient()
    {
        base.OnStartClient();

        if (!isOwned)
        {
            // ① PlayerInput 비활성화 → 로컬 키가 상대 캐릭터에 전달되지 않음
            var pi = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (pi != null) pi.enabled = false;

            // ② Rigidbody kinematic
            if (rb != null) rb.isKinematic = true;

            // ③ CharacterController 비활성화
            //    → 비활성화해야 transform.position 직접 쓰기가 가능해짐
            if (cc != null) cc.enabled = false;

            // 초기 보간 위치 세팅 (첫 프레임 순간이동 방지)
            _syncPos  = transform.position;
            _syncRotY = transform.eulerAngles.y;
        }
    }

    // ─────────────────────────────────────────────
    public void ServerSetOwnerPlayerNetwork(PlayerNetwork owner)
    {
        _ownerPlayerNetwork = owner;
    }

    // ─────────────────────────────────────────────
    // Update
    // ─────────────────────────────────────────────
    void Update()
    {
        if (IsLocallyControlled)
        {
            if (isAttacking || isRoll) return;

            PlayerNetwork pn = GetComponent<PlayerNetwork>();
            if (pn != null && !pn.CanMove()) return;

            currentSpeed = moveInput.magnitude > 0.01f
                ? (isRunning ? runSpeed : walkSpeed)
                : 0f;

            if (animator != null)
            {
                animator.SetFloat("MoveX", moveInput.x);
                animator.SetFloat("MoveY", moveInput.z);
                animator.SetFloat("Speed", currentSpeed);
            }

            if (groundCheck != null)
                Debug.DrawRay(groundCheck.position,
                    Vector3.down * groundCheckDistance,
                    IsGrounded() ? Color.green : Color.red);

            // 20Hz 서버 동기화
            if (NetworkClient.active)
            {
                _syncTimer += Time.deltaTime;
                if (_syncTimer >= SyncInterval)
                {
                    _syncTimer = 0f;
                    CmdSyncState(moveInput.x, moveInput.z, currentSpeed,
                                 transform.position, transform.eulerAngles.y);
                }
            }
        }
        else
        {
            // 비소유 클라이언트 : SyncVar → 보간 적용

            // 공격·구르기 중에는 애니메이터 파라미터 덮어쓰지 않음
            if (!isAttacking && !isRoll && animator != null)
            {
                float t = 15f * Time.deltaTime;
                _localMoveX = Mathf.Lerp(_localMoveX, _syncMoveX, t);
                _localMoveY = Mathf.Lerp(_localMoveY, _syncMoveY, t);
                _localSpeed = Mathf.Lerp(_localSpeed, _syncSpeed,  t);

                animator.SetFloat("MoveX", _localMoveX);
                animator.SetFloat("MoveY", _localMoveY);
                animator.SetFloat("Speed", _localSpeed);
            }

            // 위치 : SmoothDamp (Lerp보다 훨씬 부드럽고 덜덜거리지 않음)
            transform.position = Vector3.SmoothDamp(
                transform.position, _syncPos,
                ref _posVelocity, 0.08f);

            // 회전 보간
            transform.rotation = Quaternion.Lerp(
                transform.rotation,
                Quaternion.Euler(0f, _syncRotY, 0f),
                12f * Time.deltaTime);
        }
    }

    // ─────────────────────────────────────────────
    // FixedUpdate
    // ─────────────────────────────────────────────
    void FixedUpdate()
    {
        if (!IsLocallyControlled) return;
        if (isAttacking || isRoll) return;

        PlayerNetwork pn = GetComponent<PlayerNetwork>();
        if (pn != null && !pn.CanMove()) return;

        Vector3 worldMove = GetCameraRelativeMove();

        // 이동 방향으로 캐릭터 자동 회전
        if (worldMove.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(worldMove),
                10f * Time.fixedDeltaTime);
        }

        if (rb != null)
        {
            // ── Rigidbody ──
            Vector3 velocity = worldMove * currentSpeed;
            velocity.y = rb.linearVelocity.y;
            rb.linearVelocity = velocity;
        }
        else if (cc != null && cc.enabled)
        {
            // ── CharacterController ──
            // 중력 누적
            if (cc.isGrounded) _verticalVelocity = -2f;
            else               _verticalVelocity += Gravity * Time.fixedDeltaTime;

            Vector3 velocity = worldMove * currentSpeed;
            velocity.y = _verticalVelocity;
            cc.Move(velocity * Time.fixedDeltaTime);
        }
        else
        {
            // ── 폴백 : 순수 transform 이동 ──
            transform.position += worldMove * currentSpeed * Time.fixedDeltaTime;
        }
    }

    // ─────────────────────────────────────────────
    // 카메라 기준 이동 방향 변환
    // ─────────────────────────────────────────────
    private Vector3 GetCameraRelativeMove()
    {
        if (moveInput.magnitude < 0.01f) return Vector3.zero;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 fwd = cam.transform.forward; fwd.y = 0f; fwd.Normalize();
            Vector3 rgt = cam.transform.right;   rgt.y = 0f; rgt.Normalize();
            Vector3 dir = fwd * moveInput.z + rgt * moveInput.x;
            if (dir.magnitude > 0.01f) return dir.normalized;
        }
        return transform.TransformDirection(moveInput).normalized;
    }

    // ─────────────────────────────────────────────
    // Command : 위치 + 애니메이션 → 서버 → SyncVar 전파
    // ─────────────────────────────────────────────
    [Command]
    void CmdSyncState(float moveX, float moveY, float speed, Vector3 pos, float rotY)
    {
        _syncMoveX = moveX;
        _syncMoveY = moveY;
        _syncSpeed = speed;
        _syncPos   = pos;
        _syncRotY  = rotY;
    }

    // ─────────────────────────────────────────────
    // Input 콜백
    // ─────────────────────────────────────────────
    public void OnMove(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        Vector2 input = context.ReadValue<Vector2>();
        moveInput = new Vector3(input.x, 0f, input.y).normalized;
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        if (context.started)  isRunning = true;
        if (context.canceled) isRunning = false;
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        if (!context.started) return;

        if (rb != null)
        {
            if (IsGrounded())
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
        else if (cc != null && cc.enabled && cc.isGrounded)
        {
            _verticalVelocity = Mathf.Sqrt(jumpForce * -2f * Gravity);
        }
    }

    public void OnMouseLook(InputAction.CallbackContext context)
    {
        // CameraFollow.LateUpdate()가 Input.GetAxis("Mouse X")를 직접 읽음
        // 캐릭터는 회전하지 않음
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        if (!context.started || isAttacking) return;

        PlayerNetwork pn = GetComponent<PlayerNetwork>();
        if (pn != null && !pn.CanAttack()) return;

        if (NetworkClient.active) CmdRequestAttack();
        else
        {
            GetComponent<PlayerNetwork>()?.ServerRequestAttack();
            StartCoroutine(AttackAnimation());
        }
    }

    // ─────────────────────────────────────────────
    // 공격
    // ─────────────────────────────────────────────
    [Command]
    void CmdRequestAttack()
    {
        GetComponent<PlayerNetwork>()?.ServerRequestAttack();
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
        if (animator != null) animator.SetTrigger("Attack");
        yield return new WaitForSeconds(1f);
        isAttacking = false;
    }

    // ─────────────────────────────────────────────
    // 구르기
    // ─────────────────────────────────────────────
    public void Roll() => StartCoroutine(IERoll());

    public IEnumerator IERoll()
    {
        isRoll = true;

        Vector3 rollDir = GetCameraRelativeMove();
        if (rollDir.magnitude < 0.01f) rollDir = transform.forward;

        transform.rotation = Quaternion.LookRotation(rollDir);

        const float RollSpeed    = 7f;
        const float RollDuration = 0.5f;
        float elapsed = 0f;

        if (rb != null) rb.linearVelocity = Vector3.zero;

        while (elapsed < RollDuration)
        {
            elapsed += Time.deltaTime;

            if (rb != null)
            {
                rb.linearVelocity = new Vector3(
                    rollDir.x * RollSpeed,
                    rb.linearVelocity.y,
                    rollDir.z * RollSpeed);
            }
            else if (cc != null && cc.enabled)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
                Vector3 step = rollDir * RollSpeed;
                step.y = _verticalVelocity;
                cc.Move(step * Time.deltaTime);
            }
            else
            {
                Vector3 step = rollDir * RollSpeed * Time.deltaTime;
                Vector3 origin = transform.position + Vector3.up * 0.5f;
                if (!Physics.Raycast(origin, rollDir, step.magnitude + 0.2f))
                    transform.position += step;
            }

            yield return null;
        }

        if (rb != null) rb.linearVelocity = Vector3.zero;
        isRoll = false;
    }

    // ─────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────
    public bool IsGrounded()
    {
        if (cc != null && cc.enabled) return cc.isGrounded;
        if (groundCheck == null) return false;
        return Physics.Raycast(groundCheck.position, Vector3.down,
                               groundCheckDistance, groundLayer);
    }

    public void RefreshSpeed()
    {
        if (characterStats == null) return;
        walkSpeed = characterStats.speed;
        runSpeed  = characterStats.runSpeed;
    }

    public void ApplyTemporarySpeedMultiplier(float multiplier, float duration)
    {
        StartCoroutine(SpeedMultiplierRoutine(multiplier, duration));
    }

    private IEnumerator SpeedMultiplierRoutine(float multiplier, float duration)
    {
        float origWalk = walkSpeed;
        float origRun  = runSpeed;
        walkSpeed *= multiplier;
        runSpeed  *= multiplier;
        yield return new WaitForSeconds(duration);
        walkSpeed = origWalk;
        runSpeed  = origRun;
    }
}
