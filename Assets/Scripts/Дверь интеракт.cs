using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ДверьИнтеракт : MonoBehaviour
{
    private const string ЯзыкКлюч = "PauseSimple.language";
    private const string ЭффектыКлюч = "PauseSimple.volume_effects";
    private const string ИконкиПуть = "Assets/ICONS/Controls/keyboard-mouse-input-icons-251008/keyboard-input-icons/";
    private const string ЗвукПоУмолчанию = "Assets/Door, Cabinet and Locker Sound Pack (Free)/FREE VERSION/Locked Door Turn Doorknob 3.wav";
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int UnlitColorMapId = Shader.PropertyToID("_UnlitColorMap");
    private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");

    [Header("Поиск двери")]
    [SerializeField] private string тегДвери = "Door";

    [Header("Точка UI")]
    [SerializeField] private Transform точкаПривязки;
    [SerializeField] private Vector3 смещениеМира = new Vector3(0f, 1.1f, 0f);
    [SerializeField] private PlayerWASDAnimator аниматорИгрока;

    [Header("Звук")]
    [SerializeField] private AudioSource источникЗвука;
    [SerializeField] private AudioClip звукЗаперто;
    [SerializeField] [Range(0f, 1f)] private float громкостьOneShot = 1f;

    [Header("Текстура интеракта")]
    [SerializeField] private Renderer рендерерАнимации;
    [SerializeField] private DirectionalFrames кадрыИнтеракт;
    [SerializeField] private float fpsАнимации = 12f;
    [SerializeField] private string папкаКадров = "Assets/sprite/16x32/frames_16x32/Interact";

    [Header("UI стиль")]
    [SerializeField] private Font шрифт;
    [SerializeField] private int размерШрифта = 28;
    [SerializeField] private Color цветТекста = Color.white;
    [SerializeField] private Color фонТекста = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private float длительностьНадписи = 1.3f;
    [SerializeField] private Vector2 размерИконки = new Vector2(90f, 90f);
    [SerializeField] private List<KeyIconEntry> keyIcons = new List<KeyIconEntry>();

    private Canvas canvas;
    private Image иконкаКлавиши;
    private Text текстКлавиши;
    private RectTransform низПлашка;
    private Text низТекст;
    private Sprite иконкаСпрайт;
    private Material runtimeMat;
    private bool дверьРядом;
    private Collider текущаяДверь;
    private readonly HashSet<Collider> двериВТриггере = new HashSet<Collider>();
    private readonly Dictionary<Collider, int> счетчикНажатийПоДвери = new Dictionary<Collider, int>();
    private int нажатий;
    private Coroutine корутинаНадписи;
    private Coroutine корутинаАнимации;
    private FacingDirection последняяСторона = FacingDirection.Front;
    private KeyCode cachedActionKey = KeyCode.None;
    private readonly Dictionary<KeyCode, Sprite> iconMap = new Dictionary<KeyCode, Sprite>();

    [Serializable]
    private struct DirectionalFrames
    {
        public Texture2D[] front;
        public Texture2D[] frontSide;
        public Texture2D[] side;
        public Texture2D[] backSide;
        public Texture2D[] back;
    }

    private enum FacingDirection
    {
        Front,
        FrontSide,
        Side,
        BackSide,
        Back
    }

    [Serializable]
    private struct KeyIconEntry
    {
        public KeyCode key;
        public Sprite sprite;
    }

    private void Awake()
    {
        GameInputBindings.EnsureLoaded();

        if (аниматорИгрока == null)
        {
            аниматорИгрока = GetComponent<PlayerWASDAnimator>();
        }

        if (точкаПривязки == null)
        {
            точкаПривязки = transform;
        }

        if (источникЗвука == null)
        {
            источникЗвука = GetComponent<AudioSource>();
        }

        if (рендерерАнимации != null)
        {
            runtimeMat = рендерерАнимации.material;
        }

        BuildUi();
        BuildIconMap();
        LoadKeyIcon();
    }

    private void Update()
    {
        RefreshActionKeyIfNeeded();
        UpdatePromptPosition();

        if (!дверьРядом || текущаяДверь == null)
        {
            return;
        }

        if (Input.GetKeyDown(GameInputBindings.ActionKey))
        {
            if (!счетчикНажатийПоДвери.TryGetValue(текущаяДверь, out нажатий))
            {
                нажатий = 0;
            }

            нажатий++;
            счетчикНажатийПоДвери[текущаяДверь] = нажатий;
            TryResolveDoorOverrides(текущаяДверь);

            if (источникЗвука != null && звукЗаперто != null)
            {
                источникЗвука.PlayOneShot(звукЗаперто, GetScaledEffectsVolume());
            }

            if (корутинаАнимации != null)
            {
                StopCoroutine(корутинаАнимации);
            }
            корутинаАнимации = StartCoroutine(ПроигратьКадрыИнтеракт(ОпределитьСторонуИнтеракта()));

            if (нажатий >= 2)
            {
                if (корутинаНадписи != null)
                {
                    StopCoroutine(корутинаНадписи);
                }
                корутинаНадписи = StartCoroutine(ПоказатьНадписьСнизу());
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(тегДвери))
        {
            return;
        }

        двериВТриггере.Add(other);
        текущаяДверь = ChooseNearestDoor();
        дверьРядом = текущаяДверь != null;
        TryResolveDoorOverrides(текущаяДверь);
        SetPromptVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(тегДвери))
        {
            return;
        }

        двериВТриггере.Remove(other);
        if (текущаяДверь == other)
        {
            текущаяДверь = null;
        }

        текущаяДверь = ChooseNearestDoor();
        дверьРядом = текущаяДверь != null;
        if (!дверьРядом)
        {
            SetPromptVisible(false);
        }
    }

    private IEnumerator ПоказатьНадписьСнизу()
    {
        bool en = PlayerPrefs.GetInt(ЯзыкКлюч, 1) == 0;
        низТекст.text = en ? "locked" : "закрыто";
        низПлашка.gameObject.SetActive(true);

        yield return new WaitForSecondsRealtime(длительностьНадписи);

        низПлашка.gameObject.SetActive(false);
    }

    private IEnumerator ПроигратьКадрыИнтеракт(FacingDirection сторона)
    {
        Texture2D[] кадры = GetFramesForDirection(кадрыИнтеракт, сторона);
        if (runtimeMat == null || кадры == null || кадры.Length == 0)
        {
            yield break;
        }

        float frameDur = 1f / Mathf.Max(1f, fpsАнимации);
        float timer = 0f;
        int idx = 0;
        SetTexture(runtimeMat, кадры[0]);

        while (idx < кадры.Length - 1)
        {
            timer += Time.deltaTime;
            while (timer >= frameDur && idx < кадры.Length - 1)
            {
                timer -= frameDur;
                idx++;
                SetTexture(runtimeMat, кадры[idx]);
            }
            yield return null;
        }

        if (аниматорИгрока != null)
        {
            аниматорИгрока.ForceRefreshCurrentFrame();
        }
    }

    private FacingDirection ОпределитьСторонуИнтеракта()
    {
        if (аниматорИгрока != null)
        {
            string id = аниматорИгрока.GetFacingDirectionId();
            switch (id)
            {
                case "front":
                    последняяСторона = FacingDirection.Front;
                    return последняяСторона;
                case "frontSide":
                    последняяСторона = FacingDirection.FrontSide;
                    return последняяСторона;
                case "side":
                    последняяСторона = FacingDirection.Side;
                    return последняяСторона;
                case "backSide":
                    последняяСторона = FacingDirection.BackSide;
                    return последняяСторона;
                case "back":
                    последняяСторона = FacingDirection.Back;
                    return последняяСторона;
            }
        }

        Vector2 input = ReadMoveInput();
        bool isMoving = input.sqrMagnitude > 0.001f;
        if (!isMoving)
        {
            return последняяСторона;
        }

        float absX = Mathf.Abs(input.x);
        float absY = Mathf.Abs(input.y);

        if (absY > 0.001f && absX <= 0.001f)
        {
            последняяСторона = input.y > 0f ? FacingDirection.Back : FacingDirection.Front;
            return последняяСторона;
        }

        if (absX > 0.001f && absY <= 0.001f)
        {
            последняяСторона = FacingDirection.Side;
            return последняяСторона;
        }

        последняяСторона = input.y > 0f ? FacingDirection.BackSide : FacingDirection.FrontSide;
        return последняяСторона;
    }

    private static Vector2 ReadMoveInput()
    {
        GameInputBindings.EnsureLoaded();
        float x = 0f;
        float y = 0f;

        if (Input.GetKey(GameInputBindings.LeftKey)) x -= 1f;
        if (Input.GetKey(GameInputBindings.RightKey)) x += 1f;
        if (Input.GetKey(GameInputBindings.ForwardKey)) y += 1f;
        if (Input.GetKey(GameInputBindings.BackwardKey)) y -= 1f;

        return new Vector2(x, y);
    }

    private static Texture2D[] GetFramesForDirection(DirectionalFrames frames, FacingDirection direction)
    {
        switch (direction)
        {
            case FacingDirection.Front: return frames.front;
            case FacingDirection.FrontSide: return frames.frontSide;
            case FacingDirection.Side: return frames.side;
            case FacingDirection.BackSide: return frames.backSide;
            case FacingDirection.Back: return frames.back;
            default: return frames.front;
        }
    }

    private void BuildUi()
    {
        Font f = ResolveFont();

        GameObject canvasGo = new GameObject("DoorInteract Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1600;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject iconGo = new GameObject("ActionIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(canvasGo.transform, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.sizeDelta = размерИконки;
        иконкаКлавиши = iconGo.GetComponent<Image>();
        иконкаКлавиши.preserveAspect = true;

        GameObject keyTextGo = new GameObject("ActionText", typeof(RectTransform), typeof(Text));
        keyTextGo.transform.SetParent(iconGo.transform, false);
        RectTransform keyTextRect = keyTextGo.GetComponent<RectTransform>();
        keyTextRect.anchorMin = Vector2.zero;
        keyTextRect.anchorMax = Vector2.one;
        keyTextRect.offsetMin = Vector2.zero;
        keyTextRect.offsetMax = Vector2.zero;

        текстКлавиши = keyTextGo.GetComponent<Text>();
        текстКлавиши.font = f;
        текстКлавиши.fontSize = Mathf.Max(8, размерШрифта);
        текстКлавиши.color = цветТекста;
        текстКлавиши.alignment = TextAnchor.MiddleCenter;
        текстКлавиши.text = GameInputBindings.ActionKey.ToString();

        GameObject bottomGo = new GameObject("LockedPanel", typeof(RectTransform), typeof(Image));
        bottomGo.transform.SetParent(canvasGo.transform, false);
        низПлашка = bottomGo.GetComponent<RectTransform>();
        низПлашка.anchorMin = new Vector2(0.5f, 0f);
        низПлашка.anchorMax = new Vector2(0.5f, 0f);
        низПлашка.pivot = new Vector2(0.5f, 0f);
        низПлашка.anchoredPosition = new Vector2(0f, 42f);
        низПлашка.sizeDelta = new Vector2(360f, 64f);

        Image bg = bottomGo.GetComponent<Image>();
        bg.color = фонТекста;

        GameObject bottomTextGo = new GameObject("LockedText", typeof(RectTransform), typeof(Text));
        bottomTextGo.transform.SetParent(bottomGo.transform, false);
        RectTransform btr = bottomTextGo.GetComponent<RectTransform>();
        btr.anchorMin = Vector2.zero;
        btr.anchorMax = Vector2.one;
        btr.offsetMin = new Vector2(8f, 6f);
        btr.offsetMax = new Vector2(-8f, -6f);

        низТекст = bottomTextGo.GetComponent<Text>();
        низТекст.font = f;
        низТекст.fontSize = Mathf.Max(8, размерШрифта - 4);
        низТекст.color = цветТекста;
        низТекст.alignment = TextAnchor.MiddleCenter;
        низТекст.text = "закрыто";

        низПлашка.gameObject.SetActive(false);
        SetPromptVisible(false);
    }

    private void UpdatePromptPosition()
    {
        if (canvas == null || иконкаКлавиши == null || !дверьРядом || текущаяДверь == null)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Transform anchor = точкаПривязки != null ? точкаПривязки : текущаяДверь.transform;
        Vector3 world = anchor.position + смещениеМира;
        Vector3 screen = cam.WorldToScreenPoint(world);
        bool visible = screen.z > 0.05f;

        иконкаКлавиши.enabled = visible && иконкаСпрайт != null;
        текстКлавиши.enabled = visible && иконкаСпрайт == null;

        RectTransform rt = иконкаКлавиши.rectTransform;
        rt.position = screen;
    }

    private void SetPromptVisible(bool value)
    {
        if (иконкаКлавиши != null)
        {
            иконкаКлавиши.gameObject.SetActive(value);
        }
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

    private void LoadKeyIcon()
    {
        KeyCode actionKey = GameInputBindings.ActionKey;
        KeyCode normalized = NormalizeKey(actionKey);
        Sprite sp = null;
        iconMap.TryGetValue(normalized, out sp);
#if UNITY_EDITOR
        if (sp == null)
        {
            string slug = KeyToSlug(normalized);
            if (!string.IsNullOrEmpty(slug))
            {
                sp = AssetDatabase.LoadAssetAtPath<Sprite>(ИконкиПуть + "key-" + slug + ".png");
            }
        }
#endif
        иконкаСпрайт = sp;
        if (иконкаКлавиши != null)
        {
            иконкаКлавиши.sprite = sp;
            иконкаКлавиши.enabled = sp != null;
        }
        if (текстКлавиши != null)
        {
            текстКлавиши.text = actionKey.ToString();
            текстКлавиши.enabled = sp == null;
        }

        cachedActionKey = actionKey;
    }

    private void RefreshActionKeyIfNeeded()
    {
        if (cachedActionKey != GameInputBindings.ActionKey)
        {
            LoadKeyIcon();
        }
    }

    private void BuildIconMap()
    {
        iconMap.Clear();
        for (int i = 0; i < keyIcons.Count; i++)
        {
            if (keyIcons[i].sprite == null)
            {
                continue;
            }

            KeyCode k = NormalizeKey(keyIcons[i].key);
            if (!iconMap.ContainsKey(k))
            {
                iconMap.Add(k, keyIcons[i].sprite);
            }
        }
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

    private float GetScaledEffectsVolume()
    {
        int effectsLevel = Mathf.Clamp(PlayerPrefs.GetInt(ЭффектыКлюч, 10), 0, 10);
        float effects01 = effectsLevel / 10f;
        return Mathf.Clamp01(громкостьOneShot * effects01);
    }

    private Collider ChooseNearestDoor()
    {
        Collider best = null;
        float bestSqr = float.MaxValue;
        Vector3 p = transform.position;

        foreach (Collider c in двериВТриггере)
        {
            if (c == null)
            {
                continue;
            }

            float sqr = (c.ClosestPoint(p) - p).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = c;
            }
        }

        return best;
    }

    private void TryResolveDoorOverrides(Collider door)
    {
        if (door == null)
        {
            return;
        }

        if (точкаПривязки == null)
        {
            точкаПривязки = door.transform;
        }

        if (рендерерАнимации == null)
        {
            рендерерАнимации = door.GetComponentInChildren<Renderer>();
            if (рендерерАнимации != null)
            {
                runtimeMat = рендерерАнимации.material;
            }
        }

        if (источникЗвука == null)
        {
            источникЗвука = door.GetComponent<AudioSource>();
        }
    }

    private static void SetTexture(Material mat, Texture tex)
    {
        if (mat == null)
        {
            return;
        }
        if (mat.HasProperty(BaseMapId)) mat.SetTexture(BaseMapId, tex);
        if (mat.HasProperty(MainTexId)) mat.SetTexture(MainTexId, tex);
        if (mat.HasProperty(UnlitColorMapId)) mat.SetTexture(UnlitColorMapId, tex);
        if (mat.HasProperty(BaseColorMapId)) mat.SetTexture(BaseColorMapId, tex);
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
            case KeyCode.RightShift: return "shift";
            case KeyCode.LeftControl: return "ctrl";
            case KeyCode.RightControl: return "ctrl";
            case KeyCode.LeftAlt: return "alt";
            case KeyCode.RightAlt: return "alt";
            case KeyCode.Return:
            case KeyCode.KeypadEnter: return "enter";
            case KeyCode.UpArrow: return "arrow-up";
            case KeyCode.DownArrow: return "arrow-down";
            case KeyCode.LeftArrow: return "arrow-left";
            case KeyCode.RightArrow: return "arrow-right";
            default: return null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fpsАнимации = Mathf.Clamp(fpsАнимации, 1f, 30f);
        длительностьНадписи = Mathf.Clamp(длительностьНадписи, 0.3f, 5f);

        if (звукЗаперто == null)
        {
            звукЗаперто = AssetDatabase.LoadAssetAtPath<AudioClip>(ЗвукПоУмолчанию);
        }

        bool hasFrames = кадрыИнтеракт.front != null && кадрыИнтеракт.front.Length > 0;
        hasFrames |= кадрыИнтеракт.frontSide != null && кадрыИнтеракт.frontSide.Length > 0;
        hasFrames |= кадрыИнтеракт.side != null && кадрыИнтеракт.side.Length > 0;
        hasFrames |= кадрыИнтеракт.backSide != null && кадрыИнтеракт.backSide.Length > 0;
        hasFrames |= кадрыИнтеракт.back != null && кадрыИнтеракт.back.Length > 0;

        if (!hasFrames)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { папкаКадров });
            List<string> f = new List<string>();
            List<string> fr = new List<string>();
            List<string> r = new List<string>();
            List<string> br = new List<string>();
            List<string> b = new List<string>();
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(p))
                {
                    continue;
                }

                string name = System.IO.Path.GetFileNameWithoutExtension(p).ToUpperInvariant();
                if (name.StartsWith("INTERACTFR")) fr.Add(p);
                else if (name.StartsWith("INTERACTBR")) br.Add(p);
                else if (name.StartsWith("INTERACTR")) r.Add(p);
                else if (name.StartsWith("INTERACTF")) f.Add(p);
                else if (name.StartsWith("INTERACTB")) b.Add(p);
            }

            f.Sort(System.StringComparer.OrdinalIgnoreCase);
            fr.Sort(System.StringComparer.OrdinalIgnoreCase);
            r.Sort(System.StringComparer.OrdinalIgnoreCase);
            br.Sort(System.StringComparer.OrdinalIgnoreCase);
            b.Sort(System.StringComparer.OrdinalIgnoreCase);

            кадрыИнтеракт.front = LoadTextures(f);
            кадрыИнтеракт.frontSide = LoadTextures(fr);
            кадрыИнтеракт.side = LoadTextures(r);
            кадрыИнтеракт.backSide = LoadTextures(br);
            кадрыИнтеракт.back = LoadTextures(b);
        }

        if (string.IsNullOrWhiteSpace(тегДвери))
        {
            тегДвери = "Door";
        }

        PopulateIconLibrary();
        BuildIconMap();
    }

    private void PopulateIconLibrary()
    {
        EnsureIconEntry(KeyCode.A); EnsureIconEntry(KeyCode.B); EnsureIconEntry(KeyCode.C); EnsureIconEntry(KeyCode.D);
        EnsureIconEntry(KeyCode.E); EnsureIconEntry(KeyCode.F); EnsureIconEntry(KeyCode.G); EnsureIconEntry(KeyCode.H);
        EnsureIconEntry(KeyCode.I); EnsureIconEntry(KeyCode.J); EnsureIconEntry(KeyCode.K); EnsureIconEntry(KeyCode.L);
        EnsureIconEntry(KeyCode.M); EnsureIconEntry(KeyCode.N); EnsureIconEntry(KeyCode.O); EnsureIconEntry(KeyCode.P);
        EnsureIconEntry(KeyCode.Q); EnsureIconEntry(KeyCode.R); EnsureIconEntry(KeyCode.S); EnsureIconEntry(KeyCode.T);
        EnsureIconEntry(KeyCode.U); EnsureIconEntry(KeyCode.V); EnsureIconEntry(KeyCode.W); EnsureIconEntry(KeyCode.X);
        EnsureIconEntry(KeyCode.Y); EnsureIconEntry(KeyCode.Z);
        EnsureIconEntry(KeyCode.Alpha0); EnsureIconEntry(KeyCode.Alpha1); EnsureIconEntry(KeyCode.Alpha2);
        EnsureIconEntry(KeyCode.Alpha3); EnsureIconEntry(KeyCode.Alpha4); EnsureIconEntry(KeyCode.Alpha5);
        EnsureIconEntry(KeyCode.Alpha6); EnsureIconEntry(KeyCode.Alpha7); EnsureIconEntry(KeyCode.Alpha8);
        EnsureIconEntry(KeyCode.Alpha9);
        EnsureIconEntry(KeyCode.Space); EnsureIconEntry(KeyCode.LeftShift); EnsureIconEntry(KeyCode.LeftControl);
        EnsureIconEntry(KeyCode.LeftAlt); EnsureIconEntry(KeyCode.Return);
        EnsureIconEntry(KeyCode.UpArrow); EnsureIconEntry(KeyCode.DownArrow); EnsureIconEntry(KeyCode.LeftArrow); EnsureIconEntry(KeyCode.RightArrow);
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
                entry.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ИконкиПуть + "key-" + slug + ".png");
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

    private static Texture2D[] LoadTextures(List<string> paths)
    {
        if (paths == null || paths.Count == 0)
        {
            return Array.Empty<Texture2D>();
        }

        List<Texture2D> textures = new List<Texture2D>(paths.Count);
        for (int i = 0; i < paths.Count; i++)
        {
            Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
            if (t != null)
            {
                textures.Add(t);
            }
        }
        return textures.ToArray();
    }
#endif
}
