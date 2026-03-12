#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class FolderMaterialBatchImporter : EditorWindow
{
    private const string DefaultSourceFolder = "Assets/материалы/50_Free_PBR_Materials_256/Textures";
    private const string DefaultOutputFolder = "Assets/материалы/50_Free_PBR_Materials_256/Materials";
    private const string LitShaderName = "HDRP/Lit";

    private string sourceFolder = DefaultSourceFolder;
    private string outputFolder = DefaultOutputFolder;
    private bool createLitMaskTexture = true;
    private Vector2 scroll;
    private string logText = string.Empty;

    [MenuItem("Инструменты/Материалы из папки 50_Free_PBR_Materials_256")]
    public static void Open()
    {
        FolderMaterialBatchImporter wnd = GetWindow<FolderMaterialBatchImporter>("Folder -> HDRP Material");
        wnd.minSize = new Vector2(640f, 420f);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Материалы из папки текстур", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Сканирует папку с текстурами, собирает наборы по имени, создает HDRP/Lit материалы и LitMask.",
            MessageType.Info);

        sourceFolder = EditorGUILayout.TextField("Папка текстур", sourceFolder);
        outputFolder = EditorGUILayout.TextField("Папка материалов", outputFolder);
        createLitMaskTexture = EditorGUILayout.ToggleLeft("Создавать LitMask (R=Metal G=AO B=Detail A=Smooth)", createLitMaskTexture);

        EditorGUILayout.Space(6);
        if (GUILayout.Button("Запустить", GUILayout.Height(30f)))
        {
            Run();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Лог", EditorStyles.boldLabel);
        using (var view = new EditorGUILayout.ScrollViewScope(scroll))
        {
            scroll = view.scrollPosition;
            EditorGUILayout.TextArea(logText, GUILayout.ExpandHeight(true));
        }
    }

    private void Run()
    {
        logText = string.Empty;
        if (!AssetDatabase.IsValidFolder(sourceFolder))
        {
            Append("Папка не найдена: " + sourceFolder);
            return;
        }

        EnsureFolder(outputFolder);

        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { sourceFolder });
        if (guids == null || guids.Length == 0)
        {
            Append("Текстуры не найдены в: " + sourceFolder);
            return;
        }

        Dictionary<string, TextureSet> sets = new Dictionary<string, TextureSet>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (string.IsNullOrEmpty(fileName))
            {
                continue;
            }

            if (!TryParseName(fileName, out string baseName, out MapType mapType, out bool roughness))
            {
                continue;
            }

            if (!sets.TryGetValue(baseName, out TextureSet set))
            {
                set = new TextureSet(baseName);
                sets.Add(baseName, set);
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                continue;
            }

            switch (mapType)
            {
                case MapType.Color: set.Color = tex; break;
                case MapType.Normal: set.Normal = tex; break;
                case MapType.Metallic: set.Metallic = tex; break;
                case MapType.AO: set.AO = tex; break;
                case MapType.Height: set.Height = tex; break;
                case MapType.Smoothness:
                    set.SmoothOrRough = tex;
                    set.SmoothSourceIsRoughness = roughness;
                    break;
            }
        }

        if (sets.Count == 0)
        {
            Append("Наборы не найдены (имена файлов не совпали с шаблонами)." );
            return;
        }

        int created = 0;
        foreach (TextureSet set in sets.Values)
        {
            if (set.Color == null)
            {
                Append("Пропуск (нет albedo): " + set.Name);
                continue;
            }

            if (CreateMaterialForSet(set))
            {
                created++;
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Append("Готово: " + created + " материалов.");
    }

    private bool CreateMaterialForSet(TextureSet set)
    {
        Shader shader = Shader.Find(LitShaderName);
        if (shader == null)
        {
            Append("HDRP/Lit не найден. Проверь pipeline.");
            return false;
        }

        string matPath = outputFolder + "/" + set.Name + ".mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            mat.shader = shader;
        }

        if (mat.HasProperty("_BaseColorMap"))
        {
            mat.SetTexture("_BaseColorMap", set.Color);
        }

        if (set.Normal != null)
        {
            EnsureNormalMap(set.Normal);
            if (mat.HasProperty("_NormalMap"))
            {
                mat.SetTexture("_NormalMap", set.Normal);
            }
        }

        if (set.AO != null && mat.HasProperty("_OcclusionMap"))
        {
            mat.SetTexture("_OcclusionMap", set.AO);
        }

        if (set.Metallic != null && mat.HasProperty("_MetallicMap"))
        {
            mat.SetTexture("_MetallicMap", set.Metallic);
        }

        if (set.Height != null && mat.HasProperty("_HeightMap"))
        {
            mat.SetTexture("_HeightMap", set.Height);
        }

        Texture2D litMask = null;
        if (createLitMaskTexture)
        {
            litMask = CreateLitMaskTexture(outputFolder, set.Name, set.Metallic, set.AO, null, set.SmoothOrRough, set.SmoothSourceIsRoughness);
        }

        if (litMask != null && mat.HasProperty("_MaskMap"))
        {
            mat.SetTexture("_MaskMap", litMask);
        }
        else if (set.SmoothOrRough != null && mat.HasProperty("_MaskMap"))
        {
            mat.SetTexture("_MaskMap", set.SmoothOrRough);
        }

        EditorUtility.SetDirty(mat);
        Append("Материал: " + matPath);
        return true;
    }

    private static void EnsureNormalMap(Texture2D tex)
    {
        string path = AssetDatabase.GetAssetPath(tex);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.NormalMap)
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.SaveAndReimport();
        }
    }

    private static bool TryParseName(string fileName, out string baseName, out MapType mapType, out bool roughness)
    {
        baseName = null;
        mapType = MapType.Color;
        roughness = false;

        string lower = fileName.ToLowerInvariant();
        string[] suffixes =
        {
            "_albedo", "_basecolor", "_base_color", "_color", "_diffuse",
            "_normal", "_normaldx",
            "_metallic", "_metalness",
            "_ao", "_occlusion", "_ambientocclusion",
            "_roughness", "_smoothness", "_gloss",
            "_height", "_displacement"
        };

        string matched = null;
        for (int i = 0; i < suffixes.Length; i++)
        {
            if (lower.EndsWith(suffixes[i]))
            {
                matched = suffixes[i];
                break;
            }
        }

        if (matched == null)
        {
            return false;
        }

        baseName = fileName.Substring(0, fileName.Length - matched.Length);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            return false;
        }

        switch (matched)
        {
            case "_albedo":
            case "_basecolor":
            case "_base_color":
            case "_color":
            case "_diffuse":
                mapType = MapType.Color;
                break;
            case "_normal":
            case "_normaldx":
                mapType = MapType.Normal;
                break;
            case "_metallic":
            case "_metalness":
                mapType = MapType.Metallic;
                break;
            case "_ao":
            case "_occlusion":
            case "_ambientocclusion":
                mapType = MapType.AO;
                break;
            case "_roughness":
                mapType = MapType.Smoothness;
                roughness = true;
                break;
            case "_smoothness":
            case "_gloss":
                mapType = MapType.Smoothness;
                roughness = false;
                break;
            case "_height":
            case "_displacement":
                mapType = MapType.Height;
                break;
        }

        return true;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder))
        {
            return;
        }

        string[] parts = folder.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private Texture2D CreateLitMaskTexture(
        string folderAssetPath,
        string materialName,
        Texture2D metallic,
        Texture2D ao,
        Texture2D detail,
        Texture2D smoothOrRough,
        bool smoothSourceIsRoughness)
    {
        int width = 0;
        int height = 0;
        TryTakeSize(metallic, ref width, ref height);
        TryTakeSize(ao, ref width, ref height);
        TryTakeSize(detail, ref width, ref height);
        TryTakeSize(smoothOrRough, ref width, ref height);

        if (width <= 0 || height <= 0)
        {
            return null;
        }

        Color[] metalPx = SampleTexture(metallic, width, height);
        Color[] aoPx = SampleTexture(ao, width, height);
        Color[] detailPx = SampleTexture(detail, width, height);
        Color[] smoothPx = SampleTexture(smoothOrRough, width, height);

        Texture2D outTex = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        Color[] outPx = new Color[width * height];

        for (int i = 0; i < outPx.Length; i++)
        {
            float r = metalPx != null ? metalPx[i].r : 0f;
            float g = aoPx != null ? aoPx[i].r : 1f;
            float b = detailPx != null ? detailPx[i].r : 0f;
            float a = 0.5f;

            if (smoothPx != null)
            {
                a = smoothSourceIsRoughness ? 1f - smoothPx[i].r : smoothPx[i].r;
            }

            outPx[i] = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), Mathf.Clamp01(a));
        }

        outTex.SetPixels(outPx);
        outTex.Apply(false, false);

        string fileName = materialName + "_LitMask.png";
        string assetPath = folderAssetPath + "/" + fileName;
        string absPath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), assetPath).Replace('\\', '/');
        File.WriteAllBytes(absPath, outTex.EncodeToPNG());
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.SaveAndReimport();
        }

        Append("LitMask: " + assetPath);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
    }

    private static void TryTakeSize(Texture2D tex, ref int width, ref int height)
    {
        if (tex == null)
        {
            return;
        }

        if (width <= 0 || height <= 0)
        {
            width = tex.width;
            height = tex.height;
        }
    }

    private static Color[] SampleTexture(Texture2D source, int width, int height)
    {
        if (source == null)
        {
            return null;
        }

        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        RenderTexture prev = RenderTexture.active;
        Graphics.Blit(source, rt);
        RenderTexture.active = rt;

        Texture2D temp = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        temp.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        temp.Apply(false, false);
        Color[] px = temp.GetPixels();

        UnityEngine.Object.DestroyImmediate(temp);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return px;
    }

    private void Append(string line)
    {
        logText += line + Environment.NewLine;
        Repaint();
    }

    private sealed class TextureSet
    {
        public string Name;
        public Texture2D Color;
        public Texture2D Normal;
        public Texture2D Metallic;
        public Texture2D AO;
        public Texture2D Height;
        public Texture2D SmoothOrRough;
        public bool SmoothSourceIsRoughness;

        public TextureSet(string name)
        {
            Name = name;
        }
    }

    private enum MapType
    {
        Color,
        Normal,
        Metallic,
        AO,
        Smoothness,
        Height
    }
}
#endif
