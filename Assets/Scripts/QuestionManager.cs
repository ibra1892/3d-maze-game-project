using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

[System.Serializable]
public class QuestionData
{
    public int id;
    public string question_text;
    public string correct_answer;
    public string hint;
    public string difficulty;
}

[System.Serializable]
public class RoomData
{
    public string room_code;
    public int duration_minutes;
    public int question_count;
    public QuestionData[] questions;
}

public class QuestionManager : MonoBehaviour
{
    [Header("UI Elements")]
    [FormerlySerializedAs("soruPaneli")] public GameObject questionPanel;
    [FormerlySerializedAs("soruText")] public TextMeshProUGUI questionText;
    [FormerlySerializedAs("inputCevap")] public TMP_InputField answerInput;
    [FormerlySerializedAs("cevaplaButonu")] public Button submitButton;
    [FormerlySerializedAs("kapiSkorText")] public TextMeshProUGUI doorScoreText;
    [FormerlySerializedAs("skorBarFill")] public Image scoreBarFill;

    [Header("Hint & Abandon UI")]
    public GameObject hintPanel;            // Panel_Hint
    public TextMeshProUGUI hintText;        // Hint_Note
    public Camera playerCamera;             // Boş bırakılırsa Camera.main otomatik alınır

    [Header("Server")]
    public string serverUrl = "http://localhost:5000";
    [Tooltip("For testing: if filled in, this room is joined automatically when the scene starts.")]
    public string autoJoinRoomCode = "";

    public static QuestionManager instance;

    private const string OpenDoorMessage = "OpenDoor";

    private GameObject currentDoor;
    private readonly List<QuestionData> remainingQuestions = new List<QuestionData>();
    
    // Kapıya soru sabitleme hafızası
    private readonly Dictionary<GameObject, QuestionData> assignedDoorQuestions = new Dictionary<GameObject, QuestionData>();
    
    private QuestionData activeQuestion;

    private bool isRoomReady;
    private int openedDoorCount;
    private int totalDoorCount = 1;

    private Vector3 panelOriginalPosition;
    private Coroutine barAnimationCoroutine;
    private Coroutine shakeCoroutine;

    public string RoomCode { get; private set; }
    public int DurationMinutes { get; private set; }
    public QuestionData ActiveQuestion => activeQuestion;
    public bool IsRoomReady => isRoomReady;
    public int TotalDoorCount => totalDoorCount;

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        Time.timeScale = 0;

        panelOriginalPosition = questionPanel.transform.localPosition;
        questionPanel.SetActive(false);

        if (hintPanel != null) hintPanel.SetActive(false);
        if (scoreBarFill != null) scoreBarFill.fillAmount = 0f;
        UpdateScoreUI();

        if (submitButton != null)
        {
            submitButton.onClick.AddListener(SubmitAnswer);
        }

        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (!string.IsNullOrEmpty(GameSession.ServerUrl)) serverUrl = GameSession.ServerUrl;

        string roomToJoin = !string.IsNullOrWhiteSpace(GameSession.RoomCode) ? GameSession.RoomCode : autoJoinRoomCode;

