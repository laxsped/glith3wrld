using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Collider))]
public class TriggerSceneTransition : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string targetSceneName = "2";

    [Header("Fade")]
    [SerializeField] private float fadeDuration = 0.35f;
    [SerializeField] private Color fadeColor = new Color(0f, 0f, 0f, 1f);
    [SerializeField] private float minimumLoadingScreenTime = 2f;
    [SerializeField] private float fadeOutAfterLoadDuration = 0.4f;

    [Header("Loading Animation")]
    [SerializeField] private Sprite[] loadingFrames;
    [SerializeField] private float loadingFps = 14f;
    [SerializeField] private Vector2 loadingSize = new Vector2(110f, 110f);
    [SerializeField] private Vector2 loadingOffset = new Vector2(-38f, 38f);
    [SerializeField] private string loadingFramesFolder = "Assets/ICONS/loading ani";

    [Header("Trigger")]
    [SerializeField] private string playerTag = "Player";

    private bool started;
    private Canvas transitionCanvas;
    private Image fadeImage;
    private Image loadingImage;

    private void Reset()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (started)
        {
            return;
        }

        if (!other.CompareTag(playerTag))
        {
            return;
        }

        started = true;
        StartCoroutine(RunTransition());
    }

    private IEnumerator RunTransition()
    {
        DontDestroyOnLoad(gameObject);
        BuildUi();
        string sceneToLoad = NormalizeSceneName(targetSceneName);

        if (!Application.CanStreamedLevelBeLoaded(sceneToLoad))
        {
            Debug.LogError($"[TriggerSceneTransition] Scene '{sceneToLoad}' is not loadable. Add it to Build Settings (File > Build Settings > Scenes In Build).", this);
            yield break;
        }

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / Mathf.Max(0.0001f, fadeDuration));
            SetFadeAlpha(k);
            yield return null;
        }

        SetFadeAlpha(1f);
        Coroutine loadingRoutine = StartCoroutine(AnimateLoading());
        float minEndTime = Time.unscaledTime + Mathf.Max(0f, minimumLoadingScreenTime);

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneToLoad, LoadSceneMode.Single);
        if (op == null)
        {
            Debug.LogError($"[TriggerSceneTransition] LoadSceneAsync returned null for scene '{sceneToLoad}'.", this);
            yield break;
        }

        op.allowSceneActivation = false;

        while (op.progress < 0.9f || Time.unscaledTime < minEndTime)
        {
            yield return null;
        }

        op.allowSceneActivation = true;
        while (!op.isDone || Time.unscaledTime < minEndTime)
        {
            yield return null;
        }

        if (loadingRoutine != null)
        {
            StopCoroutine(loadingRoutine);
        }

        if (loadingImage != null)
        {
            loadingImage.enabled = false;
        }

        float fadeOut = Mathf.Max(0f, fadeOutAfterLoadDuration);
        if (fadeOut > 0f)
        {
            float ft = 0f;
            while (ft < fadeOut)
            {
                ft += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(ft / fadeOut);
                SetFadeAlpha(k);
                yield return null;
            }
        }

        SetFadeAlpha(0f);
        if (transitionCanvas != null)
        {
            Destroy(transitionCanvas.gameObject);
        }
        Destroy(gameObject);
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("Scene Transition Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        transitionCanvas = canvasGo.GetComponent<Canvas>();
        DontDestroyOnLoad(canvasGo);
        transitionCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        transitionCanvas.sortingOrder = 5000;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject fadeGo = new GameObject("Fade", typeof(RectTransform), typeof(Image));
        fadeGo.transform.SetParent(canvasGo.transform, false);
        RectTransform fadeRect = fadeGo.GetComponent<RectTransform>();
        fadeRect.anchorMin = Vector2.zero;
        fadeRect.anchorMax = Vector2.one;
        fadeRect.offsetMin = Vector2.zero;
        fadeRect.offsetMax = Vector2.zero;

        fadeImage = fadeGo.GetComponent<Image>();
        fadeImage.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);

        GameObject loadingGo = new GameObject("Loading", typeof(RectTransform), typeof(Image));
        loadingGo.transform.SetParent(canvasGo.transform, false);
        RectTransform loadingRect = loadingGo.GetComponent<RectTransform>();
        loadingRect.anchorMin = new Vector2(1f, 0f);
        loadingRect.anchorMax = new Vector2(1f, 0f);
        loadingRect.pivot = new Vector2(1f, 0f);
        loadingRect.sizeDelta = loadingSize;
        loadingRect.anchoredPosition = loadingOffset;

        loadingImage = loadingGo.GetComponent<Image>();
        loadingImage.preserveAspect = true;
        loadingImage.color = Color.white;
        loadingImage.enabled = loadingFrames != null && loadingFrames.Length > 0;
        if (loadingImage.enabled)
        {
            loadingImage.sprite = loadingFrames[0];
        }
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeImage == null)
        {
            return;
        }

        Color c = fadeImage.color;
        c.a = Mathf.Clamp01(alpha);
        fadeImage.color = c;
    }

    private IEnumerator AnimateLoading()
    {
        if (loadingImage == null || loadingFrames == null || loadingFrames.Length == 0)
        {
            yield break;
        }

        float frameDur = 1f / Mathf.Max(1f, loadingFps);
        float timer = 0f;
        int idx = 0;

        while (true)
        {
            timer += Time.unscaledDeltaTime;
            while (timer >= frameDur)
            {
                timer -= frameDur;
                idx = (idx + 1) % loadingFrames.Length;
                loadingImage.sprite = loadingFrames[idx];
            }

            yield return null;
        }
    }

    private static string NormalizeSceneName(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return string.Empty;
        }

        string normalized = sceneName.Trim();
        if (normalized.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase))
        {
            normalized = System.IO.Path.GetFileNameWithoutExtension(normalized);
        }

        return normalized;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fadeDuration = Mathf.Clamp(fadeDuration, 0.05f, 2f);
        minimumLoadingScreenTime = Mathf.Clamp(minimumLoadingScreenTime, 0f, 15f);
        fadeOutAfterLoadDuration = Mathf.Clamp(fadeOutAfterLoadDuration, 0f, 3f);
        loadingFps = Mathf.Clamp(loadingFps, 1f, 60f);

        if (string.IsNullOrWhiteSpace(loadingFramesFolder))
        {
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { loadingFramesFolder });
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        List<string> paths = new List<string>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!string.IsNullOrEmpty(p))
            {
                paths.Add(p);
            }
        }

        paths.Sort(System.StringComparer.OrdinalIgnoreCase);
        List<Sprite> sprites = new List<Sprite>(paths.Count);
        for (int i = 0; i < paths.Count; i++)
        {
            Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(paths[i]);
            if (s != null)
            {
                sprites.Add(s);
            }
        }

        if (sprites.Count > 0)
        {
            loadingFrames = sprites.ToArray();
        }
    }
#endif
}
