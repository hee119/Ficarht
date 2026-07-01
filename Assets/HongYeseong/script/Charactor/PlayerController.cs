using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Mirror;

/// <summary>
/// Mirror 멀티플레이 PlayerController.
///
/// ■ 소유(Owned)  : 입력 → 카메라 기준 이동 + 20Hz 서버 전송
/// ■ 비소유(Non-owned) : SyncVar → SmoothDamp 위치 보간
///
/// 이동 우선순위: CharacterController(enabled) → Rigidbody(non-kinematic) → transform.position
/// </summary>
public class PlayerController : NetworkBehaviour
{
    // ── 컴포넌트 참조 ──
    private Rigidbody            rb;
    private CharacterController  cc;
    private Animator             animator;
    private CharaStat            characterStats;
    public  UnityEngine.InputSystem.PlayerInput playerInput;

    [Header("Speed")]
    public float walkSpeed = 3f;
    public float runSpeed  = 6f;

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

    [Header("Jump")]
    public float jumpForce = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float     groundCheckDistance = 0.4f;
    public LayerMask groundLayer;

    public bool isRoll = false;

    // CharacterController 중력
    private float _verticalVelocity = 0f;
    private const float Gravity = -20f;

    // ── SyncVar ──
    [SyncVar] private Vector3 _syncPos;
    [SyncVar] private float   _syncRotY;
    [SyncVar] private float   _syncMoveX;
    [SyncVar] private float   _syncMoveY;
    [SyncVar] private float   _syncSpeed;

    private float   _localMoveX, _localMoveY, _localSpeed;
    private Vector3 _posVelocity;  // SmoothDamp용
    private bool    _syncPosInitialized = false; // OnStartClient에서 초기화 완료 여부

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
        playerInput    = GetComponent<UnityEngine.InputSystem.PlayerInput>();

        if (rb != null && cc != null && cc.enabled)
            rb.isKinematic = true;
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
    public override void OnStartClient()
    {
        base.OnStartClient();

        if (isOwned)
        {
            if (cc != null)
            {
                cc.enabled = true;
                // CC가 활성화되면 Rigidbody physics는 CC에 의해 무효화됨
                // → rb가 gravity 등으로 CC와 충돌하지 않도록 kinematic으로 설정
                if (rb != null) rb.isKinematic = true;
            }
        }
        else
        {
            // 비소유 캐릭터: 로컬 입력/물리 차단
            var pi = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (pi != null) pi.enabled = false;

            if (rb != null) rb.isKinematic = true;
            if (cc != null) cc.enabled = false;

            _syncPos             = transform.position;
            _syncRotY            = transform.eulerAngles.y;
            _syncPosInitialized  = true;
        }
    }

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
            ReadDirectInput();

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

