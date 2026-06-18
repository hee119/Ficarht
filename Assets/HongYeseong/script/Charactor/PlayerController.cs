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

    public float mouseSensitivity = 100f;
    private CharaStat characterStats;

    private float mouseInputX = 0;
    private float mouseInputY = 0;

    [Header("Jump")]
    public float jumpForce = 5f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundCheckDistance = 0.4f;
    public LayerMask groundLayer;

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
        if (isAttacking)
            return;

        Vector3 velocity = transform.TransformDirection(moveInput) * currentSpeed;

        velocity.y = rb.linearVelocity.y;

        rb.linearVelocity = velocity;
    }

    // =========================
    // MOVE INPUT
    // =========================
    public void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();

        moveInput = new Vector3(
            input.x,
            0f,
            input.y
        ).normalized;
    }

    // =========================
    // RUN INPUT
    // =========================
    public void OnRun(InputAction.CallbackContext context)
    {
        if (context.started)
            isRunning = true;

        if (context.canceled)
            isRunning = false;
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
        if (!context.started || isAttacking)
            return;

        StartCoroutine(Attack());
    }

    // =========================
    // GROUND CHECK
    // =========================
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

    IEnumerator Attack()
    {
        isAttacking = true;

        animator.SetTrigger("Attack");

        yield return new WaitForSeconds(1f);

        isAttacking = false;
    }
}