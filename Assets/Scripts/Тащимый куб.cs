using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Rigidbody))]
public class ТащимыйКуб : MonoBehaviour
{
    private const string IconBasePath = "Assets/ICONS/Controls/keyboard-mouse-input-icons-251008/keyboard-input-icons/";

    [Header("Детект игрока")]
    [SerializeField] private string тегИгрока = "Player";
    [SerializeField] private Collider триггерЗона;

    [Header("Тяга")]
    [SerializeField] private float дистанцияДоИгрока = 1.2f;
    [SerializeField] private float высотаТочкиТяги = 0.1f;
    [SerializeField] private float базовоеУскорениеТяги = 24f;
    [SerializeField] private float базоваяМаксСкорость = 2.3f;
    [SerializeField] private float множительБега = 1.35f;
    [SerializeField] private float максДистанцияРазрыва = 3f;

    [Header("Прыжковый рывок")]
    [SerializeField] private float порогМассыДляРывка = 18f;
    [SerializeField] private float силаРывкаПрыжком = 2.2f;

    [Header("Стабилизация")]
    [SerializeField] private bool блокироватьНаклонПриТяге = true;
    [SerializeField] private float демпферВращения = 12f;

    [Header("Звук по полу")]
    [SerializeField] private bool использоватьПроцедурныйСкрежет = true;
    [SerializeField] private СкрежетТяги процедурныйСкрежет;
    [SerializeField] private AudioSource источникЗвука;
    [SerializeField] private AudioClip[] звукиСкольжения;
    [SerializeField] private float интервалЗвука = 0.16f;
    [SerializeField] private float минимумСкоростиДляЗвука = 0.15f;
    [SerializeField] [Range(0f, 1f)] private float громкостьЗвука = 0.65f;

    [Header("UI Подсказка")]
    [SerializeField] private Vector3 смещениеИконки = new Vector3(0f, 1.15f, 0f);
    [SerializeField] private Vector2 размерИконки = new Vector2(86f, 86f);
    [SerializeField] private Font fallbackFont;
    [SerializeField] private int fallbackFontSize = 20;

    [Header("Дымка")]
    [SerializeField] private ParticleSystem мягкаяДымка;
    [SerializeField] private float задержкаДымки = 1.1f;
    [SerializeField] private List<KeyIconEntry> keyIcons = new List<KeyIconEntry>();

    [Header("Debug")]
    [SerializeField] private bool debugLogs;
    [SerializeField] private float debugInterval = 0.25f;

    private Rigidbody rb;
    private Transform игрок;
    private Rigidbody rbИгрока;
    private PlayerWASDAnimator аниматорИгрока;
    private bool игрокВнутриТриггера;
    private bool тащится;
    private bool блокПовторногоЗахватаДоОтпуска;
    private bool владеетЛимитомСкорости;
    private float таймерЗвука;
    private bool прошлыйКадрПрыжок;
    private float таймерТяги;

    private Canvas canvas;
    private Image иконка;
    private Text текстКлавиши;
    private Sprite иконкаСпрайт;
    private KeyCode кешКнопки = KeyCode.None;
    private readonly Dictionary<KeyCode, Sprite> iconMap = new Dictionary<KeyCode, Sprite>();
    private float debugNextTime;
    private RigidbodyConstraints исходныеОграничения;
    private static ТащимыйКуб activeDragCube;

