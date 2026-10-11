using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Builds the whole menu UI from code, so the scene only needs one empty GameObject with this script.
public class MainMenu : MonoBehaviour
{
    [Header("Server")]
    public string serverUrl = "http://localhost:5000";

    [Header("Scenes")]
    public string gameSceneName = "SampleScene";

    private const string TokenPrefsKey = "maze_student_token";

    // JSON shapes used by the server. Field names must match the JSON exactly.
    [Serializable] public class LoginRequest { public string email; public string password; }
    [Serializable] public class StudentInfo { public string first_name; public string last_name; public string email; }
    [Serializable] public class AuthResponse { public string token; public StudentInfo student; }
    [Serializable] public class MeResponse { public StudentInfo student; }
    [Serializable] public class ErrorResponse { public string error; }

    [Serializable]
    public class LeaderboardEntry
    {
        public int rank;
        public string player_name;
        public int solved_question_count;
        public int total_question_count;
        public int duration_seconds;
        public bool won;
    }

    [Serializable]
    public class LeaderboardResponse
    {
        public string room_code;
        public LeaderboardEntry[] entries;
    }

    // Colors
    private static readonly Color BackgroundColor = new Color32(23, 40, 44, 255);
    private static readonly Color PanelColor = new Color32(31, 58, 63, 255);
    private static readonly Color AccentColor = new Color32(15, 107, 107, 255);
    private static readonly Color QuietColor = new Color32(56, 84, 89, 255);
    private static readonly Color KeyColor = new Color32(227, 167, 47, 255);
    private static readonly Color InputColor = new Color32(241, 245, 244, 255);
    private static readonly Color InkColor = new Color32(23, 40, 44, 255);
    private static readonly Color PlaceholderColor = new Color32(110, 125, 125, 255);
    private static readonly Color MutedTextColor = new Color32(168, 187, 184, 255);
    private static readonly Color ErrorTextColor = new Color32(255, 140, 130, 255);

    private Transform canvasTransform;
    private GameObject loginPanel;
    private GameObject menuPanel;
    private GameObject leaderboardPanel;

    private TMP_InputField emailInput;
    private TMP_InputField passwordInput;
    private TMP_InputField roomCodeInput;

    private TextMeshProUGUI loginMessage;
    private TextMeshProUGUI menuMessage;
    private TextMeshProUGUI welcomeLabel;
    private TextMeshProUGUI leaderboardTitle;
    private TextMeshProUGUI leaderboardBody;

    private bool busy;

    // ------------------------------------------------------------ lifecycle

    void Awake()
    {
        GameSession.ServerUrl = serverUrl.TrimEnd('/');

        // The game scene can leave the cursor locked and time paused.
        Time.timeScale = 1;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();
        BuildUI();
    }

    void Start()
    {
        string savedToken = PlayerPrefs.GetString(TokenPrefsKey, "");

        if (!string.IsNullOrEmpty(savedToken))
        {
            StartCoroutine(RestoreLoginRoutine(savedToken));
        }
        else
        {
            ShowPanel(loginPanel);
        }
    }

    // ------------------------------------------------------------ actions

    private void Login()
    {
        if (busy) return;

        string email = emailInput.text.Trim();
        string password = passwordInput.text;

        if (email.Length == 0 || password.Length == 0)
        {
            SetMessage(loginMessage, "Enter your email and password.", true);
            return;
        }

        StartCoroutine(LoginRoutine(email, password));
    }

    private IEnumerator LoginRoutine(string email, string password)
    {
        busy = true;
        SetMessage(loginMessage, "Logging in...", false);

        string json = JsonUtility.ToJson(new LoginRequest { email = email, password = password });

        yield return StartCoroutine(SendRequest("POST", "/api/students/login", json, null, (status, body) =>
        {
            if (status == 200)
            {
                AuthResponse auth = JsonUtility.FromJson<AuthResponse>(body);
                passwordInput.text = "";
                SetMessage(loginMessage, "", false);
                SignIn(auth.token, auth.student);
            }
            else if (status == 0)
            {
                SetMessage(loginMessage, "Could not reach the server.", true);
            }
            else
            {
                SetMessage(loginMessage, ExtractError(body, "Login failed."), true);
            }
        }));

        busy = false;
    }

