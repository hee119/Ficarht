using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class PlayerController : MonoBehaviour
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

    void Update()
    {
        if (isAttacking)
            return;

        // 속도 결정
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
        if (isAttacking)
            return;

        Vector3 velocity = moveInput * currentSpeed;
        velocity.y = rb.linearVelocity.y;
        rb.linearVelocity = velocity;
    }

    // =========================
    // MOVE INPUT
    // =========================
    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        moveInput = new Vector3(input.x, 0f, input.y).normalized;
    }

    // =========================
    // RUN INPUT (SHIFT 전용 Action)
    // =========================
    public void OnRun(InputAction.CallbackContext context)
    {
        if (context.started)
            isRunning = true;

        if (context.canceled)
            isRunning = false;
    }

    // =========================
    // ATTACK INPUT
    // =========================
    public void OnAttack(InputAction.CallbackContext context)
    {
        if (!context.started || isAttacking)
            return;

        StartCoroutine(Attack());
    }

    IEnumerator Attack()
    {
        isAttacking = true;

        animator.SetTrigger("Attack");

        // 애니메이션 시간에 맞게 조절
        yield return new WaitForSeconds(1f);

        isAttacking = false;
    }
}