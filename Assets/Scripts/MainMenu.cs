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

// Labirent ve Ortaçağ Temasına Uyarlanmış Ana Menü
public class MainMenu : MonoBehaviour
{
    [Header("Server")]
    public string serverUrl = "http://localhost:5000";

    [Header("Scenes")]
    public string gameSceneName = "SampleScene";

    [Header("Tematik Görsel & Font Varlıkları (Opsiyonel)")]
    [Tooltip("Oyun içi parşömen görseli (ExitMessagePanel'de kullanılan sprite)")]
    public Sprite parchmentPanelSprite;
    [Tooltip("Ahşap veya taş buton sprite'ı")]
    public Sprite buttonWoodSprite;
    [Tooltip("İnput alanı için parşömen/şerit sprite'ı")]
    public Sprite inputFieldSprite;
    [Tooltip("Ortaçağ / Gotik TextMeshPro Fontu")]
    public TMP_FontAsset themeFont;

    private const string TokenPrefsKey = "maze_student_token";

    // JSON modelleri (Sunucu formatıyla birebir aynı)
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

    // --- ORTAÇAĞ / LABİRENT RENK PALETİ ---
    private static readonly Color BackgroundColor = new Color32(18, 14, 12, 255);       // Zindan karanlığı
    private static readonly Color ParchmentColor = new Color32(232, 218, 192, 255);     // Yıpranmış açık parşömen
    private static readonly Color HeaderMaroonColor = new Color32(120, 28, 28, 255);    // Mühür kırmızısı
    private static readonly Color WoodButtonColor = new Color32(62, 36, 20, 255);       // Meşe ahşap
    private static readonly Color GoldAccentColor = new Color32(205, 155, 45, 255);     // Antik altın
    private static readonly Color PapyrusInputColor = new Color32(245, 237, 220, 255);  // Papirüs kutusu
    private static readonly Color InkColor = new Color32(35, 22, 14, 255);              // Mürekkep kahvesi
    private static readonly Color PlaceholderColor = new Color32(130, 110, 95, 255);    // Soluk mürekkep
    private static readonly Color MutedTextColor = new Color32(95, 68, 50, 255);        // Sepya metin
    private static readonly Color ErrorTextColor = new Color32(165, 30, 30, 255);       // Uyarı kırmızısı
    private static readonly Color ButtonTextLight = new Color32(248, 238, 218, 255);    // Kemik rengi yazı

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

    private TextMeshProUGUI showPasswordLabel;
    private bool passwordVisible;

    private bool busy;

    // ------------------------------------------------------------ Yaşam Döngüsü

