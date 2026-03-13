using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class WhelplingBounceAnimator : MonoBehaviour
{
    public float idleCycleSeconds = 0.9f;

    private SpriteRenderer sr;
    private Sprite defaultSprite;
    private SpriteFromTexture spriteFromTexture;
    private Vector3 baseScale = Vector3.one;
    private float idleTime;
    private Follower follower;
    private WildCreatureAI wild;
    private float spriteLocalHeight;
    private float lastGroundOffsetY;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        spriteFromTexture = GetComponent<SpriteFromTexture>();
        follower = GetComponent<Follower>();
        wild = GetComponent<WildCreatureAI>();
        baseScale = transform.localScale;
        RestoreDefaultSprite();
    }

    void OnEnable()
    {
        RestoreDefaultSprite();
    }

    void OnDisable()
    {
        RemoveGroundOffset();
        transform.localScale = baseScale;
    }

    void LateUpdate()
    {
        if (sr == null) return;
        if (defaultSprite == null)
        {
            RestoreDefaultSprite();
        }
        if (defaultSprite != null && sr.sprite != defaultSprite)
        {
            sr.sprite = defaultSprite;
        }

        bool hopping = (follower != null && follower.IsHopping) || (wild != null && wild.IsHopping);
        if (hopping)
        {
            idleTime = 0f;
            RemoveGroundOffset();
            return;
        }

        ApplyIdleBounce();
    }

    void RestoreDefaultSprite()
    {
        if (sr == null) return;
        if (spriteFromTexture != null && spriteFromTexture.enabled)
        {
            spriteFromTexture.ApplySprite();
        }
        defaultSprite = sr.sprite;
        baseScale = transform.localScale;
        spriteLocalHeight = defaultSprite != null ? defaultSprite.bounds.size.y : 0f;
        lastGroundOffsetY = 0f;
    }

    public void RefreshDefaultSprite()
    {
        RestoreDefaultSprite();
    }

    void ApplyIdleBounce()
    {
        RemoveGroundOffset();

        if (idleCycleSeconds <= 0.01f)
        {
            transform.localScale = baseScale;
            lastGroundOffsetY = 0f;
            return;
        }

        idleTime += Time.deltaTime;
        float t = (idleTime % idleCycleSeconds) / idleCycleSeconds;

        float sx = 1f;
        float sy = 1f;

        if (t < 0.16f)
        {
            float u = t / 0.16f;
            sx = Mathf.Lerp(1f, 1.05f, u);
            sy = Mathf.Lerp(1f, 0.92f, u);
        }
        else if (t < 0.32f)
        {
            float u = (t - 0.16f) / 0.16f;
            sx = Mathf.Lerp(1.05f, 0.96f, u);
            sy = Mathf.Lerp(0.92f, 1.04f, u);
        }
        else if (t < 0.48f)
        {
            float u = (t - 0.32f) / 0.16f;
            sx = Mathf.Lerp(0.96f, 0.93f, u);
            sy = Mathf.Lerp(1.04f, 1.08f, u);
        }
        else if (t < 0.66f)
        {
            float u = (t - 0.48f) / 0.18f;
            sx = Mathf.Lerp(0.93f, 0.98f, u);
            sy = Mathf.Lerp(1.08f, 1.02f, u);
        }
        else if (t < 0.82f)
        {
            float u = (t - 0.66f) / 0.16f;
            sx = Mathf.Lerp(0.98f, 1.06f, u);
            sy = Mathf.Lerp(1.02f, 0.90f, u);
        }
        else
        {
            float u = (t - 0.82f) / 0.18f;
            sx = Mathf.Lerp(1.06f, 1f, u);
            sy = Mathf.Lerp(0.90f, 1f, u);
        }

        transform.localScale = new Vector3(baseScale.x * sx, baseScale.y * sy, baseScale.z);

        if (spriteLocalHeight > 0.0001f)
        {
            float baseHeight = spriteLocalHeight * Mathf.Abs(baseScale.y);
            float scaledHeight = spriteLocalHeight * Mathf.Abs(baseScale.y * sy);
            float groundedOffset = (scaledHeight - baseHeight) * 0.5f;
            Vector3 p = transform.localPosition;
            p.y += groundedOffset;
            transform.localPosition = p;
            lastGroundOffsetY = groundedOffset;
        }
    }

    void RemoveGroundOffset()
    {
        if (Mathf.Abs(lastGroundOffsetY) <= 0.00001f) return;
        Vector3 p = transform.localPosition;
        p.y -= lastGroundOffsetY;
        transform.localPosition = p;
        lastGroundOffsetY = 0f;
    }
}
