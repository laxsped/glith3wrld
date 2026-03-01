using UnityEngine;

public class PlayerWASDAnimator : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private bool useXZPlane = true;
    [SerializeField] private float rightScaleX = 0.05f;
    [SerializeField] private float leftScaleX = -0.05f;

    [Header("Render Target")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Transform visualTransform;

    [Header("Animation Frames")]
    [SerializeField] private float animationFps = 10f;
    [SerializeField] private DirectionalFrames idleDirectionalFrames;
    [SerializeField] private DirectionalFrames walkDirectionalFrames;
    [SerializeField] private DirectionalFrames runDirectionalFrames;
    [SerializeField] private float runSpeedMultiplier = 1.8f;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int UnlitColorMapId = Shader.PropertyToID("_UnlitColorMap");
    private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");

    private Material runtimeMaterial;
    private Rigidbody rb;
    private float frameTimer;
    private int frameIndex;
    private Vector2 cachedInput;
    private bool cachedIsRunning;

    private enum AnimState
    {
        Idle,
        Walk,
        Run
    }

    private enum FacingDirection
    {
        Front,
        FrontSide,
        Side,
        BackSide,
        Back
    }

    [System.Serializable]
    private struct DirectionalFrames
    {
        public Texture2D[] front;
        public Texture2D[] frontSide;
        public Texture2D[] side;
        public Texture2D[] backSide;
        public Texture2D[] back;
    }

    private AnimState currentState = AnimState.Idle;
    private FacingDirection currentDirection = FacingDirection.Front;
    private bool facingLeft;

    private void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        if (targetRenderer != null)
        {
            runtimeMaterial = targetRenderer.material;
            if (visualTransform == null)
            {
                visualTransform = targetRenderer.transform;
            }
        }

        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        cachedInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        bool isMoving = cachedInput.sqrMagnitude > 0.001f;
        cachedIsRunning = isMoving && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        UpdateFacingDirection(cachedInput, isMoving);
        UpdateVisualFlip(cachedInput, isMoving);
        UpdateAnimationState(isMoving, cachedIsRunning);
        TickAnimation();
    }

    private void FixedUpdate()
    {
        Move(cachedInput, cachedIsRunning);
    }

    private void Move(Vector2 input, bool isRunning)
    {
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }

        float horizontal = -input.x;

        Vector3 delta = useXZPlane
            ? new Vector3(input.y, 0f, horizontal)
            : new Vector3(input.x, input.y, 0f);

        float speed = isRunning ? moveSpeed * runSpeedMultiplier : moveSpeed;
        Vector3 step = delta * speed * Time.fixedDeltaTime;

        if (rb != null)
        {
            rb.MovePosition(rb.position + step);
        }
        else
        {
            transform.position += step;
        }
    }

    private void UpdateAnimationState(bool isMoving, bool isRunning)
    {
        AnimState nextState = AnimState.Idle;
        if (isMoving)
        {
            nextState = isRunning ? AnimState.Run : AnimState.Walk;
        }

        if (nextState == currentState)
        {
            return;
        }

        currentState = nextState;
        frameIndex = 0;
        frameTimer = 0f;
        ApplyCurrentFrame();
    }

    private void UpdateFacingDirection(Vector2 input, bool isMoving)
    {
        if (!isMoving)
        {
            return;
        }

        float absX = Mathf.Abs(input.x);
        float absY = Mathf.Abs(input.y);

        bool hasHorizontal = absX > 0.001f;
        bool hasVertical = absY > 0.001f;

        if (hasVertical && !hasHorizontal)
        {
            currentDirection = input.y > 0f ? FacingDirection.Back : FacingDirection.Front;
            return;
        }

        if (hasHorizontal && !hasVertical)
        {
            currentDirection = FacingDirection.Side;
            return;
        }

        currentDirection = input.y > 0f ? FacingDirection.BackSide : FacingDirection.FrontSide;
    }

    private void TickAnimation()
    {
        Texture2D[] frames = GetActiveFrames();
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        float frameDuration = 1f / Mathf.Max(1f, animationFps);
        frameTimer += Time.deltaTime;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            frameIndex = (frameIndex + 1) % frames.Length;
            ApplyCurrentFrame();
        }
    }

    private void UpdateVisualFlip(Vector2 input, bool isMoving)
    {
        float horizontal = -input.x;
        if (isMoving && Mathf.Abs(horizontal) > 0.001f)
        {
            facingLeft = horizontal < 0f;
        }

        if (visualTransform == null)
        {
            return;
        }

        Vector3 s = visualTransform.localScale;
        s.x = facingLeft ? leftScaleX : rightScaleX;
        visualTransform.localScale = s;
    }

    private Texture2D[] GetActiveFrames()
    {
        DirectionalFrames directional = idleDirectionalFrames;
        if (currentState == AnimState.Walk)
        {
            directional = walkDirectionalFrames;
        }
        else if (currentState == AnimState.Run)
        {
            directional = runDirectionalFrames;
        }

        return GetFramesForDirection(directional, currentDirection);
    }

    private static Texture2D[] GetFramesForDirection(DirectionalFrames frames, FacingDirection direction)
    {
        switch (direction)
        {
            case FacingDirection.Front:
                return frames.front;
            case FacingDirection.FrontSide:
                return frames.frontSide;
            case FacingDirection.Side:
                return frames.side;
            case FacingDirection.BackSide:
                return frames.backSide;
            case FacingDirection.Back:
                return frames.back;
            default:
                return frames.front;
        }
    }

    private void ApplyCurrentFrame()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        Texture2D[] frames = GetActiveFrames();
        if (frames == null || frames.Length == 0)
        {
            return;
        }

        Texture frame = frames[Mathf.Clamp(frameIndex, 0, frames.Length - 1)];

        if (runtimeMaterial.HasProperty(BaseMapId))
        {
            runtimeMaterial.SetTexture(BaseMapId, frame);
        }

        if (runtimeMaterial.HasProperty(MainTexId))
        {
            runtimeMaterial.SetTexture(MainTexId, frame);
        }

        if (runtimeMaterial.HasProperty(UnlitColorMapId))
        {
            runtimeMaterial.SetTexture(UnlitColorMapId, frame);
        }

        if (runtimeMaterial.HasProperty(BaseColorMapId))
        {
            runtimeMaterial.SetTexture(BaseColorMapId, frame);
        }
    }

    private void OnValidate()
    {
        animationFps = Mathf.Max(1f, animationFps);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        runSpeedMultiplier = Mathf.Max(1f, runSpeedMultiplier);

        if (Mathf.Abs(rightScaleX) < 0.0001f)
        {
            rightScaleX = 0.05f;
        }

        if (Mathf.Abs(leftScaleX) < 0.0001f)
        {
            leftScaleX = -0.05f;
        }
    }

    private void Start()
    {
        ApplyCurrentFrame();
    }
}
