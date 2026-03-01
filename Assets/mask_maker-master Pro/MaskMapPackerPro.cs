using UnityEngine;
using UnityEditor;
using System.IO;

public class MaskMapPackerPro : EditorWindow
{
    // ─── Текстуры ───────────────────────────────────────────────────────────
    private Texture2D tex_Albedo;
    private Texture2D tex_Normal;
    private Texture2D tex_Metallic;
    private Texture2D tex_AO;
    private Texture2D tex_Detail;
    private Texture2D tex_SmoothRough;

    // ─── Значения по умолчанию ──────────────────────────────────────────────
    private float def_Metallic  = 0f;
    private float def_AO        = 1f;
    private float def_Detail    = 0f;
    private float def_Smooth    = 0.5f;

    // ─── Настройки ──────────────────────────────────────────────────────────
    private bool useRoughness    = false;
    private bool autoFixReadable = true;

    // ─── Состояние ──────────────────────────────────────────────────────────
    private Texture2D finalTexture;
    private Material  previewMat;
    private Editor    previewEditor;
    private bool      previewDirty = true;
    private bool      matSetUp     = false;
    private Vector2   scrollPos;
    private string    statusMsg    = "";
    private MessageType statusType = MessageType.None;
    private Vector2Int texSize;

    private static EditorWindow window;

    // ─── Цвета каналов ──────────────────────────────────────────────────────
    private static readonly Color[] ChanColor = {
        new Color(0.90f, 0.30f, 0.30f),
        new Color(0.40f, 0.85f, 0.40f),
        new Color(0.40f, 0.60f, 1.00f),
        new Color(0.85f, 0.85f, 0.85f)
    };
    private static readonly string[] ChanLabel = {
        "R  Металличность",
        "G  Затенение (AO)",
        "B  Маска детализации",
        "A  Гладкость / Шероховатость"
    };
    private static readonly string[] ChanTip = {
        "Общая металличность поверхности.",
        "Запечённое фоновое затенение. Оставьте 1, если карты нет.",
        "Коэффициент смешения деталей.",
        "Карта гладкости или шероховатости (инвертируется автоматически)."
    };

    // ════════════════════════════════════════════════════════════════════════
    [MenuItem("Инструменты/Упаковщик Mask Map %&#p")]
    public static void OpenPackerWindow()
    {
        window = GetWindow<MaskMapPackerPro>("Упаковщик Mask Map");
        window.minSize = new Vector2(420, 600);
    }

    [MenuItem("Assets/Упаковщик Mask Map")]
    public static void OpenPackerWindowFromAssets() => OpenPackerWindow();

    private void OnInspectorUpdate()
    {
        if (!window) window = GetWindow<MaskMapPackerPro>();
    }

    // ════════════════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        GUIStyle sHeader = new GUIStyle(EditorStyles.label)
            { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
        sHeader.normal.textColor = Color.white;

        GUIStyle sSub = new GUIStyle(EditorStyles.miniLabel)
            { alignment = TextAnchor.MiddleCenter, wordWrap = true, richText = true };
        sSub.normal.textColor = new Color(0.65f, 0.65f, 0.65f);

        DrawBanner(sHeader, sSub);

        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
        EditorGUILayout.Space(6);

        DrawOptionsBar();
        EditorGUILayout.Space(4);

        DrawChannel(0, ref tex_Metallic,   ref def_Metallic, 0);
        DrawChannel(1, ref tex_AO,         ref def_AO,       1);
        DrawChannel(2, ref tex_Detail,     ref def_Detail,   2);
        DrawChannelSmoothRough();

        EditorGUILayout.Space(6);
        DrawPreviewSection();
        EditorGUILayout.Space(6);
        DrawPreviewTextures();
        EditorGUILayout.Space(8);
        DrawActionButtons();
        EditorGUILayout.Space(6);

        if (!string.IsNullOrEmpty(statusMsg))
            EditorGUILayout.HelpBox(statusMsg, statusType);

        EditorGUILayout.Space(10);
        EditorGUILayout.EndScrollView();
    }

