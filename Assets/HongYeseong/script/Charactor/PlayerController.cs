using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using Mirror;

// MonoBehaviour → NetworkBehaviour로 변경
// isLocalPlayer 체크를 통해 로컬 플레이어만 입력을 받는다
public class PlayerController : NetworkBehaviour
{
    private Rigidbody rb;
    private Animator animator;

    [Header("Speed")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;

    private Vector3 moveInput;
    private float currentSpeed;

    private bool isRunning;
    private bool isAttacking;

    private CharaStat characterStats;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        characterStats = GetComponent<CharaStat>();
    }

    void Start()
    {
        walkSpeed = characterStats.walkSpeed;
        runSpeed = characterStats.walkSpeed * 2f;
    }

    // Mirror: 클라이언트에서 이 오브젝트가 스폰될 때 호출
    public override void OnStartClient()
    {
        base.OnStartClient();

        // 내 플레이어가 아니면 이 스크립트를 비활성화
        // → Update/FixedUpdate가 실행되지 않아 상대방 캐릭터가 움직이지 않는다
        if (!isLocalPlayer)
            this.enabled = false;
    }

    void Update()
    {
        // 로컬 플레이어가 아니면 무시 (OnStartClient에서 비활성화되지만 이중 보호)
        if (!isLocalPlayer) return;
        if (isAttacking) return;

        // 속도 계산
        currentSpeed = moveInput.magnitude > 0.01f
            ? (isRunning ? runSpeed : walkSpeed)
            : 0f;

        // Animator 파라미터
        animator.SetFloat("MoveX", moveInput.x);
        animator.SetFloat("MoveY", moveInput.z);
        animator.SetFloat("Speed", currentSpeed);
    }

    void FixedUpdate()
    {
        if (!isLocalPlayer) return;
        if (isAttacking) return;

        Vector3 velocity = moveInput * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
    }

    // =========================
    // MOVE INPUT
    // =========================
    public void OnMove(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;

        Vector2 input = context.ReadValue<Vector2>();
        moveInput = new Vector3(input.x, 0f, input.y).normalized;
    }

    // =========================
    // RUN INPUT (SHIFT 누르는 Action)
    // =========================
    public void OnRun(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;

        if (context.started)  isRunning = true;
        if (context.canceled) isRunning = false;
    }

    // =========================
    // ATTACK INPUT
    // =========================
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!isLocalPlayer) return;
        if (!context.started || isAttacking) return;

        // 공격 요청을 서버에 전달
        CmdRequestAttack();
    }

    // 클라이언트 → 서버: 공격 요청
    [Command]
    void CmdRequestAttack()
    {
        // 서버에서 NetworkAPI를 통해 데미지 처리
        // 범위 안에 상대가 있으면 데미지 전달
        PlayerNetwork myNetwork = GetComponent<PlayerNetwork>();
        if (myNetwork != null)
            myNetwork.ServerRequestAttack();

        // 모든 클라이언트에 애니메이션 재생
        RpcPlayAttackAnimation();
    }

    // 서버 → 모든 클라이언트: 애니메이션 재생
    [ClientRpc]
    void RpcPlayAttackAnimation()
    {
        StartCoroutine(AttackAnimation());
    }

    IEnumerator AttackAnimation()
    {
        isAttacking = true;
        animator.SetTrigger("Attack");

        // 애니메이션 시간에 맞게 조절
        yield return new WaitForSeconds(1f);

        isAttacking = false;
    }
}
