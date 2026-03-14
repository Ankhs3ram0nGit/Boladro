using UnityEngine;

[DisallowMultipleComponent]
public class PlayerGroundShadow : MonoBehaviour
{
    [Range(0f, 1f)] public float opacity = 0.4f;
    public float widthRatio = 0.42f;
    public float heightRatio = 0.14f;
    public float verticalOffset = 0.5f;
    public int sortingOrderOffset = -1;

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer shadowRenderer;
    private Transform shadowTransform;
    private static Sprite sharedShadowSprite;

    void Awake()
    {
        sourceRenderer = GetComponent<SpriteRenderer>();
        EnsureShadowObject();
    }

    void LateUpdate()
    {
        if (sourceRenderer == null) sourceRenderer = GetComponent<SpriteRenderer>();
        if (sourceRenderer == null || !gameObject.activeInHierarchy)
        {
            SetVisible(false);
            return;
        }

        EnsureShadowObject();
        if (shadowRenderer == null || shadowTransform == null) return;

        Bounds body = sourceRenderer.bounds;
        float w = Mathf.Max(0.05f, body.size.x * Mathf.Max(0.05f, widthRatio));
        float h = Mathf.Max(0.02f, body.size.y * Mathf.Max(0.05f, heightRatio));

        shadowTransform.position = new Vector3(
            body.center.x,
            body.min.y + verticalOffset,
            transform.position.z
        );

        // Shared oval sprite is 2x1 world units at scale 1.
        shadowTransform.localScale = new Vector3(w * 0.5f, h, 1f);

        shadowRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        shadowRenderer.sortingOrder = sourceRenderer.sortingOrder + sortingOrderOffset;
        shadowRenderer.color = new Color(0f, 0f, 0f, Mathf.Clamp01(opacity));
        shadowRenderer.enabled = true;
    }

    void OnDisable()
    {
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (shadowTransform == null) return;
        if (Application.isPlaying) Destroy(shadowTransform.gameObject);
        else DestroyImmediate(shadowTransform.gameObject);
    }

    void SetVisible(bool visible)
    {
        if (shadowRenderer != null) shadowRenderer.enabled = visible;
    }

    void EnsureShadowObject()
    {
        if (shadowTransform != null && shadowRenderer != null) return;

        GameObject go = new GameObject("__PlayerGroundShadow");
        go.hideFlags = HideFlags.DontSave;
        shadowTransform = go.transform;
        shadowRenderer = go.AddComponent<SpriteRenderer>();
        shadowRenderer.sprite = GetOrCreateShadowSprite();
        shadowRenderer.color = new Color(0f, 0f, 0f, Mathf.Clamp01(opacity));
        shadowRenderer.drawMode = SpriteDrawMode.Simple;
        go.AddComponent<IgnoreOcclusionFade>();
    }

    static Sprite GetOrCreateShadowSprite()
    {
        if (sharedShadowSprite != null) return sharedShadowSprite;

        const int width = 64;
        const int height = 32;
        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
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
        sharedShadowSprite.name = "PlayerShadow_Oval_Sprite";
        return sharedShadowSprite;
    }
}
