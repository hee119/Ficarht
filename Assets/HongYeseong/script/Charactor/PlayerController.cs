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

        PlayerNetwork pn = GetComponent<PlayerNetwork>();
        if (pn != null && !pn.CanMove()) return;

        currentSpeed = moveInput.magnitude > 0.01f
            ? (isRunning ? runSpeed : walkSpeed)
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

        PlayerNetwork pn = GetComponent<PlayerNetwork>();
        if (pn != null && !pn.CanMove()) return;

        Vector3 velocity = transform.TransformDirection(moveInput) * currentSpeed;

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
        if (context.started)  isRunning = true;
        if (context.canceled) isRunning = false;
    }

    // =========================
    // JUMP INPUT
    // =========================
    public void OnJump(InputAction.CallbackContext context)
    {
        if (!context.started)
            return;

        bool grounded = IsGrounded();

        Debug.Log($"Grounded : {grounded}");

        if (grounded)
        {
            rb.AddForce(
                Vector3.up * jumpForce,
                ForceMode.Impulse
            );

            Debug.Log("점프!");
        }
    }

    // =========================
    // MOUSE LOOK
    // =========================
    public void OnMouseLook(InputAction.CallbackContext context)
    {
        Vector2 mouseInput = context.ReadValue<Vector2>();

        mouseInputY += mouseInput.x * mouseSensitivity * Time.deltaTime;

        // TPS라서 플레이어는 좌우만 회전
        transform.rotation = Quaternion.Euler(
            0f,
            mouseInputY,
            0f
        );
    }

    // =========================
    // ATTACK INPUT
    // =========================
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        if (!context.started || isAttacking) return;

        PlayerNetwork pn = GetComponent<PlayerNetwork>();
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
        Debug.Log("kkk");
        isRoll = true;
        rb.linearVelocity = Vector3.zero;
        float duration = 0.7f;
        float elapsed = 0f;

        Vector3 start = transform.position;
        Vector3 target = moveInput != Vector3.zero ? start + transform.TransformDirection(moveInput) * 4f : start + transform.forward * 4f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            rb.MovePosition(
                Vector3.Lerp(start, target, elapsed / duration)
            );

            yield return null;
        }
        isRoll = false;
    }
    
    public void RefreshSpeed()
    {
        walkSpeed = characterStats.speed;
        runSpeed = characterStats.runSpeed;
    }
}
