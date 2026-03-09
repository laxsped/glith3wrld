using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ЛифтСтарт : MonoBehaviour
{
    private const string PausePrefsPrefix = "PauseSimple.";
    private const string IconBasePath = "Assets/ICONS/Controls/keyboard-mouse-input-icons-251008/keyboard-input-icons/";

    [Serializable]
    private struct KeyIconEntry
    {
        public KeyCode key;
        public Sprite sprite;
    }

    [Header("Объекты")]
    [SerializeField] private Transform леваяДверь;
    [SerializeField] private Transform праваяДверь;
    [SerializeField] private GameObject родительЗдания;

    [Header("Аудио")]
    [SerializeField] private AudioSource источникЗвука;
    [SerializeField] private AudioClip звукOneShot;
    [SerializeField] [Range(0f, 1f)] private float громкостьSfx = 1f;

    [Header("Тайминги")]
    [SerializeField] private float задержкаПередОткрытием = 7f;
    [SerializeField] private float длительностьОткрытия = 1f;

    [Header("Целевые Z (локальные)")]
    [SerializeField] private float zЛевойДвери = -1.046f;
    [SerializeField] private float zПравойДвери = 1.046f;

    [Header("Обучение после лифта")]
    [SerializeField] private bool показатьОбучениеПослеЛифта = true;
    [SerializeField] private float длительностьПодсказкиХодьбы = 2.4f;
    [SerializeField] private float длительностьПодсказкиБега = 2.2f;
    [SerializeField] private Font шрифтОбучения;
    [SerializeField] private int размерШрифта = 24;
    [SerializeField] private Color фонПанели = new Color(0f, 0f, 0f, 0.62f);
    [SerializeField] private Color цветТекста = Color.white;
    [SerializeField] private List<KeyIconEntry> keyIcons = new List<KeyIconEntry>();

    private readonly Dictionary<KeyCode, Sprite> iconMap = new Dictionary<KeyCode, Sprite>();

    private Canvas canvas;
    private RectTransform panel;
    private Image firstIcon;
    private Image secondIcon;
    private Image thirdIcon;
    private Image fourthIcon;
    private Text label;

    private void Awake()
    {
        GameInputBindings.EnsureLoaded();
        GameInputBindings.RunLocked = true;

        if (родительЗдания != null)
        {
            родительЗдания.SetActive(false);
        }

        BuildIconMap();
    }

    private void Start()
    {
        StartCoroutine(ЗапускСценыЛифта());
    }

    private IEnumerator ЗапускСценыЛифта()
    {
        yield return new WaitForSeconds(задержкаПередОткрытием);

        yield return StartCoroutine(ПлавноОткрытьДвери());

        if (родительЗдания != null)
        {
            родительЗдания.SetActive(true);
        }

        if (источникЗвука != null && звукOneShot != null)
        {
            float fx = Mathf.Clamp01(PlayerPrefs.GetInt("PauseSimple.volume_effects", 10) / 10f);
            источникЗвука.PlayOneShot(звукOneShot, громкостьSfx * fx);
        }

        if (показатьОбучениеПослеЛифта)
        {
            yield return StartCoroutine(ПоказатьОбучениеПослеЛифта());
        }

        GameInputBindings.RunLocked = false;
    }

    private IEnumerator ПлавноОткрытьДвери()
    {
        if (леваяДверь == null || праваяДверь == null)
        {
            yield break;
        }

        Vector3 стартЛевой = леваяДверь.localPosition;
        Vector3 стартПравой = праваяДверь.localPosition;

        Vector3 цельЛевой = new Vector3(стартЛевой.x, стартЛевой.y, zЛевойДвери);
        Vector3 цельПравой = new Vector3(стартПравой.x, стартПравой.y, zПравойДвери);

        float t = 0f;
        float длительность = Mathf.Max(0.01f, длительностьОткрытия);

        while (t < длительность)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / длительность);
            леваяДверь.localPosition = Vector3.Lerp(стартЛевой, цельЛевой, k);
            праваяДверь.localPosition = Vector3.Lerp(стартПравой, цельПравой, k);
            yield return null;
        }

        леваяДверь.localPosition = цельЛевой;
        праваяДверь.localPosition = цельПравой;
    }

    private IEnumerator ПоказатьОбучениеПослеЛифта()
    {
        EnsureTutorialUi();
        if (canvas == null)
        {
            yield break;
        }

        bool isEnglish = PlayerPrefs.GetInt(PausePrefsPrefix + "language", 1) == 0;

        SetPromptMovement(
            NormalizeKey(GameInputBindings.ForwardKey),
            NormalizeKey(GameInputBindings.LeftKey),
            NormalizeKey(GameInputBindings.BackwardKey),
            NormalizeKey(GameInputBindings.RightKey),
            isEnglish ? "move" : "ходьба"
        );
        canvas.enabled = true;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, длительностьПодсказкиХодьбы));

        SetPromptSingle(
            NormalizeKey(GameInputBindings.RunKey),
            isEnglish ? "run" : "бег"
        );
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, длительностьПодсказкиБега));

        canvas.enabled = false;
    }

    private void EnsureTutorialUi()
    {
        if (canvas != null)
        {
            return;
        }

        Font font = ResolveFont();

        GameObject canvasGo = new GameObject("Lift Tutorial Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1550;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelGo = new GameObject("Lift Tutorial Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 56f);
        panel.sizeDelta = new Vector2(1120f, 148f);
        panelGo.GetComponent<Image>().color = фонПанели;

        GameObject rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(panel, false);
        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.anchorMin = Vector2.zero;
        rowRect.anchorMax = Vector2.one;
        rowRect.offsetMin = new Vector2(32f, 16f);
        rowRect.offsetMax = new Vector2(-32f, -16f);

        HorizontalLayoutGroup h = rowGo.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 20f;
        h.childControlWidth = false;
        h.childControlHeight = false;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        firstIcon = CreateIcon(rowRect, "Icon A");
        secondIcon = CreateIcon(rowRect, "Icon B");
        thirdIcon = CreateIcon(rowRect, "Icon C");
        fourthIcon = CreateIcon(rowRect, "Icon D");
        label = CreateLabel(rowRect, font);
        canvas.enabled = false;
    }

    private static Image CreateIcon(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(80f, 80f);
        Image img = go.GetComponent<Image>();
        img.preserveAspect = true;

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 80f;
        le.preferredHeight = 80f;
        return img;
    }

    private Text CreateLabel(Transform parent, Font font)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        Text t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = Mathf.Max(8, размерШрифта);
        t.color = цветТекста;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 600f;
        le.flexibleWidth = 1f;
        return t;
    }

    private Font ResolveFont()
    {
        if (шрифтОбучения != null)
        {
            return шрифтОбучения;
        }

#if UNITY_EDITOR
        шрифтОбучения = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Press_Start_2P/PressStart2P-Regular.ttf");
#endif
        return шрифтОбучения != null ? шрифтОбучения : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void SetPromptMovement(KeyCode forward, KeyCode left, KeyCode backward, KeyCode right, string text)
    {
        ApplyIcon(firstIcon, forward);
        ApplyIcon(secondIcon, left);
        ApplyIcon(thirdIcon, backward);
        ApplyIcon(fourthIcon, right);

        firstIcon.gameObject.SetActive(true);
        secondIcon.gameObject.SetActive(true);
        thirdIcon.gameObject.SetActive(true);
        fourthIcon.gameObject.SetActive(true);
        label.text = text;
    }

    private void SetPromptSingle(KeyCode key, string text)
    {
        ApplyIcon(firstIcon, key);
        firstIcon.gameObject.SetActive(true);

        secondIcon.gameObject.SetActive(false);
        thirdIcon.gameObject.SetActive(false);
        fourthIcon.gameObject.SetActive(false);
        label.text = text;
    }


    private void BuildIconMap()
    {
        iconMap.Clear();
        for (int i = 0; i < keyIcons.Count; i++)
        {
            KeyIconEntry entry = keyIcons[i];
            if (entry.sprite != null)
            {
                iconMap[NormalizeKey(entry.key)] = entry.sprite;
            }
        }
    }

    private void ApplyIcon(Image image, KeyCode key)
    {
        if (image == null)
        {
            return;
        }

        if (iconMap.TryGetValue(key, out Sprite sprite) && sprite != null)
        {
            image.enabled = true;
            image.sprite = sprite;
            return;
        }

#if UNITY_EDITOR
        string slug = KeyToSlug(key);
        if (!string.IsNullOrEmpty(slug))
        {
            Sprite loaded = AssetDatabase.LoadAssetAtPath<Sprite>(IconBasePath + "key-" + slug + ".png");
            if (loaded != null)
            {
                iconMap[key] = loaded;
                image.enabled = true;
                image.sprite = loaded;
                return;
            }
        }
#endif

        image.enabled = false;
        image.sprite = null;
    }

    private static KeyCode NormalizeKey(KeyCode key)
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

    private static string KeyToSlug(KeyCode key)
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
            case KeyCode.LeftShift: return "shift";
            case KeyCode.LeftControl: return "ctrl";
            case KeyCode.LeftAlt: return "alt";
            case KeyCode.UpArrow: return "arrow-up";
            case KeyCode.DownArrow: return "arrow-down";
            case KeyCode.LeftArrow: return "arrow-left";
            case KeyCode.RightArrow: return "arrow-right";
            case KeyCode.Return: return "enter";
            case KeyCode.Escape: return "esc";
            case KeyCode.Tab: return "tab";
            default: return null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (шрифтОбучения == null)
        {
            шрифтОбучения = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Press_Start_2P/PressStart2P-Regular.ttf");
        }

        PopulateIconLibrary();
    }

    private void PopulateIconLibrary()
    {
        EnsureIconEntry(KeyCode.A);
        EnsureIconEntry(KeyCode.B);
        EnsureIconEntry(KeyCode.C);
        EnsureIconEntry(KeyCode.D);
        EnsureIconEntry(KeyCode.E);
        EnsureIconEntry(KeyCode.F);
        EnsureIconEntry(KeyCode.G);
        EnsureIconEntry(KeyCode.H);
        EnsureIconEntry(KeyCode.I);
        EnsureIconEntry(KeyCode.J);
        EnsureIconEntry(KeyCode.K);
        EnsureIconEntry(KeyCode.L);
        EnsureIconEntry(KeyCode.M);
        EnsureIconEntry(KeyCode.N);
        EnsureIconEntry(KeyCode.O);
        EnsureIconEntry(KeyCode.P);
        EnsureIconEntry(KeyCode.Q);
        EnsureIconEntry(KeyCode.R);
        EnsureIconEntry(KeyCode.S);
        EnsureIconEntry(KeyCode.T);
        EnsureIconEntry(KeyCode.U);
        EnsureIconEntry(KeyCode.V);
        EnsureIconEntry(KeyCode.W);
        EnsureIconEntry(KeyCode.X);
        EnsureIconEntry(KeyCode.Y);
        EnsureIconEntry(KeyCode.Z);
        EnsureIconEntry(KeyCode.Alpha0);
        EnsureIconEntry(KeyCode.Alpha1);
        EnsureIconEntry(KeyCode.Alpha2);
        EnsureIconEntry(KeyCode.Alpha3);
        EnsureIconEntry(KeyCode.Alpha4);
        EnsureIconEntry(KeyCode.Alpha5);
        EnsureIconEntry(KeyCode.Alpha6);
        EnsureIconEntry(KeyCode.Alpha7);
        EnsureIconEntry(KeyCode.Alpha8);
        EnsureIconEntry(KeyCode.Alpha9);
        EnsureIconEntry(KeyCode.Space);
        EnsureIconEntry(KeyCode.LeftShift);
        EnsureIconEntry(KeyCode.LeftControl);
        EnsureIconEntry(KeyCode.LeftAlt);
        EnsureIconEntry(KeyCode.UpArrow);
        EnsureIconEntry(KeyCode.DownArrow);
        EnsureIconEntry(KeyCode.LeftArrow);
        EnsureIconEntry(KeyCode.RightArrow);
        EnsureIconEntry(KeyCode.Return);
        EnsureIconEntry(KeyCode.Escape);
        EnsureIconEntry(KeyCode.Tab);
    }

    private void EnsureIconEntry(KeyCode key)
    {
        int index = keyIcons.FindIndex(x => x.key == key);
        KeyIconEntry entry = index >= 0 ? keyIcons[index] : new KeyIconEntry { key = key };
        if (entry.sprite == null)
        {
            string slug = KeyToSlug(key);
            if (!string.IsNullOrEmpty(slug))
            {
                entry.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconBasePath + "key-" + slug + ".png");
            }
        }

        if (index >= 0)
        {
            keyIcons[index] = entry;
        }
        else if (entry.sprite != null)
        {
            keyIcons.Add(entry);
        }
    }
#endif
}