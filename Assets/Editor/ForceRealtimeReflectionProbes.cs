#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ForceRealtimeReflectionProbes
{
    [MenuItem("Инструменты/Lighting/Force Realtime Reflection Probes (Active Scene)")]
    public static void Run()
    {
        int changed = 0;

        ReflectionProbe[] probes = Object.FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < probes.Length; i++)
        {
            ReflectionProbe p = probes[i];
            if (p.mode != UnityEngine.Rendering.ReflectionProbeMode.Realtime)
            {
                Undo.RecordObject(p, "Set ReflectionProbe Realtime");
                p.mode = UnityEngine.Rendering.ReflectionProbeMode.Realtime;
                p.refreshMode = UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
                p.timeSlicingMode = UnityEngine.Rendering.ReflectionProbeTimeSlicingMode.NoTimeSlicing;
                EditorUtility.SetDirty(p);
                changed++;
            }

            // Optional: clear baked link if any.
            SerializedObject so = new SerializedObject(p);
            SerializedProperty bakedTex = so.FindProperty("m_BakedTexture");
            if (bakedTex != null && bakedTex.objectReferenceValue != null)
            {
                bakedTex.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(p);
                changed++;
            }
        }

        // HDRP additional data uses serialized probe settings.
        Component[] allComponents = Object.FindObjectsByType<Component>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < allComponents.Length; i++)
        {
            Component c = allComponents[i];
            if (c == null)
            {
                continue;
            }

            if (c.GetType().FullName != "UnityEngine.Rendering.HighDefinition.HDAdditionalReflectionData")
            {
                continue;
            }

            SerializedObject so = new SerializedObject(c);
            SerializedProperty mode = so.FindProperty("m_ProbeSettings.mode");
            SerializedProperty realtimeMode = so.FindProperty("m_ProbeSettings.realtimeMode");
            bool localChanged = false;

            if (mode != null && mode.intValue != 1)
            {
                mode.intValue = 1; // Realtime
                localChanged = true;
            }

            if (realtimeMode != null && realtimeMode.intValue != 1)
            {
                realtimeMode.intValue = 1;
                localChanged = true;
            }

            if (localChanged)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(c);
                changed++;
            }
        }

        if (changed > 0)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        Debug.Log("[Lighting Fix] Realtime reflection probe updates: " + changed);
    }
}
#endif
