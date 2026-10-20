using UnityEngine;
using System.Collections;

public class PlayerMovement : MonoBehaviour
{
    // ═══════════════════════════════════════════════════════
    //  REFERANSLAR
    // ═══════════════════════════════════════════════════════
    [Header("References")]
    public CharacterController controller;
    public TPSCameraRoot       cameraRoot;
    public Animator            animator;
    public InventoryUI         inventory;

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
    //  HP & STAMINA
    // ═══════════════════════════════════════════════════════
    [Header("HP & Stamina")]
    public float maxHP           = 100f;
    public float maxStamina     = 100f;
    public float staminaDrain   = 6f;
    public float staminaRecover = 8f;
    public float staminaWaitTime = 2f;

    [HideInInspector] public float HP;
    [HideInInspector] public float Stamina;
    [HideInInspector] public bool  canMove = true;
    [HideInInspector] public bool staminaInfinite = false;

    // ═══════════════════════════════════════════════════════
    //  PRIVATE
    // ═══════════════════════════════════════════════════════
    Vector3 velocity;
    bool    isGrounded;
    bool    isCrouching;
    bool    isDead;
    bool    isKnockedDown;
    bool    isExhausted;
    float   staminaCooldownTimer;

    // ═══════════════════════════════════════════════════════
    //  START
    // ═══════════════════════════════════════════════════════
    void Start()
    {
        HP      = maxHP;
        Stamina = maxStamina;

        inventory = Object.FindFirstObjectByType<InventoryUI>();
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

        if (isDead || isKnockedDown) return;

        GroundCheck();
        Move();
        Jump();
        ApplyGravity();
        HandleCrouch();
        HandleStamina();
        HandleHealing();
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
        bool  running  = Input.GetKey(KeyCode.LeftShift) && isMoving && !isCrouching && !isDead;

        if (running && !isExhausted && Stamina > 0)
        {
            if (!staminaInfinite)
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

    // ═══════════════════════════════════════════════════════
    //  İYİLEŞME
    // ═══════════════════════════════════════════════════════
    void HandleHealing()
    {
        if (Input.GetKeyDown(KeyCode.H) && HP < maxHP)
        {
            animator.SetBool("IsHealing", true);
            Invoke(nameof(StopHealing), 2f);
        }
    }

    void StopHealing()
    {
        HP = Mathf.Clamp(HP + 30f, 0f, maxHP);
        animator.SetBool("IsHealing", false);
    }

    // ═══════════════════════════════════════════════════════
    //  HASAR
    // ═══════════════════════════════════════════════════════
    public void TakeHit(float damage)
    {
        if (isDead || isKnockedDown) return;
        HP = Mathf.Clamp(HP - damage, 0f, maxHP);
        if (HP <= 0f) { Die(); return; }
        if (AudioManager.Instance != null) AudioManager.Instance.PlayHurt();
        animator.SetTrigger("IsHit");
    }

    void GetUp()
    {
        isKnockedDown = false;
        animator.SetTrigger("GettingUp");
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        animator.SetBool("IsDead", true);
        controller.enabled = false;
        if (AudioManager.Instance != null) AudioManager.Instance.PlayDeath();
        Debug.Log("<color=red>OYUNCU ÖLDÜ!</color>");
    }

    // ═══════════════════════════════════════════════════════
    //  DIŞ ÇAĞRILAR
    // ═══════════════════════════════════════════════════════
    public void Heal(float amount)
        => HP = Mathf.Clamp(HP + amount, 0f, maxHP);

    public void RestoreStamina(float amount)
        => Stamina = Mathf.Clamp(Stamina + amount, 0f, maxStamina);

    public void BoostSpeed(float duration)
        => StartCoroutine(SpeedRoutine(3f, duration));

    IEnumerator SpeedRoutine(float bonus, float duration)
    {
        walkSpeed += bonus;
        runSpeed  += bonus;
        staminaInfinite = true;

        yield return new WaitForSeconds(duration);

        walkSpeed -= bonus;
        runSpeed  -= bonus;
        staminaInfinite = false;
    }
}