    // ════════════════════════════════════════════════════════════════════════
    //  UI — блоки
    // ════════════════════════════════════════════════════════════════════════

    private void DrawBanner(GUIStyle sHeader, GUIStyle sSub)
    {
        var r = GUILayoutUtility.GetRect(0, 52, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(0.13f, 0.13f, 0.13f));
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), new Color(0.31f, 0.62f, 1f));
        GUI.Label(new Rect(r.x, r.y + 6,  r.width, 22), "  Упаковщик Mask Map", sHeader);
        GUI.Label(new Rect(r.x, r.y + 28, r.width, 18),
            "Упаковывает Металличность · AO · Детали · Гладкость в один RGBA файл", sSub);
    }

    private void DrawOptionsBar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Настройки", EditorStyles.toolbarButton, GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            autoFixReadable = GUILayout.Toggle(autoFixReadable,
                " Авто-исправление Read/Write и sRGB", EditorStyles.toolbarButton);
            GUILayout.Space(4);
        }
    }

    private void DrawChannel(int idx, ref Texture2D tex, ref float def, int chanIdx)
    {
        Color prevBg = GUI.backgroundColor;
        var c = ChanColor[chanIdx];
        GUI.backgroundColor = new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 1f);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUI.backgroundColor = prevBg;
            EditorGUILayout.Space(2);

            // Заголовок канала
            using (new EditorGUILayout.HorizontalScope())
            {
                var lr = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
                EditorGUI.DrawRect(new Rect(lr.x, lr.y + 2, 10, 10), c);
                GUILayout.Space(4);
                var cs = new GUIStyle(EditorStyles.boldLabel);
                cs.normal.textColor = c;
                GUILayout.Label(ChanLabel[chanIdx], cs);
                GUILayout.FlexibleSpace();
                if (tex) GUILayout.Label($"{tex.width}×{tex.height}", EditorStyles.miniLabel);
            }

            var prev = tex;
            tex = (Texture2D)EditorGUILayout.ObjectField(
                new GUIContent("Текстура", ChanTip[chanIdx]), tex, typeof(Texture2D), false);

            if (tex != prev) { ValidateAndFix(ref tex); previewDirty = true; matSetUp = false; }

            if (!tex)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("По умолчанию:", GUILayout.Width(110));
                    float nd = EditorGUILayout.Slider(def, 0f, 1f);
                    if (!Mathf.Approximately(nd, def)) { def = nd; previewDirty = true; matSetUp = false; }
                }
            }
            EditorGUILayout.Space(2);
        }
        GUI.backgroundColor = prevBg;
    }

    private void DrawChannelSmoothRough()
    {
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.30f, 0.30f, 0.30f, 1f);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUI.backgroundColor = prevBg;
            EditorGUILayout.Space(2);

            using (new EditorGUILayout.HorizontalScope())
            {
                var lr = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
                EditorGUI.DrawRect(new Rect(lr.x, lr.y + 2, 10, 10), ChanColor[3]);
                GUILayout.Space(4);
                var cs = new GUIStyle(EditorStyles.boldLabel);
                cs.normal.textColor = ChanColor[3];
                GUILayout.Label(ChanLabel[3], cs);
                GUILayout.FlexibleSpace();

                bool wasRough = useRoughness;
                useRoughness = GUILayout.Toggle(useRoughness, "Вход — Шероховатость",
                    EditorStyles.miniButton, GUILayout.Width(150));
                if (useRoughness != wasRough) { previewDirty = true; matSetUp = false; }

                if (tex_SmoothRough)
                    GUILayout.Label($"{tex_SmoothRough.width}×{tex_SmoothRough.height}", EditorStyles.miniLabel);
            }

            var prev = tex_SmoothRough;
            tex_SmoothRough = (Texture2D)EditorGUILayout.ObjectField(
                new GUIContent(useRoughness ? "Карта шероховатости" : "Карта гладкости",
                    ChanTip[3]), tex_SmoothRough, typeof(Texture2D), false);

            if (tex_SmoothRough != prev) { ValidateAndFix(ref tex_SmoothRough); previewDirty = true; matSetUp = false; }

            if (!tex_SmoothRough)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("По умолчанию:", GUILayout.Width(110));
                    float nd = EditorGUILayout.Slider(def_Smooth, 0f, 1f);
                    if (!Mathf.Approximately(nd, def_Smooth)) { def_Smooth = nd; previewDirty = true; matSetUp = false; }
                }
            }
            EditorGUILayout.Space(2);
        }
        GUI.backgroundColor = prevBg;
    }

    private void DrawPreviewSection()
    {
        if (!HasAnySource()) return;

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Предпросмотр", EditorStyles.boldLabel);

            if (finalTexture && !previewDirty)
            {
                // Создаём материал один раз
                if (!previewMat)
                {
                    previewMat = new Material(Shader.Find("HDRP/Lit"));
                    if (!previewMat) previewMat = new Material(Shader.Find("Standard"));
                    matSetUp = false;
                }
                if (!matSetUp && previewMat) { SetupPreviewMaterial(); matSetUp = true; }

                if (previewEditor == null && previewMat)
                    previewEditor = Editor.CreateEditor(previewMat);

                if (previewEditor != null)
                {
                    var rect = GUILayoutUtility.GetRect(256, 256, GUILayout.ExpandWidth(true));
                    previewEditor.OnPreviewGUI(rect, EditorStyles.helpBox);
                }

                // Миниатюры каналов
                GUILayout.Space(4);
                EditorGUILayout.LabelField("Упакованные каналы:", EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    string[] cnames = { "R", "G", "B", "A" };
                    foreach (var cn in cnames)
                    {
                        var r = GUILayoutUtility.GetRect(60, 60, GUILayout.Width(60));
                        EditorGUI.DrawPreviewTexture(r, finalTexture);
                        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 14, r.width, 14), new Color(0, 0, 0, 0.6f));
                        GUI.Label(new Rect(r.x, r.yMax - 14, r.width, 14), cn, EditorStyles.centeredGreyMiniLabel);
                        GUILayout.Space(3);
                    }
                }
            }
            else
            {
                var ph = new GUIStyle(EditorStyles.helpBox)
                    { alignment = TextAnchor.MiddleCenter, fixedHeight = 110 };
                ph.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                GUILayout.Box("Нажмите «Обновить предпросмотр» для генерации", ph,
                    GUILayout.ExpandWidth(true));
            }
        }
    }

    private void DrawPreviewTextures()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Текстуры для предпросмотра (необязательно)", EditorStyles.boldLabel);

            var pa = tex_Albedo;
            var pn = tex_Normal;
            tex_Albedo = (Texture2D)EditorGUILayout.ObjectField(
                new GUIContent("Альбедо (диффуз)"), tex_Albedo, typeof(Texture2D), false);
            tex_Normal = (Texture2D)EditorGUILayout.ObjectField(
                new GUIContent("Карта нормалей"), tex_Normal, typeof(Texture2D), false);

            if (tex_Albedo != pa || tex_Normal != pn) matSetUp = false;
        }
    }

    private void DrawActionButtons()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            Color prev = GUI.backgroundColor;

            GUI.backgroundColor = new Color(0.25f, 0.55f, 1.0f);
            if (GUILayout.Button("▶  Обновить предпросмотр", GUILayout.Height(38)))
            {
                SetStatus("Генерация предпросмотра…", MessageType.Info);
                EditorUtility.DisplayProgressBar("Упаковщик Mask Map", "Генерация…", 0.5f);
                BakeTexture();
                EditorUtility.ClearProgressBar();
                previewDirty = false;
                matSetUp     = false;
                if (previewEditor != null) { DestroyImmediate(previewEditor); previewEditor = null; }
                SetStatus("Предпросмотр обновлён.", MessageType.Info);
                Repaint();
            }

            GUI.backgroundColor = new Color(0.25f, 0.80f, 0.45f);
            if (GUILayout.Button("💾  Упаковать и сохранить", GUILayout.Height(38)))
            {
                EditorUtility.DisplayProgressBar("Упаковщик Mask Map", "Упаковка текстур…", 0.3f);
                BakeTexture();
                EditorUtility.DisplayProgressBar("Упаковщик Mask Map", "Сохранение…", 0.8f);
                SaveTexture();
                EditorUtility.ClearProgressBar();
                previewDirty = false;
            }

            GUI.backgroundColor = new Color(0.80f, 0.28f, 0.28f);
            if (GUILayout.Button("✖  Очистить", GUILayout.Height(38), GUILayout.Width(90)))
                ClearAll();

            GUI.backgroundColor = prev;
        }

        // ── Кнопка быстрого сохранения рядом с Albedo ───────────────────────
        {
            bool hasAlbedo = tex_Albedo != null &&
                             !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(tex_Albedo));
            bool canSave   = hasAlbedo && HasAnySource();

            Color prev2 = GUI.backgroundColor;
            GUI.enabled = canSave;
            GUI.backgroundColor = new Color(0.85f, 0.55f, 0.15f);

            string autoSaveLabel = hasAlbedo
                ? "Сохранить рядом с Albedo  ->  " + tex_Albedo.name + "_LitMask.png"
                : "Сохранить рядом с Albedo  (вставьте текстуру в поле Альбедо)";

            if (GUILayout.Button("  " + autoSaveLabel, GUILayout.Height(30)))
            {
                BakeTexture();
                SaveNextToAlbedo();
                previewDirty = false;
                matSetUp = false;
                if (previewEditor != null) { DestroyImmediate(previewEditor); previewEditor = null; }
                Repaint();
            }

            GUI.enabled = true;
            GUI.backgroundColor = prev2;
        }

        if (finalTexture && !previewDirty)
        {
            GUILayout.Space(2);
            EditorGUILayout.LabelField(
                $"Размер выходной текстуры: {finalTexture.width} × {finalTexture.height} пикс.",
                EditorStyles.centeredGreyMiniLabel);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Ядро — запекание
    // ════════════════════════════════════════════════════════════════════════

    private void BakeTexture()
    {
        RecalcSize();
        if (texSize == Vector2Int.zero)
        {
            SetStatus("Нет текстур — нечего запекать.", MessageType.Warning);
            return;
        }

        // Пробуем GPU-путь (требует шейдер Hidden/MaskMapPacker), иначе CPU
        if (!TryGPUBake()) CPUBake();
    }

    private bool TryGPUBake()
    {
        Shader packShader = Shader.Find("Hidden/MaskMapPacker");
        if (packShader == null) return false;

        Material m = new Material(packShader);
        m.SetTexture("_MetallicTex",   GetReadable(tex_Metallic));
        m.SetTexture("_AOTex",         GetReadable(tex_AO));
        m.SetTexture("_DetailTex",     GetReadable(tex_Detail));
        m.SetTexture("_SmoothTex",     GetReadable(tex_SmoothRough));
        m.SetFloat("_DefMetallic",     def_Metallic);
        m.SetFloat("_DefAO",           def_AO);
        m.SetFloat("_DefDetail",       def_Detail);
        m.SetFloat("_DefSmooth",       useRoughness ? 1f - def_Smooth : def_Smooth);
        m.SetFloat("_UseRoughness",    useRoughness ? 1f : 0f);
        m.SetFloat("_HasMetallic",     tex_Metallic    ? 1f : 0f);
        m.SetFloat("_HasAO",           tex_AO          ? 1f : 0f);
        m.SetFloat("_HasDetail",       tex_Detail      ? 1f : 0f);
        m.SetFloat("_HasSmooth",       tex_SmoothRough ? 1f : 0f);

        var rt = RenderTexture.GetTemporary(texSize.x, texSize.y, 0,
            RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        Graphics.Blit(null, rt, m);
        DestroyImmediate(m);

        finalTexture = new Texture2D(texSize.x, texSize.y, TextureFormat.RGBAFloat, false, true);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        finalTexture.ReadPixels(new Rect(0, 0, texSize.x, texSize.y), 0, 0);
        finalTexture.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return true;
    }

    private void CPUBake()
    {
        finalTexture = new Texture2D(texSize.x, texSize.y, TextureFormat.RGBAFloat, true, true);

        // Получаем readable-копию каждой текстуры ОДИН раз
        Texture2D rM = GetReadable(tex_Metallic);
        Texture2D rA = GetReadable(tex_AO);
        Texture2D rD = GetReadable(tex_Detail);
        Texture2D rS = GetReadable(tex_SmoothRough);

        // Читаем все пиксели массивом — намного быстрее, чем GetPixel в цикле
        Color[] mPx = rM != null ? rM.GetPixels() : null;
        Color[] aPx = rA != null ? rA.GetPixels() : null;
        Color[] dPx = rD != null ? rD.GetPixels() : null;
        Color[] sPx = rS != null ? rS.GetPixels() : null;

        int total  = texSize.x * texSize.y;
        var output = new Color[total];

        for (int i = 0; i < total; i++)
        {
            float R = mPx != null ? mPx[i].r : def_Metallic;
            float G = aPx != null ? aPx[i].r : def_AO;
            float B = dPx != null ? dPx[i].r : def_Detail;

            float A;
            if (sPx != null)
                A = useRoughness ? 1f - sPx[i].r : sPx[i].r;
            else
                A = useRoughness ? 1f - def_Smooth : def_Smooth;

            output[i] = new Color(R, G, B, A);
        }

        finalTexture.SetPixels(output);
        finalTexture.Apply();
    }

    private void SaveNextToAlbedo()
    {
        if (finalTexture == null)
        {
            SetStatus("Нечего сохранять — сначала запеките текстуру.", MessageType.Warning);
            return;
        }

        string albedoPath = AssetDatabase.GetAssetPath(tex_Albedo);
        if (string.IsNullOrEmpty(albedoPath))
        {
            SetStatus("Не удалось получить путь Albedo.", MessageType.Error);
            return;
        }

        string dir      = Path.GetDirectoryName(albedoPath);
        string fileName = tex_Albedo.name + "_LitMask.png";
        string savePath = Path.Combine(dir, fileName).Replace("\\", "/");

        // Предупреждение если файл уже существует
        if (File.Exists(savePath))
        {
            if (!EditorUtility.DisplayDialog("Файл уже существует",
                $"'{fileName}' уже существует в папке:\n{dir}\n\nПерезаписать?",
                "Перезаписать", "Отмена"))
                return;
        }

        File.WriteAllBytes(savePath, finalTexture.EncodeToPNG());
        AssetDatabase.Refresh();

        // Настройки импорта
        var imp = AssetImporter.GetAtPath(savePath) as TextureImporter;
        if (imp != null)
        {
            imp.sRGBTexture         = false;
            imp.isReadable          = false;
            imp.mipmapEnabled       = true;
            imp.textureType         = TextureImporterType.Default;
            imp.alphaIsTransparency = false;
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();
        }

        AssetDatabase.Refresh();

        // Выделяем сохранённый файл в Project
        var saved = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
        if (saved != null) EditorGUIUtility.PingObject(saved);

        SetStatus($"Сохранено рядом с Albedo: {savePath}", MessageType.Info);
        Debug.Log("[MaskMapPackerPro] Сохранено: " + savePath);
    }

    private void SaveTexture()
    {
        if (finalTexture == null)
        {
            SetStatus("Нечего сохранять — сначала запеките текстуру.", MessageType.Warning);
            return;
        }

        string path = EditorUtility.SaveFilePanelInProject(
            "Сохранить Mask Map", "MaskMap", "png", "Выберите папку для сохранения");
        if (string.IsNullOrEmpty(path)) { SetStatus("Сохранение отменено.", MessageType.Info); return; }

        File.WriteAllBytes(path, finalTexture.EncodeToPNG());
        AssetDatabase.Refresh();

        // Настраиваем импорт: линейное пространство, без сжатия альфы
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.sRGBTexture         = false;
            imp.isReadable          = false;
            imp.mipmapEnabled       = true;
            imp.textureType         = TextureImporterType.Default;
            imp.alphaIsTransparency = false;
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();
        }

        AssetDatabase.Refresh();
        SetStatus($"Сохранено: {path}", MessageType.Info);
        Debug.Log("[MaskMapPackerPro] Сохранено: " + path);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Вспомогательные методы
    // ════════════════════════════════════════════════════════════════════════

    private void SetupPreviewMaterial()
    {
        if (!previewMat) return;
        if (tex_Albedo)
        {
            if (previewMat.HasProperty("_BaseColorMap")) previewMat.SetTexture("_BaseColorMap", tex_Albedo);
            if (previewMat.HasProperty("_MainTex"))      previewMat.SetTexture("_MainTex",      tex_Albedo);
        }
        if (tex_Normal)
        {
            previewMat.EnableKeyword("_NORMALMAP");
            previewMat.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
            if (previewMat.HasProperty("_NormalMap")) previewMat.SetTexture("_NormalMap", tex_Normal);
            if (previewMat.HasProperty("_BumpMap"))   previewMat.SetTexture("_BumpMap",   tex_Normal);
        }
        if (finalTexture)
        {
            if (previewMat.HasProperty("_MaskMap"))         { previewMat.EnableKeyword("_MASKMAP"); previewMat.SetTexture("_MaskMap", finalTexture); }
            if (previewMat.HasProperty("_MetallicGlossMap"))  previewMat.SetTexture("_MetallicGlossMap", finalTexture);
        }
    }

    /// <summary>Возвращает CPU-читаемую копию текстуры (или null).</summary>
    private Texture2D GetReadable(Texture2D source)
    {
        if (source == null) return null;

        var rw = source.isDataSRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBFloat, rw);
        Graphics.Blit(source, rt);

        var prev  = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBAFloat, false);
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }

    /// <summary>Проверяет совпадение размера и автоматически исправляет настройки импорта.</summary>
    private void ValidateAndFix(ref Texture2D tex)
    {
        if (tex == null) return;

        // Автоисправление Read/Write и sRGB
        if (autoFixReadable)
        {
            string assetPath = AssetDatabase.GetAssetPath(tex);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (imp != null)
                {
                    bool dirty = false;
                    if (!imp.isReadable)  { imp.isReadable = true;   dirty = true; }
                    if (imp.sRGBTexture)  { imp.sRGBTexture = false; dirty = true; }
                    if (imp.textureType != TextureImporterType.Default)
                        { imp.textureType = TextureImporterType.Default; dirty = true; }
                    if (dirty)
                    {
                        imp.SaveAndReimport();
                        SetStatus($"Авто-исправлено: '{tex.name}' — Read/Write вкл., sRGB выкл.", MessageType.Info);
                    }
                }
            }
        }

        // Проверка размера
        RecalcSize();
        if (texSize != Vector2Int.zero && texSize != new Vector2Int(tex.width, tex.height))
        {
            SetStatus($"Несовпадение размеров: ожидается {texSize.x}×{texSize.y}, получено {tex.width}×{tex.height}. Текстура отклонена.", MessageType.Error);
            tex = null;
        }
    }

    private void RecalcSize()
    {
        texSize = Vector2Int.zero;
        foreach (var t in new[] { tex_Metallic, tex_AO, tex_Detail, tex_SmoothRough })
            if (t) { texSize = new Vector2Int(t.width, t.height); break; }
    }

    private bool HasAnySource() =>
        tex_Metallic || tex_AO || tex_Detail || tex_SmoothRough;

    private void ClearAll()
    {
        tex_Albedo = tex_Normal = tex_Metallic = tex_AO = tex_Detail = tex_SmoothRough = null;
        finalTexture = null;
        previewMat   = null;
        if (previewEditor != null) { DestroyImmediate(previewEditor); previewEditor = null; }
        matSetUp     = false;
        previewDirty = true;
        texSize      = Vector2Int.zero;
        statusMsg    = "";
        SetStatus("", MessageType.None);
    }

    private void SetStatus(string msg, MessageType type) { statusMsg = msg; statusType = type; }
}