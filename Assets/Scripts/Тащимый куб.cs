using UnityEngine;
using UnityEngine.UI;
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

    [Header("Debug")]
    [SerializeField] private bool debugLogs;
    [SerializeField] private float debugInterval = 0.25f;

    private Rigidbody rb;
    private Transform игрок;
    private Rigidbody rbИгрока;
    private PlayerWASDAnimator аниматорИгрока;
    private bool игрокРядом;
    private bool игрокВнутриТриггера;
    private bool тащится;
    private bool блокПовторногоЗахватаДоОтпуска;
    private float таймерЗвука;
    private bool прошлыйКадрПрыжок;

    private Canvas canvas;
    private Image иконка;
    private Text текстКлавиши;
    private Sprite иконкаСпрайт;
    private KeyCode кешКнопки = KeyCode.None;
    private float debugNextTime;
    private RigidbodyConstraints исходныеОграничения;

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

        BuildUi();
        UpdatePromptKeyVisual(true);
        SetPromptVisible(false);
    }

    private void Update()
    {
        UpdatePromptKeyVisual(false);
        UpdatePromptPosition();

        if (игрок == null)
        {
            if (тащится && debugLogs)
            {
                Debug.Log("[ТащимыйКуб] stop: игрок не рядом или null.", this);
            }
            игрокРядом = false;
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
            игрокРядом = false;
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
        float massFactor = Mathf.Max(0.35f, rb.mass);
        float targetMaxSpeed = (базоваяМаксСкорость / Mathf.Sqrt(massFactor)) * (running ? множительБега : 1f);

        Vector3 horizontalVel = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        float pullScaleByDistance = Mathf.Clamp01(distToPlayer / Mathf.Max(0.25f, дистанцияДоИгрока + 0.2f));
        float minPullScale = 0.35f;
        if (distToPlayer < дистанцияДоИгрока * 0.9f)
        {
            minPullScale = 0.18f;
        }
        Vector3 desiredVel = dirToPlayer * (targetMaxSpeed * Mathf.Max(minPullScale, pullScaleByDistance));
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

        PlayDragSound(horizontalVel.magnitude);
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
        игрокРядом = true;
        игрокВнутриТриггера = true;
        if (debugLogs)
        {
            Debug.Log($"[ТащимыйКуб] trigger enter by '{other.name}'. rbPlayer={(rbИгрока != null)}", this);
        }
        if (!Input.GetKey(GameInputBindings.ActionKey))
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

        игрокРядом = false;
        игрок = null;
        rbИгрока = null;
        if (debugLogs)
        {
            Debug.Log($"[ТащимыйКуб] trigger exit by '{other.name}', released.", this);
        }
        StopDragging();
        SetPromptVisible(false);
    }

    private void StopDragging()
    {
        if (аниматорИгрока != null)
        {
            аниматорИгрока.ClearExternalSpeedCap();
        }
        if (!игрокВнутриТриггера)
        {
            игрок = null;
            rbИгрока = null;
            игрокРядом = false;
            SetPromptVisible(false);
        }
        ApplyDragConstraints(false);
        тащится = false;
        прошлыйКадрПрыжок = false;
    }

    private void ApplyPlayerSpeedSync(float cubeTargetSpeed, float cubeActualSpeed, float distanceToPlayer)
    {
        if (аниматорИгрока == null)
        {
            return;
        }

        // Sync to real cube speed so player cannot run away when heavy cube accelerates slowly.
        float byActual = cubeActualSpeed + 0.12f + Mathf.Clamp01(distanceToPlayer / Mathf.Max(0.2f, максДистанцияРазрыва)) * 0.12f;
        float byTarget = cubeTargetSpeed * 0.95f;
        float cap = Mathf.Min(byActual, byTarget);
        cap = Mathf.Clamp(cap, 0.08f, cubeTargetSpeed);
        аниматорИгрока.SetExternalSpeedCap(cap);
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
#if UNITY_EDITOR
        string slug = KeyToSlug(key);
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
        debugInterval = Mathf.Clamp(debugInterval, 0.05f, 2f);
        демпферВращения = Mathf.Clamp(демпферВращения, 0f, 60f);
    }
}
