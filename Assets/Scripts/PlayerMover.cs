using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMover : MonoBehaviour
{
    public float moveSpeed = 3.5f;
    public float runtimeSpeedMultiplier = 0.90f;
    public bool spriteFacesRight = true;
    public float rollSpeed = 9.0f;
    public float rollDuration = 0.28f;
    public float rollCooldown = 0.45f;
    public float invulnerabilityDuration = 0.28f;
    public bool useVisualProxyAnimation = true;
    public bool faceMouseCursor = true;
    public float mouseFacingDeadZone = 0.02f;
    public float walkSwayAngle = 4.5f;
    public float walkSwaySpeed = 11f;
    public float idleBreathAmount = 0.035f;
    public float idleBreathSpeed = 2.4f;
    public float idleSwayAngle = 1.2f;
    public float idleSwaySpeed = 1.1f;

    private Rigidbody2D rb;
    private Vector2 input;
    private SpriteRenderer sr;
    private PlayerHealth playerHealth;
    private bool isRolling;
    private Vector2 rollDirection;
    private float rollTimeRemaining;
    private float rollCooldownRemaining;
    private float rollSpinDirection = -1f;
    private Vector2 lastMoveDirection = Vector2.right;
    private readonly RaycastHit2D[] rollHits = new RaycastHit2D[8];
    private readonly Collider2D[] contactHits = new Collider2D[8];
    private ContactFilter2D rollFilter;
    private Collider2D bodyCollider;
    private Vector2 lastSafePosition;
    private bool hasLastSafePosition;
    private const float CollisionSkin = 0.01f;
    private Transform rollVisualTransform;
    private SpriteRenderer rollVisualRenderer;
    private BattleSystem battleSystem;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.sleepMode = RigidbodySleepMode2D.NeverSleep;
        sr = GetComponent<SpriteRenderer>();
        playerHealth = GetComponent<PlayerHealth>();
        EnsureCollider();
        bodyCollider = GetComponent<Collider2D>();
        lastSafePosition = rb.position;
        hasLastSafePosition = true;
        EnsureRollVisual();
        ShowRootVisual();
        battleSystem = GetComponent<BattleSystem>();

        rollFilter.useTriggers = false;
        rollFilter.useLayerMask = true;
        rollFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
    }

    void Update()
    {
        if (rollCooldownRemaining > 0f)
        {
            rollCooldownRemaining -= Time.deltaTime;
        }

        Vector2 raw = Vector2.zero;

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) raw.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) raw.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) raw.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) raw.y += 1f;
        }

        Gamepad gp = Gamepad.current;
        if (gp != null)
        {
            raw += gp.leftStick.ReadValue();
        }

        input = raw;
        if (input.sqrMagnitude > 1f)
        {
            input = input.normalized;
        }

        if (input.sqrMagnitude > 0.0001f)
        {
            lastMoveDirection = input.normalized;
        }

        EnsureOpaqueBody();
        ApplyLocomotionVisualAnimation();

        if (!isRolling && sr != null)
        {
            bool appliedMouseFacing = TryApplyMouseFacing();
            if (!appliedMouseFacing && Mathf.Abs(input.x) > 0.01f)
            {
                bool movingLeft = input.x < 0f;
                SetFacingFlip(spriteFacesRight ? movingLeft : !movingLeft);
            }
        }

        if (kb != null && kb.spaceKey.wasPressedThisFrame && !isRolling && rollCooldownRemaining <= 0f)
        {
            StartRoll();
        }

        if (kb != null && kb.eKey.wasPressedThisFrame)
        {
            if (battleSystem == null) battleSystem = GetComponent<BattleSystem>();
            if (battleSystem != null)
            {
                if (!battleSystem.enabled) battleSystem.enabled = true;
                battleSystem.TryStartBattleFromInput();
            }
        }

        if (isRolling && rollDuration > 0.001f)
        {
            float progress = 1f - Mathf.Clamp01(rollTimeRemaining / rollDuration);
            float angle = progress * 360f;
            if (rollVisualTransform != null)
            {
                rollVisualTransform.localRotation = Quaternion.Euler(0f, 0f, angle * rollSpinDirection);
            }
        }
    }

    void FixedUpdate()
    {
        if (!IsTouchingBlockingCollider())
        {
            lastSafePosition = rb.position;
            hasLastSafePosition = true;
        }

        if (isRolling)
        {
            if (IsTouchingBlockingCollider())
            {
                CancelRollAndRewind();
                return;
            }

            float distance = rollSpeed * Time.fixedDeltaTime;
            bool blocked = MoveWithCollision(rollDirection * distance, true);
            if (blocked || IsTouchingBlockingCollider())
            {
                CancelRollAndRewind();
                return;
            }

            rollTimeRemaining -= Time.fixedDeltaTime;
            if (rollTimeRemaining <= 0f)
            {
                EndRoll();
            }
            return;
        }

        float speed = moveSpeed * Mathf.Max(0.01f, runtimeSpeedMultiplier);
        MoveWithCollision(input * speed * Time.fixedDeltaTime, false);
    }

    void LateUpdate()
    {
        if (!useVisualProxyAnimation) return;
        if (sr == null || rollVisualRenderer == null) return;
        if (!rollVisualRenderer.enabled) return;
        rollVisualRenderer.sortingLayerID = sr.sortingLayerID;
        rollVisualRenderer.sortingOrder = sr.sortingOrder;
    }

    void OnDisable()
    {
        isRolling = false;
        transform.localRotation = Quaternion.identity;
        if (rollVisualTransform != null) rollVisualTransform.localRotation = Quaternion.identity;
        if (rollVisualTransform != null) rollVisualTransform.localPosition = Vector3.zero;
        if (rollVisualTransform != null) rollVisualTransform.localScale = Vector3.one;
        if (rollVisualRenderer != null) rollVisualRenderer.enabled = false;
        if (sr != null)
        {
            sr.enabled = true;
            sr.color = Color.white;
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!isRolling) return;
        CancelRollAndRewind();
    }

    void OnCollisionStay2D(Collision2D collision)
    {
        if (!isRolling) return;
        CancelRollAndRewind();
    }

    void EnsureCollider()
    {
        Collider2D existing = GetComponent<Collider2D>();
        if (existing != null) return;

        CapsuleCollider2D capsule = gameObject.AddComponent<CapsuleCollider2D>();
        capsule.direction = CapsuleDirection2D.Vertical;

        Vector2 size = new Vector2(0.42f, 0.36f);
        Vector2 offset = new Vector2(0f, -0.48f);
        if (sr != null && sr.sprite != null)
        {
            Bounds sb = sr.sprite.bounds;
            float footH = Mathf.Max(0.20f, sb.size.y * 0.16f);
            float footW = Mathf.Max(0.20f, sb.size.x * 0.22f);
            size = new Vector2(footW, footH);
            offset = new Vector2(sb.center.x, sb.min.y + footH * 0.5f);
        }

        capsule.size = size;
        capsule.offset = offset;
    }

    void StartRoll()
    {
        Vector2 desired;
        if (input.sqrMagnitude > 0.0001f)
        {
            desired = input.normalized;
        }
        else
        {
            desired = GetFacingDirection();
        }

        if (desired.sqrMagnitude <= 0.0001f)
        {
            desired = lastMoveDirection.sqrMagnitude > 0.0001f ? lastMoveDirection.normalized : Vector2.right;
        }

        isRolling = true;
        rollDirection = desired.normalized;
        rollTimeRemaining = Mathf.Max(0.01f, rollDuration);
        rollCooldownRemaining = Mathf.Max(0f, rollCooldown);
        rollSpinDirection = ResolveRollSpinDirection(rollDirection);
        ShowRollVisual();

        if (playerHealth != null)
        {
            playerHealth.SetInvulnerable(invulnerabilityDuration);
        }
    }

    void EndRoll()
    {
        isRolling = false;
        transform.localRotation = Quaternion.identity;
        ShowRootVisual();
    }

    Vector2 GetFacingDirection()
    {
        if (sr != null)
        {
            bool facingLeft = spriteFacesRight ? sr.flipX : !sr.flipX;
            return facingLeft ? Vector2.left : Vector2.right;
        }
        return Vector2.right;
    }

    float ResolveRollSpinDirection(Vector2 dir)
    {
        if (dir.x > 0.01f) return -1f; // right: keep current direction
        if (dir.x < -0.01f) return 1f; // left: opposite direction

        Vector2 facing = GetFacingDirection();
        return facing.x < 0f ? 1f : -1f;
    }

    bool IsTouchingBlockingCollider()
    {
        if (bodyCollider == null) return false;
        int count = bodyCollider.GetContacts(rollFilter, contactHits);
        for (int i = 0; i < count; i++)
        {
            Collider2D c = contactHits[i];
            if (c == null) continue;
            if (c.isTrigger) continue;
            return true;
        }
        return false;
    }

    bool MoveWithCollision(Vector2 delta, bool stopAtAnyBlock)
    {
        float distance = delta.magnitude;
        if (distance <= 0.00001f) return false;

        Vector2 dir = delta / distance;
        int hitCount = bodyCollider != null
            ? bodyCollider.Cast(dir, rollFilter, rollHits, distance + CollisionSkin)
            : rb.Cast(dir, rollFilter, rollHits, distance + CollisionSkin);

        float allowed = distance;
        bool blocked = false;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = rollHits[i];
            if (hit.collider == null) continue;
            blocked = true;
            if (hit.distance < allowed) allowed = hit.distance;
        }

        // For dodge roll, any upcoming block cancels the roll immediately.
        if (blocked && stopAtAnyBlock)
        {
            return true;
        }

        allowed = Mathf.Max(0f, allowed - CollisionSkin);
        if (allowed > 0.00001f)
        {
            rb.MovePosition(rb.position + dir * allowed);
        }

        return blocked && allowed <= 0.00001f;
    }

    void CancelRollAndRewind()
    {
        if (hasLastSafePosition)
        {
            rb.position = lastSafePosition;
            transform.position = lastSafePosition;
        }
        EndRoll();
    }

    void EnsureRollVisual()
    {
        if (sr == null) return;
        if (rollVisualRenderer != null && rollVisualTransform != null) return;

        Transform existing = transform.Find("__RollVisual");
        if (existing != null)
        {
            rollVisualTransform = existing;
            rollVisualRenderer = existing.GetComponent<SpriteRenderer>();
            if (rollVisualRenderer == null) rollVisualRenderer = existing.gameObject.AddComponent<SpriteRenderer>();
            return;
        }

        GameObject go = new GameObject("__RollVisual");
        rollVisualTransform = go.transform;
        rollVisualTransform.SetParent(transform, false);
        rollVisualRenderer = go.AddComponent<SpriteRenderer>();
        rollVisualRenderer.enabled = false;
    }

    void SyncRollVisualFromRoot()
    {
        if (sr == null || rollVisualRenderer == null) return;
        rollVisualRenderer.sprite = sr.sprite;
        rollVisualRenderer.color = sr.color;
        rollVisualRenderer.material = sr.sharedMaterial;
        rollVisualRenderer.sortingLayerID = sr.sortingLayerID;
        rollVisualRenderer.sortingOrder = sr.sortingOrder;
        rollVisualRenderer.flipX = sr.flipX;
        rollVisualRenderer.flipY = sr.flipY;
        rollVisualRenderer.drawMode = sr.drawMode;
        rollVisualRenderer.size = sr.size;
        rollVisualRenderer.maskInteraction = sr.maskInteraction;
        rollVisualRenderer.spriteSortPoint = sr.spriteSortPoint;
    }

    void ShowRollVisual()
    {
        EnsureRollVisual();
        SyncRollVisualFromRoot();
        if (rollVisualTransform != null) rollVisualTransform.localRotation = Quaternion.identity;
        if (rollVisualTransform != null) rollVisualTransform.localPosition = Vector3.zero;
        if (rollVisualTransform != null) rollVisualTransform.localScale = Vector3.one;
        if (rollVisualRenderer != null) rollVisualRenderer.enabled = true;
        if (sr != null) sr.enabled = false;
    }

    void ShowRootVisual()
    {
        if (useVisualProxyAnimation)
        {
            EnsureRollVisual();
            SyncRollVisualFromRoot();
            if (rollVisualTransform != null)
            {
                rollVisualTransform.localRotation = Quaternion.identity;
                rollVisualTransform.localPosition = Vector3.zero;
                rollVisualTransform.localScale = Vector3.one;
            }
            if (rollVisualRenderer != null) rollVisualRenderer.enabled = true;
            if (sr != null) sr.enabled = false;
        }
        else
        {
            if (rollVisualTransform != null) rollVisualTransform.localRotation = Quaternion.identity;
            if (rollVisualRenderer != null) rollVisualRenderer.enabled = false;
            if (sr != null)
            {
                sr.enabled = true;
                sr.color = Color.white;
            }
        }
    }

    void SetFacingFlip(bool flip)
    {
        if (sr != null) sr.flipX = flip;
        if (rollVisualRenderer != null) rollVisualRenderer.flipX = flip;
    }

    bool TryApplyMouseFacing()
    {
        if (!faceMouseCursor) return false;

        Camera cam = Camera.main;
        if (cam == null) return false;

        Vector2 mouseScreen = Vector2.zero;
        bool hasPointer = false;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            mouseScreen = mouse.position.ReadValue();
            hasPointer = true;
        }
