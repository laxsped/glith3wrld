#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class APVBakingSetRepairTool : EditorWindow
{
    private sealed class BrokenSetInfo
    {
        public string assetPath;
        public List<string> missingGuids = new List<string>();
    }

    private readonly List<BrokenSetInfo> brokenSets = new List<BrokenSetInfo>();
    private Vector2 scroll;
    private string logText = string.Empty;

    [MenuItem("Инструменты/Lighting/APV Baking Set Repair")]
    public static void Open()
    {
        APVBakingSetRepairTool wnd = GetWindow<APVBakingSetRepairTool>("APV Repair");
        wnd.minSize = new Vector2(760f, 460f);
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("APV Baking Set Repair", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Ищет битые ссылки на APV data в Baking Set ассетах и очищает их (с бэкапом .bak), чтобы сцена снова могла запекаться.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Сканировать", GUILayout.Height(28f)))
        {
            ScanBrokenSets();
        }

        using (new EditorGUI.DisabledScope(brokenSets.Count == 0))
        {
            if (GUILayout.Button("Починить найденные", GUILayout.Height(28f)))
            {
                RepairBrokenSets();
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Найдено проблемных sets: " + brokenSets.Count, EditorStyles.boldLabel);
        using (var sv = new EditorGUILayout.ScrollViewScope(scroll, GUILayout.Height(180f)))
        {
            scroll = sv.scrollPosition;
            for (int i = 0; i < brokenSets.Count; i++)
            {
                BrokenSetInfo info = brokenSets[i];
                EditorGUILayout.LabelField("- " + info.assetPath);
                for (int g = 0; g < info.missingGuids.Count; g++)
                {
                    EditorGUILayout.LabelField("    missing guid: " + info.missingGuids[g], EditorStyles.miniLabel);
                }
            }
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Лог", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(logText, GUILayout.ExpandHeight(true));
    }

    private void ScanBrokenSets()
    {
        brokenSets.Clear();
        logText = string.Empty;

        string[] guids = AssetDatabase.FindAssets("t:MonoBehaviour 2 Baking Set");
        // Fallback for any file named '*Baking Set.asset'
        string[] files = Directory.GetFiles(Path.Combine(Application.dataPath), "*Baking Set.asset", SearchOption.AllDirectories);
        HashSet<string> candidatePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrEmpty(p))
            {
                candidatePaths.Add(p.Replace('\\', '/'));
            }
        }

        for (int i = 0; i < files.Length; i++)
        {
            string rel = ToAssetPath(files[i]);
            if (!string.IsNullOrEmpty(rel))
            {
                candidatePaths.Add(rel);
            }
        }

        foreach (string assetPath in candidatePaths)
        {
            if (!File.Exists(assetPath))
            {
                continue;
            }

            BrokenSetInfo info = ScanOne(assetPath);
            if (info != null)
            {
                brokenSets.Add(info);
            }
        }

        Append("Scan finished. Broken sets: " + brokenSets.Count);
    }

    private void RepairBrokenSets()
    {
        if (brokenSets.Count == 0)
        {
            Append("Nothing to repair.");
            return;
        }

        int fixedCount = 0;
        for (int i = 0; i < brokenSets.Count; i++)
        {
            BrokenSetInfo info = brokenSets[i];
            try
            {
                RepairOne(info);
                fixedCount++;
                Append("[OK] " + info.assetPath);
            }
            catch (Exception ex)
            {
                Append("[ERROR] " + info.assetPath + " -> " + ex.Message);
            }
        }

        AssetDatabase.Refresh();
        Append("Repair finished: " + fixedCount + " / " + brokenSets.Count);
    }

    private static BrokenSetInfo ScanOne(string assetPath)
    {
        string text = File.ReadAllText(assetPath);
        if (!text.Contains("ProbeVolumeBakingSet"))
        {
            return null;
        }

        MatchCollection matches = Regex.Matches(text, @"m_AssetGUID:\s*([0-9a-f]{32})", RegexOptions.IgnoreCase);
        if (matches.Count == 0)
        {
            return null;
        }

        BrokenSetInfo info = new BrokenSetInfo { assetPath = assetPath };
        HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < matches.Count; i++)
        {
            string guid = matches[i].Groups[1].Value;
            if (!unique.Add(guid))
            {
                continue;
            }

            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(p))
            {
                info.missingGuids.Add(guid);
            }
        }

        return info.missingGuids.Count > 0 ? info : null;
    }

    private static void RepairOne(BrokenSetInfo info)
    {
        string text = File.ReadAllText(info.assetPath);
        string backupPath = info.assetPath + ".bak";
        File.WriteAllText(backupPath, text);

        // For each missing guid block:
        // 1) clear guid
        // 2) clear streamable path
        // 3) zero out object reference
        for (int i = 0; i < info.missingGuids.Count; i++)
        {
            string guid = info.missingGuids[i];
            text = text.Replace("m_AssetGUID: " + guid, "m_AssetGUID: ");

            // Clean nearby stream path if it still points to deleted bytes.
            text = Regex.Replace(
                text,
                @"(m_StreamableAssetPath:\s*APVStreamingAssets\\[^\r\n]*\\" + Regex.Escape(guid) + @"\.bytes)",
                "m_StreamableAssetPath: ",
                RegexOptions.IgnoreCase);
        }

        // If guid is empty, make sure m_Asset ref is null in the same data structs.
        text = Regex.Replace(
            text,
            @"m_AssetGUID:\s*\r?\n(\s*m_StreamableAssetPath:.*\r?\n\s*m_ElementSize:.*\r?\n\s*m_StreamableCellDescs:\r?\n(?:\s{6}.*\r?\n)*\s*m_Asset:\s*)\{fileID:\s*4900000,\s*guid:\s*[0-9a-f]{32},\s*type:\s*3\}",
            "m_AssetGUID: \n$1{fileID: 0}",
            RegexOptions.IgnoreCase);

        File.WriteAllText(info.assetPath, text);
    }

    private static string ToAssetPath(string absPath)
    {
        string dataPath = Application.dataPath.Replace('\\', '/');
        string normalized = absPath.Replace('\\', '/');
        if (!normalized.StartsWith(dataPath, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return "Assets" + normalized.Substring(dataPath.Length);
    }

    private void Append(string line)
    {
        logText += line + Environment.NewLine;
        Repaint();
    }
}
#endif