    void Awake()
    {
        GameSession.ServerUrl = serverUrl.TrimEnd('/');

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

    // ------------------------------------------------------------ İşlemler

    private void Login()
    {
        if (busy) return;

        string email = emailInput.text.Trim();
        string password = passwordInput.text;

        if (email.Length == 0 || password.Length == 0)
        {
            SetMessage(loginMessage, "E-posta ve şifrenizi giriniz.", true);
            return;
        }

        StartCoroutine(LoginRoutine(email, password));
    }

    private IEnumerator LoginRoutine(string email, string password)
    {
        busy = true;
        SetMessage(loginMessage, "Giriş yapılıyor...", false);

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
                SetMessage(loginMessage, "Sunucuya bağlanılamadı.", true);
            }
            else
            {
                SetMessage(loginMessage, ExtractError(body, "Giriş başarısız."), true);
            }
        }));

        busy = false;
    }

    private IEnumerator RestoreLoginRoutine(string savedToken)
    {
        busy = true;
        ShowPanel(loginPanel);
        SetMessage(loginMessage, "Oturum kontrol ediliyor...", false);

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
                SetMessage(loginMessage, status == 0 ? "Sunucuya ulaşılamadı." : "", status == 0);
            }
        }));

        busy = false;
    }

    private void TogglePasswordVisibility()
    {
        SetPasswordVisible(!passwordVisible);
    }

    private void SetPasswordVisible(bool visible)
    {
        passwordVisible = visible;
        passwordInput.contentType = visible ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
        passwordInput.ForceLabelUpdate();
        showPasswordLabel.text = visible ? "Gizle" : "Göster";
    }

    private void SignIn(string token, StudentInfo student)
    {
        SetPasswordVisible(false);

        GameSession.Token = token;
        GameSession.PlayerName = student.first_name + " " + student.last_name;

        PlayerPrefs.SetString(TokenPrefsKey, token);
        PlayerPrefs.Save();

        welcomeLabel.text = "Hoş Geldin, " + GameSession.PlayerName;
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
            SetMessage(menuMessage, "Lütfen bir oda anahtarı giriniz.", true);
            return;
        }

        StartCoroutine(StartGameRoutine(code));
    }

    private IEnumerator StartGameRoutine(string code)
    {
        busy = true;
        SetMessage(menuMessage, "Zindana giriliyor...", false);

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
                    SetMessage(menuMessage, "Bu odada henüz soru hazırlanmamış.", true);
                }
            }
            else if (status == 404)
            {
                SetMessage(menuMessage, "Geçit bulunamadı. Kodu kontrol ediniz.", true);
            }
            else if (status == 0)
            {
                SetMessage(menuMessage, "Sunucuya ulaşılamadı.", true);
            }
            else
            {
                SetMessage(menuMessage, ExtractError(body, "Odaya katılınamadı."), true);
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
            SetMessage(menuMessage, "Sıralama için oda kodunu giriniz.", true);
            return;
        }

        StartCoroutine(LeaderboardRoutine(code));
    }

    private IEnumerator LeaderboardRoutine(string code)
    {
        busy = true;
        SetMessage(menuMessage, "Şeref kütüğü yükleniyor...", false);

        string path = "/api/rooms/" + UnityWebRequest.EscapeURL(code) + "/leaderboard";

        yield return StartCoroutine(SendRequest("GET", path, null, null, (status, body) =>
        {
            if (status == 200)
            {
                LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(body);
                leaderboardTitle.text = "<b>ŞEREF KÜTÜĞÜ</b>\n<size=70%><color=#5F4432>Oda: " + code + "</color></size>";
                leaderboardBody.text = BuildLeaderboardText(response.entries);
                SetMessage(menuMessage, "", false);
                ShowPanel(leaderboardPanel);
            }
            else if (status == 404)
            {
                SetMessage(menuMessage, "Oda bulunamadı.", true);
            }
            else if (status == 0)
            {
                SetMessage(menuMessage, "Sunucuya bağlanılamadı.", true);
            }
            else
            {
                SetMessage(menuMessage, ExtractError(body, "Sıralama alınamadı."), true);
            }
        }));

        busy = false;
    }

    private string BuildLeaderboardText(LeaderboardEntry[] entries)
    {
        if (entries == null || entries.Length == 0)
        {
            return "<color=#5F4432>Bu labirenti henüz tamamlayan olmadı.</color>";
        }

        StringBuilder text = new StringBuilder();
        text.AppendLine("<color=#781C1C><b>#<pos=9%>Kâşif<pos=60%>Çözülen<pos=76%>Süre<pos=91%>Kaçış</b></color>");

        int shown = Mathf.Min(entries.Length, 10);
        for (int i = 0; i < shown; i++)
        {
            LeaderboardEntry entry = entries[i];
            string name = entry.player_name.Replace("<", "").Replace(">", "");
            if (name.Length > 18) name = name.Substring(0, 16) + "...";

            string time = (entry.duration_seconds / 60) + ":" + (entry.duration_seconds % 60).ToString("00");

            string rankColor = entry.rank switch
            {
                1 => "<color=#C59B27><b>#1</b></color>",
                2 => "<color=#8C8C8C><b>#2</b></color>",
                3 => "<color=#A6652C><b>#3</b></color>",
                _ => $"#{entry.rank}"
            };

            text.AppendLine(
                rankColor + "<pos=9%>" + name +
                "<pos=60%>" + entry.solved_question_count + "/" + entry.total_question_count +
                "<pos=76%>" + time +
                "<pos=91%>" + (entry.won ? "<color=#2E6B34>Evet</color>" : "<color=#8B261D>Hayır</color>"));
        }

        return text.ToString();
    }

    private void OpenTeacherPage()
    {
        Application.OpenURL(GameSession.ServerUrl);
    }

    private void OpenPasswordResetPage()
    {
        Application.OpenURL(GameSession.ServerUrl + "/#reset");
    }

    // ------------------------------------------------------------ Ağ İletişimi

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
        catch (ArgumentException) { }
        return fallback;
    }

    // ------------------------------------------------------------ Arayüz Kurulumu

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

        RectTransform background = NewRect("Background", canvasTransform);
        Stretch(background, 0, 0);
        Image bgImg = background.gameObject.AddComponent<Image>();
        bgImg.color = BackgroundColor;

        TextMeshProUGUI title = CreateLabel(canvasTransform, "LABİRENT KAÇIŞI", 88, TextAlignmentOptions.Center, GoldAccentColor, FontStyles.Bold);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0, -60);
        titleRect.sizeDelta = new Vector2(1200, 120);

        BuildLoginPanel();
        BuildMenuPanel();
        BuildLeaderboardPanel();
    }

    private void BuildLoginPanel()
    {
        loginPanel = CreatePanel("LoginPanel");
        Transform panel = loginPanel.transform;

        CreateLabel(panel, "GİRİŞ YAP", 42, TextAlignmentOptions.Center, HeaderMaroonColor, FontStyles.Bold);

        emailInput = CreateInput(panel, "E-posta Adresi", TMP_InputField.ContentType.EmailAddress);

        GameObject passwordRow = new GameObject("PasswordRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        passwordRow.transform.SetParent(panel, false);
        SetPreferredHeight(passwordRow, 64);

        HorizontalLayoutGroup rowLayout = passwordRow.GetComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        passwordInput = CreateInput(passwordRow.transform, "Şifre", TMP_InputField.ContentType.Password);
        passwordInput.GetComponent<LayoutElement>().flexibleWidth = 1;
        passwordInput.onSubmit.AddListener(_ => Login());

        Button showPasswordButton = CreateButton(passwordRow.transform, "Göster", WoodButtonColor, ButtonTextLight, TogglePasswordVisibility);
        showPasswordButton.GetComponent<LayoutElement>().preferredWidth = 130;
        showPasswordLabel = showPasswordButton.GetComponentInChildren<TextMeshProUGUI>();

        loginMessage = CreateMessageLabel(panel);

        CreateButton(panel, "Giriş Yap", GoldAccentColor, InkColor, Login);
        CreateButton(panel, "Kayıt Ol (Web Sitesi)", WoodButtonColor, ButtonTextLight, OpenTeacherPage);
        CreateButton(panel, "Şifremi Unuttum", WoodButtonColor, ButtonTextLight, OpenPasswordResetPage);
    }

    private void BuildMenuPanel()
    {
        menuPanel = CreatePanel("MenuPanel");
        Transform panel = menuPanel.transform;

        welcomeLabel = CreateLabel(panel, "Hoş Geldin Kâşif", 38, TextAlignmentOptions.Center, HeaderMaroonColor, FontStyles.Bold);

        roomCodeInput = CreateInput(panel, "Oda Anahtarı / Kodu", TMP_InputField.ContentType.Alphanumeric);
        roomCodeInput.characterLimit = 6;
        roomCodeInput.onSubmit.AddListener(_ => StartGame());

        menuMessage = CreateMessageLabel(panel);

        CreateButton(panel, "Labirente Gir", GoldAccentColor, InkColor, StartGame);
        CreateButton(panel, "Şeref Kütüğü (Sıralama)", WoodButtonColor, ButtonTextLight, ShowLeaderboard);
        CreateButton(panel, "Öğretmen Paneli", WoodButtonColor, ButtonTextLight, OpenTeacherPage);
        CreateButton(panel, "Çıkış Yap", new Color32(45, 30, 22, 255), new Color32(190, 175, 160, 255), LogOut);
    }

    private void BuildLeaderboardPanel()
    {
        leaderboardPanel = CreatePanel("LeaderboardPanel");
        Transform panel = leaderboardPanel.transform;

        leaderboardTitle = CreateLabel(panel, "ŞEREF KÜTÜĞÜ", 38, TextAlignmentOptions.Center, HeaderMaroonColor, FontStyles.Bold);
        leaderboardBody = CreateLabel(panel, "", 24, TextAlignmentOptions.Left, InkColor);

        CreateButton(panel, "Geri Dön", WoodButtonColor, ButtonTextLight, () => ShowPanel(menuPanel));
    }

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
        rect.anchoredPosition = new Vector2(0, -60);
        rect.sizeDelta = new Vector2(760, 100);

        Image img = panel.GetComponent<Image>();
        if (parchmentPanelSprite != null)
        {
            img.sprite = parchmentPanelSprite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        else
        {
            img.color = ParchmentColor;
        }

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(48, 48, 40, 40);
        layout.spacing = 16;
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
        if (themeFont != null) label.font = themeFont;
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
        TextMeshProUGUI label = CreateLabel(parent, "", 24, TextAlignmentOptions.Center, MutedTextColor);
        SetMinHeight(label.gameObject, 32);
        return label;
    }

    private Button CreateButton(Transform parent, string text, Color backgroundColor, Color textColor, UnityAction onClick)
    {
        GameObject go = new GameObject(text + " Button", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetPreferredHeight(go, 64);

        Image image = go.GetComponent<Image>();
        if (buttonWoodSprite != null)
        {
            image.sprite = buttonWoodSprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
        }
        else
        {
            image.color = backgroundColor;
        }

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(image.color.r * 1.15f, image.color.g * 1.15f, image.color.b * 1.15f);
        colors.pressedColor = new Color(image.color.r * 0.8f, image.color.g * 0.8f, image.color.b * 0.8f);
        button.colors = colors;

        TextMeshProUGUI label = CreateLabel(go.transform, text, 28, TextAlignmentOptions.Center, textColor, FontStyles.Bold);
        Stretch(label.rectTransform, 10, 0);

        return button;
    }

    private TMP_InputField CreateInput(Transform parent, string placeholderText, TMP_InputField.ContentType contentType)
    {
        GameObject root = new GameObject(placeholderText + " Input", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);
        SetPreferredHeight(root, 64);

        Image background = root.GetComponent<Image>();
        if (inputFieldSprite != null)
        {
            background.sprite = inputFieldSprite;
            background.type = Image.Type.Sliced;
            background.color = Color.white;
        }
        else
        {
            background.color = PapyrusInputColor;
        }

        RectTransform area = NewRect("Text Area", root.transform);
        area.gameObject.AddComponent<RectMask2D>();
        Stretch(area, 20, 8);

        TextMeshProUGUI placeholder = CreateLabel(area, placeholderText, 28, TextAlignmentOptions.Left, PlaceholderColor, FontStyles.Italic);
        Stretch(placeholder.rectTransform, 0, 0);

        TextMeshProUGUI text = CreateLabel(area, "", 28, TextAlignmentOptions.Left, InkColor);
        Stretch(text.rectTransform, 0, 0);

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