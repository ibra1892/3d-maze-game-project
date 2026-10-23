using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro; // Ekrana TextMeshPro ile yazı yazabilmek için gerekli
using UnityEngine.SceneManagement;

public class GameResultManager : MonoBehaviour
{
    public static GameResultManager Instance { get; private set; }

    public enum QuestionDifficulty
    {
        Easy = 5,
        Medium = 10,
        Hard = 15
    }

    [Header("Level Settings")]
    [Tooltip("Total number of doors in the maze (Default: 20)")]
    public int totalDoors = 20;

    [Tooltip("Total allocated time in seconds entered by instructor (e.g. 600s = 10 mins)")]
    public float totalTimeInSeconds = 600f;

    [Header("UI References")]
    [Tooltip("Drag the Timertext UI element here")]
    public TextMeshProUGUI timerText; // Ekranda oluşturduğumuz 10:00 yazısını tutacak referans

    [Header("Game Over & Result Panel")]
    [Tooltip("Süre bittiğinde veya çıkışta açılan parşömen (ExitMessagePanel)")]
    public GameObject exitMessagePanel;
    [Tooltip("Parşömenin içindeki metin bileşeni")]
    public TextMeshProUGUI exitMessageText;
    [Tooltip("Süre dolunca gizlenecek oyun içi HUD (Opsiyonel)")]
    public GameObject inGameHUD;

    [Header("Tracking Metrics (For Evaluation)")]
    [SerializeField] private int failedAttempts = 0;
    [SerializeField] private int hintsUsed = 0;
    [SerializeField] private int openedDoors = 0;
    [SerializeField] private int correctAnswersCount = 0;

    [Header("Question Weight Metrics")]
    [SerializeField] private float earnedQuestionPoints = 0f;
    [SerializeField] private float totalQuestionPotential = 0f;

    private float remainingTime;
    private bool isTimerRunning = false;
    private bool isGameCompleted = false;

    // Sonuç sunucuya yalnızca bir kez gönderilir (kazanma veya süre bitimi, hangisi önce olursa)
    private bool resultSubmitted = false;

    // Sunucudaki POST /api/rooms/<kod>/scores ucunun beklediği JSON. Alan adları JSON ile birebir aynı olmalı.
    [Serializable]
    private class ScoreRequest
    {
        public int duration_seconds;
        public int solved_question_count;
        public int total_question_count;
        public bool won;
        public float score;
        public int failed_attempts;
        public int hints_used;
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        remainingTime = totalTimeInSeconds;
        isTimerRunning = true;
        UpdateTimerUI();
        FindUIElements();
    }

    private void FindUIElements()
    {
        // Inspector'dan atanmadıysa sahneden otomatik bul
        if (exitMessagePanel == null)
        {
            GameObject canvas = GameObject.Find("Canvas");
            if (canvas != null)
            {
                Transform[] allChildren = canvas.GetComponentsInChildren<Transform>(true);
                foreach (Transform t in allChildren)
                {
                    if (t.name == "ExitMessagePanel")
                    {
                        exitMessagePanel = t.gameObject;
                        break;
                    }
                }
            }
        }

        if (exitMessagePanel != null && exitMessageText == null)
        {
            exitMessageText = exitMessagePanel.GetComponentInChildren<TextMeshProUGUI>(true);
        }
    }

    private void Update()
    {
        // Oda sunucudan henüz yüklenmediyse sayacı beklet
        if (QuestionManager.instance != null && !QuestionManager.instance.IsRoomReady)
            return;

        // Oyun devam ettiği sürece süreyi geriye say (Time.unscaledDeltaTime sayesinde soru ve ipucu açıkken de akar)
        if (isTimerRunning && !isGameCompleted)
        {
            if (remainingTime > 0f)
            {
                remainingTime -= Time.unscaledDeltaTime;
                UpdateTimerUI();
            }
            else
            {
                remainingTime = 0f;
                isTimerRunning = false;
                UpdateTimerUI();
                OnTimeExpired();
            }
        }
    }

