using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Rendering.HighDefinition;

public class SmartLightFlicker : MonoBehaviour
{
    [Header("Список ламп (если пусто — ищет в детях)")]
    public List<HDAdditionalLightData> lightsToAffect = new List<HDAdditionalLightData>();

    [Header("Настройки яркости")]
    public float maxIntensity = 363815f; 
    public float minIntensity = 50f;     // "Темный" режим

    [Header("Настройки вспышек")]
    [Range(0, 1)] public float flashChance = 0.05f; // Шанс вспышки в каждом кадре
    public float smoothSpeed = 20f;                // Скорость возврата к темноте

    [Header("Звук")]
    public AudioSource audioSource;
    public AudioClip[] flickerClips;

    private bool isPlayerInside = false;
    private float targetIntensity;
    private float currentIntensity;

    void Start()
    {
        if (lightsToAffect == null || lightsToAffect.Count == 0)
            lightsToAffect.AddRange(GetComponentsInChildren<HDAdditionalLightData>());

        if (audioSource == null) audioSource = GetComponent<AudioSource>();

        targetIntensity = minIntensity;
        currentIntensity = minIntensity;
        ToggleLights(false);
    }

    void Update()
    {
        if (!isPlayerInside || lightsToAffect.Count == 0) return;

        // Логика вспышки
        if (Random.value < flashChance)
        {
            currentIntensity = maxIntensity; // Мгновенная вспышка до максимума
            PlayRandomSound();
        }
        else
        {
            // Плавное затухание обратно к минимуму
            currentIntensity = Mathf.Lerp(currentIntensity, minIntensity, Time.deltaTime * smoothSpeed);
        }

        ApplyIntensity(currentIntensity);
    }

    void PlayRandomSound()
    {
        if (flickerClips.Length > 0 && audioSource != null && !audioSource.isPlaying)
        {
            audioSource.PlayOneShot(flickerClips[Random.Range(0, flickerClips.Length)]);
        }
    }

    void ApplyIntensity(float value)
    {
        foreach (var light in lightsToAffect)
        {
            if (light == null)
            {
                continue;
            }

            Light unityLight = light.GetComponent<Light>();
            if (unityLight != null)
            {
                unityLight.intensity = value;
            }
        }
    }

    private void ToggleLights(bool state)
    {
        ApplyIntensity(state ? minIntensity : 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) isPlayerInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInside = false;
            ApplyIntensity(0f);
            if (audioSource != null) audioSource.Stop();
        }
    }
}
