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

// Yatay Ferman, Antik Çerçeve ve Parşömen Flama Butonlu Ana Menü
public class MainMenu : MonoBehaviour
{
    [Header("Server")]
    public string serverUrl = "http://localhost:5000";

    [Header("Scenes")]
    public string gameSceneName = "SampleScene";

    [Header("Tematik Görsel & Font Varlıkları")]
    [Tooltip("Arka plan zindan duvarı dokusu (mossy_sandstone_diff_4k)")]
    public Sprite backgroundSprite;
    [Tooltip("Dış cephe altın çerçeve (cerceve-removebg-preview)")]
    public Sprite frameSprite;
    [Tooltip("İç parşömen görseli (parsomen_1)")]
    public Sprite parchmentPanelSprite;
    [Tooltip("Şerit buton görseli (menuButton_3-removebg-preview)")]
    public Sprite ribbonButtonSprite;
    [Tooltip("İkincil butonlar için şerit (Opsiyonel)")]
    public Sprite secondaryButtonSprite;
    [Tooltip("Ortaçağ TextMeshPro Fontu (MedievalSharp-Regular SDF)")]
    public TMP_FontAsset themeFont;

    [Header("Panel Boyutu")]
    [Tooltip("Çerçeveli panelin ekrandaki boyutu (1920x1080 referans çözünürlükte)")]
    public Vector2 panelSize = new Vector2(1400f, 900f);
    [Tooltip("Panelin ekran ortasına göre kayması (Y eksi = aşağı)")]
    public Vector2 panelOffset = new Vector2(0f, -45f);

    [Header("Çerçeve İnce Ayarı")]
    [Tooltip("Çerçeve PNG'sinin kenarlarında şeffaf boşluk var. (X = genişlik, Y = yükseklik)")]
    public Vector2 frameArtFill = new Vector2(0.96f, 0.82f);
    [Tooltip("Çerçevenin kalın süslü kısmının panele oranı. (X = sağ/sol, Y = üst/alt)")]
    public Vector2 frameWindowMargin = new Vector2(0.15f, 0.175f);

    [Header("Buton Boyutu")]
    [Tooltip("Şerit butonların yüksekliği.")]
    public float ribbonButtonHeight = 88f;

    private const string TokenPrefsKey = "maze_student_token";

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

    // --- ORTAÇAĞ RENK PALETİ ---
    private static readonly Color HeaderMaroonColor = new Color32(110, 20, 20, 255);    // Mühür kırmızısı
    private static readonly Color GoldAccentColor = new Color32(220, 170, 50, 255);     // Parlak antik altın
    private static readonly Color InkColor = new Color32(35, 20, 12, 255);              // Koyu mürekkep kahvesi
    private static readonly Color PapyrusInputColor = new Color32(248, 240, 222, 255);  // Papirüs kutusu
    private static readonly Color SmallBtnPapyrus = new Color32(236, 222, 196, 255);    // Ufak buton papirüs tonu
    private static readonly Color PlaceholderColor = new Color32(130, 110, 95, 255);    // Soluk mürekkep
    private static readonly Color MutedTextColor = new Color32(95, 68, 50, 255);        // Sepya
    private static readonly Color ErrorTextColor = new Color32(175, 25, 25, 255);       // Hata kırmızısı

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
            SetMessage(menuMessage, "Lütfen bir oda kodu giriniz.", true);
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
                SetMessage(menuMessage, "Oda bulunamadı. Kodu kontrol ediniz.", true);
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
        SetMessage(menuMessage, "Sıralama yükleniyor...", false);

        string path = "/api/rooms/" + UnityWebRequest.EscapeURL(code) + "/leaderboard";

