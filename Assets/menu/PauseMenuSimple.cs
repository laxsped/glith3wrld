
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(200)]
public class PauseMenuSimple : MonoBehaviour
{
    private enum SettingsSection
    {
        General,
        Visual,
        Controls
    }

    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("Style")]
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.42f);
    [SerializeField] private int menuFontSize = 42;
    [SerializeField] private float buttonSpacing = 70f;
    [SerializeField] private float buttonsLeftOffset = 120f;
    [SerializeField] private float settingsLabelX = 120f;
    [SerializeField] private float settingsValueX = 760f;
    [SerializeField] private int settingsItemFontSize = 18;

    [Header("Fonts")]
    [SerializeField] private Font customFont;
    [SerializeField] private Font settingsItemsFont;

    [Header("Audio Links")]
    [SerializeField] private AudioSource menuAudioSource;
    [SerializeField] private WindNoiseGenerator windNoise;
    [SerializeField] private GrassFootstepNoise grassSteps;
    [SerializeField] private SurfaceMovementAudio surfaceMovementAudio;
    [SerializeField] private СкрежетТяги[] dragScrapeAudios;

    private const string PrefPrefix = "PauseSimple.";
    private const string KeyIconDirectory = "Assets/ICONS/Controls/keyboard-mouse-input-icons-251008/keyboard-input-icons/";
    private static readonly BindingAction[] RebindableActions =
    {
        BindingAction.Forward,
        BindingAction.Backward,
        BindingAction.Left,
        BindingAction.Right,
        BindingAction.Run,
        BindingAction.Jump,
        BindingAction.Action
    };
    private static readonly KeyCode[] AllKeyCodes = (KeyCode[])Enum.GetValues(typeof(KeyCode));

    private readonly List<Vector2Int> availableResolutions = new List<Vector2Int>();
    private readonly int[] fpsOptions = { 30, 60, 120, 144, 240, -1 };

    private Canvas canvas;
    private Image overlay;
    private RectTransform mainMenuRoot;
    private RectTransform settingsRoot;
    private RectTransform generalSectionRoot;
    private RectTransform visualSectionRoot;
    private RectTransform controlsSectionRoot;

    private Button resumeButton;
    private Button settingsButton;
    private Button quitButton;
    private Button backButton;
    private Button generalSectionButton;
    private Button visualSectionButton;
    private Button controlsSectionButton;

    private HoverQuestionSuffix resumeHover;
    private HoverQuestionSuffix settingsHover;
    private HoverQuestionSuffix quitHover;
    private HoverQuestionSuffix backHover;

    private Text resumeLabel;
    private Text settingsLabel;
    private Text quitLabel;
    private Text backLabel;
    private Text generalSectionButtonLabel;
    private Text visualSectionButtonLabel;
    private Text controlsSectionButtonLabel;

    private Text sectionTitleText;
    private Text accessibilityTitleText;
    private Text audioTitleText;
    private Text visualSectionTitleText;
    private Text screenTitleText;
    private Text graphicsTitleText;
    private Text controlsSectionTitleText;
    private Text keyRebindTitleText;

    private Text languageRowLabel;
    private Text colorBlindRowLabel;
    private Text masterVolumeRowLabel;
    private Text environmentVolumeRowLabel;
    private Text menuVolumeRowLabel;
    private Text effectsVolumeRowLabel;
    private Text resolutionRowLabel;
    private Text windowModeRowLabel;
    private Text fpsRowLabel;
    private Text vsyncRowLabel;
    private Text hdrRowLabel;
    private Text brightnessRowLabel;
    private Text contrastRowLabel;
    private Text qualityRowLabel;
    private Text dynamicShadowsRowLabel;
    private Text characterLightingRowLabel;

    private readonly Dictionary<BindingAction, KeybindRowUi> keybindRows = new Dictionary<BindingAction, KeybindRowUi>();
    private readonly Dictionary<KeyCode, Sprite> keyIconCache = new Dictionary<KeyCode, Sprite>();
    private BindingAction pendingRebindAction = BindingAction.None;

    private Button languageButton;
    private Text languageValueText;
    private Button colorBlindButton;
    private Text colorBlindValueText;

    private Slider masterVolumeSlider;
    private Text masterVolumeValueText;
    private Slider environmentVolumeSlider;
    private Text environmentVolumeValueText;
    private Slider menuVolumeSlider;
    private Text menuVolumeValueText;
    private Slider effectsVolumeSlider;
    private Text effectsVolumeValueText;

    private Button resolutionButton;
    private Text resolutionValueText;
    private Button windowModeButton;
    private Text windowModeValueText;
    private Button fpsButton;
    private Text fpsValueText;
    private Button vsyncButton;
    private Text vsyncValueText;
    private Button hdrButton;
    private Text hdrValueText;
    private Button qualityButton;
    private Text qualityValueText;
    private Button dynamicShadowsButton;
    private Text dynamicShadowsValueText;
    private Button characterLightingButton;
    private Text characterLightingValueText;

    private Slider brightnessSlider;
    private Text brightnessValueText;
    private Slider contrastSlider;
    private Text contrastValueText;

    [Header("HDRP Quality Assets")]
    [SerializeField] private HDRenderPipelineAsset lowHdrpAsset;
    [SerializeField] private HDRenderPipelineAsset mediumHdrpAsset;
    [SerializeField] private HDRenderPipelineAsset highHdrpAsset;

    private Volume runtimeSettingsVolume;
    private VolumeProfile runtimeSettingsProfile;
    private ColorAdjustments colorAdjustments;

    private bool isOpen;
    private SettingsSection currentSection = SettingsSection.General;
    private RectTransform dropdownPopupRoot;
    private RectTransform dropdownPopupViewport;
    private RectTransform dropdownPopupContent;
    private ScrollRect dropdownPopupScroll;
    private bool dropdownPopupOpen;
    private readonly List<Button> dropdownPopupButtons = new List<Button>();
    private Action<int> dropdownPopupOnSelect;

    // 0 - english, 1 - русский
    private int languageIndex = 1;
    private int colorBlindIndex;

    // 0..10 scale
    private int masterVolumeLevel = 10;
    private int environmentVolumeLevel = 10;
    private int menuVolumeLevel = 10;
    private int effectsVolumeLevel = 10;

    private int resolutionIndex;
    private int windowModeIndex;
    private int fpsIndex = 1;
    private int vSyncIndex;
    private int hdrIndex = 1;
    private int dynamicShadowsIndex = 1;
    private int graphicsQualityIndex = 1;
    // 0 - classic (unlit), 1 - modern (lit)
    private int characterLightingMode = 0;

    // HDRP color settings
    private float brightnessExposure;
    private float contrastAmount;

    // 100% baseline values captured from current scene state
    private float environmentBaseVolume = 1f;
    private float menuBaseVolume = 1f;
    private float effectsBaseVolume = 1f;
    private float ambientBaseIntensity = 1f;
    private readonly Dictionary<Light, LightShadows> cachedLightShadows = new Dictionary<Light, LightShadows>();

    private bool IsEnglish => languageIndex == 0;
    private static readonly FullScreenMode[] windowModeOptions =
    {
        FullScreenMode.ExclusiveFullScreen,
        FullScreenMode.FullScreenWindow,
        FullScreenMode.Windowed
    };

    private sealed class KeybindRowUi
    {
        public BindingAction Action;
        public Text Label;
        public Button Button;
        public Image Icon;
        public Text Value;
    }

    private void Awake()
    {
        GameInputBindings.EnsureLoaded();
        EnsureEventSystem();
        BuildUi();
        AutoFindAudioLinks();
        AutoAssignHdrpAssets();
        BuildResolutionList();
        EnsureVisualSettingsController();

        ambientBaseIntensity = RenderSettings.ambientIntensity;

        LoadSettings();
        ApplyAllSettings();
        ApplySettingsToUi();
        ApplyLocalization();

        ShowMainMenu();
        SetOpen(false, true);
    }

    private void Update()
    {
        if (pendingRebindAction != BindingAction.None)
        {
            CaptureRebindKey();
            return;
        }

        if (Input.GetKeyDown(toggleKey))
        {
            SetOpen(!isOpen);
        }

        if (dropdownPopupOpen && Input.GetMouseButtonDown(0) && dropdownPopupRoot != null)
        {
            Vector2 mouse = Input.mousePosition;
            if (!RectTransformUtility.RectangleContainsScreenPoint(dropdownPopupRoot, mouse, null))
            {
                CloseDropdownPopup();
            }
        }

    }

    private void OnDestroy()
    {
        if (runtimeSettingsProfile != null)
        {
            Destroy(runtimeSettingsProfile);
        }

        // Safety: if object dies while paused, do not leave global audio muted.
        if (isOpen)
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    private void SetOpen(bool open, bool force = false)
    {
        if (!force && isOpen == open)
        {
            return;
        }

        isOpen = open;
        if (canvas != null)
        {
            canvas.enabled = open;
        }

        Time.timeScale = open ? 0f : 1f;
        // Hard mute while paused.
        AudioListener.pause = open;
        Cursor.visible = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;

        if (!open)
        {
            CloseDropdownPopup();
            CancelRebind();
        }

        if (open)
        {
            ShowMainMenu();
        }
    }

    private void Resume()
    {
        SetOpen(false);
    }

    private void Quit()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OpenSettings()
    {
        SetActiveSafe(mainMenuRoot, false);
        SetActiveSafe(settingsRoot, true);
        SetSettingsSection(SettingsSection.General);
    }

    private void ShowMainMenu()
    {
        CancelRebind();
        SetActiveSafe(settingsRoot, false);
        SetActiveSafe(mainMenuRoot, true);
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("Pause Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Font sectionFont = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        Font itemsFont = ResolveSettingsItemsFont(sectionFont);

        GameObject overlayGo = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlayGo.transform.SetParent(canvasGo.transform, false);
        overlay = overlayGo.GetComponent<Image>();
        overlay.color = overlayColor;
        RectTransform overlayRect = overlayGo.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        mainMenuRoot = CreateRoot("Main Menu", overlay.transform);
        settingsRoot = CreateRoot("Settings", overlay.transform);

        resumeButton = CreateTextButton(mainMenuRoot, "Resume Button", new Vector2(buttonsLeftOffset, buttonSpacing), out resumeLabel, sectionFont, menuFontSize);
        settingsButton = CreateTextButton(mainMenuRoot, "Settings Button", new Vector2(buttonsLeftOffset, 0f), out settingsLabel, sectionFont, menuFontSize);
        quitButton = CreateTextButton(mainMenuRoot, "Quit Button", new Vector2(buttonsLeftOffset, -buttonSpacing), out quitLabel, sectionFont, menuFontSize);

        resumeButton.onClick.AddListener(Resume);
        settingsButton.onClick.AddListener(OpenSettings);
        quitButton.onClick.AddListener(Quit);

        resumeHover = AddHoverQuestionMark(resumeButton.gameObject, resumeLabel);
        settingsHover = AddHoverQuestionMark(settingsButton.gameObject, settingsLabel);
        quitHover = AddHoverQuestionMark(quitButton.gameObject, quitLabel);

        BuildSettingsUi(sectionFont, itemsFont);
    }

    private void BuildSettingsUi(Font sectionFont, Font itemsFont)
    {
        keybindRows.Clear();

        generalSectionButton = CreateSettingsTabButton(
            settingsRoot,
            "General Section Tab",
            new Vector2(settingsLabelX, -28f),
            sectionFont,
            menuFontSize - 14,
            out generalSectionButtonLabel);
        generalSectionButton.onClick.AddListener(() => SetSettingsSection(SettingsSection.General));

        visualSectionButton = CreateSettingsTabButton(
            settingsRoot,
            "Visual Section Tab",
            new Vector2(settingsLabelX + 240f, -28f),
            sectionFont,
            menuFontSize - 14,
            out visualSectionButtonLabel);
        visualSectionButton.onClick.AddListener(() => SetSettingsSection(SettingsSection.Visual));

        controlsSectionButton = CreateSettingsTabButton(
            settingsRoot,
            "Controls Section Tab",
            new Vector2(settingsLabelX + 480f, -28f),
            sectionFont,
            menuFontSize - 14,
            out controlsSectionButtonLabel);
        controlsSectionButton.onClick.AddListener(() => SetSettingsSection(SettingsSection.Controls));

        backButton = CreateTextButton(settingsRoot, "Back Button", new Vector2(settingsLabelX, -950f), out backLabel, sectionFont, menuFontSize - 10);
        backButton.onClick.AddListener(ShowMainMenu);
        backHover = AddHoverQuestionMark(backButton.gameObject, backLabel);

        generalSectionRoot = CreateRoot("General Section Root", settingsRoot);
        visualSectionRoot = CreateRoot("Visual Section Root", settingsRoot);
        controlsSectionRoot = CreateRoot("Controls Section Root", settingsRoot);

        float yGeneral = -90f;
        sectionTitleText = CreateHeader(generalSectionRoot, new Vector2(settingsLabelX, yGeneral), sectionFont, menuFontSize - 2);

        yGeneral -= 78f;
        accessibilityTitleText = CreateHeader(generalSectionRoot, new Vector2(settingsLabelX, yGeneral), sectionFont, menuFontSize - 12);
        yGeneral -= 66f;
        CreateCycleRow(generalSectionRoot, new Vector2(settingsLabelX, yGeneral), itemsFont, OnLanguagePressed, out languageRowLabel, out languageButton, out languageValueText);
        yGeneral -= 60f;
        CreateCycleRow(generalSectionRoot, new Vector2(settingsLabelX, yGeneral), itemsFont, OnColorBlindPressed, out colorBlindRowLabel, out colorBlindButton, out colorBlindValueText);

        yGeneral -= 84f;
        audioTitleText = CreateHeader(generalSectionRoot, new Vector2(settingsLabelX, yGeneral), sectionFont, menuFontSize - 12);
        yGeneral -= 66f;
        CreateSliderRow(generalSectionRoot, new Vector2(settingsLabelX, yGeneral), itemsFont, 0f, 10f, true, OnMasterVolumeChanged, out masterVolumeRowLabel, out masterVolumeSlider, out masterVolumeValueText);
        yGeneral -= 60f;
        CreateSliderRow(generalSectionRoot, new Vector2(settingsLabelX, yGeneral), itemsFont, 0f, 10f, true, OnEnvironmentVolumeChanged, out environmentVolumeRowLabel, out environmentVolumeSlider, out environmentVolumeValueText);
        yGeneral -= 60f;
        CreateSliderRow(generalSectionRoot, new Vector2(settingsLabelX, yGeneral), itemsFont, 0f, 10f, true, OnMenuVolumeChanged, out menuVolumeRowLabel, out menuVolumeSlider, out menuVolumeValueText);
        yGeneral -= 60f;
        CreateSliderRow(generalSectionRoot, new Vector2(settingsLabelX, yGeneral), itemsFont, 0f, 10f, true, OnEffectsVolumeChanged, out effectsVolumeRowLabel, out effectsVolumeSlider, out effectsVolumeValueText);

        float yVisual = -90f;
        visualSectionTitleText = CreateHeader(visualSectionRoot, new Vector2(settingsLabelX, yVisual), sectionFont, menuFontSize - 2);

        yVisual -= 70f;
        screenTitleText = CreateHeader(visualSectionRoot, new Vector2(settingsLabelX, yVisual), sectionFont, menuFontSize - 12);
        yVisual -= 66f;
        CreateCycleRow(visualSectionRoot, new Vector2(settingsLabelX, yVisual), itemsFont, OnResolutionPressed, out resolutionRowLabel, out resolutionButton, out resolutionValueText);
        yVisual -= 60f;
        CreateCycleRow(visualSectionRoot, new Vector2(settingsLabelX, yVisual), itemsFont, OnWindowModePressed, out windowModeRowLabel, out windowModeButton, out windowModeValueText);
        yVisual -= 60f;
        CreateCycleRow(visualSectionRoot, new Vector2(settingsLabelX, yVisual), itemsFont, OnFpsPressed, out fpsRowLabel, out fpsButton, out fpsValueText);
        yVisual -= 60f;
        CreateCycleRow(visualSectionRoot, new Vector2(settingsLabelX, yVisual), itemsFont, OnVsyncPressed, out vsyncRowLabel, out vsyncButton, out vsyncValueText);
        yVisual -= 60f;
        CreateCycleRow(visualSectionRoot, new Vector2(settingsLabelX, yVisual), itemsFont, OnHdrPressed, out hdrRowLabel, out hdrButton, out hdrValueText);
        yVisual -= 60f;
        CreateSliderRow(visualSectionRoot, new Vector2(settingsLabelX, yVisual), itemsFont, -2f, 2f, false, OnBrightnessChanged, out brightnessRowLabel, out brightnessSlider, out brightnessValueText);
        yVisual -= 60f;
        CreateSliderRow(visualSectionRoot, new Vector2(settingsLabelX, yVisual), itemsFont, -100f, 100f, false, OnContrastChanged, out contrastRowLabel, out contrastSlider, out contrastValueText);

        yVisual -= 84f;
        graphicsTitleText = CreateHeader(visualSectionRoot, new Vector2(settingsLabelX, yVisual), sectionFont, menuFontSize - 12);
        yVisual -= 66f;
        CreateCycleRow(visualSectionRoot, new Vector2(settingsLabelX, yVisual), itemsFont, OnGraphicsQualityPressed, out qualityRowLabel, out qualityButton, out qualityValueText);
        yVisual -= 60f;
        CreateCycleRow(visualSectionRoot, new Vector2(settingsLabelX, yVisual), itemsFont, OnDynamicShadowsPressed, out dynamicShadowsRowLabel, out dynamicShadowsButton, out dynamicShadowsValueText);
        yVisual -= 60f;
        CreateCycleRow(visualSectionRoot, new Vector2(settingsLabelX, yVisual), itemsFont, OnCharacterLightingPressed, out characterLightingRowLabel, out characterLightingButton, out characterLightingValueText);

        float yControls = -90f;
        controlsSectionTitleText = CreateHeader(controlsSectionRoot, new Vector2(settingsLabelX, yControls), sectionFont, menuFontSize - 2);

        yControls -= 70f;
        keyRebindTitleText = CreateHeader(controlsSectionRoot, new Vector2(settingsLabelX, yControls), sectionFont, menuFontSize - 12);
        yControls -= 58f;
        CreateKeybindRow(controlsSectionRoot, new Vector2(settingsLabelX, yControls), itemsFont, BindingAction.Forward);
        yControls -= 52f;
        CreateKeybindRow(controlsSectionRoot, new Vector2(settingsLabelX, yControls), itemsFont, BindingAction.Backward);
        yControls -= 52f;
        CreateKeybindRow(controlsSectionRoot, new Vector2(settingsLabelX, yControls), itemsFont, BindingAction.Left);
        yControls -= 52f;
        CreateKeybindRow(controlsSectionRoot, new Vector2(settingsLabelX, yControls), itemsFont, BindingAction.Right);
        yControls -= 52f;
        CreateKeybindRow(controlsSectionRoot, new Vector2(settingsLabelX, yControls), itemsFont, BindingAction.Run);
        yControls -= 52f;
        CreateKeybindRow(controlsSectionRoot, new Vector2(settingsLabelX, yControls), itemsFont, BindingAction.Jump);
        yControls -= 52f;
        CreateKeybindRow(controlsSectionRoot, new Vector2(settingsLabelX, yControls), itemsFont, BindingAction.Action);

        SetSettingsSection(SettingsSection.General);
    }

    private Button CreateSettingsTabButton(Transform parent, string objectName, Vector2 anchoredPosition, Font font, int size, out Text label)
    {
        GameObject buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(Button), typeof(Image));
        buttonGo.transform.SetParent(parent, false);

        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(220f, 44f);
        rect.anchoredPosition = anchoredPosition;

        Image bg = buttonGo.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.08f);

        Button button = buttonGo.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(buttonGo.transform, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(12f, 0f);
        tr.offsetMax = Vector2.zero;

        label = textGo.GetComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = Color.white;
        label.raycastTarget = false;

        return button;
    }

    private void SetSettingsSection(SettingsSection section)
    {
        if (section != SettingsSection.Controls)
        {
            CancelRebind();
        }

        currentSection = section;
        SetActiveSafe(generalSectionRoot, section == SettingsSection.General);
        SetActiveSafe(visualSectionRoot, section == SettingsSection.Visual);
        SetActiveSafe(controlsSectionRoot, section == SettingsSection.Controls);
        UpdateSectionTabVisuals();
    }

    private void UpdateSectionTabVisuals()
    {
        if (generalSectionButtonLabel != null)
        {
            generalSectionButtonLabel.color = currentSection == SettingsSection.General ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
        }

        if (visualSectionButtonLabel != null)
        {
            visualSectionButtonLabel.color = currentSection == SettingsSection.Visual ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
        }

        if (controlsSectionButtonLabel != null)
        {
            controlsSectionButtonLabel.color = currentSection == SettingsSection.Controls ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
        }
    }


    private Button CreateTextButton(Transform parent, string objectName, Vector2 anchoredPosition, out Text label, Font font, int size)
    {
        GameObject buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(Button), typeof(Image));
        buttonGo.transform.SetParent(parent, false);

        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(640f, 56f);
        rect.anchoredPosition = anchoredPosition;

        Image bg = buttonGo.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0f);

        Button button = buttonGo.GetComponent<Button>();
        button.transition = Selectable.Transition.None;

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(buttonGo.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(24f, 0f);
        textRect.offsetMax = Vector2.zero;

        label = textGo.GetComponent<Text>();
        label.font = font;
        label.fontSize = size;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = Color.white;
        label.raycastTarget = false;

        return button;
    }

    private Text CreateHeader(Transform parent, Vector2 anchoredPosition, Font font, int size)
    {
        GameObject textGo = new GameObject("Header", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(parent, false);

        RectTransform rect = textGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(800f, 48f);
        rect.anchoredPosition = anchoredPosition;

        Text text = textGo.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.alignment = TextAnchor.MiddleLeft;
        text.color = new Color(1f, 1f, 1f, 0.98f);
        text.raycastTarget = false;

        return text;
    }

    private void CreateCycleRow(
        Transform parent,
        Vector2 anchoredPosition,
        Font itemsFont,
        UnityEngine.Events.UnityAction onPressed,
        out Text label,
        out Button button,
        out Text value)
    {
        label = CreateRowLabel(parent, anchoredPosition, itemsFont);

        GameObject valueButtonGo = new GameObject("Value Button", typeof(RectTransform), typeof(Button), typeof(Image));
        valueButtonGo.transform.SetParent(parent, false);

        RectTransform rect = valueButtonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(430f, 64f);
        rect.anchoredPosition = new Vector2(settingsValueX, anchoredPosition.y + 3f);

        Image bg = valueButtonGo.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.08f);

        button = valueButtonGo.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(onPressed);

        GameObject valueTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        valueTextGo.transform.SetParent(valueButtonGo.transform, false);
        RectTransform valueRect = valueTextGo.GetComponent<RectTransform>();
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(12f, 0f);
        valueRect.offsetMax = Vector2.zero;

        value = valueTextGo.GetComponent<Text>();
        value.font = itemsFont;
        value.fontSize = settingsItemFontSize;
        value.alignment = TextAnchor.MiddleLeft;
        value.color = Color.white;
        value.raycastTarget = false;
    }


    private void CreateKeybindRow(Transform parent, Vector2 anchoredPosition, Font itemsFont, BindingAction action)
    {
        Text label = CreateRowLabel(parent, anchoredPosition, itemsFont);

        GameObject valueButtonGo = new GameObject("Keybind Button", typeof(RectTransform), typeof(Button), typeof(Image));
        valueButtonGo.transform.SetParent(parent, false);

        RectTransform rect = valueButtonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(430f, 42f);
        rect.anchoredPosition = new Vector2(settingsValueX, anchoredPosition.y + 3f);

        Image bg = valueButtonGo.GetComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.08f);

        Button button = valueButtonGo.GetComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => BeginRebind(action));

        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(valueButtonGo.transform, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.anchorMin = new Vector2(0f, 0.5f);
        iconRect.anchorMax = new Vector2(0f, 0.5f);
        iconRect.pivot = new Vector2(0f, 0.5f);
        iconRect.sizeDelta = new Vector2(60f, 60f);
        iconRect.anchoredPosition = new Vector2(10f, 0f);

        Image iconImage = iconGo.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;

        GameObject valueTextGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        valueTextGo.transform.SetParent(valueButtonGo.transform, false);
        RectTransform valueRect = valueTextGo.GetComponent<RectTransform>();
        valueRect.anchorMin = Vector2.zero;
        valueRect.anchorMax = Vector2.one;
        valueRect.offsetMin = new Vector2(86f, 0f);
        valueRect.offsetMax = Vector2.zero;

        Text value = valueTextGo.GetComponent<Text>();
        value.font = itemsFont;
        value.fontSize = settingsItemFontSize;
        value.alignment = TextAnchor.MiddleLeft;
        value.color = Color.white;
        value.raycastTarget = false;

        keybindRows[action] = new KeybindRowUi
        {
            Action = action,
            Label = label,
            Button = button,
            Icon = iconImage,
            Value = value
        };
    }

    private void CreateSliderRow(
        Transform parent,
        Vector2 anchoredPosition,
        Font itemsFont,
        float min,
        float max,
        bool wholeNumbers,
        UnityEngine.Events.UnityAction<float> onChanged,
        out Text label,
        out Slider slider,
        out Text value)
    {
        label = CreateRowLabel(parent, anchoredPosition, itemsFont);

        GameObject sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
        sliderGo.transform.SetParent(parent, false);

        RectTransform rect = sliderGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(280f, 20f);
        rect.anchoredPosition = new Vector2(settingsValueX, anchoredPosition.y - 2f);

        GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(sliderGo.transform, false);
        RectTransform bgRect = bgGo.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bgImg = bgGo.GetComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.22f);

        GameObject fillAreaGo = new GameObject("Fill Area", typeof(RectTransform));
        fillAreaGo.transform.SetParent(sliderGo.transform, false);
        RectTransform fillAreaRect = fillAreaGo.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = Vector2.zero;
        fillAreaRect.offsetMax = Vector2.zero;

        GameObject fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(fillAreaGo.transform, false);
        RectTransform fillRect = fillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fillImg = fillGo.GetComponent<Image>();
        fillImg.color = Color.white;

        slider = sliderGo.GetComponent<Slider>();
        slider.targetGraphic = bgImg;
        slider.fillRect = fillRect;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.onValueChanged.AddListener(onChanged);

        GameObject valueTextGo = new GameObject("Value", typeof(RectTransform), typeof(Text));
        valueTextGo.transform.SetParent(parent, false);
        RectTransform valueRect = valueTextGo.GetComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0f, 1f);
        valueRect.anchorMax = new Vector2(0f, 1f);
        valueRect.pivot = new Vector2(0f, 1f);
        valueRect.sizeDelta = new Vector2(120f, 42f);
        valueRect.anchoredPosition = new Vector2(settingsValueX + 300f, anchoredPosition.y + 2f);

        value = valueTextGo.GetComponent<Text>();
        value.font = itemsFont;
        value.fontSize = settingsItemFontSize;
        value.alignment = TextAnchor.MiddleLeft;
        value.color = Color.white;
        value.raycastTarget = false;
    }

    private Text CreateRowLabel(Transform parent, Vector2 anchoredPosition, Font itemsFont)
    {
        GameObject labelGo = new GameObject("Row Label", typeof(RectTransform), typeof(Text));
        labelGo.transform.SetParent(parent, false);

        RectTransform rect = labelGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(620f, 44f);
        rect.anchoredPosition = anchoredPosition;

        Text label = labelGo.GetComponent<Text>();
        label.font = itemsFont;
        label.fontSize = settingsItemFontSize;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        label.raycastTarget = false;

        return label;
    }

    private static RectTransform CreateRoot(string objectName, Transform parent)
    {
        GameObject rootGo = new GameObject(objectName, typeof(RectTransform));
        rootGo.transform.SetParent(parent, false);

        RectTransform rect = rootGo.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private Font ResolveSettingsItemsFont(Font fallback)
    {
        if (settingsItemsFont != null)
        {
            return settingsItemsFont;
        }

#if UNITY_EDITOR
        settingsItemsFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Press_Start_2P/PressStart2P-Regular.ttf");
#endif

        return settingsItemsFont != null ? settingsItemsFont : fallback;
    }

    private void AutoFindAudioLinks()
    {
        if (windNoise == null)
        {
            windNoise = UnityEngine.Object.FindFirstObjectByType<WindNoiseGenerator>();
        }

        if (grassSteps == null)
        {
            grassSteps = UnityEngine.Object.FindFirstObjectByType<GrassFootstepNoise>();
        }
        if (surfaceMovementAudio == null)
        {
            surfaceMovementAudio = UnityEngine.Object.FindFirstObjectByType<SurfaceMovementAudio>();
        }

        if (menuAudioSource == null)
        {
            menuAudioSource = GetComponent<AudioSource>();
        }

        if (dragScrapeAudios == null || dragScrapeAudios.Length == 0)
        {
            dragScrapeAudios = UnityEngine.Object.FindObjectsByType<СкрежетТяги>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        // Capture baseline levels for 100% mapping.
        environmentBaseVolume = windNoise != null ? Mathf.Max(0.0001f, windNoise.GetMasterVolume()) : 1f;
        if (surfaceMovementAudio != null)
        {
            effectsBaseVolume = Mathf.Max(0.0001f, surfaceMovementAudio.GetMasterVolume());
        }
        else
        {
            effectsBaseVolume = grassSteps != null ? Mathf.Max(0.0001f, grassSteps.GetMasterVolume()) : 1f;
        }
        menuBaseVolume = menuAudioSource != null ? Mathf.Max(0.0001f, menuAudioSource.volume) : 1f;
    }

    private void AutoAssignHdrpAssets()
    {
#if UNITY_EDITOR
        if (lowHdrpAsset == null)
        {
            lowHdrpAsset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>("Assets/Settings/Low.asset");
        }
        if (mediumHdrpAsset == null)
        {
            mediumHdrpAsset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>("Assets/Settings/Medium.asset");
        }
        if (highHdrpAsset == null)
        {
            highHdrpAsset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>("Assets/Settings/High.asset");
        }
#endif
    }

    private void EnsureVisualSettingsController()
    {
        GameObject volumeGo = new GameObject("PauseMenu Runtime Volume", typeof(Volume));
        volumeGo.transform.SetParent(transform, false);

        runtimeSettingsVolume = volumeGo.GetComponent<Volume>();
        runtimeSettingsVolume.isGlobal = true;
        runtimeSettingsVolume.priority = 500f;

        runtimeSettingsProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        runtimeSettingsVolume.profile = runtimeSettingsProfile;

        colorAdjustments = runtimeSettingsProfile.Add<ColorAdjustments>(true);
        colorAdjustments.postExposure.overrideState = true;
        colorAdjustments.contrast.overrideState = true;
        colorAdjustments.colorFilter.overrideState = true;
    }

    private void BuildResolutionList()
    {
        availableResolutions.Clear();

        Resolution[] raw = Screen.resolutions;
        for (int i = 0; i < raw.Length; i++)
        {
            Vector2Int candidate = new Vector2Int(raw[i].width, raw[i].height);
            if (!availableResolutions.Contains(candidate))
            {
                availableResolutions.Add(candidate);
            }
        }

        if (availableResolutions.Count == 0)
        {
            availableResolutions.Add(new Vector2Int(Screen.width, Screen.height));
        }

        resolutionIndex = 0;
        for (int i = 0; i < availableResolutions.Count; i++)
        {
            if (availableResolutions[i].x == Screen.width && availableResolutions[i].y == Screen.height)
            {
                resolutionIndex = i;
                break;
            }
        }
    }

    private void LoadSettings()
    {
        string languageKey = PrefPrefix + "language";
        if (PlayerPrefs.HasKey(languageKey))
        {
            languageIndex = Mathf.Clamp(PlayerPrefs.GetInt(languageKey, 1), 0, 1);
        }
        else
        {
            languageIndex = Application.systemLanguage == SystemLanguage.Russian ? 1 : 0;
            PlayerPrefs.SetInt(languageKey, languageIndex);
        }
        colorBlindIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "color_blind", 0), 0, 3);

        masterVolumeLevel = Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "volume_master", 10), 0, 10);
        environmentVolumeLevel = Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "volume_environment", 10), 0, 10);
        menuVolumeLevel = Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "volume_menu", 10), 0, 10);
        effectsVolumeLevel = Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "volume_effects", 10), 0, 10);

        int savedW = PlayerPrefs.GetInt(PrefPrefix + "resolution_w", Screen.width);
        int savedH = PlayerPrefs.GetInt(PrefPrefix + "resolution_h", Screen.height);
        for (int i = 0; i < availableResolutions.Count; i++)
        {
            if (availableResolutions[i].x == savedW && availableResolutions[i].y == savedH)
            {
                resolutionIndex = i;
                break;
            }
        }

        windowModeIndex = FindWindowModeIndex((FullScreenMode)PlayerPrefs.GetInt(PrefPrefix + "window_mode", (int)Screen.fullScreenMode));
        fpsIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "fps_index", fpsIndex), 0, fpsOptions.Length - 1);
        vSyncIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "vsync", QualitySettings.vSyncCount > 0 ? 1 : 0), 0, 1);
        hdrIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "hdr", 1), 0, 1);
        dynamicShadowsIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "dynamic_shadows", 1), 0, 1);
        graphicsQualityIndex = Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "graphics_quality", 1), 0, 2);
        characterLightingMode = Mathf.Clamp(PlayerPrefs.GetInt(PrefPrefix + "character_lighting", 0), 0, 1);

        brightnessExposure = Mathf.Clamp(PlayerPrefs.GetFloat(PrefPrefix + "brightness_ev", 0f), -2f, 2f);
        contrastAmount = Mathf.Clamp(PlayerPrefs.GetFloat(PrefPrefix + "contrast", 0f), -100f, 100f);
    }

    private void ApplyAllSettings()
    {
        ApplyMasterVolume();
        ApplyEnvironmentVolume();
        ApplyMenuVolume();
        ApplyEffectsVolume();

        ApplyGraphicsQuality();
        ApplyCharacterLighting();
        ApplyWindowMode();
        ApplyResolution();
        ApplyFpsLimit();
        ApplyVsync();
        ApplyHdr();
        ApplyDynamicShadows();
        ApplyVisualSettings();
    }

    private void ApplySettingsToUi()
    {
        SetSliderSafe(masterVolumeSlider, masterVolumeLevel);
        SetSliderSafe(environmentVolumeSlider, environmentVolumeLevel);
        SetSliderSafe(menuVolumeSlider, menuVolumeLevel);
        SetSliderSafe(effectsVolumeSlider, effectsVolumeLevel);

        SetTextSafe(masterVolumeValueText, masterVolumeLevel.ToString());
        SetTextSafe(environmentVolumeValueText, environmentVolumeLevel.ToString());
        SetTextSafe(menuVolumeValueText, menuVolumeLevel.ToString());
        SetTextSafe(effectsVolumeValueText, effectsVolumeLevel.ToString());

        SetSliderSafe(brightnessSlider, brightnessExposure);
        SetSliderSafe(contrastSlider, contrastAmount);
        SetTextSafe(brightnessValueText, brightnessExposure.ToString("0.00"));
        SetTextSafe(contrastValueText, contrastAmount.ToString("0"));

        SetTextSafe(languageValueText, languageIndex == 0 ? "english" : "русский");
        SetTextSafe(colorBlindValueText, GetColorBlindLabel());
        SetTextSafe(resolutionValueText, ResolutionLabel());
        SetTextSafe(windowModeValueText, GetWindowModeLabel());
        SetTextSafe(fpsValueText, fpsOptions[fpsIndex] < 0 ? (IsEnglish ? "unlimited" : "безлимит") : fpsOptions[fpsIndex].ToString());
        SetTextSafe(vsyncValueText, vSyncIndex == 1 ? (IsEnglish ? "on" : "вкл") : (IsEnglish ? "off" : "выкл"));
        SetTextSafe(hdrValueText, hdrIndex == 1 ? (IsEnglish ? "on" : "вкл") : (IsEnglish ? "off" : "выкл"));
        SetTextSafe(dynamicShadowsValueText, dynamicShadowsIndex == 1 ? (IsEnglish ? "on" : "вкл") : (IsEnglish ? "off" : "выкл"));
        SetTextSafe(qualityValueText, GetGraphicsQualityLabel());
        SetTextSafe(characterLightingValueText, GetCharacterLightingLabel());
        RefreshKeybindRows();
    }

    private void ApplyLocalization()
    {
        if (IsEnglish)
        {
            SetButtonTextAndHover(resumeLabel, resumeHover, "resume");
            SetButtonTextAndHover(settingsLabel, settingsHover, "settings");
            SetButtonTextAndHover(quitLabel, quitHover, "quit");
            SetButtonTextAndHover(backLabel, backHover, "back");
            SetTextSafe(generalSectionButtonLabel, "general");
            SetTextSafe(visualSectionButtonLabel, "visual");
            SetTextSafe(controlsSectionButtonLabel, "controls");

            SetTextSafe(sectionTitleText, "general");
            SetTextSafe(accessibilityTitleText, "accessibility");
            SetTextSafe(audioTitleText, "audio");
            SetTextSafe(visualSectionTitleText, "visual");
            SetTextSafe(screenTitleText, "screen");
            SetTextSafe(graphicsTitleText, "graphics");
            SetTextSafe(controlsSectionTitleText, "controls");
            SetTextSafe(keyRebindTitleText, "key rebinding");

            SetTextSafe(languageRowLabel, "language");
            SetTextSafe(colorBlindRowLabel, "color blindness");
            SetTextSafe(masterVolumeRowLabel, "master volume");
            SetTextSafe(environmentVolumeRowLabel, "environment volume");
            SetTextSafe(menuVolumeRowLabel, "menu volume");
            SetTextSafe(effectsVolumeRowLabel, "effects volume");
            SetTextSafe(resolutionRowLabel, "resolution");
            SetTextSafe(windowModeRowLabel, "window mode");
            SetTextSafe(fpsRowLabel, "fps limit");
            SetTextSafe(vsyncRowLabel, "vsync");
            SetTextSafe(hdrRowLabel, "hdr");
            SetTextSafe(brightnessRowLabel, "brightness");
            SetTextSafe(contrastRowLabel, "contrast");
            SetTextSafe(qualityRowLabel, "quality");
            SetTextSafe(dynamicShadowsRowLabel, "dynamic shadows");
            SetTextSafe(characterLightingRowLabel, "character lighting");
        }
        else
        {
            SetButtonTextAndHover(resumeLabel, resumeHover, "вернуться");
            SetButtonTextAndHover(settingsLabel, settingsHover, "настройки");
            SetButtonTextAndHover(quitLabel, quitHover, "уйти");
            SetButtonTextAndHover(backLabel, backHover, "назад");
            SetTextSafe(generalSectionButtonLabel, "основное");
            SetTextSafe(visualSectionButtonLabel, "визуал");
            SetTextSafe(controlsSectionButtonLabel, "управление");

            SetTextSafe(sectionTitleText, "основное");
            SetTextSafe(accessibilityTitleText, "доступность");
            SetTextSafe(audioTitleText, "аудио");
            SetTextSafe(visualSectionTitleText, "визуал");
            SetTextSafe(screenTitleText, "экран");
            SetTextSafe(graphicsTitleText, "графика");
            SetTextSafe(controlsSectionTitleText, "управление");
            SetTextSafe(keyRebindTitleText, "переназначение клавиш");

            SetTextSafe(languageRowLabel, "язык");
            SetTextSafe(colorBlindRowLabel, "цветовая слепота");
            SetTextSafe(masterVolumeRowLabel, "громкость общая");
            SetTextSafe(environmentVolumeRowLabel, "громкость окружения");
            SetTextSafe(menuVolumeRowLabel, "громкость меню");
            SetTextSafe(effectsVolumeRowLabel, "громкость эффектов");
            SetTextSafe(resolutionRowLabel, "разрешение");
            SetTextSafe(windowModeRowLabel, "режим окна");
            SetTextSafe(fpsRowLabel, "ограничение фпс");
            SetTextSafe(vsyncRowLabel, "vsync");
            SetTextSafe(hdrRowLabel, "hdr");
            SetTextSafe(brightnessRowLabel, "яркость");
            SetTextSafe(contrastRowLabel, "контраст");
            SetTextSafe(qualityRowLabel, "качество");
            SetTextSafe(dynamicShadowsRowLabel, "динамические тени");
            SetTextSafe(characterLightingRowLabel, "освещение персонажа");
        }

        SetTextSafe(languageValueText, languageIndex == 0 ? "english" : "русский");
        SetTextSafe(colorBlindValueText, GetColorBlindLabel());
        SetTextSafe(windowModeValueText, GetWindowModeLabel());
        SetTextSafe(fpsValueText, fpsOptions[fpsIndex] < 0 ? (IsEnglish ? "unlimited" : "безлимит") : fpsOptions[fpsIndex].ToString());
        SetTextSafe(vsyncValueText, vSyncIndex == 1 ? (IsEnglish ? "on" : "вкл") : (IsEnglish ? "off" : "выкл"));
        SetTextSafe(hdrValueText, hdrIndex == 1 ? (IsEnglish ? "on" : "вкл") : (IsEnglish ? "off" : "выкл"));
        SetTextSafe(dynamicShadowsValueText, dynamicShadowsIndex == 1 ? (IsEnglish ? "on" : "вкл") : (IsEnglish ? "off" : "выкл"));
        SetTextSafe(qualityValueText, GetGraphicsQualityLabel());
        SetTextSafe(characterLightingValueText, GetCharacterLightingLabel());
        RefreshKeybindRows();
        UpdateSectionTabVisuals();
    }

    private void BeginRebind(BindingAction action)
    {
        if (!keybindRows.ContainsKey(action))
        {
            return;
        }

        pendingRebindAction = action;
        RefreshKeybindRows();
    }

    private void CancelRebind()
    {
        if (pendingRebindAction == BindingAction.None)
        {
            return;
        }

        pendingRebindAction = BindingAction.None;
        RefreshKeybindRows();
    }

    private void CaptureRebindKey()
    {
        if (!isOpen)
        {
            CancelRebind();
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            CancelRebind();
            return;
        }

        for (int i = 0; i < AllKeyCodes.Length; i++)
        {
            KeyCode candidate = AllKeyCodes[i];
            if (!IsAllowedRebindKey(candidate))
            {
                continue;
            }

            if (!Input.GetKeyDown(candidate))
            {
                continue;
            }

            ApplyRebind(candidate);
            return;
        }
    }

    private static bool IsAllowedRebindKey(KeyCode key)
    {
        if (key == KeyCode.None || key == KeyCode.Escape)
        {
            return false;
        }

        string name = key.ToString();
        return !name.StartsWith("Mouse", StringComparison.Ordinal) &&
               !name.StartsWith("Joystick", StringComparison.Ordinal);
    }

    private void ApplyRebind(KeyCode newKey)
    {
        if (pendingRebindAction == BindingAction.None)
        {
            return;
        }

        BindingAction targetAction = pendingRebindAction;
        pendingRebindAction = BindingAction.None;

        KeyCode previousKey = GameInputBindings.GetKey(targetAction);
        BindingAction existingOwner = FindActionByKey(newKey, targetAction);
        GameInputBindings.SetKey(targetAction, newKey);

        if (existingOwner != BindingAction.None)
        {
            GameInputBindings.SetKey(existingOwner, previousKey);
        }

        RefreshKeybindRows();
    }

    private static BindingAction FindActionByKey(KeyCode key, BindingAction ignoreAction)
    {
        for (int i = 0; i < RebindableActions.Length; i++)
        {
            BindingAction action = RebindableActions[i];
            if (action == ignoreAction)
            {
                continue;
            }

            if (GameInputBindings.GetKey(action) == key)
            {
                return action;
            }
        }

        return BindingAction.None;
    }

    private void RefreshKeybindRows()
    {
        for (int i = 0; i < RebindableActions.Length; i++)
        {
            BindingAction action = RebindableActions[i];
            if (!keybindRows.TryGetValue(action, out KeybindRowUi row))
            {
                continue;
            }

            SetTextSafe(row.Label, GetBindingActionLabel(action));
            bool isPending = pendingRebindAction == action;
            SetKeybindButtonHighlight(row.Button, isPending);

            if (isPending)
            {
                SetTextSafe(row.Value, IsEnglish ? "press key..." : "нажми клавишу...");
                if (row.Icon != null)
                {
                    row.Icon.enabled = false;
                    row.Icon.sprite = null;
                }

                continue;
            }

            KeyCode key = GameInputBindings.GetKey(action);
            Sprite icon = GetKeyIcon(key);
            if (row.Icon != null)
            {
                row.Icon.enabled = icon != null;
                row.Icon.sprite = icon;
            }

            SetTextSafe(row.Value, icon != null ? string.Empty : GetReadableKeyName(key));
        }
    }

    private string GetBindingActionLabel(BindingAction action)
    {
        if (IsEnglish)
        {
            switch (action)
            {
                case BindingAction.Forward: return "move forward";
                case BindingAction.Backward: return "move backward";
                case BindingAction.Left: return "move left";
                case BindingAction.Right: return "move right";
                case BindingAction.Run: return "run";
                case BindingAction.Jump: return "jump";
                case BindingAction.Action: return "action";
                default: return action.ToString().ToLowerInvariant();
            }
        }

        switch (action)
        {
            case BindingAction.Forward: return "ходьба вперед";
            case BindingAction.Backward: return "ходьба назад";
            case BindingAction.Left: return "ходьба влево";
            case BindingAction.Right: return "ходьба вправо";
            case BindingAction.Run: return "бег";
            case BindingAction.Jump: return "прыжок";
            case BindingAction.Action: return "действие";
            default: return action.ToString().ToLowerInvariant();
        }
    }

    private static string GetReadableKeyName(KeyCode key)
    {
        if (key >= KeyCode.A && key <= KeyCode.Z)
        {
            int offset = (int)key - (int)KeyCode.A;
            return ((char)('a' + offset)).ToString();
        }

        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
        {
            return ((int)key - (int)KeyCode.Alpha0).ToString();
        }

        switch (key)
        {
            case KeyCode.Space: return "space";
            case KeyCode.LeftShift: return "left shift";
            case KeyCode.RightShift: return "right shift";
            case KeyCode.LeftControl: return "left ctrl";
            case KeyCode.RightControl: return "right ctrl";
            case KeyCode.LeftAlt: return "left alt";
            case KeyCode.RightAlt: return "right alt";
            case KeyCode.UpArrow: return "up";
            case KeyCode.DownArrow: return "down";
            case KeyCode.LeftArrow: return "left";
            case KeyCode.RightArrow: return "right";
            case KeyCode.Return: return "enter";
            case KeyCode.KeypadEnter: return "enter";
            default:
            {
                string value = key.ToString();
                if (value.StartsWith("Alpha", StringComparison.Ordinal))
                {
                    return value.Substring(5);
                }

                return value.ToLowerInvariant();
            }
        }
    }

    private Sprite GetKeyIcon(KeyCode key)
    {
        KeyCode normalized = NormalizeKeyForIcon(key);
        if (keyIconCache.TryGetValue(normalized, out Sprite cached))
        {
            return cached;
        }

        Sprite icon = null;
#if UNITY_EDITOR
        string slug = KeyToIconSlug(normalized);
        if (!string.IsNullOrEmpty(slug))
        {
            icon = AssetDatabase.LoadAssetAtPath<Sprite>(KeyIconDirectory + "key-" + slug + ".png");
        }
#endif
        keyIconCache[normalized] = icon;
        return icon;
    }

    private static KeyCode NormalizeKeyForIcon(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.RightShift: return KeyCode.LeftShift;
            case KeyCode.RightControl: return KeyCode.LeftControl;
            case KeyCode.RightAlt: return KeyCode.LeftAlt;
            case KeyCode.KeypadEnter: return KeyCode.Return;
            default: return key;
        }
    }

    private static string KeyToIconSlug(KeyCode key)
    {
        if (key >= KeyCode.A && key <= KeyCode.Z)
        {
            int offset = (int)key - (int)KeyCode.A;
            return ((char)('a' + offset)).ToString();
        }

        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
        {
            return ((int)key - (int)KeyCode.Alpha0).ToString();
        }

        if (key >= KeyCode.F1 && key <= KeyCode.F12)
        {
            return "f" + ((int)key - (int)KeyCode.F1 + 1);
        }

        switch (key)
        {
            case KeyCode.Space: return "space";
            case KeyCode.LeftShift: return "shift";
            case KeyCode.LeftControl: return "ctrl";
            case KeyCode.LeftAlt: return "alt";
            case KeyCode.Escape: return "esc";
            case KeyCode.Tab: return "tab";
            case KeyCode.Backspace: return "backspace";
            case KeyCode.Return: return "enter";
            case KeyCode.UpArrow: return "arrow-up";
            case KeyCode.DownArrow: return "arrow-down";
            case KeyCode.LeftArrow: return "arrow-left";
            case KeyCode.RightArrow: return "arrow-right";
            case KeyCode.Minus: return "hyphen";
            case KeyCode.Equals: return "equals";
            case KeyCode.Comma: return "comma";
            case KeyCode.Period: return "dot";
            case KeyCode.Slash: return "forward-slash";
            case KeyCode.Backslash: return "backward-slash";
            case KeyCode.LeftBracket: return "bracket-open";
            case KeyCode.RightBracket: return "bracket-close";
            case KeyCode.Semicolon: return "semi-colon";
            case KeyCode.Quote: return "quote";
            case KeyCode.BackQuote: return "tilde";
            case KeyCode.Home: return "home";
            case KeyCode.End: return "end";
            case KeyCode.PageUp: return "pgup";
            case KeyCode.PageDown: return "pgdn";
            case KeyCode.Insert: return "ins";
            case KeyCode.Delete: return "del";
            default: return null;
        }
    }

    private static void SetKeybindButtonHighlight(Button button, bool active)
    {
        if (button == null)
        {
            return;
        }

        Image bg = button.GetComponent<Image>();
        if (bg == null)
        {
            return;
        }

        bg.color = active
            ? new Color(1f, 1f, 1f, 0.22f)
            : new Color(1f, 1f, 1f, 0.08f);
    }

    private void SetButtonTextAndHover(Text label, HoverQuestionSuffix hover, string text)
    {
        if (label != null)
        {
            label.text = text;
        }

        if (hover != null)
        {
            hover.SetBaseText(text);
        }
    }

    private string GetColorBlindLabel()
    {
        if (IsEnglish)
        {
            switch (colorBlindIndex)
            {
                case 0: return "off";
                case 1: return "protanopia";
                case 2: return "deuteranopia";
                default: return "tritanopia";
            }
        }

        switch (colorBlindIndex)
        {
            case 0: return "выкл";
            case 1: return "protanopia";
            case 2: return "deuteranopia";
            default: return "tritanopia";
        }
    }

    private string GetGraphicsQualityLabel()
    {
        if (IsEnglish)
        {
            switch (graphicsQualityIndex)
            {
                case 0: return "low";
                case 1: return "medium";
                default: return "high";
            }
        }

        switch (graphicsQualityIndex)
        {
            case 0: return "низкое(low)";
            case 1: return "среднее(medium)";
            default: return "высокое(high)";
        }
    }

    private void OnLanguagePressed()
    {
        languageIndex = languageIndex == 0 ? 1 : 0;
        PlayerPrefs.SetInt(PrefPrefix + "language", languageIndex);
        ApplyLocalization();
    }

    private void OnColorBlindPressed()
    {
        List<string> options = new List<string>(4)
        {
            IsEnglish ? "off" : "выкл",
            "protanopia",
            "deuteranopia",
            "tritanopia"
        };

        OpenDropdownPopup(colorBlindButton, options, colorBlindIndex, index =>
        {
            colorBlindIndex = Mathf.Clamp(index, 0, 3);
            PlayerPrefs.SetInt(PrefPrefix + "color_blind", colorBlindIndex);
            ApplyVisualSettings();
            SetTextSafe(colorBlindValueText, GetColorBlindLabel());
        });
    }

    private void OnGraphicsQualityPressed()
    {
        List<string> options = new List<string>(3)
        {
            IsEnglish ? "low" : "низкое(low)",
            IsEnglish ? "medium" : "среднее(medium)",
            IsEnglish ? "high" : "высокое(high)"
        };

        OpenDropdownPopup(qualityButton, options, graphicsQualityIndex, index =>
        {
            graphicsQualityIndex = Mathf.Clamp(index, 0, 2);
            PlayerPrefs.SetInt(PrefPrefix + "graphics_quality", graphicsQualityIndex);
            ApplyGraphicsQuality();
            SetTextSafe(qualityValueText, GetGraphicsQualityLabel());
        });
    }

    private void OnCharacterLightingPressed()
    {
        characterLightingMode = characterLightingMode == 0 ? 1 : 0;
        PlayerPrefs.SetInt(PrefPrefix + "character_lighting", characterLightingMode);
        ApplyCharacterLighting();
        SetTextSafe(characterLightingValueText, GetCharacterLightingLabel());
    }

    private void OnMasterVolumeChanged(float value)
    {
        masterVolumeLevel = Mathf.Clamp(Mathf.RoundToInt(value), 0, 10);
        PlayerPrefs.SetInt(PrefPrefix + "volume_master", masterVolumeLevel);
        ApplyMasterVolume();
        SetTextSafe(masterVolumeValueText, masterVolumeLevel.ToString());
    }

    private void OnEnvironmentVolumeChanged(float value)
    {
        environmentVolumeLevel = Mathf.Clamp(Mathf.RoundToInt(value), 0, 10);
        PlayerPrefs.SetInt(PrefPrefix + "volume_environment", environmentVolumeLevel);
        ApplyEnvironmentVolume();
        SetTextSafe(environmentVolumeValueText, environmentVolumeLevel.ToString());
    }

    private void OnMenuVolumeChanged(float value)
    {
        menuVolumeLevel = Mathf.Clamp(Mathf.RoundToInt(value), 0, 10);
        PlayerPrefs.SetInt(PrefPrefix + "volume_menu", menuVolumeLevel);
        ApplyMenuVolume();
        SetTextSafe(menuVolumeValueText, menuVolumeLevel.ToString());
    }

    private void OnEffectsVolumeChanged(float value)
    {
        effectsVolumeLevel = Mathf.Clamp(Mathf.RoundToInt(value), 0, 10);
        PlayerPrefs.SetInt(PrefPrefix + "volume_effects", effectsVolumeLevel);
        ApplyEffectsVolume();
        SetTextSafe(effectsVolumeValueText, effectsVolumeLevel.ToString());
    }

    private void ApplyMasterVolume()
    {
        AudioListener.volume = masterVolumeLevel / 10f;
    }

    private void ApplyEnvironmentVolume()
    {
        if (windNoise != null)
        {
            float value = environmentBaseVolume * (environmentVolumeLevel / 10f);
            windNoise.SetMasterVolume(value);
        }
    }

    private void ApplyMenuVolume()
    {
        if (menuAudioSource != null)
        {
            menuAudioSource.volume = Mathf.Clamp01(menuBaseVolume * (menuVolumeLevel / 10f));
        }
    }

    private void ApplyEffectsVolume()
    {
        float value = effectsBaseVolume * (effectsVolumeLevel / 10f);

        if (surfaceMovementAudio != null)
        {
            surfaceMovementAudio.SetMasterVolume(value);
        }

        if (grassSteps != null)
        {
            grassSteps.SetMasterVolume(value);
        }

        if (dragScrapeAudios != null)
        {
            for (int i = 0; i < dragScrapeAudios.Length; i++)
            {
                if (dragScrapeAudios[i] != null)
                {
                    dragScrapeAudios[i].SetMasterVolume(value);
                }
            }
        }
    }

    private void ApplyGraphicsQuality()
    {
        HDRenderPipelineAsset asset = null;
        switch (graphicsQualityIndex)
        {
            case 0:
                asset = lowHdrpAsset;
                break;
            case 1:
                asset = mediumHdrpAsset;
                break;
            case 2:
                asset = highHdrpAsset;
                break;
        }

        if (asset == null)
        {
            return;
        }

        GraphicsSettings.defaultRenderPipeline = asset;
        QualitySettings.renderPipeline = asset;
    }

    private void ApplyCharacterLighting()
    {
        PlayerPrefs.SetInt(PrefPrefix + "character_lighting", characterLightingMode);
        PlayerPrefs.Save();
    }

    private string GetCharacterLightingLabel()
    {
        if (IsEnglish)
        {
            return characterLightingMode == 0 ? "classic" : "modern";
        }

        return characterLightingMode == 0 ? "классический" : "современный";
    }

    private void OnResolutionPressed()
    {
        List<string> options = new List<string>(availableResolutions.Count);
        for (int i = 0; i < availableResolutions.Count; i++)
        {
            Vector2Int r = availableResolutions[i];
            options.Add($"{r.x} x {r.y}");
        }

        OpenDropdownPopup(resolutionButton, options, resolutionIndex, index =>
        {
            resolutionIndex = Mathf.Clamp(index, 0, availableResolutions.Count - 1);
            PlayerPrefs.SetInt(PrefPrefix + "resolution_w", availableResolutions[resolutionIndex].x);
            PlayerPrefs.SetInt(PrefPrefix + "resolution_h", availableResolutions[resolutionIndex].y);
            ApplyResolution();
            SetTextSafe(resolutionValueText, ResolutionLabel());
        });
    }

    private void OnWindowModePressed()
    {
        List<string> options = new List<string>(windowModeOptions.Length);
        for (int i = 0; i < windowModeOptions.Length; i++)
        {
            options.Add(GetWindowModeLabelForIndex(i));
        }

        OpenDropdownPopup(windowModeButton, options, windowModeIndex, index =>
        {
            windowModeIndex = Mathf.Clamp(index, 0, windowModeOptions.Length - 1);
            PlayerPrefs.SetInt(PrefPrefix + "window_mode", (int)windowModeOptions[windowModeIndex]);
            ApplyWindowMode();
            SetTextSafe(windowModeValueText, GetWindowModeLabel());
        });
    }

    private void OnFpsPressed()
    {
        List<string> options = new List<string>(fpsOptions.Length);
        for (int i = 0; i < fpsOptions.Length; i++)
        {
            int fps = fpsOptions[i];
            options.Add(fps < 0 ? (IsEnglish ? "unlimited" : "безлимит") : fps.ToString());
        }

        OpenDropdownPopup(fpsButton, options, fpsIndex, index =>
        {
            fpsIndex = Mathf.Clamp(index, 0, fpsOptions.Length - 1);
            PlayerPrefs.SetInt(PrefPrefix + "fps_index", fpsIndex);
            ApplyFpsLimit();
            SetTextSafe(fpsValueText, fpsOptions[fpsIndex] < 0 ? (IsEnglish ? "unlimited" : "безлимит") : fpsOptions[fpsIndex].ToString());
        });
    }

    private void OpenDropdownPopup(Button sourceButton, List<string> options, int selectedIndex, Action<int> onSelect)
    {
        if (sourceButton == null || settingsRoot == null || options == null || options.Count == 0)
        {
            return;
        }

        EnsureDropdownPopup();
        CloseDropdownPopup();

        dropdownPopupOnSelect = onSelect;

        RectTransform sourceRect = sourceButton.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        sourceRect.GetWorldCorners(corners);
        Vector2 screenBottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(settingsRoot, screenBottomLeft, null, out Vector2 localBottomLeft);

        float rowHeight = 34f;
        float popupHeight = Mathf.Clamp(options.Count * rowHeight + 10f, 80f, 240f);
        dropdownPopupRoot.sizeDelta = new Vector2(430f, popupHeight);

        float x = settingsValueX;
        // Convert center-local Y to top-anchored Y and place popup just below source button.
        float y = (localBottomLeft.y - settingsRoot.rect.height * 0.5f) - 4f;

        RectTransform rootRect = settingsRoot;
        float topLimit = -6f;
        float bottomLimit = -rootRect.rect.height + popupHeight + 8f;
        y = Mathf.Clamp(y, bottomLimit, topLimit);

        dropdownPopupRoot.anchoredPosition = new Vector2(x, y);
        dropdownPopupRoot.SetAsLastSibling();
        dropdownPopupContent.sizeDelta = new Vector2(0f, options.Count * rowHeight + 6f);
        dropdownPopupContent.anchoredPosition = Vector2.zero;
        dropdownPopupScroll.verticalNormalizedPosition = 1f;

        Font f = ResolveSettingsItemsFont(customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"));
        for (int i = 0; i < options.Count; i++)
        {
            int idx = i;
            GameObject rowGo = new GameObject("Option " + i, typeof(RectTransform), typeof(Button), typeof(Image));
            rowGo.transform.SetParent(dropdownPopupContent, false);
            RectTransform rr = rowGo.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0f, 1f);
            rr.anchorMax = new Vector2(1f, 1f);
            rr.pivot = new Vector2(0.5f, 1f);
            rr.anchoredPosition = new Vector2(0f, -6f - i * rowHeight);
            rr.sizeDelta = new Vector2(0f, rowHeight - 2f);

            Image bg = rowGo.GetComponent<Image>();
            bg.color = i == selectedIndex ? new Color(1f, 1f, 1f, 0.2f) : new Color(1f, 1f, 1f, 0.07f);

            Button btn = rowGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                dropdownPopupOnSelect?.Invoke(idx);
                CloseDropdownPopup();
            });

            GameObject txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(rowGo.transform, false);
            RectTransform tr = txtGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(10f, 0f);
            tr.offsetMax = new Vector2(-10f, 0f);
            Text t = txtGo.GetComponent<Text>();
            t.font = f;
            t.fontSize = settingsItemFontSize;
            t.alignment = TextAnchor.MiddleLeft;
            t.color = new Color(1f, 1f, 1f, 0.98f);
            t.text = options[i];
            t.raycastTarget = false;

            dropdownPopupButtons.Add(btn);
        }

        dropdownPopupOpen = true;
        dropdownPopupRoot.gameObject.SetActive(true);
    }

    private void EnsureDropdownPopup()
    {
        if (dropdownPopupRoot != null)
        {
            return;
        }

        GameObject popupGo = new GameObject("Dropdown Popup", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        popupGo.transform.SetParent(settingsRoot, false);
        dropdownPopupRoot = popupGo.GetComponent<RectTransform>();
        dropdownPopupRoot.anchorMin = new Vector2(0f, 1f);
        dropdownPopupRoot.anchorMax = new Vector2(0f, 1f);
        dropdownPopupRoot.pivot = new Vector2(0f, 1f);
        dropdownPopupRoot.sizeDelta = new Vector2(430f, 200f);

        Image bg = popupGo.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.92f);
        bg.raycastTarget = true;

        dropdownPopupScroll = popupGo.GetComponent<ScrollRect>();
        dropdownPopupScroll.horizontal = false;
        dropdownPopupScroll.vertical = true;
        dropdownPopupScroll.inertia = false;
        dropdownPopupScroll.movementType = ScrollRect.MovementType.Clamped;
        dropdownPopupScroll.scrollSensitivity = 48f;

        GameObject viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        viewportGo.transform.SetParent(dropdownPopupRoot, false);
        dropdownPopupViewport = viewportGo.GetComponent<RectTransform>();
        dropdownPopupViewport.anchorMin = Vector2.zero;
        dropdownPopupViewport.anchorMax = Vector2.one;
        dropdownPopupViewport.offsetMin = new Vector2(2f, 2f);
        dropdownPopupViewport.offsetMax = new Vector2(-2f, -2f);

        Image viewportImage = viewportGo.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0f);
        

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(dropdownPopupViewport, false);
        dropdownPopupContent = contentGo.GetComponent<RectTransform>();
        dropdownPopupContent.anchorMin = new Vector2(0f, 1f);
        dropdownPopupContent.anchorMax = new Vector2(1f, 1f);
        dropdownPopupContent.pivot = new Vector2(0.5f, 1f);
        dropdownPopupContent.anchoredPosition = Vector2.zero;
        dropdownPopupContent.sizeDelta = new Vector2(0f, 0f);

        dropdownPopupScroll.viewport = dropdownPopupViewport;
        dropdownPopupScroll.content = dropdownPopupContent;

        dropdownPopupRoot.gameObject.SetActive(false);
    }

    private void CloseDropdownPopup()
    {
        if (dropdownPopupRoot == null)
        {
            return;
        }

        for (int i = 0; i < dropdownPopupButtons.Count; i++)
        {
            if (dropdownPopupButtons[i] != null)
            {
                Destroy(dropdownPopupButtons[i].gameObject);
            }
        }
        dropdownPopupButtons.Clear();
        dropdownPopupOnSelect = null;
        dropdownPopupOpen = false;
        dropdownPopupRoot.gameObject.SetActive(false);
    }



    private void OnVsyncPressed()
    {
        vSyncIndex = vSyncIndex == 0 ? 1 : 0;
        PlayerPrefs.SetInt(PrefPrefix + "vsync", vSyncIndex);
        ApplyVsync();
        SetTextSafe(vsyncValueText, vSyncIndex == 1 ? (IsEnglish ? "on" : "вкл") : (IsEnglish ? "off" : "выкл"));
    }

    private void OnHdrPressed()
    {
        hdrIndex = hdrIndex == 0 ? 1 : 0;
        PlayerPrefs.SetInt(PrefPrefix + "hdr", hdrIndex);
        ApplyHdr();
        SetTextSafe(hdrValueText, hdrIndex == 1 ? (IsEnglish ? "on" : "вкл") : (IsEnglish ? "off" : "выкл"));
        SetTextSafe(dynamicShadowsValueText, dynamicShadowsIndex == 1 ? (IsEnglish ? "on" : "вкл") : (IsEnglish ? "off" : "выкл"));
    }
    private void OnDynamicShadowsPressed()
    {
        dynamicShadowsIndex = dynamicShadowsIndex == 0 ? 1 : 0;
        PlayerPrefs.SetInt(PrefPrefix + "dynamic_shadows", dynamicShadowsIndex);
        ApplyDynamicShadows();
        SetTextSafe(dynamicShadowsValueText, dynamicShadowsIndex == 1 ? (IsEnglish ? "on" : "вкл") : (IsEnglish ? "off" : "выкл"));
    }

    private void ApplyDynamicShadows()
    {
        bool enabled = dynamicShadowsIndex == 1;
        QualitySettings.shadows = enabled ? ShadowQuality.All : ShadowQuality.Disable;

        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (enabled)
        {
            for (int i = 0; i < lights.Length; i++)
            {
                Light l = lights[i];
                if (l == null)
                {
                    continue;
                }

                if (cachedLightShadows.TryGetValue(l, out LightShadows prev))
                {
                    l.shadows = prev;
                }
                else if (l.shadows == LightShadows.None)
                {
                    // Fallback when no cached value exists.
                    l.shadows = LightShadows.Soft;
                }
            }
            return;
        }

        for (int i = 0; i < lights.Length; i++)
        {
            Light l = lights[i];
            if (l == null)
            {
                continue;
            }

            if (!cachedLightShadows.ContainsKey(l))
            {
                cachedLightShadows[l] = l.shadows;
            }

            l.shadows = LightShadows.None;
        }
    }

    private void OnBrightnessChanged(float value)
    {
        brightnessExposure = Mathf.Clamp(value, -2f, 2f);
        PlayerPrefs.SetFloat(PrefPrefix + "brightness_ev", brightnessExposure);
        ApplyVisualSettings();
        SetTextSafe(brightnessValueText, brightnessExposure.ToString("0.00"));
    }

    private void OnContrastChanged(float value)
    {
        contrastAmount = Mathf.Clamp(value, -100f, 100f);
        PlayerPrefs.SetFloat(PrefPrefix + "contrast", contrastAmount);
        ApplyVisualSettings();
        SetTextSafe(contrastValueText, contrastAmount.ToString("0"));
    }

    private void ApplyResolution()
    {
        if (availableResolutions.Count == 0)
        {
            return;
        }

        Vector2Int res = availableResolutions[Mathf.Clamp(resolutionIndex, 0, availableResolutions.Count - 1)];
        Screen.SetResolution(res.x, res.y, Screen.fullScreenMode);
    }

    private void ApplyWindowMode()
    {
        windowModeIndex = Mathf.Clamp(windowModeIndex, 0, windowModeOptions.Length - 1);
        FullScreenMode mode = windowModeOptions[windowModeIndex];
        int width = Screen.width;
        int height = Screen.height;

        if (availableResolutions.Count > 0)
        {
            Vector2Int res = availableResolutions[Mathf.Clamp(resolutionIndex, 0, availableResolutions.Count - 1)];
            width = res.x;
            height = res.y;
        }

        Screen.SetResolution(width, height, mode);
    }

    private void ApplyFpsLimit()
    {
        Application.targetFrameRate = fpsOptions[fpsIndex];
    }

    private void ApplyVsync()
    {
        QualitySettings.vSyncCount = vSyncIndex;
    }

    private void ApplyHdr()
    {
        bool enabled = hdrIndex == 1;
        Camera[] cameras = Camera.allCameras;
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].allowHDR = enabled;
        }
    }

    private void ApplyVisualSettings()
    {
        if (colorAdjustments != null)
        {
            colorAdjustments.postExposure.value = brightnessExposure;
            colorAdjustments.contrast.value = contrastAmount;
            colorAdjustments.colorFilter.value = GetColorBlindFilter();
            colorAdjustments.active = Mathf.Abs(brightnessExposure) > 0.001f
                || Mathf.Abs(contrastAmount) > 0.001f
                || colorBlindIndex != 0;
        }
        else
        {
            RenderSettings.ambientIntensity = Mathf.Clamp(ambientBaseIntensity + brightnessExposure * 0.25f, 0f, 8f);
        }
    }

    private Color GetColorBlindFilter()
    {
        switch (colorBlindIndex)
        {
            case 1: return new Color(0.82f, 1.0f, 1.0f, 1f);
            case 2: return new Color(1.0f, 0.82f, 1.0f, 1f);
            case 3: return new Color(1.0f, 1.0f, 0.75f, 1f);
            default: return Color.white;
        }
    }

    private string ResolutionLabel()
    {
        if (availableResolutions.Count == 0)
        {
            return Screen.width + "x" + Screen.height;
        }

        Vector2Int r = availableResolutions[Mathf.Clamp(resolutionIndex, 0, availableResolutions.Count - 1)];
        return r.x + "x" + r.y;
    }

    private string GetWindowModeLabelForIndex(int index)
    {
        int clamped = Mathf.Clamp(index, 0, windowModeOptions.Length - 1);
        FullScreenMode mode = windowModeOptions[clamped];
        if (IsEnglish)
        {
            switch (mode)
            {
                case FullScreenMode.ExclusiveFullScreen: return "fullscreen";
                case FullScreenMode.FullScreenWindow: return "borderless";
                case FullScreenMode.Windowed: return "windowed";
                default: return mode.ToString().ToLowerInvariant();
            }
        }

        switch (mode)
        {
            case FullScreenMode.ExclusiveFullScreen: return "полноэкранный";
            case FullScreenMode.FullScreenWindow: return "без рамки";
            case FullScreenMode.Windowed: return "оконный";
            default: return mode.ToString().ToLowerInvariant();
        }
    }
    private string GetWindowModeLabel()
    {
        FullScreenMode mode = windowModeOptions[Mathf.Clamp(windowModeIndex, 0, windowModeOptions.Length - 1)];
        if (IsEnglish)
        {
            switch (mode)
            {
                case FullScreenMode.ExclusiveFullScreen: return "fullscreen";
                case FullScreenMode.FullScreenWindow: return "borderless";
                case FullScreenMode.Windowed: return "windowed";
                default: return mode.ToString().ToLowerInvariant();
            }
        }

        switch (mode)
        {
            case FullScreenMode.ExclusiveFullScreen: return "полноэкранный";
            case FullScreenMode.FullScreenWindow: return "без рамки";
            case FullScreenMode.Windowed: return "оконный";
            default: return mode.ToString().ToLowerInvariant();
        }
    }

    private static int FindWindowModeIndex(FullScreenMode mode)
    {
        for (int i = 0; i < windowModeOptions.Length; i++)
        {
            if (windowModeOptions[i] == mode)
            {
                return i;
            }
        }

        return 1;
    }

    private static HoverQuestionSuffix AddHoverQuestionMark(GameObject go, Text targetLabel)
    {
        HoverQuestionSuffix hover = go.AddComponent<HoverQuestionSuffix>();
        hover.Initialize(targetLabel, string.Empty);
        return hover;
    }

    private static void SetTextSafe(Text text, string value)
    {
        if (text != null)
        {
            text.text = value;
        }
    }

    private static void SetSliderSafe(Slider slider, float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(value);
        }
    }

    private static void SetActiveSafe(Component component, bool value)
    {
        if (component != null)
        {
            component.gameObject.SetActive(value);
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}

public class HoverQuestionSuffix : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Text label;
    private string baseText;
    private bool hovered;

    public void Initialize(Text targetLabel, string text)
    {
        label = targetLabel;
        SetBaseText(text);
    }

    public void SetBaseText(string text)
    {
        baseText = text;
        if (label != null)
        {
            label.text = hovered ? baseText + "?" : baseText;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (label != null)
        {
            label.text = baseText + "?";
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        if (label != null)
        {
            label.text = baseText;
        }
    }
}