        if (!string.IsNullOrWhiteSpace(roomToJoin))
        {
            JoinRoom(roomToJoin, (success, message) =>
            {
                if (!success) Debug.LogWarning("Auto join failed: " + message);
            });
        }
    }

    void Update()
    {
        if (questionPanel.activeSelf || (hintPanel != null && hintPanel.activeSelf))
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                SubmitAnswer();
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (hintPanel != null && hintPanel.activeSelf)
                {
                    CloseHint();
                }
                else
                {
                    AbandonQuestion();
                }
            }
        }
    }

    // --- LOADING QUESTIONS FROM SERVER ---

    public void JoinRoom(string roomCode, System.Action<bool, string> onDone)
    {
        StartCoroutine(JoinRoomRoutine(roomCode.Trim().ToUpperInvariant(), onDone));
    }

    private IEnumerator JoinRoomRoutine(string roomCode, System.Action<bool, string> onDone)
    {
        string url = serverUrl + "/api/rooms/" + UnityWebRequest.EscapeURL(roomCode) + "/questions";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 10;
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                RoomData room = JsonUtility.FromJson<RoomData>(request.downloadHandler.text);

                if (room == null || room.questions == null || room.questions.Length == 0)
                {
                    onDone?.Invoke(false, "This room has no questions yet.");
                    yield break;
                }

                RoomCode = room.room_code;
                DurationMinutes = room.duration_minutes;

                remainingQuestions.Clear();
                assignedDoorQuestions.Clear();
                remainingQuestions.AddRange(room.questions);
                totalDoorCount = remainingQuestions.Count;
                openedDoorCount = 0;
                isRoomReady = true;
                Time.timeScale = 1;

                if (GameResultManager.Instance != null)
                {
                    if (room.duration_minutes > 0)
                    {
                        GameResultManager.Instance.SetTotalTime(room.duration_minutes * 60f);
                    }

                    foreach (var q in room.questions)
                    {
                        GameResultManager.Instance.RegisterQuestion(ParseDifficulty(q.difficulty));
                    }
                }

                if (scoreBarFill != null) scoreBarFill.fillAmount = 0f;
                UpdateScoreUI();

                onDone?.Invoke(true, null);
            }
            else if (request.responseCode == 404)
            {
                onDone?.Invoke(false, "Room not found. Check the code and try again.");
            }
            else
            {
                onDone?.Invoke(false, "Could not reach the server.");
            }
        }
    }

    // --- QUESTION FLOW ---

    public void AskQuestion(GameObject door)
    {
        if (questionPanel.activeSelf) return;

        if (!isRoomReady)
        {
            Debug.LogWarning("No room joined yet. Join a room before opening doors.");
            return;
        }

        if (remainingQuestions.Count == 0 && !assignedDoorQuestions.ContainsKey(door))
        {
            if (openedDoorCount < totalDoorCount)
            {
                openedDoorCount++;
                UpdateScoreUI();
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();
            door.SendMessage(OpenDoorMessage, SendMessageOptions.DontRequireReceiver);
            return;
        }

        currentDoor = door;
        
        if (hintPanel != null) hintPanel.SetActive(false);
        questionPanel.SetActive(true);

        if (AudioManager.Instance != null) AudioManager.Instance.PlayPaperOpen();

        Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (assignedDoorQuestions.ContainsKey(door))
        {
            activeQuestion = assignedDoorQuestions[door];
        }
        else
        {
            int randomIndex = UnityEngine.Random.Range(0, remainingQuestions.Count);
            activeQuestion = remainingQuestions[randomIndex];
            assignedDoorQuestions[door] = activeQuestion;
        }

        questionText.text = activeQuestion.question_text;

        if (answerInput != null)
        {
            answerInput.text = "";
            answerInput.ActivateInputField();
        }
    }

    public void SubmitAnswer()
    {
        if (activeQuestion == null) return;
        if (answerInput == null || string.IsNullOrWhiteSpace(answerInput.text)) return;

        string enteredAnswer = answerInput.text;

        if (IsAnswerCorrect(enteredAnswer, activeQuestion.correct_answer))
        {
            if (GameResultManager.Instance != null)
            {
                GameResultManager.Instance.RecordCorrectAnswer(ParseDifficulty(activeQuestion.difficulty));
            }

            if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();

            if (openedDoorCount < totalDoorCount)
            {
                openedDoorCount++;
            }
            UpdateScoreUI();

            remainingQuestions.Remove(activeQuestion);
            if (currentDoor != null && assignedDoorQuestions.ContainsKey(currentDoor))
            {
                assignedDoorQuestions.Remove(currentDoor);
            }

            activeQuestion = null;
            ResetDoorDrawer();

            if (hintPanel != null) hintPanel.SetActive(false);
            questionPanel.SetActive(false);
            Time.timeScale = 1;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (currentDoor != null) currentDoor.SendMessage(OpenDoorMessage, SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            if (GameResultManager.Instance != null)
            {
                GameResultManager.Instance.RecordFailedAttempt();
            }

            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeEffect());
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWrong();

            questionText.text = "<color=red>YANLIŞ CEVAP, TEKRAR DENE!</color>\n\n" + activeQuestion.question_text;

            if (answerInput != null)
            {
                answerInput.text = "";
                answerInput.ActivateInputField();
            }
        }
    }

    // --- HINT & ABANDON ---

    public void AbandonQuestion()
    {
        ResetDoorDrawer();

        if (hintPanel != null) hintPanel.SetActive(false);
        if (questionPanel != null) questionPanel.SetActive(false);

        activeQuestion = null;
        currentDoor = null;

        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void OpenHint()
    {
        if (hintPanel == null || hintText == null) return;

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPaperOpen();
        }

        if (questionPanel != null) questionPanel.SetActive(false);

        string hintContent = (activeQuestion != null && !string.IsNullOrWhiteSpace(activeQuestion.hint))
            ? activeQuestion.hint
            : "Bu kapı için ipucu bulunamadı.";

        hintText.text = "<b>İPUCU:</b>\n" + hintContent;

        if (GameResultManager.Instance != null)
        {
            GameResultManager.Instance.RecordHintUsed();
        }

        DoorHintDrawer drawer = null;
        if (currentDoor != null)
        {
            drawer = currentDoor.GetComponent<DoorHintDrawer>() 
                  ?? currentDoor.GetComponentInChildren<DoorHintDrawer>() 
                  ?? currentDoor.GetComponentInParent<DoorHintDrawer>();
        }

        if (drawer != null && drawer.drawerSlider != null)
        {
            Transform camTransform = playerCamera != null ? playerCamera.transform : (Camera.main != null ? Camera.main.transform : null);

            drawer.OpenDrawerCinematic(camTransform, () =>
            {
                hintPanel.SetActive(true);
            });
        }
        else
        {
            hintPanel.SetActive(true);
        }
    }

    public void CloseHint()
    {
        if (hintPanel != null) hintPanel.SetActive(false);

        ResetDoorDrawer();

        if (questionPanel != null)
        {
            questionPanel.SetActive(true);
            if (answerInput != null) answerInput.ActivateInputField();
        }
    }

    private void ResetDoorDrawer()
    {
        if (currentDoor != null)
        {
            DoorHintDrawer drawer = currentDoor.GetComponent<DoorHintDrawer>() 
                                  ?? currentDoor.GetComponentInChildren<DoorHintDrawer>() 
                                  ?? currentDoor.GetComponentInParent<DoorHintDrawer>();
            if (drawer != null)
            {
                drawer.CloseDrawerInstant();
            }
        }
    }

    public bool AreAllQuestionsSolved()
    {
        return openedDoorCount >= totalDoorCount;
    }

    private GameResultManager.QuestionDifficulty ParseDifficulty(string difficultyStr)
    {
        if (string.IsNullOrEmpty(difficultyStr)) return GameResultManager.QuestionDifficulty.Easy;

        string normalized = difficultyStr.Trim().ToLowerInvariant();
        if (normalized == "hard" || normalized == "zor" || normalized == "3")
            return GameResultManager.QuestionDifficulty.Hard;
        if (normalized == "medium" || normalized == "orta" || normalized == "2")
            return GameResultManager.QuestionDifficulty.Medium;

        return GameResultManager.QuestionDifficulty.Easy;
    }

    // --- CEVAP KONTROLÜ (SAYISAL AYRIŞTIRMA + LEVENSHTEIN) ---

    // Metinden nokta/virgül karmaşasını çözerek sayı ayrıştıran yardımcı fonksiyon
    private bool TryParseNumber(string text, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string s = text.Trim().Replace(',', '.');
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        string cleaned = text.Trim();

        cleaned = cleaned.Replace("İ", "i").Replace("I", "i");
        cleaned = cleaned.ToLowerInvariant();

        cleaned = cleaned.Replace('ı', 'i')
                         .Replace('ğ', 'g')
                         .Replace('ü', 'u')
                         .Replace('ş', 's')
                         .Replace('ö', 'o')
                         .Replace('ç', 'c');

        cleaned = Regex.Replace(cleaned, @"(?<=\d)\.(?=\d)", ",");

        cleaned = Regex.Replace(cleaned, @"[\{\}\[\]\(\)]", "");
        cleaned = Regex.Replace(cleaned, @"['""!?;:]", "");
        cleaned = Regex.Replace(cleaned, @"\.(?!\d)", "");
        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        return cleaned.Trim();
    }

    private bool IsAnswerCorrect(string playerText, string correctText)
    {
        if (string.IsNullOrWhiteSpace(playerText) || string.IsNullOrWhiteSpace(correctText)) return false;

        string[] alternatives = correctText.Split('|');

        foreach (string alternative in alternatives)
        {
            if (string.IsNullOrWhiteSpace(alternative)) continue;

            // 1. KONTROL: SAYISAL CEVAP KİLİDİ
            // Eğer veritabanındaki hedef cevap bir sayı ise (3.1417, 3.25 vb.)
            if (TryParseNumber(alternative, out double targetNum))
            {
                if (TryParseNumber(playerText, out double playerNum))
                {
                    // Sayısal değerler 0.0001 hassasiyetle birebir eşit mi?
                    if (Math.Abs(playerNum - targetNum) < 0.0001)
                        return true;
                }

                // Hedef cevap sayıysa Levenshtein ve Contains ASLA ÇALIŞMAZ!
                // Böylece 3.25 beklerken 3.29 giren oyuncu doğrudan elenir.
                continue;
            }

            // 2. KONTROL: METİNSEL CEVAP (Pisagor, Hipotenüs vb. kelimeler için typo toleransı)
            string cleanedPlayer = CleanText(playerText);
            string cleanedTarget = CleanText(alternative);

            // 2.1 Tam eşleşme
            if (cleanedPlayer == cleanedTarget) return true;

            // 2.2 Kapsama kontrolü
            if (cleanedPlayer.Length >= 4 && cleanedTarget.Length >= 4)
            {
                if (cleanedPlayer.Contains(cleanedTarget) || cleanedTarget.Contains(cleanedPlayer))
                {
                    if (Mathf.Abs(cleanedPlayer.Length - cleanedTarget.Length) <= 4)
                        return true;
                }
            }

            // 2.3 Levenshtein benzerlik kontrolü
            int distance = LevenshteinDistance(cleanedPlayer, cleanedTarget);
            int maxLength = Mathf.Max(cleanedPlayer.Length, cleanedTarget.Length);

            if (maxLength == 0) continue;

            if (maxLength <= 3)
            {
                if (distance == 0) return true;
            }
            else if (maxLength <= 6)
            {
                if (distance <= 1) return true;
            }
            else if (maxLength <= 10)
            {
                if (distance <= 2) return true;
            }
            else
            {
                if (distance <= 3) return true;
            }

            float similarity = 1.0f - ((float)distance / maxLength);
            if (similarity >= 0.65f) return true;
        }

        return false;
    }

    private int LevenshteinDistance(string s, string t)
    {
        if (string.IsNullOrEmpty(s)) return string.IsNullOrEmpty(t) ? 0 : t.Length;
        if (string.IsNullOrEmpty(t)) return s.Length;

        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        for (int i = 0; i <= n; i++) d[i, 0] = i;
        for (int j = 0; j <= m; j++) d[0, j] = j;

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (s[i - 1] == t[j - 1]) ? 0 : 1;
                d[i, j] = Mathf.Min(
                    Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost
                );
            }
        }
        return d[n, m];
    }

    private void UpdateScoreUI()
    {
        if (doorScoreText != null)
            doorScoreText.text = "Açılan Kapı: " + openedDoorCount + " / " + totalDoorCount;

        if (scoreBarFill != null && totalDoorCount > 0)
        {
            float targetFill = (float)openedDoorCount / totalDoorCount;
            if (barAnimationCoroutine != null) StopCoroutine(barAnimationCoroutine);
            barAnimationCoroutine = StartCoroutine(FillBarSmoothly(targetFill));
        }
    }

    private IEnumerator FillBarSmoothly(float target)
    {
        float startFill = scoreBarFill.fillAmount;
        float elapsed = 0f;
        float duration = 0.4f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            scoreBarFill.fillAmount = Mathf.Lerp(startFill, target, elapsed / duration);
            yield return null;
        }
        scoreBarFill.fillAmount = float.IsNaN(target) ? 0f : target;
    }

    private IEnumerator ShakeEffect()
    {
        Transform panelTransform = questionPanel.transform;
        float duration = 0.20f;
        float speed = 65f;
        float amount = 15f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float newX = panelOriginalPosition.x + Mathf.Sin(Time.unscaledTime * speed) * amount;
            panelTransform.localPosition = new Vector3(newX, panelOriginalPosition.y, panelOriginalPosition.z);
            yield return null;
        }
        panelTransform.localPosition = panelOriginalPosition;
    }
}