using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Collider))]
public class ПодсказкаТащить : MonoBehaviour
{
    private const string LanguageKey = "PauseSimple.language";
    private const string SeenKey = "Tutorial.DragHint.Seen";
    private const string IconBasePath = "Assets/ICONS/Controls/keyboard-mouse-input-icons-251008/keyboard-input-icons/";

    [Serializable]
    private struct KeyIconEntry
    {
        public KeyCode key;
        public Sprite sprite;
    }

    [Header("Триггер")]
    [SerializeField] private string тегИгрока = "Player";
    [SerializeField] private bool показыватьТолькоОдинРаз = true;
    [SerializeField] private float длительностьПоказа = 4f;

    [Header("Вид")]
    [SerializeField] private Font шрифт;
    [SerializeField] private int размерШрифта = 22;
    [SerializeField] private Color цветТекста = Color.white;
    [SerializeField] private Color цветФона = new Color(0f, 0f, 0f, 0.62f);
    [SerializeField] private Vector2 размерПанели = new Vector2(820f, 150f);
    [SerializeField] private Vector2 позицияПанели = new Vector2(0f, 86f);

    [Header("Key Icons")]
    [SerializeField] private List<KeyIconEntry> keyIcons = new List<KeyIconEntry>();

    private readonly Dictionary<KeyCode, Sprite> iconMap = new Dictionary<KeyCode, Sprite>();

    private Canvas canvas;
    private RectTransform panel;
    private Text labelTop;      // "некоторые предметы можно ТАЩИТЬ"
    private Text labelLeft;     // "удерживайте " / "hold "
    private Image iconImage;    // иконка клавиши
    private Text labelRight;    // " для этого" / " to do this"
    private float hideAt;
    private bool shown;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Awake()
    {
        GameInputBindings.EnsureLoaded();
        BuildIconMap();

        if (показыватьТолькоОдинРаз && PlayerPrefs.GetInt(SeenKey, 0) == 1)
        {
            shown = true;
            return;
        }

        BuildUi();
        panel.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (panel == null || !panel.gameObject.activeSelf) return;
        if (Time.unscaledTime >= hideAt) panel.gameObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (shown || !other.CompareTag(тегИгрока)) return;

        shown = true;
        if (показыватьТолькоОдинРаз)
        {
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
        }

        ShowHint();
    }

    private void ShowHint()
    {
        if (panel == null) return;

        bool english = PlayerPrefs.GetInt(LanguageKey, 1) == 0;
        KeyCode actionKey = NormalizeKey(GameInputBindings.ActionKey);
        Sprite icon = GetIconForKey(actionKey);

        if (english)
        {
            if (labelTop != null) labelTop.text = "some objects can be DRAGGED";
            if (labelLeft != null) labelLeft.text = "hold ";
            if (labelRight != null) labelRight.text = " to do this";
        }
        else
        {
            if (labelTop != null) labelTop.text = "некоторые предметы можно ТАЩИТЬ";
            if (labelLeft != null) labelLeft.text = "удерживайте ";
            if (labelRight != null) labelRight.text = " для этого";
        }

        if (icon != null)
        {
            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = true;
                iconImage.gameObject.SetActive(true);
            }
        }
        else
        {
            // Нет иконки — дописываем название клавиши в левый текст, скрываем слот
            if (iconImage != null) iconImage.gameObject.SetActive(false);
            if (labelLeft != null) labelLeft.text += ReadableKey(GameInputBindings.ActionKey);
        }

