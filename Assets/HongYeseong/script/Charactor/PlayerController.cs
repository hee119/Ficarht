using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Mirror;

/// <summary>
/// Mirror 멀티플레이 PlayerController.
///
/// ■ 소유(Owned) 클라이언트 : 입력 처리 + 20Hz로 위치/애니 서버 전송
/// ■ 비소유(Non-owned) 클라이언트 : SyncVar 보간 → 위치·회전·애니메이터 반영
///   - PlayerInput 비활성화 → 키보드 입력이 상대 캐릭터에 전달되지 않음
///   - Rigidbody kinematic → 위치 동기화와 물리가 충돌하지 않음
/// </summary>
public class PlayerController : NetworkBehaviour
{
    private Rigidbody rb;
    private Animator  animator;
    private CharaStat characterStats;

    [Header("Speed")]
    public float walkSpeed = 3f;
    public float runSpeed  = 6f;

    private Vector3 moveInput;
    private float   currentSpeed;
    private bool    isRunning;
    [HideInInspector] public bool isAttacking;

    private bool IsLocallyControlled => isOwned || !NetworkClient.active;

    // 마우스 감도는 CameraFollow가 자체적으로 처리하므로 PlayerController에서는 사용 안 함

    [Header("Jump")]
    public float jumpForce = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float     groundCheckDistance = 0.4f;
    public LayerMask groundLayer;

    public bool isRoll = false;

    // ── SyncVar : 위치 · 회전 · 애니메이션 파라미터 동기화 ──
    [SyncVar] private Vector3 _syncPos;
    [SyncVar] private float   _syncRotY;
    [SyncVar] private float   _syncMoveX;
    [SyncVar] private float   _syncMoveY;
    [SyncVar] private float   _syncSpeed;

    // 비소유 클라이언트에서 부드러운 보간을 위한 로컬 값
    private float _localMoveX;
    private float _localMoveY;
    private float _localSpeed;

    private float       _syncTimer;
    private const float SyncInterval = 0.05f; // 20 Hz

    private PlayerNetwork _ownerPlayerNetwork;

