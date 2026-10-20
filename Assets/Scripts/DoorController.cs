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