        panel.gameObject.SetActive(true);
        hideAt = Time.unscaledTime + Mathf.Max(0.5f, длительностьПоказа);
    }

    private void BuildUi()
    {
        Font f = ResolveFont();

        GameObject canvasGo = new GameObject("Drag Hint Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1700;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // Панель
        GameObject panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = позицияПанели;
        panel.sizeDelta = размерПанели;
        panelGo.GetComponent<Image>().color = цветФона;

        // Вертикальный контейнер: строка 1 сверху, строка 2 снизу
        GameObject colGo = new GameObject("Col", typeof(RectTransform), typeof(VerticalLayoutGroup));
        colGo.transform.SetParent(panel, false);
        RectTransform colRect = colGo.GetComponent<RectTransform>();
        colRect.anchorMin = Vector2.zero;
        colRect.anchorMax = Vector2.one;
        colRect.offsetMin = new Vector2(22f, 12f);
        colRect.offsetMax = new Vector2(-22f, -12f);

        VerticalLayoutGroup vlg = colGo.GetComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.MiddleCenter;
        vlg.spacing = 10f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // --- Строка 1: просто текст ---
        labelTop = CreateInlineText(colRect, "Line1", f);
        labelTop.alignment = TextAnchor.MiddleCenter;

        // --- Строка 2: Text + Icon + Text ---
        GameObject rowGo = new GameObject("Line2", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(colRect, false);
        rowGo.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, размерШрифта * 2.2f);

        HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.spacing = 6f;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        RectTransform rowRect = rowGo.GetComponent<RectTransform>();

        labelLeft = CreateInlineText(rowRect, "TextLeft", f);
        labelLeft.alignment = TextAnchor.MiddleRight;
        labelLeft.GetComponent<LayoutElement>().flexibleWidth = 1f;

        // Иконка
        GameObject iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        iconGo.transform.SetParent(rowRect, false);
        float iconSize = размерШрифта * 2.2f;
        iconGo.GetComponent<RectTransform>().sizeDelta = new Vector2(iconSize, iconSize);
        iconImage = iconGo.GetComponent<Image>();
        iconImage.preserveAspect = true;
        iconImage.enabled = false;
        LayoutElement ile = iconGo.GetComponent<LayoutElement>();
        ile.preferredWidth = iconSize;
        ile.preferredHeight = iconSize;

        labelRight = CreateInlineText(rowRect, "TextRight", f);
        labelRight.alignment = TextAnchor.MiddleLeft;
        labelRight.GetComponent<LayoutElement>().flexibleWidth = 1f;
    }

    private Text CreateInlineText(Transform parent, string name, Font f)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = new Vector2(0f, размерШрифта * 2.2f);

        Text t = go.GetComponent<Text>();
        t.font = f;
        t.fontSize = Mathf.Max(8, размерШрифта);
        t.color = цветТекста;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        return t;
    }

    private void BuildIconMap()
    {
        iconMap.Clear();
        for (int i = 0; i < keyIcons.Count; i++)
        {
            KeyIconEntry entry = keyIcons[i];
            if (entry.sprite != null) iconMap[NormalizeKey(entry.key)] = entry.sprite;
        }
    }

    private Sprite GetIconForKey(KeyCode key)
    {
        if (iconMap.TryGetValue(key, out Sprite sprite) && sprite != null) return sprite;

#if UNITY_EDITOR
        string slug = KeyToSlug(key);
        if (!string.IsNullOrEmpty(slug))
        {
            Sprite loaded = AssetDatabase.LoadAssetAtPath<Sprite>(IconBasePath + "key-" + slug + ".png");
            if (loaded != null) { iconMap[key] = loaded; return loaded; }
        }
#endif
        return null;
    }

    private Font ResolveFont()
    {
        if (шрифт != null) return шрифт;
#if UNITY_EDITOR
        шрифт = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Press_Start_2P/PressStart2P-Regular.ttf");
#endif
        return шрифт != null ? шрифт : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static KeyCode NormalizeKey(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.RightShift:   return KeyCode.LeftShift;
            case KeyCode.RightControl: return KeyCode.LeftControl;
            case KeyCode.RightAlt:     return KeyCode.LeftAlt;
            case KeyCode.KeypadEnter:  return KeyCode.Return;
            default: return key;
        }
    }

    private static string KeyToSlug(KeyCode key)
    {
        if (key >= KeyCode.A && key <= KeyCode.Z)
            return ((char)('a' + (int)key - (int)KeyCode.A)).ToString();

        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
            return ((int)key - (int)KeyCode.Alpha0).ToString();

        switch (key)
        {
            case KeyCode.Space:       return "space";
            case KeyCode.LeftShift:   return "shift";
            case KeyCode.LeftControl: return "ctrl";
            case KeyCode.LeftAlt:     return "alt";
            case KeyCode.UpArrow:     return "arrow-up";
            case KeyCode.DownArrow:   return "arrow-down";
            case KeyCode.LeftArrow:   return "arrow-left";
            case KeyCode.RightArrow:  return "arrow-right";
            case KeyCode.Return:      return "enter";
            case KeyCode.Escape:      return "esc";
            case KeyCode.Tab:         return "tab";
            default: return null;
        }
    }

    private static string ReadableKey(KeyCode key)
    {
        if (key >= KeyCode.A && key <= KeyCode.Z) return key.ToString().ToUpperInvariant();
        if (key == KeyCode.Space) return "SPACE";
        return key.ToString().ToUpperInvariant();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (шрифт == null)
            шрифт = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Press_Start_2P/PressStart2P-Regular.ttf");

        PopulateIconLibrary();
    }

    private void PopulateIconLibrary()
    {
        for (KeyCode k = KeyCode.A; k <= KeyCode.Z; k++) EnsureIconEntry(k);
        for (KeyCode k = KeyCode.Alpha0; k <= KeyCode.Alpha9; k++) EnsureIconEntry(k);
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
                entry.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconBasePath + "key-" + slug + ".png");
        }

        if (index >= 0) keyIcons[index] = entry;
        else if (entry.sprite != null) keyIcons.Add(entry);
    }
#endif
}