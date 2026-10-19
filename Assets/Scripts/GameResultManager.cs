using UnityEngine;
using TMPro; // Ekrana TextMeshPro ile yazı yazabilmek için gerekli

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

    private void OnTimeExpired()
    {
        Debug.LogWarning("[GameResultManager] Süre doldu!");
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
}