    private void Awake()
    {
        GameInputBindings.EnsureLoaded();
        rb = GetComponent<Rigidbody>();
        исходныеОграничения = rb.constraints;

        if (триггерЗона == null)
        {
            Collider[] colliders = GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].isTrigger)
                {
                    триггерЗона = colliders[i];
                    break;
                }
            }
        }

        if (источникЗвука == null)
        {
            источникЗвука = GetComponent<AudioSource>();
        }
        if (процедурныйСкрежет == null)
        {
            процедурныйСкрежет = GetComponent<СкрежетТяги>();
        }

        BuildUi();
        BuildIconMap();
        UpdatePromptKeyVisual(true);
        SetPromptVisible(false);
    }

    private void Update()
    {
        UpdatePromptKeyVisual(false);
        UpdatePromptPosition();

        if (GameInputBindings.InputBlocked)
        {
            StopDragging();
            return;
        }

        if (игрок == null)
        {
            if (тащится && debugLogs)
            {
                Debug.Log("[ТащимыйКуб] stop: игрок не рядом или null.", this);
            }
            игрокВнутриТриггера = false;
            StopDragging();
            return;
        }

        bool holdAction = Input.GetKey(GameInputBindings.ActionKey);
        if (!holdAction && блокПовторногоЗахватаДоОтпуска)
        {
            блокПовторногоЗахватаДоОтпуска = false;
            if (debugLogs)
            {
                Debug.Log("[ТащимыйКуб] re-grab unlocked after key release.", this);
            }
        }

        if (!игрокВнутриТриггера && !тащится)
        {
            return;
        }

        // Only one cube can be dragged at a time.
        if (activeDragCube != null && activeDragCube != this)
        {
            if (тащится)
            {
                StopDragging();
            }
            return;
        }

        if (блокПовторногоЗахватаДоОтпуска)
        {
            if (тащится)
            {
                StopDragging();
            }
            return;
        }

        if (holdAction)
        {
            if (!тащится && debugLogs)
            {
                Debug.Log($"[ТащимыйКуб] start drag. key={GameInputBindings.ActionKey}", this);
            }
            тащится = true;
            activeDragCube = this;
            ApplyDragConstraints(true);
            SetPromptVisible(false);
        }
        else
        {
            if (тащится && debugLogs)
            {
                Debug.Log("[ТащимыйКуб] stop: action key released.", this);
            }
            StopDragging();
            SetPromptVisible(true);
        }
    }

    private void FixedUpdate()
    {
        if (!тащится || игрок == null)
        {
            таймерЗвука = 0f;
            прошлыйКадрПрыжок = false;
            таймерТяги = 0f;
            UpdateDust(false);
            UpdateProceduralScrape(false, 0f, false);
            return;
        }

        Vector3 playerPos = игрок.position + Vector3.up * высотаТочкиТяги;
        Vector3 cubeToPlayer = playerPos - rb.position;
        cubeToPlayer.y = 0f;
        float distToPlayer = cubeToPlayer.magnitude;
        if (distToPlayer > максДистанцияРазрыва)
        {
            if (debugLogs)
            {
                Debug.Log($"[ТащимыйКуб] stop: distance break. dist={distToPlayer:F2}, max={максДистанцияРазрыва:F2}", this);
            }
            блокПовторногоЗахватаДоОтпуска = true;
            StopDragging();
            return;
        }

        Vector3 dirToPlayer = distToPlayer > 0.001f ? (cubeToPlayer / distToPlayer) : Vector3.zero;

        bool running = !GameInputBindings.RunLocked && Input.GetKey(GameInputBindings.RunKey);
        Vector2 moveInput = ReadMoveInput();
        bool hasMoveInput = moveInput.sqrMagnitude > 0.001f;

        float massFactor = Mathf.Max(0.35f, rb.mass);
        float targetMaxSpeed = (базоваяМаксСкорость / Mathf.Sqrt(massFactor)) * (running ? множительБега : 1f);

        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float pullScaleByDistance = Mathf.Clamp01(distToPlayer / Mathf.Max(0.25f, дистанцияДоИгрока + 0.2f));
        float minPullScale = hasMoveInput ? 0.35f : 0f;
        if (hasMoveInput && distToPlayer < дистанцияДоИгрока * 0.9f)
        {
            minPullScale = 0.18f;
        }
        float pullScale = hasMoveInput ? Mathf.Max(minPullScale, pullScaleByDistance) : 0f;
        Vector3 desiredVel = dirToPlayer * (targetMaxSpeed * pullScale);
        Vector3 dv = desiredVel - horizontalVel;

        float accel = базовоеУскорениеТяги * (running ? 1.25f : 1f);
        Vector3 accelVec = Vector3.ClampMagnitude(dv / Mathf.Max(Time.fixedDeltaTime, 0.0001f), accel);
        rb.AddForce(new Vector3(accelVec.x, 0f, accelVec.z), ForceMode.Acceleration);
        if (блокироватьНаклонПриТяге)
        {
            rb.angularVelocity *= 1f / (1f + демпферВращения * Time.fixedDeltaTime);
        }
        rb.WakeUp();
        ApplyPlayerSpeedSync(targetMaxSpeed, horizontalVel.magnitude, distToPlayer);

        if (debugLogs && Time.unscaledTime >= debugNextTime)
        {
            debugNextTime = Time.unscaledTime + Mathf.Max(0.05f, debugInterval);
            Debug.Log(
                $"[ТащимыйКуб] drag tick: dist={distToPlayer:F2}, mass={rb.mass:F2}, run={running}, " +
                $"v={horizontalVel.magnitude:F2}, targetV={desiredVel.magnitude:F2}, accel={accelVec.magnitude:F2}",
                this);
        }

        bool jumpPressed = Input.GetKey(GameInputBindings.JumpKey);
        bool jumpJustPressed = jumpPressed && !прошлыйКадрПрыжок;
        прошлыйКадрПрыжок = jumpPressed;
        if (jumpJustPressed && rb.mass <= порогМассыДляРывка)
        {
            rb.AddForce(new Vector3(dirToPlayer.x, 0f, dirToPlayer.z) * силаРывкаПрыжком, ForceMode.Impulse);
            if (debugLogs)
            {
                Debug.Log($"[ТащимыйКуб] jump impulse applied. mass={rb.mass:F2}, impulse={силаРывкаПрыжком:F2}", this);
            }
        }

        bool draggingMoving = hasMoveInput && horizontalVel.magnitude > минимумСкоростиДляЗвука;
        if (draggingMoving)
        {
            таймерТяги += Time.fixedDeltaTime;
        }
        else
        {
            таймерТяги = 0f;
        }

        UpdateDust(таймерТяги >= задержкаДымки);
        UpdateProceduralScrape(draggingMoving, horizontalVel.magnitude, running);
        if (!использоватьПроцедурныйСкрежет)
        {
            PlayDragSound(horizontalVel.magnitude);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other == null || !other.CompareTag(тегИгрока))
        {
            return;
        }

        игрок = other.transform;
        rbИгрока = other.attachedRigidbody;
        if (rbИгрока != null)
        {
            игрок = rbИгрока.transform;
        }
        if (аниматорИгрока == null && игрок != null)
        {
            аниматорИгрока = игрок.GetComponent<PlayerWASDAnimator>();
        }
        игрокВнутриТриггера = true;
        if (debugLogs)
        {
            Debug.Log($"[ТащимыйКуб] trigger enter by '{other.name}'. rbPlayer={(rbИгрока != null)}", this);
        }
        if (!Input.GetKey(GameInputBindings.ActionKey) && (activeDragCube == null || activeDragCube == this))
        {
            SetPromptVisible(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other == null || игрок == null)
        {
            return;
        }

        Transform otherRoot = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
        if (otherRoot != игрок)
        {
            return;
        }

        игрокВнутриТриггера = false;
        bool keepSticky = тащится && Input.GetKey(GameInputBindings.ActionKey);
        if (keepSticky)
        {
            if (debugLogs)
            {
                Debug.Log($"[ТащимыйКуб] trigger exit, sticky hold active for '{other.name}'.", this);
            }
            return;
        }

        игрок = null;
        rbИгрока = null;
        if (debugLogs)
        {
            Debug.Log($"[ТащимыйКуб] trigger exit by '{other.name}', released.", this);
        }
        StopDragging();
        SetPromptVisible(false);
    }

    private void OnDisable()
    {
        // Safety cleanup in case object gets disabled mid-drag.
        StopDragging();
        игрок = null;
        rbИгрока = null;
        игрокВнутриТриггера = false;
        блокПовторногоЗахватаДоОтпуска = false;
    }

    private void OnDestroy()
    {
        StopDragging();
    }

    private void StopDragging()
    {
        if (activeDragCube == this)
        {
            activeDragCube = null;
        }

        if (владеетЛимитомСкорости && аниматорИгрока != null)
        {
            аниматорИгрока.ClearExternalSpeedCap();
            владеетЛимитомСкорости = false;
        }
        if (!игрокВнутриТриггера)
        {
            игрок = null;
            rbИгрока = null;
            SetPromptVisible(false);
        }
        ApplyDragConstraints(false);
        тащится = false;
        прошлыйКадрПрыжок = false;
        таймерТяги = 0f;
        UpdateDust(false);
        UpdateProceduralScrape(false, 0f, false);
    }

    private void ApplyPlayerSpeedSync(float cubeTargetSpeed, float cubeActualSpeed, float distanceToPlayer)
    {
        if (аниматорИгрока == null)
        {
            return;
        }

        // Hard anti-runaway sync:
        // 1) primarily follow actual cube speed
        // 2) when player is already too far, clamp to 0 until cube catches up
        float softLimitDistance = дистанцияДоИгрока + 0.22f;
        if (distanceToPlayer > softLimitDistance)
        {
            аниматорИгрока.SetExternalSpeedCap(0f);
            владеетЛимитомСкорости = true;
            return;
        }

        float byActual = cubeActualSpeed * 0.9f + 0.03f;
        float byTarget = cubeTargetSpeed * 0.8f;
        float cap = Mathf.Min(byActual, byTarget);
        cap = Mathf.Clamp(cap, 0f, cubeTargetSpeed);
        аниматорИгрока.SetExternalSpeedCap(cap);
        владеетЛимитомСкорости = true;
    }

    private void ApplyDragConstraints(bool dragging)
    {
        if (rb == null || !блокироватьНаклонПриТяге)
        {
            return;
        }

        if (dragging)
        {
            rb.constraints = исходныеОграничения | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
        else
        {
            rb.constraints = исходныеОграничения;
        }
    }

    private void PlayDragSound(float speed)
    {
        if (источникЗвука == null || звукиСкольжения == null || звукиСкольжения.Length == 0)
        {
            return;
        }

        bool grounded = Physics.Raycast(rb.worldCenterOfMass, Vector3.down, 0.65f, ~0, QueryTriggerInteraction.Ignore);
        if (!grounded || speed < минимумСкоростиДляЗвука)
        {
            таймерЗвука = 0f;
            return;
        }

        таймерЗвука += Time.fixedDeltaTime;
        if (таймерЗвука < интервалЗвука)
        {
            return;
        }

        таймерЗвука = 0f;
        int i = Random.Range(0, звукиСкольжения.Length);
        AudioClip clip = звукиСкольжения[i];
        if (clip == null)
        {
            return;
        }

        float effectsScale = Mathf.Clamp01(PlayerPrefs.GetInt("PauseSimple.volume_effects", 10) / 10f);
        float vol = Mathf.Clamp01(громкостьЗвука * effectsScale * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(speed / 2.5f)));
        источникЗвука.pitch = Random.Range(0.96f, 1.04f);
        источникЗвука.PlayOneShot(clip, vol);
    }

    private void UpdateProceduralScrape(bool dragging, float speed, bool running)
    {
        if (!использоватьПроцедурныйСкрежет || процедурныйСкрежет == null)
        {
            return;
        }

        процедурныйСкрежет.SetDragState(dragging, speed, running, rb != null ? rb.mass : 1f);
    }

    private void UpdateDust(bool active)
    {
        if (мягкаяДымка == null)
        {
            return;
        }

        if (active)
        {
            if (!мягкаяДымка.isPlaying)
            {
                мягкаяДымка.Play();
            }
        }
        else if (мягкаяДымка.isPlaying)
        {
            мягкаяДымка.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("Drag Prompt Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1650;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject iconGo = new GameObject("ActionIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(canvasGo.transform, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.sizeDelta = размерИконки;

        иконка = iconGo.GetComponent<Image>();
        иконка.preserveAspect = true;

        GameObject txtGo = new GameObject("ActionText", typeof(RectTransform), typeof(Text));
        txtGo.transform.SetParent(iconGo.transform, false);
        RectTransform tr = txtGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;

        Text t = txtGo.GetComponent<Text>();
        t.font = ResolveFont();
        t.fontSize = Mathf.Max(8, fallbackFontSize);
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        текстКлавиши = t;
    }

    private void UpdatePromptPosition()
    {
        if (иконка == null || !иконка.gameObject.activeSelf)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector3 screen = cam.WorldToScreenPoint(transform.position + смещениеИконки);
        bool visible = screen.z > 0.05f;
        иконка.enabled = visible && иконкаСпрайт != null;
        текстКлавиши.enabled = visible && иконкаСпрайт == null;
        иконка.rectTransform.position = screen;
    }

    private void SetPromptVisible(bool visible)
    {
        if (иконка != null)
        {
            иконка.gameObject.SetActive(visible);
        }
    }

    private void UpdatePromptKeyVisual(bool force)
    {
        KeyCode actionKey = GameInputBindings.ActionKey;
        if (!force && actionKey == кешКнопки)
        {
            return;
        }

        кешКнопки = actionKey;
        иконкаСпрайт = LoadKeyIcon(actionKey);
        if (иконка != null)
        {
            иконка.sprite = иконкаСпрайт;
            иконка.enabled = иконкаСпрайт != null;
        }

        if (текстКлавиши != null)
        {
            текстКлавиши.text = actionKey.ToString().ToUpperInvariant();
            текстКлавиши.enabled = иконкаСпрайт == null;
        }
    }

    private Sprite LoadKeyIcon(KeyCode key)
    {
        KeyCode normalized = NormalizeKey(key);
        if (iconMap.TryGetValue(normalized, out Sprite icon) && icon != null)
        {
            return icon;
        }
#if UNITY_EDITOR
        string slug = KeyToSlug(normalized);
        if (!string.IsNullOrEmpty(slug))
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(IconBasePath + "key-" + slug + ".png");
        }
#endif
        return null;
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
            case KeyCode.LeftShift:
            case KeyCode.RightShift: return "shift";
            case KeyCode.LeftControl:
            case KeyCode.RightControl: return "ctrl";
            case KeyCode.LeftAlt:
            case KeyCode.RightAlt: return "alt";
            case KeyCode.Return:
            case KeyCode.KeypadEnter: return "enter";
            default: return null;
        }
    }

    [System.Serializable]
    private struct KeyIconEntry
    {
        public KeyCode key;
        public Sprite sprite;
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

    private Font ResolveFont()
    {
        if (fallbackFont != null)
        {
            return fallbackFont;
        }
#if UNITY_EDITOR
        fallbackFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Press_Start_2P/PressStart2P-Regular.ttf");
#endif
        return fallbackFont != null ? fallbackFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnValidate()
    {
        интервалЗвука = Mathf.Clamp(интервалЗвука, 0.05f, 0.6f);
        минимумСкоростиДляЗвука = Mathf.Clamp(минимумСкоростиДляЗвука, 0.01f, 2f);
        дистанцияДоИгрока = Mathf.Clamp(дистанцияДоИгрока, 0.6f, 2.5f);
        максДистанцияРазрыва = Mathf.Max(1.1f, максДистанцияРазрыва);
        базоваяМаксСкорость = Mathf.Max(0.1f, базоваяМаксСкорость);
        базовоеУскорениеТяги = Mathf.Max(0.1f, базовоеУскорениеТяги);
        множительБега = Mathf.Max(1f, множительБега);
        debugInterval = Mathf.Clamp(debugInterval, 0.05f, 2f);
        демпферВращения = Mathf.Clamp(демпферВращения, 0f, 60f);
        задержкаДымки = Mathf.Clamp(задержкаДымки, 0f, 10f);
#if UNITY_EDITOR
        PopulateIconLibrary();
        BuildIconMap();
#endif
    }

    private static Vector2 ReadMoveInput()
    {
        float x = 0f;
        float y = 0f;
        if (Input.GetKey(GameInputBindings.LeftKey)) x -= 1f;
        if (Input.GetKey(GameInputBindings.RightKey)) x += 1f;
        if (Input.GetKey(GameInputBindings.ForwardKey)) y += 1f;
        if (Input.GetKey(GameInputBindings.BackwardKey)) y -= 1f;
        return new Vector2(x, y);
    }

#if UNITY_EDITOR
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

