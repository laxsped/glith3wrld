using UnityEngine;

public class PlayerWASDAnimator : MonoBehaviour
{
    private const string PausePrefPrefix = "PauseSimple.";
    private const string CharacterLightingKey = PausePrefPrefix + "character_lighting";
    private const string HdrpUnlitShaderName = "HDRP/Unlit";
    private const string HdrpLitShaderName = "HDRP/Lit";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private bool useXZPlane = true;
    [SerializeField] private float rightScaleX = 0.05f;
    [SerializeField] private float leftScaleX = -0.05f;
    [SerializeField] private bool lockVerticalInput;
    [SerializeField] private float collisionSkin = 0.01f;

    [Header("Render Target")]
    [SerializeField] private Renderer targetRenderer;
    [SerializeField] private Transform visualTransform;

    [Header("Animation Frames")]
    [SerializeField] private float animationFps = 10f;
    [SerializeField] private DirectionalFrames idleDirectionalFrames;
    [SerializeField] private DirectionalFrames walkDirectionalFrames;
    [SerializeField] private DirectionalFrames runDirectionalFrames;
    [SerializeField] private DirectionalFrames jumpDirectionalFrames;
    [SerializeField] private float runSpeedMultiplier = 1.8f;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 4.5f;
    [SerializeField] private float groundCheckDistance = 0.08f;
    [SerializeField] private LayerMask groundMask = ~0;

    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int UnlitColorMapId = Shader.PropertyToID("_UnlitColorMap");
    private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");

    private Material runtimeMaterial;
    private Material baseMaterialAsset;
    private Rigidbody rb;
    private Collider bodyCollider;
    private float frameTimer;
    private int frameIndex;
    private Vector2 cachedInput;
    private bool cachedIsRunning;
    private bool cachedJumpPressed;
    private bool isGrounded;
    private bool inputEnabled = true;
    private bool hasExternalSpeedCap;
    private float externalSpeedCap = float.PositiveInfinity;
    private int cachedCharacterLightingMode = -1;

    private enum AnimState
    {
        Idle,
        Walk,
        Run,
        Jump
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
        GameInputBindings.EnsureLoaded();

        if (targetRenderer == null)
        {
            targetRenderer = GetComponentInChildren<Renderer>();
        }

        if (targetRenderer != null)
        {
            baseMaterialAsset = targetRenderer.sharedMaterial;
            runtimeMaterial = targetRenderer.material;
            if (visualTransform == null)
            {
                visualTransform = targetRenderer.transform;
            }
        }

        rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();
        ApplyCharacterLightingFromPrefs(true);
    }

    private void Update()
    {
        if (!inputEnabled || GameInputBindings.InputBlocked)
        {
            cachedInput = Vector2.zero;
            cachedIsRunning = false;
            cachedJumpPressed = false;
            UpdateAnimationState(false, false, isGrounded);
            TickAnimation();
            return;
        }

        cachedInput = ReadMovementInput();
        if (lockVerticalInput)
        {
            cachedInput.y = 0f;
        }

        bool isMoving = cachedInput.sqrMagnitude > 0.001f;
        cachedIsRunning = isMoving && !GameInputBindings.RunLocked && Input.GetKey(GameInputBindings.RunKey);
        if (Input.GetKeyDown(GameInputBindings.JumpKey) && isGrounded)
        {
            cachedJumpPressed = true;
        }

        UpdateFacingDirection(cachedInput, isMoving);
        UpdateVisualFlip(cachedInput, isMoving);
        ApplyCharacterLightingFromPrefs(false);
        UpdateAnimationState(isMoving, cachedIsRunning, isGrounded);
        TickAnimation();
    }