    // On start-up: check that the login saved on this computer is still valid.
    private IEnumerator RestoreLoginRoutine(string savedToken)
    {
        busy = true;
        ShowPanel(loginPanel);
        SetMessage(loginMessage, "Checking saved login...", false);

        yield return StartCoroutine(SendRequest("GET", "/api/students/me", null, savedToken, (status, body) =>
        {
            if (status == 200)
            {
                MeResponse me = JsonUtility.FromJson<MeResponse>(body);
                SetMessage(loginMessage, "", false);
                SignIn(savedToken, me.student);
            }
            else
            {
                if (status == 401) PlayerPrefs.DeleteKey(TokenPrefsKey);
                SetMessage(loginMessage, status == 0 ? "Could not reach the server." : "", status == 0);
            }
        }));

        busy = false;
    }

    private void SignIn(string token, StudentInfo student)
    {
        GameSession.Token = token;
        GameSession.PlayerName = student.first_name + " " + student.last_name;

        PlayerPrefs.SetString(TokenPrefsKey, token);
        PlayerPrefs.Save();

        welcomeLabel.text = "Welcome, " + GameSession.PlayerName;
        SetMessage(menuMessage, "", false);
        ShowPanel(menuPanel);
    }

    private void LogOut()
    {
        if (busy) return;

        PlayerPrefs.DeleteKey(TokenPrefsKey);
        GameSession.Token = "";
        GameSession.PlayerName = "";
        GameSession.RoomCode = "";

        roomCodeInput.text = "";
        SetMessage(loginMessage, "", false);
        ShowPanel(loginPanel);
    }

    private void StartGame()
    {
        if (busy) return;

        string code = roomCodeInput.text.Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            SetMessage(menuMessage, "Enter a room code.", true);
            return;
        }

