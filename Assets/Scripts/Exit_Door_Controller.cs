using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
using System.Collections;

public class Exit_Door_Controller : MonoBehaviour
{
    private Transform player;
    private bool isOpening = false;
    private Vector3 startPosition;
    private Vector3 targetPosition;

    [Header("Door Movement")]
    [FormerlySerializedAs("yukselmeMiktari")] public float riseAmount = 8f;
    [FormerlySerializedAs("acilmaHizi")] public float openSpeed = 2.5f;

    [Header("UI")]
    [FormerlySerializedAs("mesajPaneli")] public GameObject messagePanel;
    private TextMeshProUGUI messageText;

    [Header("Distance")]
    [FormerlySerializedAs("algilamaMesafesi")] public float detectionDistance = 5f;

    void Start()
    {
        startPosition = transform.position;
        targetPosition = startPosition + Vector3.up * riseAmount;

        if (messagePanel == null)
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                Transform panel = canvas.transform.Find("ExitMessagePanel");
                if (panel != null) messagePanel = panel.gameObject;
            }
        }

        if (messagePanel != null)
        {
            messageText = messagePanel.GetComponentInChildren<TextMeshProUGUI>();
            messagePanel.SetActive(false);
        }

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null) player = playerObject.transform;
    }

    void Update()
    {
        if (player == null) return;

        if (isOpening)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, openSpeed * Time.unscaledDeltaTime);
            return;
        }

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= detectionDistance && Input.GetKeyDown(KeyCode.E))
        {
            // Aynı karede başka bir kapı açıldıysa veya soru açıksa çıkışı tetikleme
            if (Time.frameCount == DoorController.lastInteractionFrame) return;
            if (QuestionManager.instance != null && QuestionManager.instance.questionPanel != null && QuestionManager.instance.questionPanel.activeSelf) return;

            // Oyuncu çıkış kapısına doğru bakmıyorsa tetikleme
            if (!IsFacingDoor()) return;

            // Duvar arkasından tetiklenmeyi engellemek için araya engel girip girmediğine bak
            if (IsBlockedByWall()) return;

            DoorController.lastInteractionFrame = Time.frameCount;
            TryExit();
        }
    }

    private bool IsFacingDoor()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : player;
        if (cam == null) return true;

        Vector3 toDoor = (transform.position - cam.position);
        toDoor.y = 0;

        Vector3 forward = cam.forward;
        forward.y = 0;

        if (toDoor.sqrMagnitude < 0.001f) return true;

        float angle = Vector3.Angle(forward, toDoor);
        return angle <= 60f;
    }

    private bool IsBlockedByWall()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : player;
        if (cam == null) return false;

        Vector3 origin = cam.position;
        Vector3 target = transform.position + Vector3.up * 1.5f;
        Vector3 dir = (target - origin).normalized;
        float dist = Vector3.Distance(origin, target);

        if (Physics.Raycast(origin, dir, out RaycastHit hit, dist))
        {
            // Eğer ışın kapıya ulaşmadan önce başka bir katı nesneye (duvar vb.) çarptıysa engellenmiştir
            if (hit.transform != transform && !hit.transform.IsChildOf(transform) && !hit.collider.isTrigger)
            {
                return true;
            }
        }
        return false;
    }

    void TryExit()
    {
        if (messageText != null)
        {
            if (QuestionManager.instance != null && QuestionManager.instance.AreAllQuestionsSolved())
            {
                isOpening = true;

                if (GameResultManager.Instance != null)
                {
                    GameResultManager.Instance.RecordDoorOpened();
                }

                messageText.text = BuildResultSummaryText();
                messagePanel.SetActive(true);

                Collider doorCollider = GetComponent<Collider>();
                if (doorCollider != null) doorCollider.enabled = false;

                if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();

                Time.timeScale = 0;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                string warningMessage = "<b><size=130%><color=#800000>KAPI KİLİTLİ!</color></size></b>\n\n" +
                                        "<size=90%><color=#2C1A0C>Labirentteki tüm soruları çözmeden buradan çıkamazsın.</color></size>";

                StartCoroutine(ShowWarning(warningMessage));
            }
        }
    }

    private string BuildResultSummaryText()
    {
        if (GameResultManager.Instance != null)
        {
            float finalScore = GameResultManager.Instance.CalculateFinalScore();
            int failedAttempts = GameResultManager.Instance.GetFailedAttempts();
            int hintsUsed = GameResultManager.Instance.GetHintsUsed();
            int openedDoors = GameResultManager.Instance.GetOpenedDoors();
            int totalDoors = GameResultManager.Instance.totalDoors;
            float remainingSeconds = GameResultManager.Instance.GetRemainingTime();

            int correctAnswers = GameResultManager.Instance.GetCorrectAnswersCount();
            int emptyDoors = Mathf.Max(0, openedDoors - correctAnswers);

            int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
            int seconds = Mathf.FloorToInt(remainingSeconds % 60f);

            return "<b><size=130%><color=#650F0F>TEBRİKLER!</color></size></b>\n" +
                   "<size=90%><color=#3E1F07>Labirenti Başarıyla Tamamladın.</color></size>\n\n" +
                   "<b><size=115%><color=#1F4E5B>SKOR: " + finalScore.ToString("F1") + " / 100</color></size></b>\n\n" +
                   "<size=80%><color=#2C1A0C>" +
                   "• Çözülen Sorulu Kapı: " + correctAnswers + "\n" +
                   "• Açılan Boş Kapı: " + emptyDoors + "\n" +
                   "• Toplam Açılan Kapı: " + openedDoors + " / " + totalDoors + "\n" +
                   "• Hatalı Deneme: " + failedAttempts + "\n" +
                   "• Kullanılan İpucu: " + hintsUsed + "\n" +
                   "• Kalan Süre: " + string.Format("{0:00}:{1:00}", minutes, seconds) +
                   "</color></size>";
        }

        return "<b><size=130%><color=#650F0F>TEBRİKLER!</color></size></b>\n\n" +
               "<size=90%><color=#2C1A0C>Labirenti başarıyla tamamladın.</color></size>";
    }

    IEnumerator ShowWarning(string message)
    {
        if (messagePanel != null && messageText != null)
        {
            messageText.text = message;
            messagePanel.SetActive(true);
            yield return new WaitForSecondsRealtime(3.5f);
            messagePanel.SetActive(false);
        }
    }

    public void CloseMessagePanel()
    {
        if (messagePanel != null)
        {
            messagePanel.SetActive(false);
        }
    }
}