#if ENABLE_LEGACY_INPUT_MANAGER
        if (!hasPointer)
        {
            mouseScreen = Input.mousePosition;
            hasPointer = true;
        }
#endif
        if (!hasPointer) return false;

        float zToPlayer = Mathf.Abs(transform.position.z - cam.transform.position.z);
        Vector3 mouseWorld3 = cam.ScreenToWorldPoint(new Vector3(mouseScreen.x, mouseScreen.y, zToPlayer));
        float dx = mouseWorld3.x - transform.position.x;
        if (Mathf.Abs(dx) <= mouseFacingDeadZone) return true;

        bool lookingLeft = dx < 0f;
        SetFacingFlip(spriteFacesRight ? lookingLeft : !lookingLeft);
        return true;
    }

    void EnsureOpaqueBody()
    {
        if (sr != null && sr.color != Color.white)
        {
            sr.color = Color.white;
        }
        if (rollVisualRenderer != null && rollVisualRenderer.color != Color.white)
        {
            rollVisualRenderer.color = Color.white;
        }
    }

    void ApplyLocomotionVisualAnimation()
    {
        if (!useVisualProxyAnimation) return;
        if (rollVisualTransform == null || rollVisualRenderer == null) return;
        if (!rollVisualRenderer.enabled) return;

        SyncRollVisualFromRoot();

        if (isRolling)
        {
            return;
        }

        float t = Time.time;
        if (input.sqrMagnitude > 0.0001f)
        {
            float sway = Mathf.Sin(t * walkSwaySpeed) * walkSwayAngle;
            rollVisualTransform.localRotation = Quaternion.Euler(0f, 0f, sway);
            rollVisualTransform.localScale = Vector3.one;
            rollVisualTransform.localPosition = Vector3.zero;
            return;
        }

        float breath = (Mathf.Sin(t * idleBreathSpeed) + 1f) * 0.5f;
        float yScale = 1f - (idleBreathAmount * breath);
        float xScale = 1f + (idleBreathAmount * 0.55f * breath);
        float halfHeight = ResolveVisualHalfHeight();
        float footCompensation = halfHeight * (1f - yScale);
        float idleTilt = Mathf.Sin(t * idleSwaySpeed) * idleSwayAngle;

        rollVisualTransform.localRotation = Quaternion.Euler(0f, 0f, idleTilt);
        rollVisualTransform.localScale = new Vector3(xScale, yScale, 1f);
        rollVisualTransform.localPosition = new Vector3(0f, -footCompensation, 0f);
    }

    float ResolveVisualHalfHeight()
    {
        Sprite sprite = null;
        if (rollVisualRenderer != null) sprite = rollVisualRenderer.sprite;
        if (sprite == null && sr != null) sprite = sr.sprite;
        if (sprite == null) return 0.5f;
        return Mathf.Max(0.01f, sprite.bounds.extents.y);
    }

    public Transform GetVisualRoot()
    {
        if (useVisualProxyAnimation && rollVisualTransform != null && rollVisualRenderer != null && rollVisualRenderer.enabled)
        {
            return rollVisualTransform;
        }
        return transform;
    }
}
