using System;
using System.Text;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RuntimeConsoleWindow : MonoBehaviour
{
    private const string LanguagePrefKey = "PauseSimple.language";

    [Header("Toggle")]
    [SerializeField] private KeyCode toggleKey = KeyCode.BackQuote;
    [SerializeField] private bool startOpened;

    [Header("Window")]
    [SerializeField] private Vector2 initialSize = new Vector2(860f, 500f);
    [SerializeField] private Vector2 initialPosition = new Vector2(120f, -90f);
    [SerializeField] private Vector2 minSize = new Vector2(520f, 280f);
    [SerializeField] private Vector2 maxSize = new Vector2(1500f, 920f);
    [SerializeField] private Color windowColor = new Color(0.12f, 0.12f, 0.12f, 0.96f);
    [SerializeField] private Color inputBarColor = new Color(0.03f, 0.03f, 0.03f, 0.98f);
    [SerializeField] private Sprite consoleBackgroundSprite;
    [SerializeField] [Range(0f,1f)] private float consoleBackgroundAlpha = 0.12f;

    [Header("Typography")]
    [SerializeField] private Font titleFont;
    [SerializeField] private int titleFontSize = 22;
    [SerializeField] private int logFontSize = 16;
    [SerializeField] private int inputFontSize = 17;

    [Header("Resize")]
    [SerializeField] private float resizeHandleSize = 18f;

    private Canvas rootCanvas;
    private RectTransform window;
    private Text titleText;
    private Button closeButton;
    private InputField logOutputField;
    private Text logText;
    private InputField commandInput;
    private Text placeholderText;
    private RectTransform resizeHandle;

    private readonly StringBuilder logBuilder = new StringBuilder(4096);
    private bool isOpen;
    private int cachedLanguage = -1;

    private Font runtimeBodyFont;
    private CursorLockMode prevCursorLockMode;
    private bool prevCursorVisible;
    private bool cursorStateCaptured;
    private float clearConfirmUntil;

    private void Awake()
    {
        BuildUi();
        Application.logMessageReceived += OnUnityLog;
        ApplyLanguage(force: true);
        SetOpen(startOpened);
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= OnUnityLog;
        GameInputBindings.InputBlocked = false;

        if (cursorStateCaptured)
        {
            Cursor.lockState = prevCursorLockMode;
            Cursor.visible = prevCursorVisible;
            cursorStateCaptured = false;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetOpen(!isOpen);
        }

        if (!isOpen)
        {
            return;
        }

        // Keep UI cursor state while console is open even if gameplay scripts try to lock it.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        ApplyLanguage(force: false);

        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            if (commandInput != null && commandInput.isFocused)
            {
                SubmitCommand();
            }
        }
    }

    private void BuildUi()
    {
        runtimeBodyFont = Resources.GetBuiltinResource<Font>("Roboto-Bold.ttf");
        if (runtimeBodyFont == null)
        {
            runtimeBodyFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        if (titleFont == null)
        {
#if UNITY_EDITOR
            titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Press_Start_2P/PressStart2P-Regular.ttf");
#endif
            if (titleFont == null)
            {
                titleFont = runtimeBodyFont;
            }
        }

#if UNITY_EDITOR
        if (consoleBackgroundSprite == null)
        {
            consoleBackgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ICONS/console background yuki77mi_bw.png");
        }
#endif


        GameObject canvasGo = new GameObject("Runtime Console Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        rootCanvas = canvasGo.GetComponent<Canvas>();
        rootCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        rootCanvas.sortingOrder = 2500;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        window = CreateRect("Console Window", canvasGo.transform, typeof(Image));
        window.anchorMin = new Vector2(0f, 1f);
        window.anchorMax = new Vector2(0f, 1f);
        window.pivot = new Vector2(0f, 1f);
        window.anchoredPosition = initialPosition;
        window.sizeDelta = initialSize;
        window.GetComponent<Image>().color = windowColor;

        if (consoleBackgroundSprite != null)
        {
            RectTransform bgRect = CreateRect("Console Background", window, typeof(Image));
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;

            Image bg = bgRect.GetComponent<Image>();
            bg.sprite = consoleBackgroundSprite;
            bg.preserveAspect = false;
            bg.raycastTarget = false;
            bg.color = new Color(1f, 1f, 1f, consoleBackgroundAlpha);
            bgRect.SetAsFirstSibling();
        }


        RectTransform header = CreateRect("Header", window, typeof(Image));
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = new Vector2(1f, 1f);
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 46f);
        header.anchoredPosition = Vector2.zero;
        header.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.04f);

        WindowHeaderDrag headerDrag = header.gameObject.AddComponent<WindowHeaderDrag>();
        headerDrag.Initialize(window);

        titleText = CreateText("Title", header, titleFont, titleFontSize, TextAnchor.MiddleLeft, FontStyle.Bold);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(14f, 0f);
        titleRect.offsetMax = new Vector2(-58f, 0f);
        titleText.color = Color.white;

        closeButton = CreateButton("Close Button", header, "x", runtimeBodyFont, 20);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 0.5f);
        closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.sizeDelta = new Vector2(42f, 30f);
        closeRect.anchoredPosition = new Vector2(-8f, 0f);
        closeButton.onClick.AddListener(() => SetOpen(false));

        RectTransform body = CreateRect("Body", window, typeof(Image));
        body.anchorMin = new Vector2(0f, 0f);
        body.anchorMax = new Vector2(1f, 1f);
        body.offsetMin = new Vector2(10f, 52f);
        body.offsetMax = new Vector2(-10f, -56f);
        body.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.015f);

        RectTransform viewport = CreateRect("Viewport", body, typeof(Image), typeof(RectMask2D));
        viewport.anchorMin = new Vector2(0f, 0f);
        viewport.anchorMax = new Vector2(1f, 1f);
        viewport.offsetMin = new Vector2(8f, 8f);
        viewport.offsetMax = new Vector2(-8f, -8f);
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
        viewportImage.raycastTarget = true;

        logText = CreateText("LogText", viewport, runtimeBodyFont, logFontSize, TextAnchor.UpperLeft, FontStyle.Normal);
        logText.horizontalOverflow = HorizontalWrapMode.Wrap;
        logText.verticalOverflow = VerticalWrapMode.Overflow;
        logText.supportRichText = true;
        logText.color = new Color(0.88f, 0.88f, 0.88f, 1f);
        RectTransform logTextRect = logText.rectTransform;
        logTextRect.anchorMin = Vector2.zero;
        logTextRect.anchorMax = Vector2.one;
        logTextRect.offsetMin = new Vector2(6f, 6f);
        logTextRect.offsetMax = new Vector2(-6f, -6f);

        RectTransform caretRect = CreateRect("LogCaret", viewport, typeof(CanvasRenderer));
        Image caretImg = caretRect.gameObject.AddComponent<Image>();
        caretImg.color = new Color(1f, 1f, 1f, 0f);

        logOutputField = viewport.gameObject.AddComponent<InputField>();
        logOutputField.readOnly = true;
        logOutputField.lineType = InputField.LineType.MultiLineNewline;
        logOutputField.textComponent = logText;
        logOutputField.caretBlinkRate = 0f;
        logOutputField.caretWidth = 1;
        logOutputField.customCaretColor = true;
        logOutputField.caretColor = new Color(1f, 1f, 1f, 0f);
        logOutputField.selectionColor = new Color(0.32f, 0.54f, 0.96f, 0.45f);
        logOutputField.targetGraphic = viewportImage;

        RectTransform inputBar = CreateRect("Input Bar", window, typeof(Image));
        inputBar.anchorMin = new Vector2(0f, 0f);
        inputBar.anchorMax = new Vector2(1f, 0f);
        inputBar.pivot = new Vector2(0.5f, 0f);
        inputBar.sizeDelta = new Vector2(0f, 40f);
        inputBar.anchoredPosition = new Vector2(0f, 0f);
        inputBar.GetComponent<Image>().color = inputBarColor;

        RectTransform inputRoot = CreateRect("Input Root", inputBar, typeof(Image));
        inputRoot.anchorMin = new Vector2(0f, 0f);
        inputRoot.anchorMax = new Vector2(1f, 1f);
        inputRoot.offsetMin = new Vector2(8f, 6f);
        inputRoot.offsetMax = new Vector2(-8f, -6f);
        inputRoot.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.02f);

        commandInput = inputRoot.gameObject.AddComponent<InputField>();
        commandInput.lineType = InputField.LineType.SingleLine;
        commandInput.textComponent = CreateText("Input Text", inputRoot, runtimeBodyFont, inputFontSize, TextAnchor.MiddleLeft, FontStyle.Normal);
        commandInput.textComponent.rectTransform.offsetMin = new Vector2(8f, 0f);
        commandInput.textComponent.rectTransform.offsetMax = new Vector2(-8f, 0f);
        commandInput.textComponent.color = Color.white;

        placeholderText = CreateText("Placeholder", inputRoot, runtimeBodyFont, inputFontSize, TextAnchor.MiddleLeft, FontStyle.Italic);
        placeholderText.rectTransform.offsetMin = new Vector2(8f, 0f);
        placeholderText.rectTransform.offsetMax = new Vector2(-8f, 0f);
        placeholderText.color = new Color(1f, 1f, 1f, 0.35f);
        commandInput.placeholder = placeholderText;

        commandInput.onEndEdit.AddListener(OnInputEndEdit);

        resizeHandle = CreateRect("ResizeHandle", window, typeof(Image));
        resizeHandle.anchorMin = new Vector2(1f, 0f);
        resizeHandle.anchorMax = new Vector2(1f, 0f);
        resizeHandle.pivot = new Vector2(1f, 0f);
        resizeHandle.sizeDelta = new Vector2(resizeHandleSize, resizeHandleSize);
        resizeHandle.anchoredPosition = new Vector2(0f, 0f);
        resizeHandle.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.28f);

        ResizeHandleDrag drag = resizeHandle.gameObject.AddComponent<ResizeHandleDrag>();
        drag.Initialize(window, minSize, maxSize);

        AppendLog("art by Yuki Nanami @yuki77mi");
        AppendLog("ready.");
    }

    private void ApplyLanguage(bool force)
    {
        int lang = PlayerPrefs.GetInt(LanguagePrefKey, 1);
        if (!force && lang == cachedLanguage)
        {
            return;
        }

        cachedLanguage = lang;
        bool en = lang == 0;

        if (titleText != null)
        {
            titleText.text = en ? "CONSOLE" : "КОНСОЛЬ";
        }

        if (placeholderText != null)
        {
            placeholderText.text = en ? "type any command you want!" : "введите любую команду!";
        }
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        GameInputBindings.InputBlocked = open;

        if (window != null)
        {
            window.gameObject.SetActive(open);
        }

        if (open)
        {
            if (!cursorStateCaptured)
            {
                prevCursorLockMode = Cursor.lockState;
                prevCursorVisible = Cursor.visible;
                cursorStateCaptured = true;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            ApplyLanguage(force: true);
            EventSystem es = EventSystem.current;
            if (es != null && commandInput != null)
            {
                es.SetSelectedGameObject(commandInput.gameObject);
                commandInput.ActivateInputField();
            }
            return;
        }

        if (cursorStateCaptured)
        {
            Cursor.lockState = prevCursorLockMode;
            Cursor.visible = prevCursorVisible;
            cursorStateCaptured = false;
        }
    }

    private void OnInputEndEdit(string _)
    {
        if (!isOpen)
        {
            return;
        }


        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SubmitCommand();
        }
    }

    private void SubmitCommand()
    {
        if (commandInput == null)
        {
            return;
        }

        string cmd = commandInput.text != null ? commandInput.text.Trim() : string.Empty;
        if (cmd.Length == 0)
        {
            commandInput.text = string.Empty;
            commandInput.ActivateInputField();
            return;
        }

        AppendLog("> " + cmd);
        ExecuteCommand(cmd);
        commandInput.text = string.Empty;
        commandInput.ActivateInputField();
    }

    private void ExecuteCommand(string cmd)
    {
        string lower = cmd.ToLowerInvariant();
        bool en = cachedLanguage == 0;
        string[] parts = cmd.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (lower == "clear" || lower == "cls")
        {
            float now = Time.unscaledTime;
            if (now <= clearConfirmUntil)
            {
                logBuilder.Length = 0;
                SyncLogText();
                clearConfirmUntil = 0f;
                AppendLog("art by Yuki Nanami @yuki77mi");
                AppendLog("ready.");
            }
            else
            {
                clearConfirmUntil = now + 2.5f;
                AppendLog(en ? "[warn] repeat clear/cls within 2.5s to confirm." : "[warn] повторите clear/cls в течение 2.5с для подтверждения.");
            }
            return;
        }

        if (lower == "clear!" || lower == "cls!")
        {
            logBuilder.Length = 0;
            SyncLogText();
            clearConfirmUntil = 0f;
            AppendLog("art by Yuki Nanami @yuki77mi");
            AppendLog("ready.");
            return;
        }

        if (lower == "help")
        {
            PrintHelp(en);
            return;
        }

        if (lower.StartsWith("echo "))
        {
            AppendLog(cmd.Substring(5));
            return;
        }

        if (lower == "time")
        {
            AppendLog(DateTime.Now.ToString("HH:mm:ss"));
            return;
        }

        if (parts.Length > 0 && parts[0].Equals("fps", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length == 1)
            {
                int fr = Application.targetFrameRate;
                AppendLog(fr < 0 ? (en ? "fps: unlimited" : "фпс: безлимит") : (en ? "fps: " : "фпс: ") + fr);
                return;
            }

            if (parts[1].Equals("unlimited", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("inf", StringComparison.OrdinalIgnoreCase))
            {
                Application.targetFrameRate = -1;
                AppendLog(en ? "fps set: unlimited" : "фпс установлен: безлимит");
                return;
            }

            if (int.TryParse(parts[1], out int fps) && fps >= 15)
            {
                Application.targetFrameRate = fps;
                AppendLog((en ? "fps set: " : "фпс установлен: ") + fps);
                return;
            }

            AppendLog(en ? "[warn] usage: fps <number|unlimited>" : "[warn] использование: fps <число|unlimited>");
            return;
        }

        if (parts.Length > 0 && parts[0].Equals("timescale", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length == 1)
            {
                AppendLog((en ? "timescale: " : "таймскейл: ") + Time.timeScale.ToString("0.###", CultureInfo.InvariantCulture));
                return;
            }

            if (TryParseFloatLoose(parts[1], out float ts))
            {
                ts = Mathf.Clamp(ts, 0f, 20f);
                Time.timeScale = ts;
                AppendLog((en ? "timescale set: " : "таймскейл установлен: ") + ts.ToString("0.###", CultureInfo.InvariantCulture));
                return;
            }

            AppendLog(en ? "[warn] usage: timescale <0..20>" : "[warn] использование: timescale <0..20>");
            return;
        }

        if (parts.Length > 0 && parts[0].Equals("lang", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length == 1)
            {
                AppendLog(en ? "lang: en" : "lang: ru");
                return;
            }

            if (parts[1].Equals("en", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("english", StringComparison.OrdinalIgnoreCase))
            {
                PlayerPrefs.SetInt(LanguagePrefKey, 0);
                ApplyLanguage(true);
                AppendLog("language -> english");
                return;
            }

            if (parts[1].Equals("ru", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("russian", StringComparison.OrdinalIgnoreCase) || parts[1].Equals("рус", StringComparison.OrdinalIgnoreCase))
            {
                PlayerPrefs.SetInt(LanguagePrefKey, 1);
                ApplyLanguage(true);
                AppendLog("язык -> русский");
                return;
            }

            AppendLog(en ? "[warn] usage: lang <en|ru>" : "[warn] использование: lang <en|ru>");
            return;
        }

        if (parts.Length > 0 && parts[0].Equals("scene", StringComparison.OrdinalIgnoreCase))
        {
            Scene active = SceneManager.GetActiveScene();
            if (parts.Length == 1)
            {
                AppendLog((en ? "scene: " : "сцена: ") + active.name + " (" + active.buildIndex + ")");
                return;
            }

            if (parts[1].Equals("reload", StringComparison.OrdinalIgnoreCase))
            {
                SceneManager.LoadScene(active.buildIndex);
                return;
            }

            if (parts[1].Equals("load", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3)
            {
                if (int.TryParse(parts[2], out int idx))
                {
                    SceneManager.LoadScene(idx);
                    return;
                }

                SceneManager.LoadScene(parts[2]);
                return;
            }

            AppendLog(en ? "[warn] usage: scene | scene reload | scene load <name|index>" : "[warn] использование: scene | scene reload | scene load <имя|индекс>");
            return;
        }

        if (parts.Length > 0 && parts[0].Equals("window", StringComparison.OrdinalIgnoreCase))
        {
            if (parts.Length >= 2 && parts[1].Equals("reset", StringComparison.OrdinalIgnoreCase))
            {
                float w = Mathf.Clamp(initialSize.x, minSize.x, maxSize.x);
                float h = Mathf.Clamp(initialSize.y, minSize.y, maxSize.y);
                window.anchoredPosition = initialPosition;
                window.sizeDelta = new Vector2(w, h);
                AppendLog(en ? "window reset: pos + size restored" : "window reset: позиция и размер восстановлены");
                return;
            }

            if (parts.Length >= 4 && parts[1].Equals("pos", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseFloatLoose(parts[2], out float x) && TryParseFloatLoose(parts[3], out float y))
                {
                    window.anchoredPosition = new Vector2(x, y);
                    AppendLog((en ? "window pos: " : "позиция окна: ") + x.ToString("0.##", CultureInfo.InvariantCulture) + ", " + y.ToString("0.##", CultureInfo.InvariantCulture));
                    return;
                }
                AppendLog(en ? "[warn] usage: window pos <x> <y>" : "[warn] использование: window pos <x> <y>");
                return;
            }

            if (parts.Length >= 4 && parts[1].Equals("size", StringComparison.OrdinalIgnoreCase))
            {
                if (TryParseFloatLoose(parts[2], out float w) && TryParseFloatLoose(parts[3], out float h))
                {
                    w = Mathf.Clamp(w, minSize.x, maxSize.x);
                    h = Mathf.Clamp(h, minSize.y, maxSize.y);
                    window.sizeDelta = new Vector2(w, h);
                    AppendLog((en ? "window size: " : "размер окна: ") + w.ToString("0.##", CultureInfo.InvariantCulture) + " x " + h.ToString("0.##", CultureInfo.InvariantCulture));
                    return;
                }
                AppendLog(en ? "[warn] usage: window size <w> <h>" : "[warn] использование: window size <w> <h>");
                return;
            }

            AppendLog(en ? "[warn] usage: window reset | window pos <x> <y> | window size <w> <h>" : "[warn] использование: window reset | window pos <x> <y> | window size <w> <h>");
            return;
        }

        if (lower == "close")
        {
            SetOpen(false);
            return;
        }

        AppendLog(en ? "[warn] unknown command. type 'help'." : "[warn] неизвестная команда. напишите 'help'.");
    }

    private static bool TryParseFloatLoose(string value, out float result)
    {
        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
        {
            return true;
        }

        string normalized = value.Replace(',', '.');
        return float.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }

    private void PrintHelp(bool en)
    {
        AppendLog("[help] ============================== ");
        AppendLog(en ? "[help] available commands" : "[help] доступные команды");
        AppendLog("[help] ------------------------------ ");
        AppendLog(en ? "help                 - show this help" : "help                 - показать эту справку");
        AppendLog(en ? "clear | cls          - clear console (with confirm)" : "clear | cls          - очистить консоль (с подтверждением)");
        AppendLog(en ? "clear! | cls!        - force clear" : "clear! | cls!        - очистить без подтверждения");
        AppendLog(en ? "echo <text>          - print custom text" : "echo <текст>         - вывести свой текст");
        AppendLog(en ? "time                 - show local time" : "time                 - показать локальное время");
        AppendLog(en ? "fps [n|unlimited]    - get/set target fps" : "fps [n|unlimited]    - получить/задать лимит фпс");
        AppendLog(en ? "timescale [v]        - get/set time scale" : "timescale [v]        - получить/задать таймскейл");
        AppendLog(en ? "lang [en|ru]         - switch console language" : "lang [en|ru]         - сменить язык консоли");
        AppendLog(en ? "scene                - active scene info" : "scene                - информация об активной сцене");
        AppendLog(en ? "scene reload         - reload active scene" : "scene reload         - перезагрузить активную сцену");
        AppendLog(en ? "scene load <name/id> - load scene" : "scene load <имя/id>  - загрузить сцену");
        AppendLog(en ? "window reset         - reset pos and size" : "window reset         - сбросить позицию и размер");
        AppendLog(en ? "window pos x y       - move console window" : "window pos x y       - переместить окно");
        AppendLog(en ? "window size w h      - resize console window" : "window size w h      - изменить размер окна");
        AppendLog(en ? "close                - close console" : "close                - закрыть консоль");
        AppendLog("[help] ============================== ");
    }

    private void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log)
        {
            return;
        }

        string prefix = type == LogType.Error || type == LogType.Exception ? "[error] " : "[warn] ";
        AppendLog(prefix + condition);
    }

    private void AppendLog(string line)
    {
        if (line == null)
        {
            return;
        }

        if (logBuilder.Length > 0)
        {
            logBuilder.Append('\n');
        }

        logBuilder.Append(ColorizeLogLine(line));
        SyncLogText();
    }

    private static string ColorizeLogLine(string line)
    {
        string trimmed = line.TrimStart();
        if (trimmed.StartsWith("[help]", StringComparison.OrdinalIgnoreCase))
        {
            return "<color=#8FD4FF>" + line + "</color>";
        }

        if (trimmed.StartsWith("[warn]", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("[warm]", StringComparison.OrdinalIgnoreCase))
        {
            return "<color=#F2D35A>" + line + "</color>";
        }

        if (trimmed.StartsWith("[error]", StringComparison.OrdinalIgnoreCase))
        {
            return "<color=#FF6B6B>" + line + "</color>";
        }

        if (trimmed.StartsWith("art by Yuki Nanami @yuki77mi", StringComparison.OrdinalIgnoreCase))
        {
            return "<color=#C9D1D9>" + line + "</color>";
        }

        if (trimmed.Equals("ready.", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("ready", StringComparison.OrdinalIgnoreCase))
        {
            return "<color=#7CFF7A>" + line + "</color>";
        }

        return "<color=#FFFFFF>" + line + "</color>";
    }

    private void SyncLogText()
    {
        if (logOutputField == null || logText == null)
        {
            return;
        }

        string value = logBuilder.ToString();
        logOutputField.SetTextWithoutNotify(value);

        Canvas.ForceUpdateCanvases();
        logOutputField.caretPosition = value.Length;
        logOutputField.selectionAnchorPosition = value.Length;
        logOutputField.selectionFocusPosition = value.Length;
    }

    private static RectTransform CreateRect(string name, Transform parent, params Type[] extra)
    {
        Type[] comps;
        if (extra == null || extra.Length == 0)
        {
            comps = new[] { typeof(RectTransform) };
        }
        else
        {
            comps = new Type[extra.Length + 1];
            comps[0] = typeof(RectTransform);
            for (int i = 0; i < extra.Length; i++)
            {
                comps[i + 1] = extra[i];
            }
        }

        GameObject go = new GameObject(name, comps);
        go.transform.SetParent(parent, false);
        return go.GetComponent<RectTransform>();
    }

    private static Text CreateText(string name, Transform parent, Font font, int size, TextAnchor align, FontStyle style)
    {
        RectTransform rect = CreateRect(name, parent, typeof(Text));
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Text t = rect.GetComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.fontStyle = style;
        t.color = Color.white;
        return t;
    }

    private static Button CreateButton(string name, Transform parent, string label, Font font, int fontSize)
    {
        RectTransform rect = CreateRect(name, parent, typeof(Image), typeof(Button));
        Image bg = rect.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.08f);

        Text t = CreateText("Label", rect, font, fontSize, TextAnchor.MiddleCenter, FontStyle.Bold);
        t.text = label;

        return rect.GetComponent<Button>();
    }
}

public class WindowHeaderDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform target;
    private RectTransform parentRect;
    private Vector2 startMouseLocal;
    private Vector2 startWindowPos;

    public void Initialize(RectTransform targetWindow)
    {
        target = targetWindow;
        parentRect = targetWindow != null ? targetWindow.parent as RectTransform : null;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (target == null || parentRect == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out startMouseLocal);
        startWindowPos = target.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null || parentRect == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, eventData.pressEventCamera, out Vector2 currentMouseLocal);
        Vector2 delta = currentMouseLocal - startMouseLocal;
        target.anchoredPosition = startWindowPos + delta;
    }
}

public class ResizeHandleDrag : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    private RectTransform target;
    private Vector2 minSize;
    private Vector2 maxSize;
    private Vector2 startSize;
    private Vector2 startMouse;

    public void Initialize(RectTransform targetWindow, Vector2 min, Vector2 max)
    {
        target = targetWindow;
        minSize = min;
        maxSize = max;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (target == null)
        {
            return;
        }

        startSize = target.sizeDelta;
        startMouse = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null)
        {
            return;
        }

        Vector2 delta = eventData.position - startMouse;
        Vector2 size = new Vector2(startSize.x + delta.x, startSize.y - delta.y);
        size.x = Mathf.Clamp(size.x, minSize.x, maxSize.x);
        size.y = Mathf.Clamp(size.y, minSize.y, maxSize.y);
        target.sizeDelta = size;
    }
}