        StartCoroutine(StartGameRoutine(code));
    }

    private IEnumerator StartGameRoutine(string code)
    {
        busy = true;
        SetMessage(menuMessage, "Joining room...", false);

        bool canStart = false;
        string path = "/api/rooms/" + UnityWebRequest.EscapeURL(code) + "/questions";

        yield return StartCoroutine(SendRequest("GET", path, null, null, (status, body) =>
        {
            if (status == 200)
            {
                RoomData room = JsonUtility.FromJson<RoomData>(body);
                if (room != null && room.questions != null && room.questions.Length > 0)
                {
                    canStart = true;
                }
                else
                {
                    SetMessage(menuMessage, "This room has no questions yet.", true);
                }
            }
            else if (status == 404)
            {
                SetMessage(menuMessage, "Room not found. Check the code and try again.", true);
            }
            else if (status == 0)
            {
                SetMessage(menuMessage, "Could not reach the server.", true);
            }
            else
            {
                SetMessage(menuMessage, ExtractError(body, "Could not join the room."), true);
            }
        }));

        busy = false;

        if (canStart)
        {
            GameSession.RoomCode = code;
            SceneManager.LoadScene(gameSceneName);
        }
    }

    private void ShowLeaderboard()
    {
        if (busy) return;

        string code = roomCodeInput.text.Trim().ToUpperInvariant();
        if (code.Length == 0)
        {
            SetMessage(menuMessage, "Enter a room code to see its leaderboard.", true);
            return;
        }

        StartCoroutine(LeaderboardRoutine(code));
    }

    private IEnumerator LeaderboardRoutine(string code)
    {
        busy = true;
        SetMessage(menuMessage, "Loading leaderboard...", false);

        string path = "/api/rooms/" + UnityWebRequest.EscapeURL(code) + "/leaderboard";

        yield return StartCoroutine(SendRequest("GET", path, null, null, (status, body) =>
        {
            if (status == 200)
            {
                LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(body);
                leaderboardTitle.text = "Leaderboard  -  " + code;
                leaderboardBody.text = BuildLeaderboardText(response.entries);
                SetMessage(menuMessage, "", false);
                ShowPanel(leaderboardPanel);
            }
            else if (status == 404)
            {
                SetMessage(menuMessage, "Room not found. Check the code and try again.", true);
            }
            else if (status == 0)
            {
                SetMessage(menuMessage, "Could not reach the server.", true);
            }
            else
            {
                SetMessage(menuMessage, ExtractError(body, "Could not load the leaderboard."), true);
            }
        }));

        busy = false;
    }

    private string BuildLeaderboardText(LeaderboardEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            return "No results yet for this room.";
        }

        StringBuilder text = new StringBuilder();
        text.AppendLine("<b>#<pos=9%>Player<pos=60%>Solved<pos=76%>Time<pos=91%>Exit</b>");

        int shown = Mathf.Min(entries.Length, 10);
        for (int i = 0; i < shown; i++)
        {
            LeaderboardEntry entry = entries[i];

            // Remove < and > so a name can never inject formatting tags.
            string name = entry.player_name.Replace("<", "").Replace(">", "");
            if (name.Length > 18) name = name.Substring(0, 16) + "...";

            string time = (entry.duration_seconds / 60) + ":" + (entry.duration_seconds % 60).ToString("00");

            text.AppendLine(
                entry.rank + "<pos=9%>" + name +
                "<pos=60%>" + entry.solved_question_count + "/" + entry.total_question_count +
                "<pos=76%>" + time +
                "<pos=91%>" + (entry.won ? "Yes" : "No"));
        }

        return text.ToString();
    }

    private void OpenTeacherPage()
    {
        Application.OpenURL(GameSession.ServerUrl);
    }

    // ------------------------------------------------------------ networking

    // Sends a request and reports (status code, response text). Status 0 means the server could not be reached.
    private IEnumerator SendRequest(string method, string path, string jsonBody, string token, Action<long, string> onDone)
    {
        using (UnityWebRequest request = new UnityWebRequest(GameSession.ServerUrl + path, method))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = 10;

            if (jsonBody != null)
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (!string.IsNullOrEmpty(token))
            {
                request.SetRequestHeader("Authorization", "Bearer " + token);
            }

            yield return request.SendWebRequest();

            bool reachedServer =
                request.result == UnityWebRequest.Result.Success ||
                request.result == UnityWebRequest.Result.ProtocolError;

            onDone(reachedServer ? request.responseCode : 0, request.downloadHandler.text);
        }
    }

    private string ExtractError(string body, string fallback)
    {
        try
        {
            ErrorResponse error = JsonUtility.FromJson<ErrorResponse>(body);
            if (error != null && !string.IsNullOrEmpty(error.error)) return error.error;
        }
        catch (ArgumentException)
        {
            // The body was not JSON, use the fallback.
        }
        return fallback;
    }

    // ------------------------------------------------------------ UI building

    private void ShowPanel(GameObject target)
    {
        loginPanel.SetActive(target == loginPanel);
        menuPanel.SetActive(target == menuPanel);
        leaderboardPanel.SetActive(target == leaderboardPanel);
    }

    private void SetMessage(TextMeshProUGUI label, string text, bool isError)
    {
        label.text = text;
        label.color = isError ? ErrorTextColor : MutedTextColor;
    }

    private void EnsureEventSystem()
    {
#if UNITY_2023_1_OR_NEWER
        EventSystem existing = FindFirstObjectByType<EventSystem>();
#else
        EventSystem existing = FindObjectOfType<EventSystem>();
#endif
        if (existing == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasTransform = canvasObject.transform;

        // Full-screen background
        RectTransform background = NewRect("Background", canvasTransform);
        Stretch(background, 0, 0);
        background.gameObject.AddComponent<Image>().color = BackgroundColor;

        // Game title
        TextMeshProUGUI title = CreateLabel(canvasTransform, "MAZE QUIZ", 96, TextAlignmentOptions.Center, KeyColor, FontStyles.Bold);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0, -50);
        titleRect.sizeDelta = new Vector2(1200, 120);

        BuildLoginPanel();
        BuildMenuPanel();
        BuildLeaderboardPanel();
    }

    private void BuildLoginPanel()
    {
        loginPanel = CreatePanel("LoginPanel");
        Transform panel = loginPanel.transform;

        CreateLabel(panel, "Log in", 44, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);

        emailInput = CreateInput(panel, "Email", TMP_InputField.ContentType.EmailAddress);
        passwordInput = CreateInput(panel, "Password", TMP_InputField.ContentType.Password);
        passwordInput.onSubmit.AddListener(_ => Login());

        loginMessage = CreateMessageLabel(panel);

        CreateButton(panel, "Log in", AccentColor, Color.white, Login);
        CreateButton(panel, "Create account (opens website)", QuietColor, Color.white, OpenTeacherPage);
    }

    private void BuildMenuPanel()
    {
        menuPanel = CreatePanel("MenuPanel");
        Transform panel = menuPanel.transform;

        welcomeLabel = CreateLabel(panel, "Welcome", 40, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);

        roomCodeInput = CreateInput(panel, "Room code", TMP_InputField.ContentType.Alphanumeric);
        roomCodeInput.characterLimit = 6;
        roomCodeInput.onSubmit.AddListener(_ => StartGame());

        menuMessage = CreateMessageLabel(panel);

        CreateButton(panel, "Start", KeyColor, InkColor, StartGame);
        CreateButton(panel, "Leaderboard", AccentColor, Color.white, ShowLeaderboard);
        CreateButton(panel, "Teacher Login", AccentColor, Color.white, OpenTeacherPage);
        CreateButton(panel, "Log out", QuietColor, Color.white, LogOut);
    }

    private void BuildLeaderboardPanel()
    {
        leaderboardPanel = CreatePanel("LeaderboardPanel");
        Transform panel = leaderboardPanel.transform;

        leaderboardTitle = CreateLabel(panel, "Leaderboard", 40, TextAlignmentOptions.Center, Color.white, FontStyles.Bold);
        leaderboardBody = CreateLabel(panel, "", 24, TextAlignmentOptions.Left, Color.white);

        CreateButton(panel, "Back", QuietColor, Color.white, () => ShowPanel(menuPanel));
    }

    // ------------------------------------------------------------ UI helpers

    private RectTransform NewRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static void Stretch(RectTransform rect, float horizontalMargin, float verticalMargin)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(horizontalMargin, verticalMargin);
        rect.offsetMax = new Vector2(-horizontalMargin, -verticalMargin);
    }

    private static void SetPreferredHeight(GameObject target, float height)
    {
        LayoutElement element = target.GetComponent<LayoutElement>();
        if (element == null) element = target.AddComponent<LayoutElement>();
        element.preferredHeight = height;
    }

    private static void SetMinHeight(GameObject target, float height)
    {
        LayoutElement element = target.GetComponent<LayoutElement>();
        if (element == null) element = target.AddComponent<LayoutElement>();
        element.minHeight = height;
    }

    private GameObject CreatePanel(string name)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(canvasTransform, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0, -70);
        rect.sizeDelta = new Vector2(760, 100);

        panel.GetComponent<Image>().color = PanelColor;

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 40, 40);
        layout.spacing = 18;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        panel.SetActive(false);
        return panel;
    }

    private TextMeshProUGUI CreateLabel(Transform parent, string text, float size, TextAlignmentOptions alignment, Color color, FontStyles style = FontStyles.Normal)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = color;
        label.fontStyle = style;
        label.raycastTarget = false;
        return label;
    }

    private TextMeshProUGUI CreateMessageLabel(Transform parent)
    {
        TextMeshProUGUI label = CreateLabel(parent, "", 26, TextAlignmentOptions.Center, MutedTextColor);
        SetMinHeight(label.gameObject, 36);
        return label;
    }

    private Button CreateButton(Transform parent, string text, Color backgroundColor, Color textColor, UnityAction onClick)
    {
        GameObject go = new GameObject(text + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetPreferredHeight(go, 68);

        Image image = go.GetComponent<Image>();
        image.color = backgroundColor;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        TextMeshProUGUI label = CreateLabel(go.transform, text, 30, TextAlignmentOptions.Center, textColor, FontStyles.Bold);
        Stretch(label.rectTransform, 10, 0);

        return button;
    }

    private TMP_InputField CreateInput(Transform parent, string placeholderText, TMP_InputField.ContentType contentType)
    {
        GameObject root = new GameObject(placeholderText + " Input", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        SetPreferredHeight(root, 68);

        Image background = root.GetComponent<Image>();
        background.color = InputColor;

        // The text area clips the text to the box
        RectTransform area = NewRect("Text Area", root.transform);
        area.gameObject.AddComponent<RectMask2D>();
        Stretch(area, 20, 8);

        TextMeshProUGUI placeholder = CreateLabel(area, placeholderText, 30, TextAlignmentOptions.Left, PlaceholderColor, FontStyles.Italic);
        Stretch(placeholder.rectTransform, 0, 0);

        TextMeshProUGUI text = CreateLabel(area, "", 30, TextAlignmentOptions.Left, InkColor);
        Stretch(text.rectTransform, 0, 0);

        // Add the input component last, after the children it needs exist
        root.SetActive(false);
        TMP_InputField input = root.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.targetGraphic = background;
        input.contentType = contentType;
        input.lineType = TMP_InputField.LineType.SingleLine;
        root.SetActive(true);

        return input;
    }
}