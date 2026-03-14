#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using UnityEngine;

public class ДверьОткрываемая : MonoBehaviour
{
    private const string ЭффектыКлюч = "PauseSimple.volume_effects";
    private const string ЗвукОткрытияПоУмолчанию =
        "Assets/Door, Cabinet and Locker Sound Pack (Free)/FREE VERSION/Open Door 13.wav";
    private const string ЗвукЗакрытияПоУмолчанию =
        "Assets/Door, Cabinet and Locker Sound Pack (Free)/FREE VERSION/Close Door 12.wav";

    [Header("Связи")]
    [SerializeField] private Transform петля;
    [SerializeField] private AudioSource источникЗвука;
    [SerializeField] private AudioClip звукОткрытия;
    [SerializeField] private AudioClip звукЗакрытия;

    [Header("Углы (локальные Y)")]
    [SerializeField] private float уголОткрытия = 90f;
    [SerializeField] private float уголЗакрытия = 0f;

    [Header("Скорость")]
    [SerializeField] private float длительностьОткрытия = 0.9f;
    [SerializeField] private float задержкаПовтора = 0.35f;

    [Header("Ограничения")]
    [SerializeField] private bool блокироватьПлюсY = true;

    private Coroutine openRoutine;
    private float базовыйУголY;
    private float следующийРазрешенныйКлик;
    private bool открыт;

    private void Awake()
    {
        if (петля == null)
        {
            петля = transform;
        }

        if (источникЗвука == null)
        {
            источникЗвука = GetComponent<AudioSource>();
        }

        длительностьОткрытия = Mathf.Max(0.05f, длительностьОткрытия);
        базовыйУголY = GetLocalYaw();
        уголЗакрытия = базовыйУголY;
    }

    public void Toggle(Transform игрок)
    {
        if (петля == null)
        {
            return;
        }

        if (Time.unscaledTime < следующийРазрешенныйКлик)
        {
            return;
        }
        следующийРазрешенныйКлик = Time.unscaledTime + Mathf.Max(0.05f, задержкаПовтора);

        if (openRoutine != null)
        {
            StopCoroutine(openRoutine);
        }

        float targetYaw;
        if (!открыт)
        {
            targetYaw = базовыйУголY + GetOpenDirectionYawOffset(игрок);
            openRoutine = StartCoroutine(ПовернутьПлавно(targetYaw));
            ПроигратьЗвук(звукОткрытия);
            открыт = true;
        }
        else
        {
            targetYaw = базовыйУголY;
            openRoutine = StartCoroutine(ПовернутьПлавно(targetYaw));
            ПроигратьЗвук(звукЗакрытия);
            открыт = false;
        }
    }

    private IEnumerator ПовернутьПлавно(float targetYaw)
    {
        float start = GetLocalYaw();
        float target = ClampYaw(targetYaw);
        float t = 0f;
        float duration = Mathf.Max(0.05f, длительностьОткрытия);

        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float eased = k * k * (3f - 2f * k);
            float next = Mathf.LerpAngle(start, target, eased);
            SetLocalYaw(ClampYaw(next));
            yield return null;
        }

        SetLocalYaw(ClampYaw(target));
        openRoutine = null;
    }

    private void ПроигратьЗвук(AudioClip clip)
    {
        if (источникЗвука == null || clip == null)
        {
            return;
        }

        float fx = Mathf.Clamp01(PlayerPrefs.GetInt(ЭффектыКлюч, 10) / 10f);
        источникЗвука.PlayOneShot(clip, fx);
    }

    private float GetOpenDirectionYawOffset(Transform игрок)
    {
        if (игрок == null || петля == null)
        {
            return -Mathf.Abs(уголОткрытия);
        }

        Vector3 toPlayer = игрок.position - петля.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            return -Mathf.Abs(уголОткрытия);
        }

        Vector3 fwd = петля.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f)
        {
            return -Mathf.Abs(уголОткрытия);
        }

        float sign = Mathf.Sign(Vector3.Dot(петля.up, Vector3.Cross(fwd.normalized, toPlayer.normalized)));
        float offset = (sign >= 0f ? -1f : 1f) * Mathf.Abs(уголОткрытия);
        return offset;
    }

    private float GetLocalYaw()
    {
        if (петля == null)
        {
            return 0f;
        }

        return NormalizeAngle(петля.localEulerAngles.y);
    }

    private void SetLocalYaw(float yaw)
    {
        if (петля == null)
        {
            return;
        }

        Vector3 e = петля.localEulerAngles;
        e.y = yaw;
        петля.localEulerAngles = e;
    }

    private float ClampYaw(float yaw)
    {
        float min = Mathf.Min(базовыйУголY - Mathf.Abs(уголОткрытия), базовыйУголY);
        float max = Mathf.Max(базовыйУголY + Mathf.Abs(уголОткрытия), базовыйУголY);
        float clamped = Mathf.Clamp(yaw, min, max);
        if (блокироватьПлюсY)
        {
            clamped = Mathf.Min(clamped, базовыйУголY);
        }
        return clamped;
    }

    private static float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (петля == null)
        {
            петля = transform;
        }

        if (источникЗвука == null)
        {
            источникЗвука = GetComponent<AudioSource>();
        }

        длительностьОткрытия = Mathf.Max(0.05f, длительностьОткрытия);

        if (звукОткрытия == null)
        {
            звукОткрытия = AssetDatabase.LoadAssetAtPath<AudioClip>(ЗвукОткрытияПоУмолчанию);
        }

        if (звукЗакрытия == null)
        {
            звукЗакрытия = AssetDatabase.LoadAssetAtPath<AudioClip>(ЗвукЗакрытияПоУмолчанию);
        }
    }
#endif
}
