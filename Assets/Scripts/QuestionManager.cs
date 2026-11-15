using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

[System.Serializable]
public class QuestionData
{
    public int id;
    public string question_text;
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

[System.Serializable]
public class EvaluateRequest
{
    public int question_id;
    public string student_answer;
}

[System.Serializable]
public class EvaluateResponse
{
    public bool is_correct;
    public string feedback;
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
    private bool isEvaluating;
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
        if (isEvaluating) return;

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
        if (questionPanel.activeSelf || isEvaluating) return;

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
            answerInput.interactable = true;
            answerInput.text = "";
            answerInput.ActivateInputField();
        }

        if (submitButton != null)
        {
            submitButton.interactable = true;
        }
    }

    public void SubmitAnswer()
    {
        if (isEvaluating || activeQuestion == null) return;
        if (answerInput == null || string.IsNullOrWhiteSpace(answerInput.text)) return;

        StartCoroutine(EvaluateAnswerRoutine(activeQuestion, answerInput.text.Trim()));
    }

    private IEnumerator EvaluateAnswerRoutine(QuestionData question, string enteredAnswer)
    {
        isEvaluating = true;
        if (submitButton != null) submitButton.interactable = false;
        if (answerInput != null) answerInput.interactable = false;

        questionText.text = "<i><color=#3A1F04>Yapay zeka cevabını değerlendiriyor...</color></i>\n\n" + question.question_text;

        EvaluateRequest payload = new EvaluateRequest
        {
            question_id = question.id,
            student_answer = enteredAnswer
        };

        string url = serverUrl + "/api/evaluate";
        byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            isEvaluating = false;
            if (submitButton != null) submitButton.interactable = true;
            if (answerInput != null) answerInput.interactable = true;

            if (request.result == UnityWebRequest.Result.Success)
            {
                EvaluateResponse verdict = JsonUtility.FromJson<EvaluateResponse>(request.downloadHandler.text);

                if (verdict != null && verdict.is_correct)
                {
                    HandleCorrectAnswer(question);
                }
                else
                {
                    string feedback = (verdict != null && !string.IsNullOrWhiteSpace(verdict.feedback))
                        ? verdict.feedback
                        : "Cevabını kontrol edip tekrar dene.";
                    HandleWrongAnswer(question, feedback);
                }
            }
            else if (request.responseCode == 502 || request.responseCode == 503)
            {
                Debug.LogWarning("Gemini yoğunluk hatası: " + request.responseCode);
                questionText.text = "<color=red>Yapay zeka şu an yoğun, tekrar Cevapla'ya bas!</color>\n\n" + question.question_text;
                if (answerInput != null) answerInput.ActivateInputField();
            }
            else
            {
                Debug.LogWarning("Evaluate API error: " + request.responseCode + " - " + request.error);
                questionText.text = "<color=red>Sunucuya ulaşılamadı, tekrar dene!</color>\n\n" + question.question_text;
                if (answerInput != null) answerInput.ActivateInputField();
            }
        }
    }

    private void HandleCorrectAnswer(QuestionData question)
    {
        if (GameResultManager.Instance != null)
        {
            GameResultManager.Instance.RecordCorrectAnswer(ParseDifficulty(question.difficulty));
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();

        if (openedDoorCount < totalDoorCount)
        {
            openedDoorCount++;
        }
        UpdateScoreUI();

        remainingQuestions.Remove(question);
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

    private void HandleWrongAnswer(QuestionData question, string feedback)
    {
        if (GameResultManager.Instance != null)
        {
            GameResultManager.Instance.RecordFailedAttempt();
        }

        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ShakeEffect());
        if (AudioManager.Instance != null) AudioManager.Instance.PlayWrong();

        questionText.text = "<color=red>YANLIŞ CEVAP!</color>\n<color=#3A1F04><i>" + feedback + "</i></color>\n\n" + question.question_text;

        if (answerInput != null)
        {
            answerInput.text = "";
            answerInput.ActivateInputField();
        }
    }

    // --- HINT & ABANDON ---

    public void AbandonQuestion()
    {
        if (isEvaluating) return;

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
        if (isEvaluating || hintPanel == null || hintText == null) return;

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