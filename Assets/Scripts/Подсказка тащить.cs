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

    private Canvas canvas;
    private RectTransform panel;
    private Text label;
    private float hideAt;
    private bool shown;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void Awake()
    {
        GameInputBindings.EnsureLoaded();
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
        if (panel == null || !panel.gameObject.activeSelf)
        {
            return;
        }

        if (Time.unscaledTime >= hideAt)
        {
            panel.gameObject.SetActive(false);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (shown || !other.CompareTag(тегИгрока))
        {
            return;
        }

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
        if (panel == null || label == null)
        {
            return;
        }

        bool english = PlayerPrefs.GetInt(LanguageKey, 1) == 0;
        string actionKey = ReadableKey(GameInputBindings.ActionKey);
        label.text = english
            ? $"some objects can be DRAGGED\nhold {actionKey} to do this"
            : $"некоторые предметы можно ТАЩИТЬ\nудерживайте {actionKey} для этого";

        panel.gameObject.SetActive(true);
        hideAt = Time.unscaledTime + Mathf.Max(0.5f, длительностьПоказа);
    }

    private void BuildUi()
    {
        Font resolvedFont = ResolveFont();

        GameObject canvasGo = new GameObject("Drag Hint Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1700;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelGo = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = позицияПанели;
        panel.sizeDelta = размерПанели;

        Image bg = panelGo.GetComponent<Image>();
        bg.color = цветФона;

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(panel, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = new Vector2(22f, 16f);
        tr.offsetMax = new Vector2(-22f, -16f);

        label = textGo.GetComponent<Text>();
        label.font = resolvedFont;
        label.fontSize = Mathf.Max(8, размерШрифта);
        label.color = цветТекста;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private Font ResolveFont()
    {
        if (шрифт != null)
        {
            return шрифт;
        }
#if UNITY_EDITOR
        шрифт = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Press_Start_2P/PressStart2P-Regular.ttf");
#endif
        return шрифт != null ? шрифт : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private static string ReadableKey(KeyCode key)
    {
        if (key >= KeyCode.A && key <= KeyCode.Z)
        {
            return key.ToString().ToUpperInvariant();
        }

        if (key == KeyCode.Space)
        {
            return "SPACE";
        }

        return key.ToString().ToUpperInvariant();
    }
}
