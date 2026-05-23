using UnityEngine;

// PlayerMovement ile aynı objeye ekle
public class FootstepController : MonoBehaviour
{
    [Header("Adım Ayarları")]
    public float walkStepInterval = 0.5f;   // yürürken adım arası (sn)
    public float runStepInterval  = 0.3f;   // koşarken adım arası (sn)

    private PlayerMovement player;
    private float stepTimer = 0f;
    private bool wasRunning = false;

    void Start()
    {
        player = GetComponent<PlayerMovement>();
    }

    void Update()
    {
        if (player == null || AudioManager.Instance == null) return;

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        bool  isMoving  = new Vector2(h, v).sqrMagnitude > 0.01f;
        bool  isRunning = Input.GetKey(KeyCode.LeftShift) && isMoving;
        bool  isGrounded = player.controller != null && player.controller.isGrounded;

        if (isMoving && isGrounded && player.canMove)
        {
            if(isRunning != wasRunning)
            {
                AudioManager.Instance.StopFootstep();
                stepTimer = 0f;
                wasRunning = isRunning;
            }

            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                AudioManager.Instance.PlayFootstep(isRunning);
                stepTimer = isRunning ? runStepInterval : walkStepInterval;
            }
        }
        else
        {
            AudioManager.Instance.StopFootstep();
            stepTimer = 0f;
            wasRunning = false;
        }
    }
}
