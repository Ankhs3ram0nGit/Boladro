using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(-10000)]
[ExecuteAlways]
public class PlantShadowStyleBootstrap : MonoBehaviour
{
    public string shadowSheetPath = "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Shadow Plant.png";
    public float refreshIntervalSeconds = 0.35f;
    public bool setShadowIgnoreFade = true;
    public int shadowSortingLayerId = 0;
    public int shadowSortingOrder = -999;
    public bool useGlobalShadowLayer = true;

    private static readonly Regex TreeVariantRegex = new Regex(@"tree\D*0*(\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex BushVariantRegex = new Regex(@"bush\D*0*(\d+)", RegexOptions.IgnoreCase);
    private static readonly Regex GenericTRegex = new Regex(@"T0*(\d+)", RegexOptions.IgnoreCase);

    private static PlantShadowStyleBootstrap instance;
    private float nextRefreshAt;
    private readonly Dictionary<int, Sprite> treeShadowSprites = new Dictionary<int, Sprite>();
    private readonly Dictionary<int, Sprite> bushShadowSprites = new Dictionary<int, Sprite>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RuntimeBoot()
    {
        EnsureInstance();
    }

#if UNITY_EDITOR
    [InitializeOnLoadMethod]
    private static void EditorBoot()
    {
        EditorApplication.delayCall += EnsureInstance;
    }
#endif

    private static void EnsureInstance()
    {
        if (instance != null) return;
        instance = FindAnyObjectByType<PlantShadowStyleBootstrap>();
        if (instance != null) return;

        GameObject go = new GameObject("__PlantShadowStyleBootstrap");
        go.hideFlags = HideFlags.HideAndDontSave;
        instance = go.AddComponent<PlantShadowStyleBootstrap>();
    }

    private void Awake()
    {
        ApplyNow();
    }

    private void OnEnable()
    {
        nextRefreshAt = 0f;
        ApplyNow();
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup < nextRefreshAt) return;
        nextRefreshAt = Time.realtimeSinceStartup + Mathf.Max(0.1f, refreshIntervalSeconds);
        ApplyNow();
    }

    private void ApplyNow()
    {
        LoadShadowSprites();

        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        if (renderers == null || renderers.Length == 0) return;

        HashSet<Transform> roots = new HashSet<Transform>();
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;
            if (!sr.gameObject.scene.IsValid()) continue;
            if (string.IsNullOrEmpty(sr.gameObject.scene.path)) continue;

            Transform root = FindTreeOrBushRoot(sr.transform);
            if (root != null) roots.Add(root);
        }

