using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════
    //  REFERANSLAR
    // ═══════════════════════════════════════════════════════
    [Header("References")]
    public CharacterController controller;
    public TPSCameraRoot       cameraRoot;
    public Animator            animator;

    // ═══════════════════════════════════════════════════════
    //  HAREKET
    // ═══════════════════════════════════════════════════════
    [Header("Movement")]
    public float walkSpeed     = 3f;
    public float runSpeed      = 6f;
    public float rotationSpeed = 10f;

    [Header("Jump & Gravity")]
    public float jumpForce = 1.5f;
    public float gravity   = -9.81f;

    [Header("Crouch")]
    public float standHeight   = 1.8f;
    public float crouchHeight  = 1.2f;
    public float standCenterY  = 0.9f;
    public float crouchCenterY = 0.6f;
    public float crouchSpeed   = 1.5f;

    // ═══════════════════════════════════════════════════════
    //  STAMINA (koşma enerjisi)
    // ═══════════════════════════════════════════════════════
    [Header("Stamina")]
    public float maxStamina     = 100f;
    public float staminaDrain   = 6f;
    public float staminaRecover = 8f;
    public float staminaWaitTime = 2f;

    [HideInInspector] public float Stamina;
    [HideInInspector] public bool  canMove = true;

    // ═══════════════════════════════════════════════════════
    //  PRIVATE
    // ═══════════════════════════════════════════════════════
    Vector3 velocity;
    bool    isGrounded;
    bool    isCrouching;
    bool    isExhausted;
    float   staminaCooldownTimer;

    // ═══════════════════════════════════════════════════════
    //  START
    // ═══════════════════════════════════════════════════════
    void Start()
    {
        Stamina = maxStamina;

        animator  = GetComponentInChildren<Animator>();
        if (animator == null)
            Debug.LogError("Animator bulunamadı!");

        controller.height = standHeight;
        controller.center = new Vector3(0f, standCenterY, 0f);
    }

    // ═══════════════════════════════════════════════════════
    //  UPDATE
    // ═══════════════════════════════════════════════════════
    void Update()
    {
        // Panel açıkken veya oyun duraklatıldığında tüm hareketi ve animasyonu dondur
        if (Time.timeScale == 0)
        {
            if (animator != null) animator.SetFloat("Speed", 0f);
            return;
        }

        GroundCheck();
        Move();
        Jump();
        ApplyGravity();
        HandleCrouch();
        HandleStamina();
    }

    // ═══════════════════════════════════════════════════════
    //  GROUND CHECK
    // ═══════════════════════════════════════════════════════
    void GroundCheck()
    {
        isGrounded = controller.isGrounded;
        animator.SetBool("isGrounded", isGrounded);
        animator.SetFloat("VerticalVelocity", velocity.y);

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;
    }

    // ═══════════════════════════════════════════════════════
    //  HAREKET
    // ═══════════════════════════════════════════════════════
    void Move()
    {
        if (!canMove) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 inputDir = new Vector3(h, 0f, v);
        bool    moving   = inputDir.sqrMagnitude > 0.01f;

        if (moving)
        {
            float   yaw     = cameraRoot.GetYaw();
            Vector3 moveDir = (Quaternion.Euler(0f, yaw, 0f) * inputDir).normalized;

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(moveDir),
                rotationSpeed * Time.deltaTime
            );

            bool  canRun = Input.GetKey(KeyCode.LeftShift) && Stamina > 0f && !isCrouching;
            float speed  = isCrouching ? crouchSpeed : (canRun ? runSpeed : walkSpeed);
            controller.Move(moveDir * speed * Time.deltaTime);

            float animSpeed = canRun ? 1f : 0.5f;
            float dampTime = isCrouching ? 0.3f : 0.1f;
            animator.SetFloat("Speed", moving ? animSpeed : 0f, dampTime, Time.deltaTime);
        }
        else
        {
            animator.SetFloat("Speed", 0f, 0.1f, Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  ZIPLAMA
    // ═══════════════════════════════════════════════════════
    void Jump()
    {
        if (Input.GetButtonDown("Jump") && isGrounded && !isCrouching)
        {
            velocity.y = Mathf.Sqrt(jumpForce * -2f * gravity);
            animator.SetTrigger("Jump");
        }
    }

    // ═══════════════════════════════════════════════════════
    //  YERÇEKİMİ
    // ═══════════════════════════════════════════════════════
    void ApplyGravity()
    {
        if (controller != null && controller.enabled)
        {
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  EĞİLME
    // ═══════════════════════════════════════════════════════
    void HandleCrouch()
    {
        if (Input.GetKey(KeyCode.LeftControl))
        {
            if (!isCrouching) StartCrouch();
        }
        else
        {
            if (isCrouching) StopCrouch();
        }
    }

    void StartCrouch()
    {
        isCrouching       = true;
        controller.height = crouchHeight;
        controller.center = new Vector3(0f, crouchCenterY, 0f);
        animator.SetBool("isCrouching", true);
    }

    void StopCrouch()
    {
        isCrouching       = false;
        controller.height = standHeight;
        controller.center = new Vector3(0f, standCenterY, 0f);
        animator.SetBool("isCrouching", false);
    }

    // ═══════════════════════════════════════════════════════
    //  STAMINA
    // ═══════════════════════════════════════════════════════
    void HandleStamina()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool  isMoving = (new Vector2(h, v)).sqrMagnitude > 0.01f;
        bool  running  = Input.GetKey(KeyCode.LeftShift) && isMoving && !isCrouching;

        if (running && !isExhausted && Stamina > 0)
        {
            Stamina -= staminaDrain * Time.deltaTime;
            staminaCooldownTimer = staminaWaitTime;
        }
        else
        {
            if (staminaCooldownTimer > 0)
                staminaCooldownTimer -= Time.deltaTime;
            else
                Stamina += staminaRecover * Time.deltaTime;
        }

        if (Stamina <= 0)                             isExhausted = true;
        if (isExhausted && Stamina >= maxStamina * 0.2f) isExhausted = false;

        Stamina = Mathf.Clamp(Stamina, 0f, maxStamina);
    }
}
