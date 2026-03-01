using UnityEngine;
using UnityEditor;
using System.IO;

public class AlphaPacker : EditorWindow
{
    private Texture2D tex_Color;
    private Texture2D tex_Opacity;

    private bool  invertOpacity   = false;
    private bool  autoFixReadable = true;
    private int   opacityChannel  = 0;
    private float opacityDefault  = 1f;

    private Texture2D   finalTexture;
    private bool        previewDirty = true;
    private Vector2     scrollPos;
    private string      statusMsg  = "";
    private MessageType statusType = MessageType.None;
    private Vector2Int  texSize;

    private static readonly string[] ChannelOptions = { "R", "G", "B", "Luminance (RGB)" };

    [MenuItem("Инструменты/Упаковщик Alpha (Color + Opacity)")]
    public static void OpenWindow()
    {
        var w = GetWindow<AlphaPacker>("Упаковщик Alpha");
        w.minSize = new Vector2(400, 520);
    }

    [MenuItem("Assets/Упаковщик Alpha (Color + Opacity)")]
    public static void OpenWindowAssets() => OpenWindow();

    private void OnGUI()
    {
        DrawBanner();
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
        EditorGUILayout.Space(6);
        DrawOptionsBar();
        EditorGUILayout.Space(4);
        DrawColorSlot();
        EditorGUILayout.Space(4);
        DrawOpacitySlot();
        EditorGUILayout.Space(8);
        DrawPreview();
        EditorGUILayout.Space(6);
        DrawActionButtons();
        EditorGUILayout.Space(6);
        if (!string.IsNullOrEmpty(statusMsg))
            EditorGUILayout.HelpBox(statusMsg, statusType);
        EditorGUILayout.Space(10);
        EditorGUILayout.EndScrollView();
    }

