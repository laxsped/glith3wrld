using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class WindNoiseGenerator : MonoBehaviour
{
    [Header("Level")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 0.043f;
    [Range(0f, 1f)]
    [SerializeField] private float baseWind = 0.35f;
    [Range(0f, 1f)]
    [SerializeField] private float gustAmount = 0.65f;
    [SerializeField] private float runVolumeMultiplier = 1.45f;
    [SerializeField] private float runVolumeSmoothing = 6f;

    [Header("Motion")]
    [SerializeField] private float gustSpeed = 0.12f;
    [SerializeField] private float turbulenceSpeed = 1.6f;
    [Range(0f, 0.35f)]
    [SerializeField] private float stereoWidth = 0.12f;
    [SerializeField] private float stereoPanSpeed = 0.17f;

    [Header("Tone")]
    [SerializeField] private float lowCutHz = 120f;
    [SerializeField] private float highCutHz = 2500f;

    private AudioSource source;
    private AudioClip driverClip;
    private float noiseTime;
    private float sampleRate = 48000f;
    private uint rngState = 0x12345678u;
    private float runtimeVolumeMultiplier = 1f;

    // Simple 1-pole filters state
    private float hpState;
    private float lpState;
    private float prevInput;

    // Pink-ish noise helper state (Voss-McCartney style approximation)
    private float pinkA;
    private float pinkB;
    private float pinkC;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 1f;
        source.mute = false;

        // Keep AudioSource running even without user clip, so filter callback always executes.
        sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
        if (source.clip == null)
        {
            int sr = Mathf.RoundToInt(sampleRate);
            driverClip = AudioClip.Create("WindDriver", sr, 1, sr, false);
            source.clip = driverClip;
        }

        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        float sr = sampleRate;
        float dt = 1f / sr;

        float hpA = Mathf.Exp(-2f * Mathf.PI * Mathf.Max(10f, lowCutHz) / sr);
        float lpA = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Max(lowCutHz + 10f, highCutHz) / sr);

        for (int i = 0; i < data.Length; i += channels)
        {
            noiseTime += dt;

            float gust = Mathf.PerlinNoise(noiseTime * gustSpeed, 0.1234f);
            float turb = Mathf.PerlinNoise(noiseTime * turbulenceSpeed, 9.4321f);
            float env = Mathf.Clamp01(baseWind + gustAmount * (gust * 0.85f + turb * 0.15f));

            float white = NextWhite();

            // Pink-ish blend to remove harshness
            pinkA = 0.99765f * pinkA + white * 0.0990460f;
            pinkB = 0.96300f * pinkB + white * 0.2965164f;
            pinkC = 0.57000f * pinkC + white * 1.0526913f;
            float pink = (pinkA + pinkB + pinkC + white * 0.1848f) * 0.2f;

            float sample = pink * env;

            // High-pass
            hpState = hpA * (hpState + sample - prevInput);
            prevInput = sample;

            // Low-pass
            lpState += lpA * (hpState - lpState);

            float outSample = lpState * masterVolume * runtimeVolumeMultiplier;
            float pan = (Mathf.PerlinNoise(noiseTime * stereoPanSpeed, 21.7f) * 2f - 1f) * stereoWidth;
            float left = outSample * (1f - pan);
            float right = outSample * (1f + pan);

            if (channels <= 1)
            {
                data[i] += outSample;
                continue;
            }

            data[i] += left;
            data[i + 1] += right;

            for (int c = 2; c < channels; c++)
            {
                data[i + c] += outSample;
            }
        }
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        bool isMoving = (horizontal * horizontal + vertical * vertical) > 0.001f;
        bool isRunning = isMoving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
        float target = isRunning ? runVolumeMultiplier : 1f;
        runtimeVolumeMultiplier = Mathf.Lerp(runtimeVolumeMultiplier, target, Time.unscaledDeltaTime * runVolumeSmoothing);
    }

    private float NextWhite()
    {
        // xorshift32 PRNG, safe for audio thread and deterministic.
        rngState ^= rngState << 13;
        rngState ^= rngState >> 17;
        rngState ^= rngState << 5;
        return (rngState / 4294967295f) * 2f - 1f;
    }
}