    private void FixedUpdate()
    {
        RefreshGrounded();
        TryJump();
        Move(cachedInput, cachedIsRunning);
        RefreshGrounded();
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
        if (hasExternalSpeedCap)
        {
            speed = Mathf.Min(speed, Mathf.Max(0f, externalSpeedCap));
        }
        Vector3 step = delta * speed * Time.fixedDeltaTime;

        if (rb != null)
        {
            Vector3 nextPosition = rb.position + step;
            float stepDistance = step.magnitude;
            if (stepDistance > 0.0001f && rb.SweepTest(step.normalized, out RaycastHit hit, stepDistance + collisionSkin, QueryTriggerInteraction.Ignore))
            {
                float safeDistance = Mathf.Max(0f, hit.distance - collisionSkin);
                Vector3 basePosition = rb.position + step.normalized * safeDistance;

                // Slide along the hit surface instead of stopping dead in corners.
                Vector3 remaining = step - (step.normalized * safeDistance);
                Vector3 slideVector = Vector3.ProjectOnPlane(remaining, hit.normal);
                float slideDistance = slideVector.magnitude;

                if (slideDistance > 0.0001f)
                {
                    Vector3 slideDir = slideVector / slideDistance;
                    if (rb.SweepTest(slideDir, out RaycastHit slideHit, slideDistance + collisionSkin, QueryTriggerInteraction.Ignore))
                    {
                        float safeSlide = Mathf.Max(0f, slideHit.distance - collisionSkin);
                        nextPosition = basePosition + slideDir * safeSlide;
                    }
                    else
                    {
                        nextPosition = basePosition + slideVector;
                    }
                }
                else
                {
                    nextPosition = basePosition;
                }
            }

            rb.MovePosition(nextPosition);
        }
        else
        {
            transform.position += step;
        }
    }

    private void UpdateAnimationState(bool isMoving, bool isRunning, bool grounded)
    {
        AnimState nextState = AnimState.Idle;
        if (!grounded)
        {
            nextState = AnimState.Jump;
        }
        else if (isMoving)
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
        else if (currentState == AnimState.Jump)
        {
            directional = jumpDirectionalFrames;
        }

        return GetFramesForDirection(directional, currentDirection);
    }

    private void TryJump()
    {
        if (!cachedJumpPressed || !isGrounded || rb == null)
        {
            return;
        }

        Vector3 v = rb.linearVelocity;
        v.y = 0f;
        rb.linearVelocity = v;
        rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        cachedJumpPressed = false;
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
        jumpForce = Mathf.Max(0f, jumpForce);
        groundCheckDistance = Mathf.Max(0.001f, groundCheckDistance);

        if (Mathf.Abs(rightScaleX) < 0.0001f)
        {
            rightScaleX = 0.05f;
        }

        if (Mathf.Abs(leftScaleX) < 0.0001f)
        {
            leftScaleX = -0.05f;
        }

        collisionSkin = Mathf.Clamp(collisionSkin, 0.001f, 0.05f);
    }

    private void Start()
    {
        ApplyCharacterLightingFromPrefs(true);
        ApplyCurrentFrame();
    }

    public void SetVerticalInputLocked(bool isLocked)
    {
        lockVerticalInput = isLocked;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        if (!inputEnabled || GameInputBindings.InputBlocked)
        {
            cachedInput = Vector2.zero;
            cachedIsRunning = false;
            cachedJumpPressed = false;
        }
    }

    public string GetFacingDirectionId()
    {
        switch (currentDirection)
        {
            case FacingDirection.Front:
                return "front";
            case FacingDirection.FrontSide:
                return "frontSide";
            case FacingDirection.Side:
                return "side";
            case FacingDirection.BackSide:
                return "backSide";
            case FacingDirection.Back:
                return "back";
            default:
                return "front";
        }
    }

    public void ForceRefreshCurrentFrame()
    {
        ApplyCurrentFrame();
    }

    public void SetExternalSpeedCap(float maxSpeed)
    {
        hasExternalSpeedCap = true;
        externalSpeedCap = Mathf.Max(0f, maxSpeed);
    }

    public void ClearExternalSpeedCap()
    {
        hasExternalSpeedCap = false;
        externalSpeedCap = float.PositiveInfinity;
    }

    private void ApplyCharacterLightingFromPrefs(bool force)
    {
        if (targetRenderer == null)
        {
            return;
        }

        int mode = Mathf.Clamp(PlayerPrefs.GetInt(CharacterLightingKey, 0), 0, 1);
        if (!force && mode == cachedCharacterLightingMode)
        {
            return;
        }

        cachedCharacterLightingMode = mode;
        string targetShaderName = mode == 0 ? HdrpUnlitShaderName : HdrpLitShaderName;
        Shader targetShader = Shader.Find(targetShaderName);
        if (targetShader == null)
        {
            return;
        }

        Material src = runtimeMaterial != null ? runtimeMaterial : baseMaterialAsset;
        if (src != null && src.shader == targetShader)
        {
            return;
        }

        Material newMat = src != null ? new Material(src) : new Material(targetShader);
        newMat.shader = targetShader;

        targetRenderer.material = newMat;
        runtimeMaterial = targetRenderer.material;
        ApplyCurrentFrame();
    }

    private static Vector2 ReadMovementInput()
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
}


