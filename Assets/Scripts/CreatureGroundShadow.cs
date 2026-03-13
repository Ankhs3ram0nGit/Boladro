using UnityEngine;

[DisallowMultipleComponent]
public class CreatureGroundShadow : MonoBehaviour
{
    public float opacity = 0.6f;
    public float widthRatio = 0.58f;
    public float heightRatio = 0.22f;
    public float minAirScale = 0.58f;
    public float verticalOffset = -0.38f;
    public int sortingOrderOffset = -1;

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer shadowRenderer;
    private Transform shadowTransform;
    private WildCreatureAI wildAI;
    private Follower followerAI;
    private float baseLocalScaleYAbs = 1f;

    private static Sprite sharedShadowSprite;

    void Awake()
    {
        sourceRenderer = GetComponent<SpriteRenderer>();
        wildAI = GetComponent<WildCreatureAI>();
        followerAI = GetComponent<Follower>();
        verticalOffset = Mathf.Clamp(verticalOffset, -0.40f, -0.28f);
        if (sourceRenderer != null)
        {
            baseLocalScaleYAbs = Mathf.Max(0.001f, Mathf.Abs(sourceRenderer.transform.localScale.y));
        }

        EnsureShadowObject();
    }

    void LateUpdate()
    {
        if (sourceRenderer == null)
        {
            sourceRenderer = GetComponent<SpriteRenderer>();
        }
        if (sourceRenderer == null || !gameObject.activeInHierarchy)
        {
            SetShadowVisible(false);
            return;
        }

        EnsureShadowObject();
        if (shadowRenderer == null || shadowTransform == null) return;

        float air = GetAirFactor();
        float shrink = Mathf.Lerp(1f, Mathf.Clamp01(minAirScale), air);
        float arcComp = GetArcCompensation() * air;

        Bounds bodyBounds = sourceRenderer.bounds;
        float targetWidth = Mathf.Max(0.18f, bodyBounds.size.x * Mathf.Max(0.2f, widthRatio)) * shrink;
        float targetHeight = Mathf.Max(0.06f, bodyBounds.size.y * Mathf.Max(0.1f, heightRatio)) * shrink;

        // Shared oval sprite has world size 2x1 at scale 1 (64x32 @ 32 PPU).
        shadowTransform.localScale = new Vector3(targetWidth * 0.5f, targetHeight, 1f);
        shadowTransform.position = transform.position + new Vector3(0f, verticalOffset - arcComp, 0f);

        shadowRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;

        Color c = shadowRenderer.color;
        c.r = 0f;
        c.g = 0f;
        c.b = 0f;
        c.a = Mathf.Clamp01(opacity);
        shadowRenderer.color = c;
        shadowRenderer.enabled = true;
    }

    void OnDisable()
    {
        SetShadowVisible(false);
    }

    void OnDestroy()
    {
        if (shadowTransform != null)
        {
            if (Application.isPlaying) Destroy(shadowTransform.gameObject);
            else DestroyImmediate(shadowTransform.gameObject);
        }
    }

    private void SetShadowVisible(bool visible)
    {
        if (shadowRenderer != null)
        {
            shadowRenderer.enabled = visible;
        }
    }

    private void EnsureShadowObject()
    {
        if (shadowTransform != null && shadowRenderer != null) return;

        GameObject shadowObj = new GameObject(name + "_GroundBlob");
        shadowObj.hideFlags = HideFlags.DontSave;
        shadowTransform = shadowObj.transform;
        shadowRenderer = shadowObj.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GetOrCreateShadowSprite();
        shadowRenderer.drawMode = SpriteDrawMode.Simple;
        shadowRenderer.color = new Color(0f, 0f, 0f, Mathf.Clamp01(opacity));
    }

    private float GetAirFactor()
    {
        if (sourceRenderer == null) return 0f;
        float currentYAbs = Mathf.Max(0.0001f, Mathf.Abs(sourceRenderer.transform.localScale.y));
        float scaleRise = (currentYAbs / Mathf.Max(0.0001f, baseLocalScaleYAbs)) - 1f;
        float scaleAir = Mathf.Clamp01(scaleRise / 0.10f); // hop stretch usually peaks near +10%

        bool hopping = false;
        if (wildAI != null) hopping = wildAI.IsHopping;
        else if (followerAI != null) hopping = followerAI.IsHopping;

        if (!hopping) return 0f;
        return Mathf.Max(0.08f, scaleAir);
    }

    private float GetArcCompensation()
    {
        if (wildAI != null) return Mathf.Max(0f, wildAI.hopArcHeight);
        if (followerAI != null) return Mathf.Max(0f, followerAI.hopArcHeight);
        return 0.18f;
    }

    private static Sprite GetOrCreateShadowSprite()
    {
        if (sharedShadowSprite != null) return sharedShadowSprite;

        const int width = 64;
        const int height = 32;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        tex.name = "CreatureShadow_Oval";
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        float cx = (width - 1) * 0.5f;
        float cy = (height - 1) * 0.5f;
        float rx = width * 0.5f - 1f;
        float ry = height * 0.5f - 1f;

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x - cx) / rx;
                float dy = (y - cy) / ry;
                float d = dx * dx + dy * dy;
                float a = d <= 1f ? Mathf.Clamp01(1f - d * 0.40f) : 0f;
                pixels[y * width + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);

        sharedShadowSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            32f
        );
        sharedShadowSprite.name = "CreatureShadow_Oval_Sprite";
        return sharedShadowSprite;
    }
}
