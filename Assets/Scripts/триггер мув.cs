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

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private float elapsed = 0f;
    private bool isMoving = false;

    private float fadeElapsed = 0f;
    private bool isFading = false;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (cameraTransform == null) return;

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
            audioSource.volume = Mathf.Lerp(0f, targetVolume * fx, t);

            if (t >= 1f)
                isFading = false;
        }
    }
}

