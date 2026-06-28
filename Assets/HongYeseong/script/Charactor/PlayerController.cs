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
            walkSpeed = characterStats.walkSpeed;
            runSpeed = characterStats.walkSpeed * 2f;
        }
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
        if (isAttacking) return;

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
    }

    void FixedUpdate()
    {
        if (!IsLocallyControlled) return;
        if (isAttacking) return;

        PlayerNetwork pn = GetComponent<PlayerNetwork>();
        if (pn != null && !pn.CanMove()) return;

        Vector3 velocity = moveInput * currentSpeed;
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
        moveInput = new Vector3(input.x, 0f, input.y).normalized;
    }

    public void OnRun(InputAction.CallbackContext context)
    {
        if (!IsLocallyControlled) return;
        if (context.started)  isRunning = true;
        if (context.canceled) isRunning = false;
    }

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
}
