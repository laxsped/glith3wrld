using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class СкрежетТяги : MonoBehaviour
{
    [Header("Громкость")]
    [Range(0f, 1f)]
    [SerializeField] private float masterVolume = 0.3f;
    [SerializeField] private float runMultiplier = 1.2f;
    [SerializeField] private float smoothing = 8f;

    [Header("Тембр")]
    [SerializeField] private float highPassHz = 220f;
    [SerializeField] private float lowPassHz = 2400f;
    [SerializeField] private float tonalNoiseMix = 0.35f;

    [Header("Стерео")]
    [Range(0f, 0.25f)]
    [SerializeField] private float stereoWidth = 0.06f;
    [SerializeField] private float stereoPanSpeed = 0.7f;

    private AudioSource source;
    private AudioClip driverClip;
    private float sampleRate = 48000f;
    private uint rngState = 0xA341316Cu;
    private float noiseTime;

    private float hpState;
    private float lpState;
    private float prevInput;
    private float tonalPhase;

    private float targetIntensity;
    private float currentIntensity;
    private float targetSpeedNorm;
    private float targetMassNorm;
    private bool targetRunning;

    private void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 1f;
        source.mute = false;

        sampleRate = Mathf.Max(8000, AudioSettings.outputSampleRate);
        if (source.clip == null)
        {
            int sr = Mathf.RoundToInt(sampleRate);
            driverClip = AudioClip.Create("DragNoiseDriver", sr, 1, sr, false);
            source.clip = driverClip;
        }

        if (!source.isPlaying)
        {
            source.Play();
        }
    }

    private void Update()
    {
        float target = targetIntensity * (targetRunning ? runMultiplier : 1f);
        currentIntensity = Mathf.Lerp(currentIntensity, target, Time.unscaledDeltaTime * Mathf.Max(0.01f, smoothing));
    }

    private void OnAudioFilterRead(float[] data, int channels)
    {
        float sr = sampleRate;
        float dt = 1f / sr;

        float hpA = Mathf.Exp(-2f * Mathf.PI * Mathf.Max(20f, highPassHz) / sr);
        float lpA = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Max(highPassHz + 10f, lowPassHz) / sr);

        for (int i = 0; i < data.Length; i += channels)
        {
            noiseTime += dt;

            float white = NextWhite();
            float tonalFreq = Mathf.Lerp(65f, 150f, targetMassNorm);
            tonalPhase += 2f * Mathf.PI * tonalFreq * dt;
            if (tonalPhase > Mathf.PI * 2f)
            {
                tonalPhase -= Mathf.PI * 2f;
            }

            float tonal = Mathf.Sin(tonalPhase) * 0.35f + Mathf.Sin(tonalPhase * 2f) * 0.15f;
            float sourceSample = Mathf.Lerp(white, tonal, tonalNoiseMix) * Mathf.Lerp(0.2f, 1f, targetSpeedNorm);

            hpState = hpA * (hpState + sourceSample - prevInput);
            prevInput = sourceSample;
            lpState += lpA * (hpState - lpState);

            float outSample = lpState * currentIntensity * masterVolume;
            float pan = (Mathf.PerlinNoise(noiseTime * stereoPanSpeed, 31.7f) * 2f - 1f) * stereoWidth;
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

    private float NextWhite()
    {
        rngState ^= rngState << 13;
        rngState ^= rngState >> 17;
        rngState ^= rngState << 5;
        return (rngState / 4294967295f) * 2f - 1f;
    }

    public void SetDragState(bool dragging, float horizontalSpeed, bool running, float mass)
    {
        targetRunning = running;
        targetSpeedNorm = Mathf.Clamp01(horizontalSpeed / 2.7f);
        targetMassNorm = Mathf.Clamp01((mass - 1f) / 25f);
        targetIntensity = dragging ? Mathf.Clamp01(0.2f + targetSpeedNorm * 0.8f) : 0f;
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
    }

    public float GetMasterVolume()
    {
        return masterVolume;
    }
}