    private void DrawBanner()
    {
        var r = GUILayoutUtility.GetRect(0, 52, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(0.11f, 0.11f, 0.13f));
        EditorGUI.DrawRect(new Rect(r.x, r.yMax - 2, r.width, 2), new Color(0.6f, 0.35f, 1f));

        var sTitle = new GUIStyle(EditorStyles.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        sTitle.normal.textColor = Color.white;
        var sSub = new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
        sSub.normal.textColor = new Color(0.6f, 0.6f, 0.65f);

        GUI.Label(new Rect(r.x, r.y + 6,  r.width, 22), "  Упаковщик Alpha", sTitle);
        GUI.Label(new Rect(r.x, r.y + 28, r.width, 18), "Вставляет Opacity в Alpha-канал Color  ->  итоговый RGBA PNG", sSub);
    }

    private void DrawOptionsBar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Настройки", EditorStyles.toolbarButton, GUILayout.Width(80));
            GUILayout.FlexibleSpace();
            autoFixReadable = GUILayout.Toggle(autoFixReadable, " Авто Read/Write", EditorStyles.toolbarButton);
            GUILayout.Space(4);
        }
    }

    private void DrawColorSlot()
    {
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.28f, 0.20f, 0.38f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUI.backgroundColor = prevBg;
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                var lr = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
                EditorGUI.DrawRect(new Rect(lr.x, lr.y + 2, 10, 10), new Color(0.95f, 0.55f, 0.95f));
                GUILayout.Space(4);
                var cs = new GUIStyle(EditorStyles.boldLabel);
                cs.normal.textColor = new Color(0.95f, 0.65f, 1f);
                GUILayout.Label("RGB  Color Map", cs);
                GUILayout.FlexibleSpace();
                if (tex_Color) GUILayout.Label(tex_Color.width + "x" + tex_Color.height, EditorStyles.miniLabel);
            }

            var prev = tex_Color;
            tex_Color = (Texture2D)EditorGUILayout.ObjectField(
                new GUIContent("Color Map", "Цветная текстура — RGB каналы итогового файла."),
                tex_Color, typeof(Texture2D), false);
            if (tex_Color != prev) { ValidateAndFix(ref tex_Color); previewDirty = true; }

            if (!tex_Color)
                GUILayout.Label("Вставьте цветную текстуру", EditorStyles.centeredGreyMiniLabel);

            EditorGUILayout.Space(2);
        }
        GUI.backgroundColor = prevBg;
    }

    private void DrawOpacitySlot()
    {
        Color prevBg = GUI.backgroundColor;
        GUI.backgroundColor = new Color(0.16f, 0.26f, 0.20f);
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            GUI.backgroundColor = prevBg;
            EditorGUILayout.Space(2);
            using (new EditorGUILayout.HorizontalScope())
            {
                var lr = GUILayoutUtility.GetRect(12, 12, GUILayout.Width(12));
                EditorGUI.DrawRect(new Rect(lr.x, lr.y + 2, 10, 10), new Color(0.5f, 1f, 0.6f));
                GUILayout.Space(4);
                var cs = new GUIStyle(EditorStyles.boldLabel);
                cs.normal.textColor = new Color(0.6f, 1f, 0.7f);
                GUILayout.Label("A  Opacity Map", cs);
                GUILayout.FlexibleSpace();
                if (tex_Opacity) GUILayout.Label(tex_Opacity.width + "x" + tex_Opacity.height, EditorStyles.miniLabel);
            }

            var prev = tex_Opacity;
            tex_Opacity = (Texture2D)EditorGUILayout.ObjectField(
                new GUIContent("Opacity Map", "Grayscale-маска — попадает в Alpha канал итогового файла."),
                tex_Opacity, typeof(Texture2D), false);
            if (tex_Opacity != prev) { ValidateAndFix(ref tex_Opacity); previewDirty = true; }

            if (!tex_Opacity)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("По умолчанию:", GUILayout.Width(110));
                    float nd = EditorGUILayout.Slider(opacityDefault, 0f, 1f);
                    if (!Mathf.Approximately(nd, opacityDefault)) { opacityDefault = nd; previewDirty = true; }
                }
            }
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label("Читать канал:", GUILayout.Width(100));
                    int nc = EditorGUILayout.Popup(opacityChannel, ChannelOptions);
                    if (nc != opacityChannel) { opacityChannel = nc; previewDirty = true; }
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("Инвертировать Alpha:", GUILayout.Width(130));
                bool ni = EditorGUILayout.Toggle(invertOpacity);
                if (ni != invertOpacity) { invertOpacity = ni; previewDirty = true; }
            }

            EditorGUILayout.Space(2);
        }
        GUI.backgroundColor = prevBg;
    }

    private void DrawPreview()
    {
        if (!tex_Color && !tex_Opacity) return;
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Предпросмотр", EditorStyles.boldLabel);
            if (finalTexture && !previewDirty)
            {
                var rect = GUILayoutUtility.GetRect(256, 256, GUILayout.ExpandWidth(true));
                DrawCheckerBackground(rect);
                EditorGUI.DrawPreviewTexture(rect, finalTexture);
                GUILayout.Space(4);
                EditorGUILayout.LabelField(
                    "Итог: " + finalTexture.width + " x " + finalTexture.height + "  |  RGBA32",
                    EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                var ph = new GUIStyle(EditorStyles.helpBox) { alignment = TextAnchor.MiddleCenter, fixedHeight = 100 };
                ph.normal.textColor = new Color(0.5f, 0.5f, 0.5f);
                GUILayout.Box("Нажмите «Обновить предпросмотр»", ph, GUILayout.ExpandWidth(true));
            }
        }
    }

    private void DrawCheckerBackground(Rect rect)
    {
        int cell = 12;
        Color c1 = new Color(0.32f, 0.32f, 0.32f);
        Color c2 = new Color(0.52f, 0.52f, 0.52f);
        for (int x = (int)rect.x; x < rect.xMax; x += cell)
        for (int y = (int)rect.y; y < rect.yMax; y += cell)
        {
            bool odd = (((x - (int)rect.x) / cell) + ((y - (int)rect.y) / cell)) % 2 == 0;
            EditorGUI.DrawRect(new Rect(x, y,
                Mathf.Min(cell, (int)rect.xMax - x),
                Mathf.Min(cell, (int)rect.yMax - y)), odd ? c1 : c2);
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
                BakeTexture();
                previewDirty = false;
                SetStatus("Предпросмотр обновлён.", MessageType.Info);
                Repaint();
            }

            GUI.backgroundColor = new Color(0.25f, 0.80f, 0.45f);
            if (GUILayout.Button("💾  Сохранить PNG", GUILayout.Height(38)))
            {
                BakeTexture();
                SaveTexture();
                previewDirty = false;
            }

            GUI.backgroundColor = new Color(0.80f, 0.28f, 0.28f);
            if (GUILayout.Button("✖  Очистить", GUILayout.Height(38), GUILayout.Width(90)))
                ClearAll();

            GUI.backgroundColor = prev;
        }

        // Быстрое сохранение рядом с Color Map
        {
            bool hasColorPath = tex_Color != null && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(tex_Color));
            Color prev2 = GUI.backgroundColor;
            GUI.enabled = hasColorPath && (tex_Color != null || tex_Opacity != null);
            GUI.backgroundColor = new Color(0.65f, 0.35f, 1.0f);

            string lbl = hasColorPath
                ? "  Сохранить рядом с Color  ->  " + tex_Color.name + "_Alpha.png"
                : "  Сохранить рядом с Color  (вставьте Color Map)";

            if (GUILayout.Button(lbl, GUILayout.Height(30)))
            {
                BakeTexture();
                SaveNextToColor();
                previewDirty = false;
                Repaint();
            }

            GUI.enabled = true;
            GUI.backgroundColor = prev2;
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Ядро
    // ════════════════════════════════════════════════════════════════════════

    private void BakeTexture()
    {
        RecalcSize();
        if (texSize == Vector2Int.zero) { SetStatus("Нет текстур — нечего запекать.", MessageType.Warning); return; }

        Texture2D rC = GetReadable(tex_Color);
        Texture2D rO = GetReadable(tex_Opacity);

        Color[] colorPx   = rC != null ? rC.GetPixels() : null;
        Color[] opacityPx = rO != null ? rO.GetPixels() : null;

        int total  = texSize.x * texSize.y;
        var output = new Color[total];

        for (int i = 0; i < total; i++)
        {
            Color c = colorPx != null ? colorPx[i] : Color.white;

            float a;
            if (opacityPx != null)
            {
                Color op = opacityPx[i];
                switch (opacityChannel)
                {
                    case 0:  a = op.r; break;
                    case 1:  a = op.g; break;
                    case 2:  a = op.b; break;
                    default: a = op.r * 0.299f + op.g * 0.587f + op.b * 0.114f; break;
                }
            }
            else
                a = opacityDefault;

            if (invertOpacity) a = 1f - a;
            output[i] = new Color(c.r, c.g, c.b, a);
        }

        finalTexture = new Texture2D(texSize.x, texSize.y, TextureFormat.RGBA32, false);
        finalTexture.SetPixels(output);
        finalTexture.Apply();
    }

    private void SaveTexture()
    {
        if (finalTexture == null) { SetStatus("Нечего сохранять.", MessageType.Warning); return; }
        string path = EditorUtility.SaveFilePanelInProject("Сохранить Alpha PNG", GetDefaultName(), "png", "Выберите папку");
        if (string.IsNullOrEmpty(path)) { SetStatus("Сохранение отменено.", MessageType.Info); return; }
        WriteAndImport(path);
    }

    private void SaveNextToColor()
    {
        if (finalTexture == null) { SetStatus("Нечего сохранять.", MessageType.Warning); return; }
        string colorPath = AssetDatabase.GetAssetPath(tex_Color);
        if (string.IsNullOrEmpty(colorPath)) { SetStatus("Не удалось получить путь Color Map.", MessageType.Error); return; }

        string dir  = Path.GetDirectoryName(colorPath);
        string file = tex_Color.name + "_Alpha.png";
        string path = Path.Combine(dir, file).Replace("\\", "/");

        if (File.Exists(path))
            if (!EditorUtility.DisplayDialog("Файл уже существует",
                "'" + file + "' уже есть.\nПерезаписать?", "Перезаписать", "Отмена"))
                return;

        WriteAndImport(path);
        var saved = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (saved) EditorGUIUtility.PingObject(saved);
    }

    private void WriteAndImport(string path)
    {
        File.WriteAllBytes(path, finalTexture.EncodeToPNG());
        AssetDatabase.Refresh();

        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp != null)
        {
            imp.sRGBTexture         = true;
            imp.alphaSource         = TextureImporterAlphaSource.FromInput;
            imp.alphaIsTransparency = true;
            imp.isReadable          = false;
            imp.mipmapEnabled       = true;
            imp.textureType         = TextureImporterType.Default;
            EditorUtility.SetDirty(imp);
            imp.SaveAndReimport();
        }

        AssetDatabase.Refresh();
        SetStatus("Сохранено: " + path, MessageType.Info);
        Debug.Log("[AlphaPacker] Сохранено: " + path);
    }

    // ════════════════════════════════════════════════════════════════════════
    //  Вспомогательные методы
    // ════════════════════════════════════════════════════════════════════════

    private Texture2D GetReadable(Texture2D source)
    {
        if (source == null) return null;
        var rw = source.isDataSRGB ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear;
        var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, rw);
        Graphics.Blit(source, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
        copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
        copy.Apply();
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return copy;
    }

    private void ValidateAndFix(ref Texture2D tex)
    {
        if (tex == null) return;
        if (autoFixReadable)
        {
            string assetPath = AssetDatabase.GetAssetPath(tex);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var imp = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (imp != null && !imp.isReadable)
                {
                    imp.isReadable = true;
                    imp.SaveAndReimport();
                    SetStatus("Авто-исправлено: '" + tex.name + "' — Read/Write включён.", MessageType.Info);
                }
            }
        }
        RecalcSize();
        if (texSize != Vector2Int.zero && texSize != new Vector2Int(tex.width, tex.height))
        {
            SetStatus("Несовпадение размеров: " + texSize.x + "x" + texSize.y +
                      " vs " + tex.width + "x" + tex.height + ". Текстура отклонена.", MessageType.Error);
            tex = null;
        }
    }

    private void RecalcSize()
    {
        texSize = Vector2Int.zero;
        if (tex_Color)        texSize = new Vector2Int(tex_Color.width,   tex_Color.height);
        else if (tex_Opacity) texSize = new Vector2Int(tex_Opacity.width, tex_Opacity.height);
    }

    private string GetDefaultName()
    {
        if (tex_Color)   return tex_Color.name   + "_Alpha";
        if (tex_Opacity) return tex_Opacity.name + "_Alpha";
        return "Texture_Alpha";
    }

    private void ClearAll()
    {
        tex_Color = tex_Opacity = finalTexture = null;
        previewDirty = true;
        texSize = Vector2Int.zero;
        statusMsg = "";
    }

    private void SetStatus(string msg, MessageType type) { statusMsg = msg; statusType = type; }
}
