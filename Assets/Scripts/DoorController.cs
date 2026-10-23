using UnityEngine;
using UnityEngine.Serialization;

public class DoorController : MonoBehaviour
{
    public bool isOpen = false;

    [Header("Question Setting")]
    [FormerlySerializedAs("soruVarMi")] public bool hasQuestion = false;

    private bool isPlayerNearby = false;
    private Transform playerTransform;

    [Header("Door Slide Settings")]
    [FormerlySerializedAs("yukselmeMiktari")] public float riseAmount = 3.5f;
    private Vector3 startPosition;
    private Vector3 targetPosition;

    // Aynı karede iki kapının birden açılmasını engelleyen ortak kilit
    public static int lastInteractionFrame = -1;

    void Start()
    {
        startPosition = transform.localPosition;
        targetPosition = startPosition + new Vector3(0, riseAmount, 0);
    }

    void Update()
    {
        if (isPlayerNearby && Input.GetKeyDown(KeyCode.E))
        {
            // Soru ekranı açıkken veya bu karede başka bir kapı açılmışsa işlem yapma
            if (Time.frameCount == lastInteractionFrame) return;
            if (QuestionManager.instance != null && QuestionManager.instance.questionPanel != null && QuestionManager.instance.questionPanel.activeSelf) return;

            // Oyuncu doğrudan bu kapıya bakmıyorsa açma
            if (!IsFacingDoor()) return;

            // Oyuncu ile kapı arasında duvar veya başka bir kapı varsa açma
            if (!HasClearLineToDoor()) return;

            if (!isOpen)
            {
                lastInteractionFrame = Time.frameCount;

                if (hasQuestion)
                {
                    QuestionManager.instance.AskQuestion(gameObject);
                }
                else
                {
                    if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();
                    OpenDoor();
                }
            }
        }

        // Yumuşak kayma animasyonu
        Vector3 currentTarget = isOpen ? targetPosition : startPosition;
        transform.localPosition = Vector3.Lerp(transform.localPosition, currentTarget, Time.deltaTime * 5f);
    }

    public void OpenDoor()
    {
        if (!isOpen)
        {
            isOpen = true;

            if (GameResultManager.Instance != null)
            {
                GameResultManager.Instance.RecordDoorOpened();
            }
        }
    }

    // Oyuncunun kamerası veya gövdesi kapıya dönük mü?
    private bool IsFacingDoor()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : playerTransform;
        if (cam == null) return true;

        Vector3 toDoor = (transform.position - cam.position);
        toDoor.y = 0;

        Vector3 forward = cam.forward;
        forward.y = 0;

        if (toDoor.sqrMagnitude < 0.001f) return true;

        float angle = Vector3.Angle(forward, toDoor);
        return angle <= 60f; // 60 derecelik rahat bakış açısı
    }

    // Oyuncudan kapıya bir ışın atılır; ışın kapıdan önce başka bir şeye (duvar, başka kapı) çarparsa
    // kapı "görünmüyor" sayılır. Böylece ince bir duvarın arkasındaki kapı açılamaz.
    private static readonly RaycastHit[] rayHits = new RaycastHit[16];

    private bool HasClearLineToDoor()
    {
        if (playerTransform == null) return true;

        // Kapının sınırlarını yalnızca katı collider'lardan al. Oyuncu E'ye basarken zaten tetikleyici
        // alanın içinde olduğundan, tetikleyici de hesaba katılırsa kontrol hiçbir şeyi engellemez.
        Collider[] colliders = GetComponentsInChildren<Collider>();
        bool hasBounds = false;
        Bounds doorBounds = new Bounds();
        foreach (Collider col in colliders)
        {
            if (col.isTrigger || !col.enabled) continue;
            if (!hasBounds) { doorBounds = col.bounds; hasBounds = true; }
            else doorBounds.Encapsulate(col.bounds);
        }
        if (!hasBounds) return true;

        Vector3 origin = playerTransform.position + Vector3.up * 1.2f;
        Vector3 targetPoint = doorBounds.ClosestPoint(origin);
        Vector3 dir = targetPoint - origin;
        float dist = dir.magnitude;
        if (dist < 0.05f) return true;

        int count = Physics.RaycastNonAlloc(origin, dir / dist, rayHits, dist + 0.3f, ~0, QueryTriggerInteraction.Ignore);

        // Işının kapıya çarptığı en yakın mesafe
        float doorHitDistance = dist + 0.3f;
        for (int i = 0; i < count; i++)
        {
            if (BelongsTo(rayHits[i].transform, transform) && rayHits[i].distance < doorHitDistance)
                doorHitDistance = rayHits[i].distance;
        }

        // Kapıdan önce oyuncu dışında bir şeye çarpıyorsa arada engel vardır
        for (int i = 0; i < count; i++)
        {
            Transform t = rayHits[i].transform;
            if (BelongsTo(t, transform) || BelongsTo(t, playerTransform.root)) continue;
            if (rayHits[i].distance < doorHitDistance - 0.02f) return false;
        }

        return true;
    }

    private static bool BelongsTo(Transform t, Transform root)
    {
        return t == root || t.IsChildOf(root);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = true;
            playerTransform = other.transform;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerNearby = false;
            playerTransform = null;
        }
    }
}