        yield return StartCoroutine(SendRequest("GET", path, null, null, (status, body) =>
        {
            if (status == 200)
            {
                LeaderboardResponse response = JsonUtility.FromJson<LeaderboardResponse>(body);
                leaderboardTitle.text = "<b>SIRALAMA</b>  <size=70%><color=#6A4A32>(Oda: " + code + ")</color></size>";
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
        text.AppendLine("<color=#731616><b>#<pos=8%>Kâşif<pos=58%>Çözülen<pos=76%>Süre<pos=90%>Kaçış</b></color>");

        int shown = Mathf.Min(entries.Length, 8);
        for (int i = 0; i < shown; i++)
        {
            LeaderboardEntry entry = entries[i];
            string name = entry.player_name.Replace("<", "").Replace(">", "");
            if (name.Length > 20) name = name.Substring(0, 18) + "...";

            string time = (entry.duration_seconds / 60) + ":" + (entry.duration_seconds % 60).ToString("00");

            string rankColor = entry.rank switch
            {
                1 => "<color=#C59B27><b>#1</b></color>",
                2 => "<color=#8C8C8C><b>#2</b></color>",
                3 => "<color=#A6652C><b>#3</b></color>",
                _ => $"#{entry.rank}"
            };

            text.AppendLine(
                rankColor + "<pos=8%>" + name +
                "<pos=58%>" + entry.solved_question_count + "/" + entry.total_question_count +
                "<pos=76%>" + time +
                "<pos=90%>" + (entry.won ? "<color=#2E6B34>Evet</color>" : "<color=#8B261D>Hayır</color>"));
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

        // Arka Plan Duvarı
        RectTransform background = NewRect("Background", canvasTransform);
        Stretch(background, 0, 0);
        Image bgImg = background.gameObject.AddComponent<Image>();
        if (backgroundSprite != null)
        {
            bgImg.sprite = backgroundSprite;
            bgImg.color = new Color(0.42f, 0.42f, 0.42f, 1f);
        }
        else
        {
            bgImg.color = new Color32(18, 14, 12, 255);
        }

        // MAZE GAME Başlığı
        TextMeshProUGUI title = CreateLabel(canvasTransform, "MAZE GAME", 80, TextAlignmentOptions.Center, GoldAccentColor, FontStyles.Bold);
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0, -25);
        titleRect.sizeDelta = new Vector2(1000, 85);

        BuildLoginPanel();
        BuildMenuPanel();
        BuildLeaderboardPanel();
    }

    // ÇİZDİĞİN PLANA GÖRE BİREBİR HİZALANMIŞ GİRİŞ PANELİ (440px Simetrik Kutular)
    private void BuildLoginPanel()
    {
        loginPanel = CreateLandscapePanel("LoginPanel", out Transform panel);

        // Başlık
        TextMeshProUGUI title = CreateLabel(panel, "GİRİŞ YAP", 36, TextAlignmentOptions.Center, HeaderMaroonColor, FontStyles.Bold);
        SetPreferredHeight(title.gameObject, 44);

        // 1. E-posta Giriş Alanı: Tam 440px genişlik, ortalanmış
        GameObject emailRow = CreateCenteredRow(panel, 52);
        emailInput = CreateInput(emailRow.transform, "E-posta Adresi", TMP_InputField.ContentType.EmailAddress);
        LayoutElement emailEl = emailInput.GetComponent<LayoutElement>();
        emailEl.preferredWidth = 440;
        emailEl.flexibleWidth = 0;
        emailEl.preferredHeight = 48;
        CenterInputText(emailInput);

        // 2. Şifre Giriş Alanı + Göster Butonu: Toplam genişlik tam 440px (Üstteki e-posta ile milimetrik aynı boyutta)
        GameObject passwordRow = CreateCenteredRow(panel, 52);
        GameObject passContainer = new GameObject("PassContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        passContainer.transform.SetParent(passwordRow.transform, false);

        LayoutElement passContEl = passContainer.GetComponent<LayoutElement>();
        passContEl.preferredWidth = 440;
        passContEl.flexibleWidth = 0; // Taşmayı engelleyen kilit ayar
        passContEl.preferredHeight = 48;

        HorizontalLayoutGroup passLayout = passContainer.GetComponent<HorizontalLayoutGroup>();
        passLayout.spacing = 8;
        passLayout.childControlWidth = true;
        passLayout.childControlHeight = true;
        passLayout.childForceExpandWidth = false;
        passLayout.childForceExpandHeight = true;

        passwordInput = CreateInput(passContainer.transform, "Şifre", TMP_InputField.ContentType.Password);
        LayoutElement passInputEl = passwordInput.GetComponent<LayoutElement>();
        passInputEl.flexibleWidth = 1; // 440px'ten kalan 347px alanı doldurur
        passInputEl.preferredHeight = 48;
        passwordInput.onSubmit.AddListener(_ => Login());
        CenterInputText(passwordInput);

        // Göster Butonu: Papirüs dokulu, 85px genişlik
        Button showPasswordButton = CreateSmallButton(passContainer.transform, "Göster", SmallBtnPapyrus, InkColor, TogglePasswordVisibility);
        LayoutElement showBtnEl = showPasswordButton.GetComponent<LayoutElement>();
        showBtnEl.preferredWidth = 85;
        showBtnEl.flexibleWidth = 0;
        showBtnEl.preferredHeight = 48;
        showPasswordLabel = showPasswordButton.GetComponentInChildren<TextMeshProUGUI>();

        loginMessage = CreateMessageLabel(panel);

        // 3. Giriş Yap Ana Butonu (Büyük Flama)
        CreateRibbonButton(panel, "GİRİŞ YAP", Login, ribbonButtonHeight);

        // 4. Alt Butonlar (Kayıt Ol & Şifremi Unuttum)
        GameObject bottomRow = CreateCenteredRow(panel, 65);
        GameObject btnContainer = new GameObject("BtnContainer", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
        btnContainer.transform.SetParent(bottomRow.transform, false);
        LayoutElement btnContEl = btnContainer.GetComponent<LayoutElement>();
        btnContEl.preferredWidth = 560;
        btnContEl.flexibleWidth = 0;
        btnContEl.preferredHeight = 60;

        HorizontalLayoutGroup btnLayout = btnContainer.GetComponent<HorizontalLayoutGroup>();
        btnLayout.spacing = 14;
        btnLayout.childControlWidth = true;
        btnLayout.childControlHeight = true;
        btnLayout.childForceExpandWidth = true;
        btnLayout.childForceExpandHeight = true;

        CreateRibbonButton(btnContainer.transform, "KAYIT OL (WEB)", OpenTeacherPage, 58f, false);
        CreateRibbonButton(btnContainer.transform, "ŞİFREMİ UNUTTUM", OpenPasswordResetPage, 58f, false);
    }

    private void BuildMenuPanel()
    {
        menuPanel = CreateLandscapePanel("MenuPanel", out Transform panel);

        welcomeLabel = CreateLabel(panel, "Hoş Geldin Kâşif", 38, TextAlignmentOptions.Center, HeaderMaroonColor, FontStyles.Bold);
        welcomeLabel.enableAutoSizing = true;
        welcomeLabel.fontSizeMin = 22f;
        welcomeLabel.fontSizeMax = 38f;
        welcomeLabel.overflowMode = TextOverflowModes.Ellipsis;
        SetPreferredHeight(welcomeLabel.gameObject, 52);

        GameObject codeRow = CreateCenteredRow(panel, 58);
        roomCodeInput = CreateInput(codeRow.transform, "Oda Kodunu Giriniz", TMP_InputField.ContentType.Alphanumeric);
        roomCodeInput.characterLimit = 6;
        roomCodeInput.onSubmit.AddListener(_ => StartGame());
        LayoutElement codeElement = roomCodeInput.GetComponent<LayoutElement>();
        codeElement.preferredWidth = 440;
        codeElement.flexibleWidth = 0;
        codeElement.preferredHeight = 48;
        CenterInputText(roomCodeInput);

        menuMessage = CreateMessageLabel(panel);

        CreateRibbonButton(panel, "LABİRENTE GİR", StartGame, ribbonButtonHeight + 16f);
        CreateRibbonButton(panel, "SIRALAMA", ShowLeaderboard);
        CreateRibbonButton(panel, "ÖĞRETMEN PANELİ", OpenTeacherPage);
        CreateRibbonButton(panel, "ÇIKIŞ YAP", LogOut);
    }

    private void BuildLeaderboardPanel()
    {
        leaderboardPanel = CreateLandscapePanel("LeaderboardPanel", out Transform panel);

        leaderboardTitle = CreateLabel(panel, "SIRALAMA", 36, TextAlignmentOptions.Center, HeaderMaroonColor, FontStyles.Bold);
        leaderboardBody = CreateLabel(panel, "", 23, TextAlignmentOptions.Left, InkColor);

        CreateRibbonButton(panel, "GERİ DÖN", () => ShowPanel(menuPanel));
    }

    // ------------------------------------------------------------ UI İnşa Araçları

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

    private static void Stretch(RectTransform rect, float left, float right, float top, float bottom)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
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

    private GameObject CreateHorizontalRow(Transform parent, float height, float spacing)
    {
        GameObject row = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        row.transform.SetParent(parent, false);
        SetPreferredHeight(row, height);

        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        return row;
    }

    private GameObject CreateCenteredRow(Transform parent, float height)
    {
        GameObject row = CreateHorizontalRow(parent, height, 0f);
        row.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        return row;
    }

    private GameObject CreateLandscapePanel(string name, out Transform contentParent)
    {
        GameObject panelRoot = new GameObject(name, typeof(RectTransform));
        panelRoot.transform.SetParent(canvasTransform, false);

        RectTransform rootRect = panelRoot.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = panelOffset;
        rootRect.sizeDelta = panelSize;

        float windowX = panelSize.x * frameWindowMargin.x;
        float windowY = panelSize.y * frameWindowMargin.y;

        // 1. Katman: Parşömen Fermanı
        GameObject bgObj = new GameObject("ParchmentBG", typeof(RectTransform), typeof(Image));
        bgObj.transform.SetParent(panelRoot.transform, false);
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        Stretch(bgRect, windowX * 0.55f, windowX * 0.55f, windowY * 0.55f, windowY * 0.55f);

        Image bgImg = bgObj.GetComponent<Image>();
        if (parchmentPanelSprite != null)
        {
            bgImg.sprite = parchmentPanelSprite;
            bgImg.type = Image.Type.Simple;
            bgImg.color = Color.white;
        }
        else
        {
            bgImg.color = new Color32(232, 218, 192, 255);
        }

        // 2. Katman: İçerik
        GameObject contentObj = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup));
        contentObj.transform.SetParent(panelRoot.transform, false);
        RectTransform contentRect = contentObj.GetComponent<RectTransform>();
        Stretch(contentRect, windowX, windowX, windowY, windowY);

        VerticalLayoutGroup layout = contentObj.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 10);
        layout.spacing = 10;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        contentParent = contentObj.transform;

        // 3. Katman: Altın Çerçeve
        if (frameSprite != null)
        {
            GameObject frameObj = new GameObject("FrameOverlay", typeof(RectTransform), typeof(Image));
            frameObj.transform.SetParent(panelRoot.transform, false);
            RectTransform frameRect = frameObj.GetComponent<RectTransform>();

            float visualWidth = panelSize.x / Mathf.Max(0.1f, frameArtFill.x);
            float visualHeight = panelSize.y / Mathf.Max(0.1f, frameArtFill.y);

            frameRect.localEulerAngles = new Vector3(0, 0, 90f);
            frameRect.sizeDelta = new Vector2(visualHeight, visualWidth);
            frameRect.anchoredPosition = Vector2.zero;

            Image frameImg = frameObj.GetComponent<Image>();
            frameImg.sprite = frameSprite;
            frameImg.type = Image.Type.Simple;
            frameImg.raycastTarget = false;
        }

        panelRoot.SetActive(false);
        return panelRoot;
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
        TextMeshProUGUI label = CreateLabel(parent, "", 20, TextAlignmentOptions.Center, MutedTextColor);
        SetMinHeight(label.gameObject, 22);
        return label;
    }

    private Button CreateRibbonButton(Transform parent, string text, UnityAction onClick, float height = -1f, bool autoWrapInCenteredRow = true)
    {
        if (height <= 0f) height = ribbonButtonHeight;

        Transform targetParent = parent;
        if (autoWrapInCenteredRow)
        {
            GameObject row = CreateCenteredRow(parent, height);
            targetParent = row.transform;
        }

        GameObject go = new GameObject(text + " Ribbon Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(targetParent, false);

        float maxWidth = panelSize.x * (1f - 2f * frameWindowMargin.x) - 40f;
        float width = height * 4.6f;

        Image image = go.GetComponent<Image>();
        if (ribbonButtonSprite != null)
        {
            image.sprite = ribbonButtonSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.color = Color.white;
            width = height * (ribbonButtonSprite.rect.width / ribbonButtonSprite.rect.height);
        }
        else
        {
            image.color = new Color32(160, 45, 25, 255);
        }

        width = Mathf.Min(width, maxWidth);

        LayoutElement element = go.GetComponent<LayoutElement>();
        element.preferredWidth = width;
        element.preferredHeight = height;
        element.flexibleWidth = autoWrapInCenteredRow ? 0f : 1f;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.93f, 0.78f, 1f);
        colors.pressedColor = new Color(0.78f, 0.7f, 0.58f, 1f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        TextMeshProUGUI label = CreateLabel(go.transform, text, 30, TextAlignmentOptions.Center, InkColor, FontStyles.Bold);
        label.enableAutoSizing = true;
        label.fontSizeMin = 14f;
        label.fontSizeMax = 30f;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;

        float sideMargin = width * 0.11f;
        Stretch(label.rectTransform, sideMargin, sideMargin, 0f, height * 0.05f);

        return button;
    }

    private Button CreateSmallButton(Transform parent, string text, Color bgColor, Color textColor, UnityAction onClick)
    {
        GameObject go = new GameObject(text + " Button", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();
        image.color = bgColor;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        ColorBlock colors = button.colors;
        colors.normalColor = bgColor;
        colors.highlightedColor = new Color(bgColor.r * 0.92f, bgColor.g * 0.92f, bgColor.b * 0.92f, 1f);
        colors.pressedColor = new Color(bgColor.r * 0.8f, bgColor.g * 0.8f, bgColor.b * 0.8f, 1f);
        button.colors = colors;

        TextMeshProUGUI label = CreateLabel(go.transform, text, 20, TextAlignmentOptions.Center, textColor, FontStyles.Bold);
        Stretch(label.rectTransform, 4, 4, 0, 0);

        return button;
    }

    private TMP_InputField CreateInput(Transform parent, string placeholderText, TMP_InputField.ContentType contentType)
    {
        GameObject root = new GameObject(placeholderText + " Input", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        SetPreferredHeight(root, 48);

        Image background = root.GetComponent<Image>();
        background.color = PapyrusInputColor;

        RectTransform area = NewRect("Text Area", root.transform);
        area.gameObject.AddComponent<RectMask2D>();
        Stretch(area, 18, 18, 5, 5);

        TextMeshProUGUI placeholder = CreateLabel(area, placeholderText, 23, TextAlignmentOptions.Left, PlaceholderColor, FontStyles.Italic);
        Stretch(placeholder.rectTransform, 0, 0, 0, 0);

        TextMeshProUGUI text = CreateLabel(area, "", 23, TextAlignmentOptions.Left, InkColor);
        Stretch(text.rectTransform, 0, 0, 0, 0);

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

    private static void CenterInputText(TMP_InputField input)
    {
        if (input.textComponent != null) input.textComponent.alignment = TextAlignmentOptions.Center;

        TextMeshProUGUI placeholderText = input.placeholder as TextMeshProUGUI;
        if (placeholderText != null) placeholderText.alignment = TextAlignmentOptions.Center;
    }
}