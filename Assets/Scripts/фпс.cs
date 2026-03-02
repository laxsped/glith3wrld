using UnityEngine;

public class FpsNumberTopRight : MonoBehaviour
{
    [SerializeField] private int fontSize = 28;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private Vector2 margin = new Vector2(16f, 12f);

    private float smoothedDeltaTime;
    private GUIStyle style;

    private void Awake()
    {
        style = new GUIStyle
        {
            alignment = TextAnchor.UpperRight,
            fontSize = fontSize,
            normal = { textColor = color }
        };
    }

    private void Update()
    {
        smoothedDeltaTime += (Time.unscaledDeltaTime - smoothedDeltaTime) * 0.1f;
    }

    private void OnGUI()
    {
        if (style == null)
        {
            return;
        }

        style.fontSize = fontSize;
        style.normal.textColor = color;

        float fps = smoothedDeltaTime > 0f ? 1f / smoothedDeltaTime : 0f;
        string fpsText = Mathf.RoundToInt(fps).ToString();

        Rect rect = new Rect(0f, margin.y, Screen.width - margin.x, fontSize + 10);
        GUI.Label(rect, fpsText, style);
    }
}