            if (_syncPosInitialized)
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position, _syncPos,
                    ref _posVelocity, 0.08f);

                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    Quaternion.Euler(0f, _syncRotY, 0f),
                    12f * Time.deltaTime);
            }
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

        if (worldMove.magnitude > 0.1f)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(worldMove),
                10f * Time.fixedDeltaTime);
        }

        MoveCharacter(worldMove);
    }

    /// <summary>
    /// 이동 처리.
    /// Unity 규칙: CharacterController가 활성화되면 Rigidbody physics를 완전히 덮어씀.
    /// → CC 활성 시 rb.linearVelocity 설정은 무효 → CC 우선 체크
    ///
    /// 우선순위: CharacterController(enabled) → Rigidbody(non-kinematic) → transform.position
    /// </summary>
    private void MoveCharacter(Vector3 worldMove)
    {
        // ① CharacterController 우선 (CC가 활성화되면 Rigidbody를 무효화함)
        if (cc != null && cc.enabled)
        {
            if (cc.isGrounded) _verticalVelocity = -2f;
            else               _verticalVelocity += Gravity * Time.fixedDeltaTime;
            _verticalVelocity = Mathf.Max(_verticalVelocity, -30f);

            Vector3 motion = worldMove * currentSpeed;
            motion.y = _verticalVelocity;
            cc.Move(motion * Time.fixedDeltaTime);
            return;
        }

        // ② Rigidbody (CC 없거나 disabled일 때만, non-kinematic)
        if (rb != null && !rb.isKinematic)
        {
            Vector3 vel = worldMove * currentSpeed;
            vel.y = rb.linearVelocity.y;
            rb.linearVelocity = vel;
            return;
        }

        // ③ 폴백: 순수 transform
        transform.position += worldMove * currentSpeed * Time.fixedDeltaTime;
    }

    // ─────────────────────────────────────────────
    // 직접 입력 읽기 (PlayerInput 설정 불필요)
    // ─────────────────────────────────────────────
    private void ReadDirectInput()
    {
        if (isAttacking || isRoll) return;

        float h = 0f, v = 0f;
        bool  shift = false;
        bool  jump  = false;

        // 신규 Input System (우선)
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            h     = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            v     = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            shift = keyboard.leftShiftKey.isPressed;
            jump  = keyboard.spaceKey.wasPressedThisFrame;
        }

        // 구형 Input Manager 폴백 (신규에서 입력을 못 읽은 경우)
        if (Mathf.Abs(h) < 0.01f && Mathf.Abs(v) < 0.01f)
        {
            h = Input.GetAxisRaw("Horizontal");
            v = Input.GetAxisRaw("Vertical");
            if (!shift) shift = Input.GetKey(KeyCode.LeftShift);
            if (!jump)  jump  = Input.GetKeyDown(KeyCode.Space);
        }

        moveInput  = (Mathf.Abs(h) > 0.01f || Mathf.Abs(v) > 0.01f)
                     ? new Vector3(h, 0f, v).normalized
                     : Vector3.zero;
        isRunning  = shift;

        if (jump)
        {
            if (rb != null && !rb.isKinematic && IsGrounded())
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            else if (cc != null && cc.enabled && cc.isGrounded)
                _verticalVelocity = Mathf.Sqrt(jumpForce * -2f * Gravity);
        }
    }

    // ─────────────────────────────────────────────
    // 카메라 기준 이동 방향
    // ─────────────────────────────────────────────
    private Vector3 GetCameraRelativeMove()
    {
        if (moveInput.magnitude < 0.01f) return Vector3.zero;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 fwd = cam.transform.forward; fwd.y = 0f;
            Vector3 rgt = cam.transform.right;   rgt.y = 0f;
            if (fwd.magnitude > 0.01f) fwd.Normalize(); else fwd = transform.forward;
            if (rgt.magnitude > 0.01f) rgt.Normalize(); else rgt = transform.right;
            Vector3 dir = fwd * moveInput.z + rgt * moveInput.x;
            if (dir.magnitude > 0.01f) return dir.normalized;
        }
        // 카메라 없을 때: 캐릭터 로컬 방향 기준
        return transform.TransformDirection(moveInput).normalized;
    }

    // ─────────────────────────────────────────────
    // Command
    // ─────────────────────────────────────────────
    [Command]
    void CmdSyncState(float moveX, float moveY, float speed, Vector3 pos, float rotY)
    {
        _syncMoveX = moveX; _syncMoveY = moveY; _syncSpeed = speed;
        _syncPos   = pos;   _syncRotY  = rotY;
    }

    // ─────────────────────────────────────────────
    // Input 콜백 (PlayerInput Send Messages)
    // ─────────────────────────────────────────────
    public void OnMove(InputAction.CallbackContext context)
    {
        // ReadDirectInput()에서 직접 처리 – 여기서 중복 처리 안 함
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        // ReadDirectInput()에서 직접 처리
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        // ReadDirectInput()에서 직접 처리
    }

    public void OnMouseLook(InputAction.CallbackContext context)
    {
        // CameraFollow.LateUpdate()가 처리 – 캐릭터 회전 없음
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

    [Command] void CmdRequestAttack()
    {
        GetComponent<PlayerNetwork>()?.ServerRequestAttack();
        RpcPlayAttackAnimation();
    }

    [ClientRpc] void RpcPlayAttackAnimation() { StartCoroutine(AttackAnimation()); }

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

        if (rb != null && !rb.isKinematic) rb.linearVelocity = Vector3.zero;

        while (elapsed < RollDuration)
        {
            elapsed += Time.deltaTime;

            // CC 우선 (CC가 활성화되면 Rigidbody 무효)
            if (cc != null && cc.enabled)
            {
                _verticalVelocity += Gravity * Time.deltaTime;
                _verticalVelocity  = Mathf.Max(_verticalVelocity, -30f);
                Vector3 step = rollDir * RollSpeed;
                step.y = _verticalVelocity;
                cc.Move(step * Time.deltaTime);
            }
            else if (rb != null && !rb.isKinematic)
            {
                rb.linearVelocity = new Vector3(
                    rollDir.x * RollSpeed, rb.linearVelocity.y, rollDir.z * RollSpeed);
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

        if (rb != null && !rb.isKinematic) rb.linearVelocity = Vector3.zero;
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
        float origWalk = walkSpeed, origRun = runSpeed;
        walkSpeed *= multiplier; runSpeed *= multiplier;
        yield return new WaitForSeconds(duration);
        walkSpeed = origWalk; runSpeed = origRun;
    }
}
