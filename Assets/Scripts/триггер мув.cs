using UnityEngine;

public class CameraTriggerZone : MonoBehaviour
{
    [Header("Camera")]
    public Transform cameraTransform;

    [Header("New Position (only checked axes will change)")]
    public bool changeX = false;
    public bool changeY = false;
    public bool changeZ = false;

    public float newX = 0f;
    public float newY = 0f;
    public float newZ = 0f;

    [Header("Smooth")]
    public bool smoothMove = false;
    public float duration = 1f;

    [Header("Audio")]
    public bool playAudio = false;
    public AudioSource audioSource;
    public AudioClip audioClip;

    [Header("Audio Fade In")]
    public bool fadeIn = false;
    public float fadeDuration = 1f;
    public float targetVolume = 1f;
    [Range(0f, 1f)] public float sfxOneShotVolume = 1f;

    [Header("One-Way")]
    [Tooltip("Срабатывает только когда игрок входит со стороны стрелки (transform.forward). " +
             "Повернуй объект триггера чтобы задать разрешённое направление.")]
    public bool oneWayOnly = false;

    [Header("Animation")]
    public bool playAnimation = false;

    [Tooltip("Сработает только один раз за всё время жизни объекта.")]
    public bool playAnimationOnce = false;

    [Tooltip("Если задан — используется Animator (SetTrigger / SetBool / Play).")]
    public Animator animator;

    [Tooltip("Если задан — используется Animation (legacy).")]
    public Animation legacyAnimation;

    public enum AnimationMode { PlayLegacyClip, SetTrigger, SetBool, PlayState }

    [Tooltip("PlayLegacyClip — воспроизвести клип в компоненте Animation (старый способ).\n" +
             "SetTrigger     — нажать триггер в Animator, например чтобы перейти в состояние Walk.\n" +
             "SetBool        — включить или выключить bool-параметр в Animator.\n" +
             "PlayState      — сразу перепрыгнуть в нужное состояние Animator по имени.")]
    public AnimationMode animationMode = AnimationMode.SetTrigger;

    [Tooltip("Имя триггера, bool-параметра или состояния в Animator; " +
             "либо имя клипа для Legacy Animation.")]
    public string animationName = "";

    [Tooltip("Только для режима SetBool.\n" +
             "true = включить параметр, false = выключить.")]
    public bool boolValue = true;

    [Tooltip("Только для режима PlayState.\n" +
             "Номер слоя в Animator (0 = Base Layer, 1 = второй слой и т.д.).\n" +
             "Оставь −1 если слоёв несколько и не важно какой — Unity выберет сам.")]
    public int animatorLayer = -1;

    [Tooltip("Только для режима PlayState.\n" +
             "С какого момента начать анимацию: 0 = с самого начала, 0.5 = с середины, 1 = с конца.")]
    [Range(0f, 1f)]
    public float normalizedTime = 0f;

    // ── Приватные поля ─────────────────────────────────────────────────────────
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float elapsed = 0f;
    private bool isMoving = false;

    private float fadeElapsed = 0f;
    private bool isFading = false;

    private bool animationPlayed = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (cameraTransform == null) return;

        // ── One-Way проверка ───────────────────────────────────────────────────
        if (oneWayOnly)
        {
            Vector3 toPlayer = other.transform.position - transform.position;
            if (Vector3.Dot(toPlayer, transform.forward) > 0f)
                return;
        }

        // ── Позиция камеры ─────────────────────────────────────────────────────
        if (changeX || changeY || changeZ)
        {
            Vector3 current = cameraTransform.localPosition;

            targetPosition = new Vector3(
                changeX ? newX : current.x,
                changeY ? newY : current.y,
                changeZ ? newZ : current.z
            );

            if (smoothMove)
            {
                startPosition = cameraTransform.localPosition;
                elapsed = 0f;
                isMoving = true;
            }
            else
            {
                cameraTransform.localPosition = targetPosition;
            }
        }

        // ── Аудио ──────────────────────────────────────────────────────────────
        if (playAudio && audioSource != null && audioClip != null)
        {
            if (fadeIn)
            {
                audioSource.volume = 0f;
                audioSource.clip = audioClip;
                audioSource.Play();
                fadeElapsed = 0f;
                isFading = true;
            }
            else
            {
                float fx = Mathf.Clamp01(PlayerPrefs.GetInt("PauseSimple.volume_effects", 10) / 10f);
                audioSource.PlayOneShot(audioClip, sfxOneShotVolume * fx);
            }
        }

        // ── Анимация ───────────────────────────────────────────────────────────
        if (playAnimation && !string.IsNullOrEmpty(animationName))
        {
            if (playAnimationOnce && animationPlayed)
                return;

            animationPlayed = true;

            switch (animationMode)
            {
                case AnimationMode.PlayLegacyClip:
                    if (legacyAnimation != null)
                        legacyAnimation.Play(animationName);
                    break;

                case AnimationMode.SetTrigger:
                    if (animator != null)
                        animator.SetTrigger(animationName);
                    break;

                case AnimationMode.SetBool:
                    if (animator != null)
                        animator.SetBool(animationName, boolValue);
                    break;

                case AnimationMode.PlayState:
                    if (animator != null)
                        animator.Play(animationName, animatorLayer, normalizedTime);
                    break;
            }
        }
    }

    private void Update()
    {
        // ── Движение камеры ────────────────────────────────────────────────────
        if (isMoving && cameraTransform != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            cameraTransform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);

            if (t >= 1f)
                isMoving = false;
        }

        // ── Fade In громкости ──────────────────────────────────────────────────
        if (isFading && audioSource != null)
        {
            fadeElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(fadeElapsed / fadeDuration);

            float fx = Mathf.Clamp01(PlayerPrefs.GetInt("PauseSimple.volume_effects", 10) / 10f);
            audioSource.volume = Mathf.Lerp(0f, Mathf.Max(0.1f, targetVolume * fx), t);

            if (t >= 1f)
                isFading = false;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!oneWayOnly) return;

        Gizmos.color = Color.cyan;
        Vector3 origin = transform.position;
        Vector3 dir = transform.forward;
        Gizmos.DrawRay(origin, dir * 1.5f);
        Gizmos.DrawRay(origin + dir * 1.5f, (Quaternion.Euler(0, 150, 0) * dir) * 0.4f);
        Gizmos.DrawRay(origin + dir * 1.5f, (Quaternion.Euler(0, -150, 0) * dir) * 0.4f);
    }
}