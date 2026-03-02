using System.Threading;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class GrassFootstepNoise : MonoBehaviour
{
    [Header("Step Timing")]
    [SerializeField] private float walkStepInterval = 0.42f;
    [SerializeField] private float runStepInterval = 0.28f;

    [Header("Envelope")]
    [SerializeField] private float attackTime = 0.01f;
    [SerializeField] private float decayTime = 0.14f;

    [Header("Tone")]
    [SerializeField] private float cutoffBaseHz = 2200f;
    [SerializeField] private float cutoffRandomHz = 450f;

    [Header("Randomization")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 1f;
    [SerializeField] private float stepVolume = 0.085f;
    [SerializeField] private float volumeRandom = 0.03f;
    [SerializeField] private float pitchMin = 0.92f;
    [SerializeField] private float pitchMax = 1.08f;

    [Header("Ground Check")]
    [SerializeField] private float groundCheckDistance = 0.08f;
    [SerializeField] private LayerMask groundMask = ~0;

    private AudioSource source;
    private AudioClip driverClip;
    private Collider bodyCollider;

    private float sampleRate = 48000f;
    private float stepTimer;

    private bool isGrounded;

    // Shared between main thread and audio thread.
    private int pendingStepTriggers;

    // Audio-thread state
    private uint rngState = 0x9E3779B9u;
    private float env;
    private int envStage; // 0 idle, 1 attack, 2 decay
    private float envAttackStep;
    private float envDecayStep;
    private float currentStepGain;
    private float currentCutoffHz;
    private float hpState;
    private float hpPrevIn;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        bodyCollider = GetComponent<Collider>();

        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 1f;
        source.mute = false;

        sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
        if (source.clip == null)
        {
            int sr = Mathf.RoundToInt(sampleRate);
            driverClip = AudioClip.Create("GrassStepDriver", sr, 1, sr, false);
            source.clip = driverClip;
        }

        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    private void Update()
    {
        RefreshGrounded();

        float x = Input.GetAxisRaw("Horizontal");
        float y = Input.GetAxisRaw("Vertical");
        bool isMoving = (x * x + y * y) > 0.001f;
        bool isRunning = isMoving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        if (!isMoving || !isGrounded)
        {
            stepTimer = 0f;
            return;
        }

        float interval = isRunning ? runStepInterval : walkStepInterval;
        interval = Mathf.Max(0.05f, interval);

        stepTimer += Time.deltaTime;
        if (stepTimer >= interval)
        {
            stepTimer -= interval;
            Interlocked.Increment(ref pendingStepTriggers);
        }
    }

    private void RefreshGrounded()
    {
        Vector3 origin;
        float castDistance;

        if (bodyCollider != null)
        {
            Bounds b = bodyCollider.bounds;
            origin = b.center;
            castDistance = b.extents.y + groundCheckDistance;
        }
        else
        {
            origin = transform.position + Vector3.up * 0.1f;
            castDistance = 0.6f;
        }

        isGrounded = Physics.Raycast(origin, Vector3.down, castDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        float sr = sampleRate;

        for (int i = 0; i < data.Length; i += channels)
        {
            if (Interlocked.CompareExchange(ref pendingStepTriggers, 0, 0) > 0)
            {
                ConsumeStepTrigger();
            }

            if (envStage == 1)
            {
                env += envAttackStep;
                if (env >= 1f)
                {
                    env = 1f;
                    envStage = 2;
                }
            }
            else if (envStage == 2)
            {
                env -= envDecayStep;
                if (env <= 0f)
                {
                    env = 0f;
                    envStage = 0;
                }
            }

            float white = NextWhite();
            float sample = white * env * currentStepGain;

            // High-pass for crispy grass/sand texture.
            float hpA = Mathf.Exp(-2f * Mathf.PI * Mathf.Max(40f, currentCutoffHz) / sr);
            hpState = hpA * (hpState + sample - hpPrevIn);
            hpPrevIn = sample;

            float outSample = hpState * masterVolume;

            for (int c = 0; c < channels; c++)
            {
                data[i + c] += outSample;
            }
        }
    }

    private void ConsumeStepTrigger()
    {
        // Decrement one trigger atomically.
        while (true)
        {
            int cur = Interlocked.CompareExchange(ref pendingStepTriggers, 0, 0);
            if (cur <= 0)
            {
                break;
            }

            if (Interlocked.CompareExchange(ref pendingStepTriggers, cur - 1, cur) == cur)
            {
                break;
            }
        }

        float pitch = Mathf.Lerp(pitchMin, pitchMax, Next01());
        float volJitter = (Next01() * 2f - 1f) * volumeRandom;
        float cutoffJitter = (Next01() * 2f - 1f) * cutoffRandomHz;

        currentStepGain = Mathf.Max(0f, stepVolume + volJitter);
        currentCutoffHz = Mathf.Max(80f, cutoffBaseHz + cutoffJitter);

        float att = Mathf.Max(0.001f, attackTime) / Mathf.Max(0.1f, pitch);
        float dec = Mathf.Max(0.02f, decayTime) / Mathf.Max(0.1f, pitch);

        envAttackStep = 1f / (att * sampleRate);
        envDecayStep = 1f / (dec * sampleRate);

        env = 0f;
        envStage = 1;
    }

    private float Next01()
    {
        rngState ^= rngState << 13;
        rngState ^= rngState >> 17;
        rngState ^= rngState << 5;
        return rngState / 4294967295f;
    }

    private float NextWhite()
    {
        return Next01() * 2f - 1f;
    }

    private void OnValidate()
    {
        walkStepInterval = Mathf.Max(0.05f, walkStepInterval);
        runStepInterval = Mathf.Max(0.05f, runStepInterval);
        attackTime = Mathf.Clamp(attackTime, 0.001f, 0.05f);
        decayTime = Mathf.Clamp(decayTime, 0.08f, 0.25f);
        cutoffBaseHz = Mathf.Clamp(cutoffBaseHz, 600f, 8000f);
        cutoffRandomHz = Mathf.Clamp(cutoffRandomHz, 0f, 2000f);
        stepVolume = Mathf.Clamp(stepVolume, 0f, 0.4f);
        masterVolume = Mathf.Clamp01(masterVolume);
        volumeRandom = Mathf.Clamp(volumeRandom, 0f, 0.2f);

        if (pitchMax < pitchMin)
        {
            pitchMax = pitchMin;
        }
    }
}
