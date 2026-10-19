using UnityEngine;
using UnityEngine.Serialization;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class Exit_Door_Controller : MonoBehaviour
{
    private Transform player;
    private bool isOpening = false;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Bounds doorBounds;
    private Coroutine warningRoutine;
    private readonly RaycastHit[] rayHits = new RaycastHit[16];

    [Header("Test Modu (Kapıyı Test Etmek İçin)")]
    [Tooltip("İşaretliyse sorular bitmese de kapı açılır. Normal oyunda KAPALI olmalıdır. (Inspector'daki değer koddakini ezer!)")]
    public bool testModeHerZamanAc = false;

    [Header("Door Movement")]
    [FormerlySerializedAs("yukselmeMiktari")] public float riseAmount = 8f;
    [FormerlySerializedAs("acilmaHizi")] public float openSpeed = 2.5f;

    [Header("UI")]
    [FormerlySerializedAs("mesajPaneli")] public GameObject messagePanel;
    public TextMeshProUGUI messageText;

    [Header("Distance")]
    [FormerlySerializedAs("algilamaMesafesi")] public float detectionDistance = 6f;
    [Tooltip("Oyuncunun kapıya bakış açısı toleransı (derece)")]
    public float facingAngle = 60f;

    [Header("Öncelik (Yan Yana Kapılar İçin)")]
    [Tooltip("Bu mesafedeki (metre) bir normal kapıya oyuncu daha dik bakıyorsa çıkış kapısı çekilir, E normal kapıya ait olur.")]
    public float normalDoorYieldDistance = 4f;

    void Start()
    {
        startPosition = transform.position;
        targetPosition = startPosition + Vector3.up * riseAmount;

        CalculateDoorBounds();
        FindPlayer();
        FindUIElements();

        if (testModeHerZamanAc)
        {
            Debug.LogWarning("[ExitDoor] TEST MODU AÇIK! Sorular çözülmeden çıkış kapısı açılır. Normal oyunda Inspector'dan kapat.");
        }
    }

    private void CalculateDoorBounds()
    {
        Collider[] cols = GetComponentsInChildren<Collider>();
        if (cols.Length > 0)
        {
            doorBounds = cols[0].bounds;
            for (int i = 1; i < cols.Length; i++) doorBounds.Encapsulate(cols[i].bounds);
        }
        else
        {
            doorBounds = new Bounds(transform.position, Vector3.one * 2f);
        }
    }

    private void FindPlayer()
    {
        GameObject pObj = GameObject.FindWithTag("Player");
        if (pObj == null) pObj = GameObject.Find("Player");
        if (pObj != null) player = pObj.transform;
    }

    private void FindUIElements()
    {
        if (messagePanel == null)
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                Transform[] allChildren = canvas.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allChildren)
                {
                    if (t.name == "ExitMessagePanel")
                    {
                        messagePanel = t.gameObject;
                        break;
                    }
                }
            }
        }

        if (messagePanel != null)
        {
            if (messageText == null)
            {
                messageText = messagePanel.GetComponentInChildren<TextMeshProUGUI>(true);
            }
            messagePanel.SetActive(false);
        }
    }

    void Update()
    {
        if (player == null)
        {
            FindPlayer();
            return;
        }

        if (isOpening)
        {
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, openSpeed * Time.unscaledDeltaTime);
            return;
        }

        if (!Input.GetKeyDown(KeyCode.E)) return;

        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > detectionDistance) return;

        // Aynı karede başka bir kapı zaten etkileşime girdiyse bu kapı karışmasın
        if (Time.frameCount == DoorController.lastInteractionFrame) return;

        // Soru paneli VEYA ipucu paneli açıkken çıkış kapısı çalışmasın
        if (IsQuestionUIOpen()) return;

        // Oyuncu bu kapıya bakmıyorsa çalışmasın
        if (!IsFacingDoor()) return;

        // Yakında, oyuncunun daha dik baktığı bir normal kapı varsa E o kapıya aittir
        if (ShouldYieldToNormalDoor())
        {
            Debug.Log("[ExitDoor] Yakındaki bir normal kapıya daha dik bakılıyor, çıkış kapısı çekildi.");
            return;
        }

        // Oyuncu ile çıkış kapısı arasında başka bir şey (normal kapı, duvar) varsa çalışmasın
        if (!HasClearLineToDoor())
        {
            Debug.Log("[ExitDoor] E basıldı ama araya engel (kapı/duvar) girdi, çıkış kapısı tetiklenmedi.");
            return;
        }

        // Diğer kapılar da bu karede devreye girmesin
        DoorController.lastInteractionFrame = Time.frameCount;

        TryExit();
    }

    private bool IsQuestionUIOpen()
    {
        QuestionManager qm = QuestionManager.instance;
        if (qm == null) return false;

        if (qm.questionPanel != null && qm.questionPanel.activeSelf) return true;
        if (qm.hintPanel != null && qm.hintPanel.activeSelf) return true;

        return false;
    }

    private bool IsFacingDoor()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : player;
        if (cam == null) return true;

        Vector3 toDoor = doorBounds.center - cam.position;
        toDoor.y = 0f;

        Vector3 forward = cam.forward;
        forward.y = 0f;

        if (toDoor.sqrMagnitude < 0.001f || forward.sqrMagnitude < 0.001f) return true;

        return Vector3.Angle(forward, toDoor) <= facingAngle;
    }

    // Çıkış kapısı normal kapılarla yan yana olabilir.
    // Oyuncu hangisine daha dik bakıyorsa E o kapının olur; çıkış kapısı sırayı normal kapıya bırakır.
    private bool ShouldYieldToNormalDoor()
    {
        Transform cam = Camera.main != null ? Camera.main.transform : player;
        if (cam == null) return false;

        Vector3 forward = cam.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) return false;

        Vector3 toExit = doorBounds.center - cam.position;
        toExit.y = 0f;
        float exitAngle = toExit.sqrMagnitude < 0.001f ? 0f : Vector3.Angle(forward, toExit);

        DoorController[] doors = Object.FindObjectsByType<DoorController>(FindObjectsSortMode.None);
        foreach (DoorController d in doors)
        {
            if (d == null || d.isOpen) continue;

            Vector3 flatToPlayer = d.transform.position - player.position;
            flatToPlayer.y = 0f;
            if (flatToPlayer.magnitude > normalDoorYieldDistance) continue;

            Vector3 toDoor = d.transform.position - cam.position;
            toDoor.y = 0f;
            if (toDoor.sqrMagnitude < 0.001f) return true;

            float angle = Vector3.Angle(forward, toDoor);

            // Normal kapının kendi bakış sınırı 60 derece; o sınırın içinde ve çıkıştan daha dik bakılıyorsa çekil
            if (angle <= 60f && angle < exitAngle) return true;
        }

        return false;
    }

    // Oyuncudan çıkış kapısına çizilen çizgide başka bir collider varsa kapı "görünmüyor" sayılır.
    private bool HasClearLineToDoor()
    {
        Vector3 origin = player.position + Vector3.up * 1.2f;
        Vector3 targetPoint = doorBounds.ClosestPoint(origin);

        Vector3 dir = targetPoint - origin;
        float dist = dir.magnitude;
        if (dist < 0.05f) return true;

        int count = Physics.RaycastNonAlloc(origin, dir / dist, rayHits, dist + 0.3f, ~0, QueryTriggerInteraction.Ignore);

        // Çıkış kapısının kendisine ne kadar mesafede çarpıyoruz?
        float doorHitDistance = dist + 0.3f;
        for (int i = 0; i < count; i++)
        {
            Transform t = rayHits[i].transform;
            if (BelongsTo(t, transform) && rayHits[i].distance < doorHitDistance)
                doorHitDistance = rayHits[i].distance;
        }

        // Kapıdan ÖNCE başka bir şeye çarpıyorsak engel var demektir
        for (int i = 0; i < count; i++)
        {
            Transform t = rayHits[i].transform;
            if (BelongsTo(t, transform)) continue;
            if (BelongsTo(t, player)) continue;

            if (rayHits[i].distance < doorHitDistance - 0.02f) return false;
        }

        return true;
    }

    private bool BelongsTo(Transform t, Transform root)
    {
        return t == root || t.IsChildOf(root);
    }

    void TryExit()
    {
        bool allSolved = (QuestionManager.instance != null && QuestionManager.instance.AreAllQuestionsSolved());

        if (testModeHerZamanAc || allSolved)
        {
            if (testModeHerZamanAc && !allSolved)
            {
                Debug.LogWarning("[ExitDoor] Sorular bitmediği halde TEST MODU yüzünden kapı açılıyor.");
            }

            isOpening = true;

            // Devam eden uyarı varsa durdur, sonuç panelini kapatmasın
            if (warningRoutine != null)
            {
                StopCoroutine(warningRoutine);
                warningRoutine = null;
            }

            if (GameResultManager.Instance != null)
            {
                GameResultManager.Instance.RecordDoorOpened();
            }

            if (messageText != null)
            {
                messageText.text = BuildResultSummaryText();
            }

            if (messagePanel != null)
            {
                messagePanel.SetActive(true);
            }

            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (var col in colliders) col.enabled = false;

            if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();

            Time.timeScale = 0;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            string warningMessage = "<b><size=130%><color=#800000>KAPI KİLİTLİ!</color></size></b>\n\n" +
                                    "<size=90%><color=#2C1A0C>Labirentteki tüm soruları çözmeden buradan çıkamazsın.</color></size>";

            // Üst üste binen uyarıları engelle: öncekini durdurup yenisini başlat
            if (warningRoutine != null) StopCoroutine(warningRoutine);
            warningRoutine = StartCoroutine(ShowWarning(warningMessage));
        }
    }

    private string BuildResultSummaryText()
    {
        int targetDoors = QuestionManager.instance != null ? QuestionManager.instance.TotalDoorCount : 10;
        int solvedDoors = targetDoors; // Çıkış açıldıysa hepsi çözülmüştür

        float finalScore = 100f;
        int failedAttempts = 0;
        int hintsUsed = 0;
        float remainingSeconds = 0f;

        if (GameResultManager.Instance != null)
        {
            finalScore = GameResultManager.Instance.CalculateFinalScore();
            failedAttempts = GameResultManager.Instance.GetFailedAttempts();
            hintsUsed = GameResultManager.Instance.GetHintsUsed();
            remainingSeconds = GameResultManager.Instance.GetRemainingTime();
            solvedDoors = GameResultManager.Instance.GetCorrectAnswersCount();
        }

        int minutes = Mathf.FloorToInt(remainingSeconds / 60f);
        int seconds = Mathf.FloorToInt(remainingSeconds % 60f);

        return "<b><size=130%><color=#650F0F>TEBRİKLER!</color></size></b>\n" +
               "<size=90%><color=#3E1F07>Labirenti Başarıyla Tamamladın.</color></size>\n\n" +
               "<b><size=115%><color=#1F4E5B>SKOR: " + finalScore.ToString("F1") + " / 100</color></size></b>\n\n" +
               "<size=85%><color=#2C1A0C>" +
               "• Çözülen Soru Sayısı: " + solvedDoors + " / " + targetDoors + "\n" +
               "• Hatalı Deneme: " + failedAttempts + "\n" +
               "• Kullanılan İpucu: " + hintsUsed + "\n" +
               "• Kalan Süre: " + string.Format("{0:00}:{1:00}", minutes, seconds) +
               "</color></size>";
    }

    IEnumerator ShowWarning(string message)
    {
        if (messageText != null) messageText.text = message;

        if (messagePanel != null)
        {
            messagePanel.SetActive(true);
            yield return new WaitForSecondsRealtime(3.5f);

            // Bu arada çıkış kapısı açıldıysa sonuç panelini kapatma
            if (!isOpening) messagePanel.SetActive(false);
        }

        warningRoutine = null;
    }

    // Butona bağlanacak fonksiyon
    public void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }
}