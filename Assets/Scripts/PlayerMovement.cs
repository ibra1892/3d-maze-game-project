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
    public float maxHP          = 100f;

    public float maxStamina     = 100f;
    public float staminaDrain   = 6f;
    public float staminaRecover = 8f;
    public float staminaWaitTime = 2f;
    private bool isExhausted = false;
    private float staminaCooldownTimer = 0f;

    [HideInInspector] public float HP;
    [HideInInspector] public float Stamina;
    [HideInInspector] public bool canMove = true;

    // ═══════════════════════════════════════════════════════
    //  WEAPON
    // ═══════════════════════════════════════════════════════

    [Header("Weapon Settings")]
    public int currentAmmo = 0; // Mevcut mermi
    public int maxAmmo = 90;    // Taşınabilecek max mermi

    // ═══════════════════════════════════════════════════════
    //  PRIVATE
    // ═══════════════════════════════════════════════════════
    Vector3    velocity;
    Vector3    lastMoveDir;   // son hareket yönünü hatırla
    bool       isGrounded;
    bool       isCrouching;
    bool       isDead;
    bool       isKnockedDown;

    // ═══════════════════════════════════════════════════════
    //  START
    // ═══════════════════════════════════════════════════════
    void Start()
    {
        HP      = maxHP;
        Stamina = maxStamina;
        inventory = Object.FindFirstObjectByType<InventoryUI>();
        animator = GetComponentInChildren<Animator>();
        if(animator == null) Debug.LogError("Animator alt objelerde de bulunamadı! Lütfen referansı kontrol et.");

        controller.height = standHeight;
        controller.center = new Vector3(0f, standCenterY, 0f);

        // Pistol layer'ı şimdilik kapat (silah alınınca açılacak)
        animator.SetLayerWeight(1, 0f);
    }

    // ═══════════════════════════════════════════════════════
    //  UPDATE
    // ═══════════════════════════════════════════════════════
    void Update()
    {
        if (isDead || isKnockedDown) 
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            // Envanter referansının dolu olduğundan emin ol
            if (inventory != null && inventory.hasWeapon) 
            {
                animator.SetLayerWeight(1, 1f); // Silah modu (Pistol)
                // Eğer elinde silah modeli varsa onu da aktif et:
                // if (weaponModel != null) weaponModel.SetActive(true);
                Debug.Log("Silah çekildi.");
            }
            else
            {
                Debug.Log("Envanterde silah yok.");
            }
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            animator.SetLayerWeight(1, 0f); // El modu (Idle)
            // if (weaponModel != null) weaponModel.SetActive(false);
            Debug.Log("Silah kılıfa koyuldu.");
        }   
        
        GroundCheck();
        Move();
        Jump();
        ApplyGravity();
        HandleCrouch();
        HandleStamina();
        HandleShooting();
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
        if(!canMove) return;
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 inputDir = new Vector3(h, 0f, v);
        bool    moving   = inputDir.sqrMagnitude > 0.01f;

        if (moving)
        {
            // Kamera yönünde hareket
            float   yaw     = cameraRoot.GetYaw();
            Vector3 moveDir = (Quaternion.Euler(0f, yaw, 0f) * inputDir).normalized;
            lastMoveDir = moveDir;   // yönü kaydet

            // Karakteri hareket yönüne döndür
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(moveDir),
                rotationSpeed * Time.deltaTime
            );

            bool  canRun = Input.GetKey(KeyCode.LeftShift) && Stamina > 0f && !isCrouching;
            float speed  = isCrouching ? crouchSpeed : (canRun ? runSpeed : walkSpeed);
            controller.Move(moveDir * speed * Time.deltaTime);

            // Blend Tree: 0=dur, 0.5=yürü, 1=koş
            float animSpeed = canRun ? 1f : 0.5f;
            animator.SetFloat("Speed", animSpeed, 0.1f, Time.deltaTime);
        }
        else
        {
            // Duruyorsa Speed sıfırla ama rotasyon BOZULMASIN
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
        if(controller != null && controller.enabled){
            velocity.y += gravity * Time.deltaTime;
            controller.Move(velocity * Time.deltaTime);
        }
    }

    // ═══════════════════════════════════════════════════════
    //  EĞİLME — Ctrl basılı tut
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
    bool isMoving = (new Vector2(h, v)).sqrMagnitude > 0.01f;

    bool running = Input.GetKey(KeyCode.LeftShift) && isMoving && !isCrouching && !isDead;

    // 1. EĞER KOŞUYORSA: Stamina harca ve bekleme süresini sıfırla
    if (running && !isExhausted && Stamina > 0)
    {
        Stamina -= staminaDrain * Time.deltaTime;
        staminaCooldownTimer = staminaWaitTime; // Her koştuğunda sayacı tazele
    }
    // 2. EĞER KOŞMUYORSA: Önce bekleme süresini bitir, sonra doldur
    else
    {
        if (staminaCooldownTimer > 0)
        {
            staminaCooldownTimer -= Time.deltaTime; // 0'a kadar geri sayar
        }
        else
        {
            Stamina += staminaRecover * Time.deltaTime; // Sayaç bitince dolmaya başlar
        }
    }

    // Yorgunluk kilidi (Eski mantığın devamı)
    if (Stamina <= 0) isExhausted = true;
    if (isExhausted && Stamina >= (maxStamina * 0.2f)) isExhausted = false;

    Stamina = Mathf.Clamp(Stamina, 0f, maxStamina);
}

    // ═══════════════════════════════════════════════════════
    //  SİLAH
    // ═══════════════════════════════════════════════════════
    void HandleShooting()
    {
        if (Input.GetMouseButtonDown(0)) animator.SetBool("IsShooting", true);
        if (Input.GetMouseButtonUp(0))   animator.SetBool("IsShooting", false);
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
        animator.SetTrigger("IsHit");
    }

    public void TakeHeavyHit(float damage)
    {
        if (isDead || isKnockedDown) return;
        HP = Mathf.Clamp(HP - damage, 0f, maxHP);
        if (HP <= 0f) { Die(); return; }
        isKnockedDown = true;
        animator.SetTrigger("KnockDown");
        Invoke(nameof(GetUp), 3f);
    }

    void GetUp()
    {
        isKnockedDown = false;
        animator.SetTrigger("GettingUp");
    }

    void Die()
    {
        if(isDead) return;
        isDead = true;
        Debug.Log("<color=red>OYUNCU ÖLDÜ!</color>");
        animator.SetBool("IsDead", true);
        controller.enabled = false;
        transform.position = new Vector3(transform.position.x, 0.1f, transform.position.z);
        animator.SetLayerWeight(1, 0f);
    }

    // ═══════════════════════════════════════════════════════
    //  ITEM PICKUP TARAFINDAN ÇAĞRILIR
    // ═══════════════════════════════════════════════════════
    public void Heal(float amount)
        => HP = Mathf.Clamp(HP + amount, 0f, maxHP);

    public void RestoreStamina(float amount)
        => Stamina = Mathf.Clamp(Stamina + amount, 0f, maxStamina);

    // Silah alınınca bu çağrılır
    public void EquipWeapon()
        => animator.SetLayerWeight(1, 1f);
}