        foreach (Transform root in roots)
        {
            if (root == null) continue;

            bool isTree = root.name.ToLowerInvariant().Contains("tree");
            bool isBush = !isTree && root.name.ToLowerInvariant().Contains("bush");
            if (!isTree && !isBush) continue;

            SpriteRenderer body = isTree ? FindTreeBody(root) : FindBushBody(root);
            SpriteRenderer shadow = FindBestShadowRenderer(root);
            if (shadow == null)
            {
                shadow = CreateShadowChild(root, body);
            }

            if (shadow != null)
            {
                int variant = ResolveVariant(root, body, isTree);
                Sprite expectedShadow = GetShadowSprite(variant, isTree);
                if (expectedShadow != null)
                {
                    shadow.sprite = expectedShadow;
                }

                shadow.enabled = true;
                Color sc = shadow.color;
                sc.a = 0.6f;
                shadow.color = sc;
                if (useGlobalShadowLayer)
                {
                    shadow.sortingLayerID = shadowSortingLayerId;
                }
                shadow.sortingOrder = shadowSortingOrder;

                if (body != null)
                {
                    if (!useGlobalShadowLayer)
                    {
                        shadow.sortingLayerID = body.sortingLayerID;
                    }
                    shadow.sharedMaterial = body.sharedMaterial;
                    shadow.transform.localScale = body.transform.localScale;

                    if (isTree)
                    {
                        shadow.transform.localPosition = body.transform.localPosition;
                    }
                    else
                    {
                        AlignBushShadowToBody(shadow, body);
                    }
                }
            }

            ApplyFadeMarkers(root, shadow);
        }
    }

    private void ApplyFadeMarkers(Transform root, SpriteRenderer chosenShadow)
    {
        SpriteRenderer[] all = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null) continue;
            bool isShadow = (chosenShadow != null && sr == chosenShadow) || sr.name.ToLowerInvariant().Contains("shadow");
            IgnoreOcclusionFade marker = sr.GetComponent<IgnoreOcclusionFade>();

            if (setShadowIgnoreFade && isShadow)
            {
                if (marker == null) sr.gameObject.AddComponent<IgnoreOcclusionFade>();
            }
            else
            {
                if (marker == null) continue;
                if (Application.isPlaying) Destroy(marker);
                else DestroyImmediate(marker);
            }
        }
    }

    private static Transform FindTreeOrBushRoot(Transform t)
    {
        while (t != null)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("tree") || n.Contains("bush")) return t;
            t = t.parent;
        }
        return null;
    }

    private static SpriteRenderer FindTreeBody(Transform root)
    {
        SpriteRenderer lower = FindRendererByNameContains(root, "lower");
        if (lower != null) return lower;
        return FindFirstNonShadow(root);
    }

    private static SpriteRenderer FindBushBody(Transform root)
    {
        SpriteRenderer self = root.GetComponent<SpriteRenderer>();
        if (self != null && !self.name.ToLowerInvariant().Contains("shadow")) return self;
        return FindFirstNonShadow(root);
    }

    private static SpriteRenderer FindFirstNonShadow(Transform root)
    {
        if (root == null) return null;
        SpriteRenderer[] all = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null) continue;
            if (!sr.name.ToLowerInvariant().Contains("shadow")) return sr;
        }
        return null;
    }

    private static SpriteRenderer FindBestShadowRenderer(Transform root)
    {
        if (root == null) return null;
        SpriteRenderer[] all = root.GetComponentsInChildren<SpriteRenderer>(true);
        SpriteRenderer fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null) continue;
            if (!sr.name.ToLowerInvariant().Contains("shadow")) continue;
            if (sr.sprite != null) return sr;
            if (fallback == null) fallback = sr;
        }
        return fallback;
    }

    private static SpriteRenderer FindRendererByNameContains(Transform root, string contains)
    {
        if (root == null || string.IsNullOrEmpty(contains)) return null;
        string needle = contains.ToLowerInvariant();
        SpriteRenderer[] all = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null) continue;
            if (sr.name.ToLowerInvariant().Contains(needle)) return sr;
        }
        return null;
    }

    private static SpriteRenderer CreateShadowChild(Transform root, SpriteRenderer bodyRef)
    {
        if (root == null) return null;
        GameObject go = new GameObject("Shadow");
        go.transform.SetParent(root, false);
        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        if (bodyRef != null)
        {
            sr.sortingLayerID = bodyRef.sortingLayerID;
            sr.sortingOrder = bodyRef.sortingOrder - 1;
            sr.sharedMaterial = bodyRef.sharedMaterial;
        }
        return sr;
    }

    private static void AlignBushShadowToBody(SpriteRenderer shadow, SpriteRenderer body)
    {
        if (shadow == null || body == null) return;
        if (shadow.sprite == null || body.sprite == null)
        {
            shadow.transform.localPosition = body.transform.localPosition;
            return;
        }

        Bounds bodyBounds = body.bounds;
        Bounds shadowBounds = shadow.bounds;

        float deltaX = bodyBounds.center.x - shadowBounds.center.x;
        float deltaY = bodyBounds.min.y - shadowBounds.min.y;
        shadow.transform.position += new Vector3(deltaX, deltaY, 0f);
    }

    private int ResolveVariant(Transform root, SpriteRenderer body, bool isTree)
    {
        int variant = TryParseVariantFromName(root.name, isTree);
        if (variant > 0) return variant;

        if (body != null && body.sprite != null)
        {
            variant = TryParseGenericVariant(body.sprite.name);
            if (variant > 0) return variant;
        }

        return 1;
    }

    private static int TryParseVariantFromName(string name, bool isTree)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        Match m = isTree ? TreeVariantRegex.Match(name) : BushVariantRegex.Match(name);
        if (!m.Success || m.Groups.Count < 2) return 0;
        if (!int.TryParse(m.Groups[1].Value, out int value)) return 0;
        return Mathf.Max(1, value);
    }

    private static int TryParseGenericVariant(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        Match m = GenericTRegex.Match(text);
        if (!m.Success || m.Groups.Count < 2) return 0;
        if (!int.TryParse(m.Groups[1].Value, out int value)) return 0;
        return Mathf.Max(1, value);
    }

    private Sprite GetShadowSprite(int variant, bool isTree)
    {
        if (isTree)
        {
            if (treeShadowSprites.TryGetValue(variant, out Sprite treeSprite) && treeSprite != null) return treeSprite;
            if (treeShadowSprites.TryGetValue(1, out Sprite fallbackTree) && fallbackTree != null) return fallbackTree;
            return null;
        }

        if (bushShadowSprites.TryGetValue(variant, out Sprite bushSprite) && bushSprite != null) return bushSprite;
        if (bushShadowSprites.TryGetValue(1, out Sprite fallbackBush) && fallbackBush != null) return fallbackBush;
        return null;
    }

    private void LoadShadowSprites()
    {
#if UNITY_EDITOR
        Object[] shadowAssets = AssetDatabase.LoadAllAssetsAtPath(shadowSheetPath);
        if (shadowAssets == null || shadowAssets.Length == 0) return;

        treeShadowSprites.Clear();
        bushShadowSprites.Clear();

        for (int i = 0; i < shadowAssets.Length; i++)
        {
            Sprite s = shadowAssets[i] as Sprite;
            if (s == null) continue;
            string name = s.name ?? string.Empty;

            Match treeMatch = Regex.Match(name, @"Shadow Tree T0*(\d+)", RegexOptions.IgnoreCase);
            if (treeMatch.Success && treeMatch.Groups.Count > 1 && int.TryParse(treeMatch.Groups[1].Value, out int treeVariant))
            {
                treeShadowSprites[Mathf.Max(1, treeVariant)] = s;
                continue;
            }

            Match bushMatch = Regex.Match(name, @"Shadow Bush T0*(\d+)", RegexOptions.IgnoreCase);
            if (bushMatch.Success && bushMatch.Groups.Count > 1 && int.TryParse(bushMatch.Groups[1].Value, out int bushVariant))
            {
                bushShadowSprites[Mathf.Max(1, bushVariant)] = s;
            }
        }
#endif
    }
}
