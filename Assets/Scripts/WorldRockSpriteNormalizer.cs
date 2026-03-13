using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class WorldRockSpriteNormalizer : MonoBehaviour
{
    public bool applyOnEnable = true;
    public bool periodicReapplyInPlayMode = true;
    [Min(0.2f)] public float reapplyIntervalSeconds = 2.5f;

    private const string PropsTexturePath = "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Props.png";
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
            if (!LooksLikeWorldStone(sr)) continue;

            SpriteFromAtlas atlas = sr.GetComponent<SpriteFromAtlas>();
            if (atlas != null) atlas.enabled = false;

            if (sr.sprite == null || !IsAllowedStoneSprite(sr.sprite))
            {
                sr.sprite = allowedSprites[rollingIndex % allowedSprites.Count];
                rollingIndex++;
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

        bool isStoneLike =
            objectName.Contains("stone") || objectName.Contains("rock") ||
            rootName.Contains("stone") || rootName.Contains("rock") ||
            spriteName.Contains("stone") || spriteName.Contains("rock");

        return isStoneLike;
    }
}