    // Süreyi ekrandaki ahşap tabelaya "09:59" formatında yazdıran fonksiyon
    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(remainingTime / 60f);
        int seconds = Mathf.FloorToInt(remainingTime % 60f);

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        // Son 60 saniye kala oyuncuyu uyarmak için yazıyı kırmızıya çevir
        if (remainingTime <= 60f && remainingTime > 0f)
        {
            timerText.color = new Color(0.9f, 0.2f, 0.2f);
        }
    }

    // Süre dolduğunda devreye giren fonksiyon
    private void OnTimeExpired()
    {
        Debug.LogWarning("[GameResultManager] Süre doldu!");
        TriggerGameOver();
    }

    public void TriggerGameOver()
    {
        if (isGameCompleted) return;

        // 1. Durumu sonlandır ve skoru hesapla
        float finalScore = CalculateFinalScore();

        // 2. Oyunu dondur ve fareyi serbest bırak
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // 3. Açık soru veya ipucu paneli varsa kapat
        if (QuestionManager.instance != null)
        {
            if (QuestionManager.instance.questionPanel != null) QuestionManager.instance.questionPanel.SetActive(false);
            if (QuestionManager.instance.hintPanel != null) QuestionManager.instance.hintPanel.SetActive(false);
        }

        // 4. Oyun içi HUD öğelerini gizle (isteğe bağlı)
        if (inGameHUD != null)
        {
            inGameHUD.SetActive(false);
        }

        // 5. Parşömeni yenilgi metniyle ekrana getir
        FindUIElements();

        if (exitMessagePanel != null && exitMessageText != null)
        {
            int targetDoors = QuestionManager.instance != null ? QuestionManager.instance.TotalDoorCount : 10;

            exitMessageText.text = 
                "<b><size=130%><color=#800000>SÜRE DOLDU!</color></size></b>\n" +
                "<size=90%><color=#3E1F07>Zaman tükendi, labirentte mahsur kaldın...</color></size>\n\n" +
                "<b><size=115%><color=#5B1F1F>SKOR: " + finalScore.ToString("F1") + " / 100</color></size></b>\n\n" +
                "<size=85%><color=#2C1A0C>" +
                "• Çözülen Soru Sayısı: " + correctAnswersCount + " / " + targetDoors + "\n" +
                "• Hatalı Deneme: " + failedAttempts + "\n" +
                "• Kullanılan İpucu: " + hintsUsed + "\n" +
                "• Kalan Süre: 00:00" +
                "</color></size>";

            exitMessagePanel.SetActive(true);
        }

        // 6. Sonucu öğretmen paneline ve liderlik tablosuna kaydet
        SubmitResult(false, exitMessageText);
    }

    // --- RESULT SUBMISSION ---

    // Oyun bitince (kazanma veya süre dolması) çağrılır. Skoru ve ölçütleri sunucuya gönderir.
    // statusText verilirse, sonuç panelinin altına "kaydedildi / kaydedilemedi" satırı eklenir.
    public void SubmitResult(bool won, TextMeshProUGUI statusText)
    {
        if (resultSubmitted) return;
        resultSubmitted = true;

        QuestionManager qm = QuestionManager.instance;
        if (qm == null || string.IsNullOrEmpty(qm.RoomCode))
        {
            Debug.LogWarning("[GameResultManager] Odaya bağlı değil, sonuç gönderilmedi.");
            return;
        }

        if (!GameSession.IsLoggedIn)
        {
            Debug.LogWarning("[GameResultManager] Öğrenci girişi yok (sahne menüsüz başlatıldı), sonuç gönderilmedi.");
            AppendStatus(statusText, "Giriş yapılmadığı için sonuç kaydedilmedi.", true);
            return;
        }

        float elapsed = Mathf.Clamp(totalTimeInSeconds - remainingTime, 0f, totalTimeInSeconds);

        ScoreRequest payload = new ScoreRequest
        {
            duration_seconds = Mathf.RoundToInt(elapsed),
            solved_question_count = correctAnswersCount,
            total_question_count = qm.TotalDoorCount,
            won = won,
            score = Mathf.Round(CalculateFinalScore() * 10f) / 10f, // ekrandaki gibi tek ondalık
            failed_attempts = failedAttempts,
            hints_used = hintsUsed
        };

        string url = qm.serverUrl.TrimEnd('/') + "/api/rooms/" + UnityWebRequest.EscapeURL(qm.RoomCode) + "/scores";
        StartCoroutine(SubmitResultRoutine(url, JsonUtility.ToJson(payload), statusText));
    }

    private IEnumerator SubmitResultRoutine(string url, string json, TextMeshProUGUI statusText)
    {
        AppendStatus(statusText, "Sonuç kaydediliyor...", false);

        // Oyun bittiğinde Time.timeScale = 0 olsa da web isteği normal şekilde tamamlanır.
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(json));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", "Bearer " + GameSession.Token);
            request.timeout = 15;

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("[GameResultManager] Sonuç kaydedildi: " + json);
                AppendStatus(statusText, "Sonucun öğretmenine iletildi.", false);
            }
            else
            {
                Debug.LogWarning($"[GameResultManager] Sonuç gönderilemedi ({request.responseCode}): {request.error} {request.downloadHandler?.text}");
                string reason = request.responseCode == 0 ? "sunucuya ulaşılamadı"
                              : request.responseCode == 401 ? "oturumun süresi dolmuş, tekrar giriş yap"
                              : "sunucu kabul etmedi";
                AppendStatus(statusText, "Sonuç kaydedilemedi (" + reason + ").", true);
            }
        }
    }

    // Sonuç paneline durum satırı ekler; önceki durum satırının yerine yazar.
    private TextMeshProUGUI statusTarget;
    private string statusBaseText;

    private void AppendStatus(TextMeshProUGUI target, string message, bool isError)
    {
        if (target == null) return;

        // Panel metni ilk kez değiştirilirken orijinal hali saklanır, sonraki durumlar onun altına yazılır
        if (target != statusTarget)
        {
            statusTarget = target;
            statusBaseText = target.text;
        }

        string color = isError ? "#800000" : "#2C4A1C";
        target.text = statusBaseText + "\n\n<size=70%><color=" + color + ">" + message + "</color></size>";
    }

    // --- SETUP & REGISTRATION ---

    public void RegisterQuestion(QuestionDifficulty difficulty)
    {
        totalQuestionPotential += (int)difficulty;
    }

    public void RegisterQuestion(int customPoints)
    {
        totalQuestionPotential += customPoints;
    }

    // Web sitesinden hoca yeni süre gönderdiğinde süreyi günceller
    public void SetTotalTime(float timeInSeconds)
    {
        totalTimeInSeconds = timeInSeconds;
        remainingTime = timeInSeconds;
        UpdateTimerUI();
    }

    // --- EVENT RECORDERS ---

    public void RecordFailedAttempt()
    {
        failedAttempts++;
        Debug.Log($"[GameResultManager] Failed attempt recorded. Total: {failedAttempts}");
    }

    public void RecordHintUsed()
    {
        hintsUsed++;
        Debug.Log($"[GameResultManager] Hint used. Total: {hintsUsed}");
    }

    public void RecordDoorOpened()
    {
        if (openedDoors < totalDoors)
        {
            openedDoors++;
            Debug.Log($"[GameResultManager] Door opened ({openedDoors}/{totalDoors})");
        }
    }

    public void RecordCorrectAnswer(QuestionDifficulty difficulty)
    {
        correctAnswersCount++;
        earnedQuestionPoints += (int)difficulty;
        Debug.Log($"[GameResultManager] Question solved! Difficulty: {difficulty} (+{(int)difficulty} pts)");
    }

    public void RecordCorrectAnswer(int customPoints)
    {
        correctAnswersCount++;
        earnedQuestionPoints += customPoints;
    }

    // --- EVALUATION & FINAL SCORE CALCULATION ---

    public float CalculateFinalScore()
    {
        isGameCompleted = true;
        isTimerRunning = false;

        float questionScore = 0f;
        if (totalQuestionPotential > 0f)
        {
            questionScore = (earnedQuestionPoints / totalQuestionPotential) * 60f;
        }

        float doorScore = Mathf.Clamp(openedDoors, 0, totalDoors);
        float timeRatio = (totalTimeInSeconds > 0f) ? Mathf.Clamp01(remainingTime / totalTimeInSeconds) : 0f;
        float timeScore = timeRatio * 20f;

        float penalties = (failedAttempts * 1f) + (hintsUsed * 2f);
        float rawScore = (questionScore + doorScore + timeScore) - penalties;
        float finalScore = Mathf.Clamp(rawScore, 0f, 100f);

        Debug.Log("================ EVALUATION SUMMARY ================");
        Debug.Log($"Question Score: {questionScore:F1} / 60");
        Debug.Log($"Door Score: {doorScore:F0} / 20");
        Debug.Log($"Time Score: {timeScore:F1} / 20");
        Debug.Log($"Penalties: -{penalties:F0} (Failed: {failedAttempts}, Hints: {hintsUsed})");
        Debug.Log($"FINAL SCORE: {finalScore:F1} / 100");
        Debug.Log("====================================================");

        return finalScore;
    }

    // --- GETTERS ---
    public int GetFailedAttempts() => failedAttempts;
    public int GetHintsUsed() => hintsUsed;
    public int GetOpenedDoors() => openedDoors;
    public int GetCorrectAnswersCount() => correctAnswersCount;
    public float GetRemainingTime() => remainingTime;
    public float GetTotalTime() => totalTimeInSeconds;
    public bool IsGameCompleted() => isGameCompleted;
}