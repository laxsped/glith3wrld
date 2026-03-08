using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class SurfaceMovementAudio : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Rigidbody targetRigidbody;
    [SerializeField] private Collider groundProbeCollider;
    [SerializeField] private Transform rayOrigin;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.1f;

    [Header("Step Timing")]
    [SerializeField] private float walkStepInterval = 0.45f;
    [SerializeField] private float runStepInterval = 0.3f;
    [SerializeField] private float minHorizontalSpeed = 0.08f;

    [Header("Volume")]
    [SerializeField] [Range(0f, 1f)] private float walkVolume = 0.6f;
    [SerializeField] [Range(0f, 1f)] private float runVolume = 0.8f;
    [SerializeField] [Range(0f, 1f)] private float jumpVolume = 0.75f;
    [SerializeField] [Range(0f, 1f)] private float landVolume = 0.85f;
    [SerializeField] [Range(-3f, 3f)] private float pitchRandomRange = 0.06f;
    [SerializeField] [Range(0f, 2f)] private float masterVolume = 1f;

    [Header("Surface Audio")]
    [SerializeField] private SurfaceAudioSet defaultSurface = new SurfaceAudioSet { id = "Default" };
    [SerializeField] private SurfaceAudioSet[] surfaces;

    [Header("Surface Detection Priority")]
    [SerializeField] private bool preferPhysicMaterial = true;
    [SerializeField] private bool preferTag = true;
    [SerializeField] private bool preferLayer = true;

    private AudioSource audioSource;
    private readonly Dictionary<string, SurfaceAudioSet> byId = new Dictionary<string, SurfaceAudioSet>(StringComparer.OrdinalIgnoreCase);
    private float stepTimer;
    private bool wasGrounded;

    [Serializable]
    public class SurfaceAudioSet
    {
        public string id;
        public PhysicsMaterial physicMaterial;
        public string surfaceTag;
        public LayerMask layerMask;
        public AudioClip[] walk;
        public AudioClip[] run;
        public AudioClip[] jump;
        public AudioClip[] land;
    }

    private void Awake()
    {
        GameInputBindings.EnsureLoaded();

        audioSource = GetComponent<AudioSource>();
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody>();
        }
        if (groundProbeCollider == null)
        {
            groundProbeCollider = GetComponent<Collider>();
        }
        if (rayOrigin == null)
        {
            rayOrigin = transform;
        }

        audioSource.playOnAwake = false;
        audioSource.loop = false;

        RebuildLookup();
    }

    private void OnValidate()
    {
        groundCheckDistance = Mathf.Max(0.01f, groundCheckDistance);
        walkStepInterval = Mathf.Max(0.05f, walkStepInterval);
        runStepInterval = Mathf.Max(0.05f, runStepInterval);
        minHorizontalSpeed = Mathf.Max(0.001f, minHorizontalSpeed);
    }

    private void Update()
    {
        if (GameInputBindings.InputBlocked)
        {
            stepTimer = 0f;
            wasGrounded = TryGetGroundHit(out _);
            return;
        }

        bool groundedNow = TryGetGroundHit(out RaycastHit hit);
        Vector2 moveInput = ReadMoveInput();
        bool hasMoveInput = moveInput.sqrMagnitude > 0.001f;

        Vector3 horizontalVelocity = Vector3.zero;
        if (targetRigidbody != null)
        {
            Vector3 rbVelocity = targetRigidbody.linearVelocity;
            horizontalVelocity = new Vector3(rbVelocity.x, 0f, rbVelocity.z);
        }

        bool hasHorizontalMotion = horizontalVelocity.magnitude >= minHorizontalSpeed || hasMoveInput;
        bool runPressed = !GameInputBindings.RunLocked && Input.GetKey(GameInputBindings.RunKey);
        bool runAllowedByInput = runPressed && hasMoveInput;

        if (groundedNow && !wasGrounded)
        {
            PlayOneShot(hit, FootEvent.Land, landVolume);
            stepTimer = 0f;
        }

        if (!groundedNow && wasGrounded)
        {
            PlayOneShot(hit, FootEvent.Jump, jumpVolume);
            stepTimer = 0f;
        }

        if (groundedNow && hasHorizontalMotion)
        {
            float interval = runAllowedByInput ? runStepInterval : walkStepInterval;
            stepTimer += Time.deltaTime;
            if (stepTimer >= interval)
            {
                stepTimer = 0f;
                PlayOneShot(hit, runAllowedByInput ? FootEvent.Run : FootEvent.Walk, runAllowedByInput ? runVolume : walkVolume);
            }
        }
        else if (!groundedNow)
        {
            stepTimer = 0f;
        }

        wasGrounded = groundedNow;
    }

    private static Vector2 ReadMoveInput()
    {
        float x = 0f;
        float y = 0f;

        if (Input.GetKey(GameInputBindings.LeftKey))
        {
            x -= 1f;
        }
        if (Input.GetKey(GameInputBindings.RightKey))
        {
            x += 1f;
        }
        if (Input.GetKey(GameInputBindings.ForwardKey))
        {
            y += 1f;
        }
        if (Input.GetKey(GameInputBindings.BackwardKey))
        {
            y -= 1f;
        }

        return new Vector2(x, y);
    }

    private bool TryGetGroundHit(out RaycastHit hit)
    {
        Vector3 origin;
        float distance;

        if (groundProbeCollider != null)
        {
            Bounds b = groundProbeCollider.bounds;
            origin = b.center;
            distance = b.extents.y + groundCheckDistance;
        }
        else
        {
            origin = rayOrigin.position + Vector3.up * 0.1f;
            distance = 0.7f;
        }

        return Physics.Raycast(origin, Vector3.down, out hit, distance, groundMask, QueryTriggerInteraction.Ignore);
    }

    private enum FootEvent
    {
        Walk,
        Run,
        Jump,
        Land
    }

    private void PlayOneShot(RaycastHit hit, FootEvent footEvent, float volume)
    {
        SurfaceAudioSet set = ResolveSurface(hit.collider);
        AudioClip clip = GetRandomClip(set, footEvent);
        if (clip == null)
        {
            clip = GetRandomClip(defaultSurface, footEvent);
        }
        if (clip == null)
        {
            return;
        }

        audioSource.pitch = 1f + UnityEngine.Random.Range(-pitchRandomRange, pitchRandomRange);
        audioSource.PlayOneShot(clip, volume * masterVolume);
    }

    private AudioClip GetRandomClip(SurfaceAudioSet set, FootEvent footEvent)
    {
        if (set == null)
        {
            return null;
        }

        AudioClip[] clips = null;
        switch (footEvent)
        {
            case FootEvent.Walk:
                clips = set.walk;
                break;
            case FootEvent.Run:
                clips = set.run;
                break;
            case FootEvent.Jump:
                clips = set.jump;
                break;
            case FootEvent.Land:
                clips = set.land;
                break;
        }

        if (clips == null || clips.Length == 0)
        {
            return null;
        }

        return clips[UnityEngine.Random.Range(0, clips.Length)];
    }

    private SurfaceAudioSet ResolveSurface(Collider c)
    {
        if (c == null)
        {
            return defaultSurface;
        }

        if (preferPhysicMaterial && c.sharedMaterial != null)
        {
            foreach (SurfaceAudioSet s in surfaces)
            {
                if (s != null && s.physicMaterial != null && s.physicMaterial == c.sharedMaterial)
                {
                    return s;
                }
            }
        }

        if (preferTag && !string.IsNullOrEmpty(c.tag))
        {
            foreach (SurfaceAudioSet s in surfaces)
            {
                if (s != null && !string.IsNullOrEmpty(s.surfaceTag) && c.CompareTag(s.surfaceTag))
                {
                    return s;
                }
            }
        }

        if (preferLayer)
        {
            int layerBit = 1 << c.gameObject.layer;
            foreach (SurfaceAudioSet s in surfaces)
            {
                if (s != null && (s.layerMask.value & layerBit) != 0)
                {
                    return s;
                }
            }
        }

        if (byId.TryGetValue(c.gameObject.name, out SurfaceAudioSet byName) && byName != null)
        {
            return byName;
        }

        return defaultSurface;
    }

    public void RebuildLookup()
    {
        byId.Clear();
        if (surfaces == null)
        {
            return;
        }

        foreach (SurfaceAudioSet s in surfaces)
        {
            if (s == null || string.IsNullOrWhiteSpace(s.id))
            {
                continue;
            }

            if (!byId.ContainsKey(s.id))
            {
                byId.Add(s.id.Trim(), s);
            }
        }
    }

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp(value, 0f, 2f);
    }

    public float GetMasterVolume()
    {
        return Mathf.Max(0f, masterVolume);
    }
}


