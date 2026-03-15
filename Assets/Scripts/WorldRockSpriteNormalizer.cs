using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class WorldRockSpriteNormalizer : MonoBehaviour
{
    public bool applyOnEnable = true;
    public bool periodicReapplyInPlayMode = false;
    [Min(0.2f)] public float reapplyIntervalSeconds = 2.5f;

    [Header("Rock Shadow")]
    public bool addSmallRockShadows = true;
    [Range(0f, 1f)] public float rockShadowOpacity = 0.32f;
    public float rockShadowWidthRatio = 0.36f;
    public float rockShadowHeightRatio = 0.12f;
    public float rockShadowVerticalOffset = -0.02f;
    public int rockShadowSortingOrderOffset = -1;

    private const string PropsTexturePath = "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Props.png";
    private const string RockShadowChildName = "__RockGroundShadow";
    private static readonly string[] AllowedStoneSpriteNames =
    {
        "TX Props - Stone 02",
        "TX Props - Stone 03",
        "TX Props - Stone 04",
        "TX Props - Stone 05",
        "TX Props - Stone 06"
    };

    private readonly List<Sprite> allowedSprites = new List<Sprite>();
    private float nextApplyAt;
    private int rollingIndex;
    private static Sprite sharedShadowSprite;

    void OnEnable()
    {
        if (applyOnEnable)
        {
            ApplyNow();
        }
        nextApplyAt = Time.time + Mathf.Max(0.2f, reapplyIntervalSeconds);
    }

    void Update()
    {
        if (!periodicReapplyInPlayMode || !Application.isPlaying) return;
        if (Time.time < nextApplyAt) return;
        nextApplyAt = Time.time + Mathf.Max(0.2f, reapplyIntervalSeconds);
        ApplyNow();
    }

    [ContextMenu("Apply Now")]
    public void ApplyNow()
    {
        LoadAllowedSprites();
        if (allowedSprites.Count == 0) return;

        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || !sr.gameObject.activeInHierarchy) continue;
            if (sr.GetComponentInParent<Canvas>() != null) continue;
            if (sr.GetComponent<IgnoreOcclusionFade>() != null) continue;
            if (sr.gameObject.name.ToLowerInvariant().Contains("shadow")) continue;
            if (!LooksLikeWorldStone(sr)) continue;

            SpriteFromAtlas atlas = sr.GetComponent<SpriteFromAtlas>();
            if (atlas != null) atlas.enabled = false;

            if (sr.sprite == null || !IsAllowedStoneSprite(sr.sprite))
            {
                sr.sprite = allowedSprites[rollingIndex % allowedSprites.Count];
                rollingIndex++;
            }

            if (addSmallRockShadows)
            {
                EnsureRockShadow(sr);
            }
        }
    }

    void LoadAllowedSprites()
    {
        if (allowedSprites.Count > 0) return;

#if UNITY_EDITOR
        Object[] all = AssetDatabase.LoadAllAssetsAtPath(PropsTexturePath);
        if (all == null) return;

        for (int i = 0; i < AllowedStoneSpriteNames.Length; i++)
        {
            string wanted = AllowedStoneSpriteNames[i];
            for (int j = 0; j < all.Length; j++)
            {
                Sprite s = all[j] as Sprite;
                if (s == null) continue;
                if (!string.Equals(s.name, wanted, System.StringComparison.OrdinalIgnoreCase)) continue;
                allowedSprites.Add(s);
                break;
            }
        }
#endif
    }

    bool IsAllowedStoneSprite(Sprite s)
    {
        if (s == null) return false;
        for (int i = 0; i < AllowedStoneSpriteNames.Length; i++)
        {
            if (string.Equals(s.name, AllowedStoneSpriteNames[i], System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    bool LooksLikeWorldStone(SpriteRenderer sr)
    {
        string objectName = sr.gameObject.name.ToLowerInvariant();
        string rootName = sr.transform.root != null ? sr.transform.root.name.ToLowerInvariant() : string.Empty;
        string spriteName = sr.sprite != null ? sr.sprite.name.ToLowerInvariant() : string.Empty;

        if (objectName.Contains("shadow") || spriteName.Contains("shadow"))
        {
            return false;
        }

        bool isStoneLike =
            objectName.Contains("stone") || objectName.Contains("rock") ||
            rootName.Contains("stone") || rootName.Contains("rock") ||
            spriteName.Contains("stone") || spriteName.Contains("rock");

        return isStoneLike;
    }

    void EnsureRockShadow(SpriteRenderer rockRenderer)
    {
        if (rockRenderer == null || rockRenderer.sprite == null) return;

        Transform existing = rockRenderer.transform.Find(RockShadowChildName);
        SpriteRenderer shadow = null;
        if (existing != null)
        {
            shadow = existing.GetComponent<SpriteRenderer>();
        }

        if (shadow == null)
        {
            GameObject go = new GameObject(RockShadowChildName);
            go.transform.SetParent(rockRenderer.transform, false);
            shadow = go.AddComponent<SpriteRenderer>();
            go.AddComponent<IgnoreOcclusionFade>();
        }

        shadow.sprite = GetOrCreateShadowSprite();
        shadow.drawMode = SpriteDrawMode.Simple;
        shadow.color = new Color(0f, 0f, 0f, Mathf.Clamp01(rockShadowOpacity));
        shadow.sortingLayerID = rockRenderer.sortingLayerID;
        shadow.sortingOrder = rockRenderer.sortingOrder + rockShadowSortingOrderOffset;

        Bounds b = rockRenderer.sprite.bounds;
        float width = Mathf.Max(0.02f, b.size.x * Mathf.Max(0.05f, rockShadowWidthRatio));
        float height = Mathf.Max(0.01f, b.size.y * Mathf.Max(0.05f, rockShadowHeightRatio));
        shadow.transform.localPosition = new Vector3(
            b.center.x,
            b.min.y + rockShadowVerticalOffset,
            0f
        );
        // Shared oval sprite has world size 2x1 at scale 1.
        shadow.transform.localScale = new Vector3(width * 0.5f, height, 1f);
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
        sharedShadowSprite.name = "RockShadow_Oval_Sprite";
        return sharedShadowSprite;
    }
}