    // ─────────────────────────────────────────────
    void Awake()
    {
        rb            = GetComponent<Rigidbody>();
        animator      = GetComponent<Animator>();
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
            // ① PlayerInput 비활성화 → 로컬 키보드가 상대 캐릭터에 전달되지 않음
            //    (이게 없으면 내가 스킬 키를 누를 때 상대 캐릭터도 같은 스킬을 사용)
            var pi = GetComponent<UnityEngine.InputSystem.PlayerInput>();
            if (pi != null) pi.enabled = false;

            // ② Rigidbody kinematic → SyncVar 위치 보간과 물리가 충돌하지 않음
            if (rb != null) rb.isKinematic = true;

            // SyncVar 초기값을 현재 위치로 세팅 (첫 프레임 순간이동 방지)
            _syncPos  = transform.position;
            _syncRotY = transform.eulerAngles.y;
        }
    }

    // ─────────────────────────────────────────────
    // 소유 PlayerNetwork 설정 (서버/싱글에서 호출)
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
            // 공격·구르기 중에는 이동 입력 처리 스킵
            if (isAttacking || isRoll) return;

            PlayerNetwork pn = GetComponent<PlayerNetwork>();
            if (pn != null && !pn.CanMove()) return;

            currentSpeed = moveInput.magnitude > 0.01f
                ? (isRunning ? runSpeed : walkSpeed)
                : 0f;

            animator.SetFloat("MoveX",  moveInput.x);
            animator.SetFloat("MoveY",  moveInput.z);
            animator.SetFloat("Speed",  currentSpeed);

            if (groundCheck != null)
                Debug.DrawRay(
                    groundCheck.position,
                    Vector3.down * groundCheckDistance,
                    IsGrounded() ? Color.green : Color.red
                );

            // 20Hz 서버 동기화
            if (NetworkClient.active)
            {
                _syncTimer += Time.deltaTime;
                if (_syncTimer >= SyncInterval)
                {
                    _syncTimer = 0f;
                    CmdSyncState(
                        moveInput.x, moveInput.z, currentSpeed,
                        transform.position, transform.eulerAngles.y
                    );
                }
            }
        }
        else
        {
            // ── 비소유 클라이언트 : SyncVar → 보간 적용 ──

            // 공격·구르기 중에는 애니메이터 파라미터를 덮어쓰지 않음
            // (Attack 트리거가 재생 중에 Speed 등이 변경되면 전환이 끊김)
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

            // 위치·회전 보간 (항상 적용)
            if (_syncPos != Vector3.zero)
            {
                float pt = 10f * Time.deltaTime;
                transform.position = Vector3.Lerp(
                    transform.position, _syncPos, pt);
                transform.rotation = Quaternion.Lerp(
                    transform.rotation,
                    Quaternion.Euler(0f, _syncRotY, 0f),
                    pt
                );
            }
        }
    }

    void FixedUpdate()
    {
        if (!IsLocallyControlled) return;
        if (isAttacking || isRoll) return;
        if (rb == null) return;

        PlayerNetwork pn = GetComponent<PlayerNetwork>();
        if (pn != null && !pn.CanMove()) return;

        // 카메라 기준 이동 방향 계산
        Vector3 worldMove = GetCameraRelativeMove();

        // 이동 중 캐릭터가 이동 방향을 바라보도록 자동 회전
        if (worldMove.magnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(worldMove);
            transform.rotation = Quaternion.Slerp(
                transform.rotation, targetRot, 10f * Time.fixedDeltaTime);
        }

        Vector3 velocity = worldMove * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
    }

    // 카메라 yaw 기준으로 moveInput을 월드 방향으로 변환
    private Vector3 GetCameraRelativeMove()
    {
        if (moveInput.magnitude < 0.01f) return Vector3.zero;

        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = cam.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
            return (camForward * moveInput.z + camRight * moveInput.x).normalized;
        }
        // 카메라가 없으면 캐릭터 로컬 방향으로 폴백
        return transform.TransformDirection(moveInput).normalized;
    }

    // ─────────────────────────────────────────────
    // Command : 위치 + 애니메이션 상태를 서버에 전송 → SyncVar로 전파
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
    // Input 콜백 (PlayerInput → Invoke Unity Events)
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
        if (rb == null) return;

        if (IsGrounded())
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            Debug.Log("점프!");
        }
    }

    public void OnMouseLook(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        Vector2 mouseInput = context.ReadValue<Vector2>();
        // 캐릭터를 직접 돌리지 않고 카메라 Yaw만 변경
        // CameraFollow.mouseSensitivity가 감도를 처리함
        CameraFollow cam = Camera.main?.GetComponent<CameraFollow>();
        cam?.AddYawDelta(mouseInput.x);
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        if (!context.started || isAttacking) return;

        PlayerNetwork pn = GetComponent<PlayerNetwork>();
        if (pn != null && !pn.CanAttack()) return;

        if (NetworkClient.active)
        {
            CmdRequestAttack();
        }
        else
        {
            GetComponent<PlayerNetwork>()?.ServerRequestAttack();
            StartCoroutine(AttackAnimation());
        }
    }

    // ─────────────────────────────────────────────
    // 공격 네트워크
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
        if (animator != null)
            animator.SetTrigger("Attack");
        yield return new WaitForSeconds(1f);
        isAttacking = false;
    }

    // ─────────────────────────────────────────────
    // 유틸
    // ─────────────────────────────────────────────
    public bool IsGrounded()
    {
        if (groundCheck == null) return false;
        return Physics.Raycast(
            groundCheck.position,
            Vector3.down,
            groundCheckDistance,
            groundLayer
        );
    }

    public void Roll()
    {
        StartCoroutine(IERoll());
    }

    public IEnumerator IERoll()
    {
        isRoll = true;
        if (rb != null) rb.linearVelocity = Vector3.zero;
        float  duration = 0.7f;
        float  elapsed  = 0f;
        Vector3 start   = transform.position;
        Vector3 target  = moveInput != Vector3.zero
            ? start + transform.TransformDirection(moveInput) * 4f
            : start + transform.forward * 4f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (rb != null)
                rb.MovePosition(Vector3.Lerp(start, target, elapsed / duration));
            else
                transform.position = Vector3.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        isRoll = false;
    }

    public void RefreshSpeed()
    {
        if (characterStats == null) return;
        walkSpeed = characterStats.speed;
        runSpeed  = characterStats.runSpeed;
    }

    // ─────────────────────────────────────────────
    // 임시 속도 배율 (트랩 카드 등에서 사용)
    // ─────────────────────────────────────────────
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
