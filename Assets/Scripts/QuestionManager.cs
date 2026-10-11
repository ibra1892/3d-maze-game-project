using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

// Field names match the JSON sent by the server (snake_case) because JsonUtility needs exact names.
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
    // [FormerlySerializedAs] keeps the references you already assigned in the Inspector
    // after the fields were renamed from Turkish to English.
    [Header("UI Elements")]
    [FormerlySerializedAs("soruPaneli")] public GameObject questionPanel;
    [FormerlySerializedAs("soruText")] public TextMeshProUGUI questionText;
    [FormerlySerializedAs("inputCevap")] public TMP_InputField answerInput;
    [FormerlySerializedAs("cevaplaButonu")] public Button submitButton;
    [FormerlySerializedAs("kapiSkorText")] public TextMeshProUGUI doorScoreText;
    [FormerlySerializedAs("skorBarFill")] public Image scoreBarFill;

    [Header("Server")]
    public string serverUrl = "http://localhost:5000";
    [Tooltip("For testing: if filled in, this room is joined automatically when the scene starts.")]
    public string autoJoinRoomCode = "";

    public static QuestionManager instance;

    // Name of the method on DoorController that opens the door.
    private const string OpenDoorMessage = "OpenDoor";

    private GameObject currentDoor;
    private readonly List<QuestionData> remainingQuestions = new List<QuestionData>();
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
        // The maze is built only after a room is joined. Pause the game until then,
        // otherwise the player falls through the empty scene.
        Time.timeScale = 0;

        panelOriginalPosition = questionPanel.transform.localPosition;
        questionPanel.SetActive(false);
        if (scoreBarFill != null) scoreBarFill.fillAmount = 0f;
        UpdateScoreUI();

        if (submitButton != null)
        {
            submitButton.onClick.AddListener(SubmitAnswer);
        }

        // If the player came from the main menu, use the room and server address chosen there.
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
        // While the panel is open, Enter also submits the answer.
        if (questionPanel.activeSelf && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            SubmitAnswer();
        }
    }

    // --- LOADING QUESTIONS FROM THE SERVER ---

    // Called from the "Join" button after the student types the room code.
    // onDone(success, errorMessage) lets the UI show an error or start the game.
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
                remainingQuestions.AddRange(room.questions);
                totalDoorCount = remainingQuestions.Count;
                openedDoorCount = 0;
                isRoomReady = true;
                Time.timeScale = 1;

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

    // Called by a door when the player interacts with it.
    public void AskQuestion(GameObject door)
    {
        if (questionPanel.activeSelf) return;

        if (!isRoomReady)
        {
            Debug.LogWarning("No room joined yet. Join a room before opening doors.");
            return;
        }

        // Every question has been used: remaining doors open without a question.
        if (remainingQuestions.Count == 0)
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
        questionPanel.SetActive(true);

        if (AudioManager.Instance != null) AudioManager.Instance.PlayPaperOpen();

        Time.timeScale = 0;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        int randomIndex = Random.Range(0, remainingQuestions.Count);
        activeQuestion = remainingQuestions[randomIndex];

        questionText.text = activeQuestion.question_text;

        // Clear the input field and focus it so the player can type right away.
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
            if (AudioManager.Instance != null) AudioManager.Instance.PlayDoorOpen();

            if (openedDoorCount < totalDoorCount)
            {
                openedDoorCount++;
            }
            UpdateScoreUI();

            remainingQuestions.Remove(activeQuestion);
            activeQuestion = null;
            questionPanel.SetActive(false);
            Time.timeScale = 1;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (currentDoor != null) currentDoor.SendMessage(OpenDoorMessage, SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
            shakeCoroutine = StartCoroutine(ShakeEffect());
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWrong();

            questionText.text = "<color=red>WRONG ANSWER, TRY AGAIN!</color>\n\n" + activeQuestion.question_text;

            if (answerInput != null)
            {
                answerInput.text = "";
                answerInput.ActivateInputField();
            }
        }
    }

    public bool AreAllQuestionsSolved()
    {
        return openedDoorCount >= totalDoorCount;
    }

    // --- TEXT FILTERING & LEVENSHTEIN ---

    // 1. Cleans math/set symbols, punctuation and extra spaces before comparing.
    private string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        // Lowercase with Turkish rules. Both sides get the same treatment, so English works too.
        CultureInfo turkishCulture = new CultureInfo("tr-TR");
        string cleaned = text.Trim().ToLower(turkishCulture);

        // Remove set and bracket symbols: { } [ ] ( )
        cleaned = Regex.Replace(cleaned, @"[\{\}\[\]\(\)]", "");

        // Remove ? ! quotes ; : (keep commas and dots)
        cleaned = Regex.Replace(cleaned, @"['""!?;:]", "");

        // Remove sentence-ending dots ("ankara." -> "ankara") but keep decimals ("1.4")
        cleaned = Regex.Replace(cleaned, @"\.(?!\d)", "");

        // Remove spaces around commas and dots ("2, 3" -> "2,3")
        cleaned = Regex.Replace(cleaned, @"\s*([,\.])\s*", "$1");

        // Decimal standard: "1.4" becomes "1,4"
        cleaned = Regex.Replace(cleaned, @"(?<=\d)\.(?=\d)", ",");

        // Collapse repeated spaces
        cleaned = Regex.Replace(cleaned, @"\s+", " ");

        return cleaned.Trim();
    }

    // 2. Checks the answer against every alternative (separated by "|") with typo tolerance.
    private bool IsAnswerCorrect(string playerText, string correctText)
    {
        if (string.IsNullOrEmpty(playerText) || string.IsNullOrEmpty(correctText)) return false;

        string cleanedPlayer = CleanText(playerText);
        string[] alternatives = correctText.Split('|');

        foreach (string alternative in alternatives)
        {
            string cleanedTarget = CleanText(alternative);

            if (cleanedPlayer == cleanedTarget) return true;

            int distance = LevenshteinDistance(cleanedPlayer, cleanedTarget);
            int maxLength = Mathf.Max(cleanedPlayer.Length, cleanedTarget.Length);

            // 3 characters or fewer (e.g. "Nil", "1,4"): exact match only
            if (maxLength <= 3)
            {
                if (distance == 0) return true;
            }
            // 4-5 characters: allow 1 typo
            else if (maxLength <= 5)
            {
                if (distance <= 1) return true;
            }
            // Longer: 75% similarity or at most 2 typos
            else
            {
                float similarity = 1.0f - ((float)distance / maxLength);
                if (similarity >= 0.75f || distance <= 2) return true;
            }
        }

        return false;
    }

    // 3. Number of single-character edits between two strings.
    private int LevenshteinDistance(string s, string t)
    {
        int n = s.Length;
        int m = t.Length;
        int[,] d = new int[n + 1, m + 1];

        if (n == 0) return m;
        if (m == 0) return n;

        for (int i = 0; i <= n; d[i, 0] = i++) { }
        for (int j = 0; j <= m; d[0, j] = j++) { }

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
                d[i, j] = Mathf.Min(
                    Mathf.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }
        return d[n, m];
    }

    // --- UI AND EFFECTS ---

    private void UpdateScoreUI()
    {
        if (doorScoreText != null)
            doorScoreText.text = "Doors Opened: " + openedDoorCount + " / " + totalDoorCount;

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