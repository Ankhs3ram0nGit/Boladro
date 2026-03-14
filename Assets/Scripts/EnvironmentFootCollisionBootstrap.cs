using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine.Rendering;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class FootColliderMarker : MonoBehaviour
{
    public string obstacleKind;
}

[DefaultExecutionOrder(-1000)]
[ExecuteAlways]
public class EnvironmentFootCollisionBootstrap : MonoBehaviour
{
    private static EnvironmentFootCollisionBootstrap instance;
    private float nextScanAt;
    public bool periodicRefreshInPlayMode = false;
    public bool periodicRefreshInEditMode = true;
    [Min(0.1f)] public float refreshIntervalSeconds = 1.0f;
    public float tileWorldSize = 1f;
    public float treeColliderTileOffset = 3f;
    public bool useTreeTemplateCollider = true;
    public string treeTemplateName = "Tree01_A";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
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
        instance = FindAnyObjectByType<EnvironmentFootCollisionBootstrap>();
        if (instance != null) return;
        GameObject go = new GameObject("__EnvironmentFootCollisionBootstrap");
        go.hideFlags = HideFlags.HideAndDontSave;
        instance = go.AddComponent<EnvironmentFootCollisionBootstrap>();
    }

    private void OnEnable()
    {
        nextScanAt = 0f;
        ApplyFootColliders();
    }

    private void Update()
    {
        bool shouldRefresh = Application.isPlaying ? periodicRefreshInPlayMode : periodicRefreshInEditMode;
        if (!shouldRefresh) return;
        if (Time.realtimeSinceStartup < nextScanAt) return;
        nextScanAt = Time.realtimeSinceStartup + Mathf.Max(0.1f, refreshIntervalSeconds);
        ApplyFootColliders();
    }

    private static void ApplyFootColliders()
    {
        bool hasTemplate = TryGetTreeTemplateCollider(out Vector2 treeTemplateSize, out Vector2 treeTemplateOffset);
        HashSet<int> processedHouseRoots = new HashSet<int>();

        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || sr.sprite == null) continue;
            bool isObstacle = TryClassifyObstacle(sr, out string obstacleKind, out float widthRatio, out float heightRatio);

            if (isObstacle && obstacleKind == "House")
            {
                ConfigureHouseObstacle(sr, processedHouseRoots);
                continue;
            }

            BoxCollider2D box = sr.GetComponent<BoxCollider2D>();
            FootColliderMarker marker = sr.GetComponent<FootColliderMarker>();
            Collider2D[] allCols = sr.GetComponents<Collider2D>();

            if (ShouldSkip(sr) || !isObstacle)
            {
                // Remove auto-generated collider if object no longer qualifies (e.g. bushes).
                if (marker != null || obstacleKind == "Bush")
                {
                    for (int c = 0; c < allCols.Length; c++)
                    {
                        if (allCols[c] == null) continue;
                        if (Application.isPlaying) Destroy(allCols[c]);
                        else DestroyImmediate(allCols[c]);
                    }
                    if (marker != null)
                    {
                        if (Application.isPlaying) Destroy(marker);
                        else DestroyImmediate(marker);
                    }
                }
                continue;
            }

            // Preserve manually edited rock colliders in scene/prefabs.
            // If a rock already has any collider, keep it as-is so editor tweaks stick.
            if (obstacleKind == "Rock" && allCols != null && allCols.Length > 0)
            {
                if (marker == null) marker = sr.gameObject.AddComponent<FootColliderMarker>();
                marker.obstacleKind = "Rock";

                for (int c = 0; c < allCols.Length; c++)
                {
                    if (allCols[c] == null) continue;
                    allCols[c].isTrigger = false;
                }
                continue;
            }

            // For tree/rock obstacles, standardize collider to a single foot BoxCollider2D.
            for (int c = 0; c < allCols.Length; c++)
            {
                Collider2D col = allCols[c];
                if (col == null) continue;
                if (box != null && col == box) continue;
                if (Application.isPlaying) Destroy(col);
                else DestroyImmediate(col);
            }

            if (box == null) box = sr.gameObject.AddComponent<BoxCollider2D>();
            if (marker == null) marker = sr.gameObject.AddComponent<FootColliderMarker>();

            marker.obstacleKind = obstacleKind;

            Bounds spriteBounds = sr.sprite.bounds;
            float footWidth = Mathf.Max(0.08f, spriteBounds.size.x * widthRatio);
            float footHeight = Mathf.Max(0.08f, spriteBounds.size.y * heightRatio);
            float footY = spriteBounds.min.y + (footHeight * 0.5f);
            if (obstacleKind == "Tree")
            {
                if (hasTemplate)
                {
                    // Match the manually tuned template tree collider exactly.
                    box.offset = treeTemplateOffset;
                    box.size = treeTemplateSize;
                    box.isTrigger = false;
                    continue;
                }
                else
                {
                    // Fallback: shift lower by configured tiles if template is unavailable.
                    float worldTile = ResolveWorldTileSize(sr);
                    float localScaleY = Mathf.Max(0.0001f, Mathf.Abs(sr.transform.lossyScale.y));
                    float localDown = (worldTile * Mathf.Max(0f, instance != null ? instance.treeColliderTileOffset : 1f)) / localScaleY;
                    footY -= localDown;
                }
            }

            box.offset = new Vector2(spriteBounds.center.x, footY);
            box.size = new Vector2(footWidth, footHeight);
            box.isTrigger = false;
        }
    }

    private static float ResolveWorldTileSize(SpriteRenderer sr)
    {
        float fallback = instance != null ? Mathf.Max(0.01f, instance.tileWorldSize) : 1f;

        Grid grid = null;
        if (sr != null)
        {
            grid = sr.GetComponentInParent<Grid>();
        }
        if (grid == null)
        {
            grid = FindAnyObjectByType<Grid>();
        }
        if (grid == null) return fallback;

        float world = Mathf.Abs(grid.cellSize.y * grid.transform.lossyScale.y);
        return Mathf.Max(0.01f, world);
    }

    private static bool TryGetTreeTemplateCollider(out Vector2 size, out Vector2 offset)
    {
        size = Vector2.zero;
        offset = Vector2.zero;

        if (instance == null || !instance.useTreeTemplateCollider || string.IsNullOrWhiteSpace(instance.treeTemplateName))
        {
            return false;
        }

        BoxCollider2D[] boxes = FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None);
        for (int i = 0; i < boxes.Length; i++)
        {
            BoxCollider2D b = boxes[i];
            if (b == null) continue;
            if (!string.Equals(b.gameObject.name, instance.treeTemplateName, System.StringComparison.OrdinalIgnoreCase)) continue;

            size = b.size;
            offset = b.offset;
            return true;
        }
        return false;
    }

    private static bool TryClassifyObstacle(SpriteRenderer sr, out string obstacleKind, out float widthRatio, out float heightRatio)
    {
        obstacleKind = "";
        widthRatio = 0.35f;
        heightRatio = 0.20f;

        string n = (sr.gameObject.name + " " + sr.transform.root.name + " " + sr.sprite.name).ToLowerInvariant();

        if (n.Contains("tree"))
        {
            obstacleKind = "Tree";
            widthRatio = 0.22f;
            heightRatio = 0.11f;
            return true;
        }

        if (n.Contains("bush"))
        {
            // Requested: bushes should have no collision.
            obstacleKind = "Bush";
            return false;
        }

        if (n.Contains("rock") || n.Contains("stone"))
        {
            obstacleKind = "Rock";
            widthRatio = 0.55f;
            heightRatio = 0.30f;
            return true;
        }

        if (n.Contains("house") || n.Contains("hut") || n.Contains("building"))
        {
            obstacleKind = "House";
            widthRatio = 0.62f;
            heightRatio = 0.14f;
            return true;
        }

        return false;
    }

    private static bool ShouldSkip(SpriteRenderer sr)
    {
        if (sr.GetComponentInParent<Canvas>() != null) return true;
        if (sr.GetComponentInParent<PlayerMover>() != null) return true;
        if (sr.GetComponentInParent<WildCreatureAI>() != null) return true;
        if (sr.GetComponentInParent<Follower>() != null) return true;
        if (sr.GetComponentInParent<BattleSystem>() != null) return true;
        if (IsChildOfTreeRootWithCollider(sr)) return true;
        if (IsChildOfHouseRootWithCollider(sr)) return true;
        return false;
    }

    private static bool IsChildOfTreeRootWithCollider(SpriteRenderer sr)
    {
        if (sr == null) return false;
        Transform t = sr.transform;
        if (t.parent == null) return false;

        Transform root = t.root;
        if (root == null || root == t) return false;
        if (!root.name.ToLowerInvariant().Contains("tree")) return false;

        // Tree prefabs use a root-level collider. Do not add duplicate colliders
        // to layered child sprite parts like Upper/Lower/Shadow.
        return root.GetComponent<BoxCollider2D>() != null;
    }

    private static bool IsChildOfHouseRootWithCollider(SpriteRenderer sr)
    {
        if (sr == null) return false;
        Transform t = sr.transform;
        if (t.parent == null) return false;

        Transform root = t.root;
        if (root == null || root == t) return false;
        bool isHouseLike = IsHouseLikeName(root.name);
        if (!isHouseLike)
        {
            SpriteRenderer[] rootSprites = root.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < rootSprites.Length; i++)
            {
                SpriteRenderer rs = rootSprites[i];
                if (rs == null || rs.sprite == null) continue;
                if (IsHouseLikeName(rs.sprite.name))
                {
                    isHouseLike = true;
                    break;
                }
            }
        }
        if (!isHouseLike) return false;

        return root.GetComponent<BoxCollider2D>() != null;
    }

    private static void ConfigureHouseObstacle(SpriteRenderer sr, HashSet<int> processedRoots)
    {
        if (sr == null) return;

        GameObject target = ResolveHouseRootObject(sr);
        if (target == null) return;

        int key = target.GetInstanceID();
        if (processedRoots.Contains(key)) return;
        processedRoots.Add(key);

        if (target.GetComponent<FadeableSprite>() == null)
        {
            target.AddComponent<FadeableSprite>();
        }
        if (target.GetComponent<UseColliderOcclusionBounds>() == null)
        {
            target.AddComponent<UseColliderOcclusionBounds>();
        }
        if (target.GetComponent<TopDownSorter>() == null)
        {
            TopDownSorter sorter = target.AddComponent<TopDownSorter>();
            sorter.sortMode = TopDownSorter.SortMode.RendererBottomY;
        }
        TopDownSorter topDown = target.GetComponent<TopDownSorter>();
        if (topDown != null)
        {
            topDown.sortMode = TopDownSorter.SortMode.FeetTransformY;
            topDown.useSortingGroupIfPresent = true;
            topDown.setSpriteSortPointToPivot = true;
            topDown.orderOffset = 0;
            topDown.orderMultiplier = 100;
        }

        TopDownSorter[] childSorters = target.GetComponentsInChildren<TopDownSorter>(true);
        for (int i = 0; i < childSorters.Length; i++)
        {
            TopDownSorter s = childSorters[i];
            if (s == null || s.gameObject == target) continue;
            if (Application.isPlaying) Destroy(s);
            else DestroyImmediate(s);
        }

        BoxCollider2D box = target.GetComponent<BoxCollider2D>();
        if (box == null)
        {
            box = target.AddComponent<BoxCollider2D>();
            InitializeHouseColliderFromSprites(target, box);
        }

        if (TryResolveHouseVariantIndex(target, out int variant) &&
            TryGetGroupedHouseColliderTemplate(variant, target, out Vector2 groupedSize, out Vector2 groupedOffset))
        {
            box.size = groupedSize;
            box.offset = groupedOffset;
        }

        box.isTrigger = false;
        EnsureHouseSortAnchor(target, box, topDown);

        FootColliderMarker marker = target.GetComponent<FootColliderMarker>();
        if (marker == null) marker = target.AddComponent<FootColliderMarker>();
        marker.obstacleKind = "House";

        AlignHouseSortingWithPlayer(target);
    }

    private static void EnsureHouseSortAnchor(GameObject houseRoot, BoxCollider2D box, TopDownSorter topDown)
    {
        if (houseRoot == null || box == null || topDown == null) return;

        const string anchorName = "__HouseSortAnchor";
        Transform anchor = houseRoot.transform.Find(anchorName);
        if (anchor == null)
        {
            GameObject go = new GameObject(anchorName);
            go.transform.SetParent(houseRoot.transform, false);
            anchor = go.transform;
        }

        // Match the same Y reference used by occlusion fade (collider top).
        anchor.localPosition = new Vector3(
            box.offset.x,
            box.offset.y + (box.size.y * 0.5f),
            0f
        );
        topDown.feetTransform = anchor;
        topDown.sortMode = TopDownSorter.SortMode.FeetTransformY;
    }

    private static GameObject ResolveHouseRootObject(SpriteRenderer sr)
    {
        if (sr == null) return null;
        Transform root = sr.transform.root;
        if (root == null) return sr.gameObject;

        if (IsHouseLikeName(root.name))
        {
            return root.gameObject;
        }
        SpriteRenderer[] rootSprites = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < rootSprites.Length; i++)
        {
            SpriteRenderer rs = rootSprites[i];
            if (rs == null || rs.sprite == null) continue;
            if (IsHouseLikeName(rs.sprite.name))
            {
                return root.gameObject;
            }
        }
        return sr.gameObject;
    }

    private static void InitializeHouseColliderFromSprites(GameObject houseRoot, BoxCollider2D box)
    {
        if (houseRoot == null || box == null) return;

        SpriteRenderer[] sprites = houseRoot.GetComponentsInChildren<SpriteRenderer>(true);
        if (sprites == null || sprites.Length == 0) return;

        bool hasBounds = false;
        Bounds world = default;
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer r = sprites[i];
            if (r == null || r.sprite == null) continue;
            string n = r.name.ToLowerInvariant();
            if (n.Contains("shadow")) continue;
            if (r.GetComponent<IgnoreOcclusionFade>() != null) continue;

            if (!hasBounds)
            {
                world = r.bounds;
                hasBounds = true;
            }
            else
            {
                world.Encapsulate(r.bounds);
            }
        }
        if (!hasBounds) return;

        float footWidthWorld = Mathf.Max(0.08f, world.size.x * 0.62f);
        float footHeightWorld = Mathf.Max(0.08f, world.size.y * 0.14f);
        float footCenterYWorld = world.min.y + (footHeightWorld * 0.5f);

        Transform tr = houseRoot.transform;
        Vector3 minLocal = tr.InverseTransformPoint(new Vector3(world.min.x, footCenterYWorld - (footHeightWorld * 0.5f), tr.position.z));
        Vector3 maxLocal = tr.InverseTransformPoint(new Vector3(world.max.x, footCenterYWorld + (footHeightWorld * 0.5f), tr.position.z));
        Vector3 centerLocal = tr.InverseTransformPoint(new Vector3(world.center.x, footCenterYWorld, tr.position.z));

        box.size = new Vector2(Mathf.Abs(maxLocal.x - minLocal.x), Mathf.Abs(maxLocal.y - minLocal.y));
        box.offset = new Vector2(centerLocal.x, centerLocal.y);
    }

    private static bool TryGetGroupedHouseColliderTemplate(int variant, GameObject selfRoot, out Vector2 size, out Vector2 offset)
    {
        size = Vector2.zero;
        offset = Vector2.zero;

        int templateVariant = -1;
        if (variant >= 1 && variant <= 3) templateVariant = 0;
        else if (variant >= 5 && variant <= 7) templateVariant = 4;
        else return false; // Keep 0,4 (and others) as manually tuned/independent.

        return TryFindHouseColliderTemplate(templateVariant, selfRoot, out size, out offset);
    }

    private static bool TryFindHouseColliderTemplate(int templateVariant, GameObject selfRoot, out Vector2 size, out Vector2 offset)
    {
        size = Vector2.zero;
        offset = Vector2.zero;

        SpriteRenderer[] all = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            SpriteRenderer sr = all[i];
            if (sr == null || sr.sprite == null) continue;
            if (!IsHouseLikeName(sr.sprite.name) && !IsHouseLikeName(sr.gameObject.name) && !IsHouseLikeName(sr.transform.root.name))
            {
                continue;
            }

            GameObject root = ResolveHouseRootObject(sr);
            if (root == null) continue;
            if (selfRoot != null && root == selfRoot) continue;
            if (!TryResolveHouseVariantIndex(root, out int idx)) continue;
            if (idx != templateVariant) continue;

            BoxCollider2D box = root.GetComponent<BoxCollider2D>();
            if (box == null) continue;

            size = box.size;
            offset = box.offset;
            return true;
        }

        return false;
    }

    private static bool TryResolveHouseVariantIndex(GameObject root, out int variant)
    {
        variant = -1;
        if (root == null) return false;

        SpriteRenderer[] srs = root.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (sr == null || sr.sprite == null) continue;
            if (TryParseHouseVariant(sr.sprite.name, out variant)) return true;
        }

        return TryParseHouseVariant(root.name, out variant);
    }

    private static bool TryParseHouseVariant(string name, out int variant)
    {
        variant = -1;
        if (string.IsNullOrWhiteSpace(name)) return false;

        string n = name.ToLowerInvariant().Trim();
        if (!IsHouseLikeName(n)) return false;

        MatchCollection matches = Regex.Matches(n, @"\d+");
        if (matches == null || matches.Count == 0) return false;

        string tailDigits = matches[matches.Count - 1].Value;
        return int.TryParse(tailDigits, out variant);
    }

    private static void AlignHouseSortingWithPlayer(GameObject houseRoot)
    {
        if (houseRoot == null) return;

        int targetSortingLayerId = 0;
        bool hasTargetLayer = false;

        PlayerMover player = FindAnyObjectByType<PlayerMover>();
        if (player != null)
        {
            SpriteRenderer playerSr = player.GetComponent<SpriteRenderer>();
            if (playerSr != null)
            {
                targetSortingLayerId = playerSr.sortingLayerID;
                hasTargetLayer = true;
            }
        }

        if (!hasTargetLayer) return;

        SortingGroup group = houseRoot.GetComponent<SortingGroup>();
        if (group != null)
        {
            group.sortingLayerID = targetSortingLayerId;
        }

        SpriteRenderer[] srs = houseRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < srs.Length; i++)
        {
            SpriteRenderer sr = srs[i];
            if (sr == null) continue;
            sr.sortingLayerID = targetSortingLayerId;
            sr.spriteSortPoint = SpriteSortPoint.Pivot;
        }
    }

    private static bool IsHouseLikeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        string n = value.ToLowerInvariant();
        return n.Contains("house") || n.Contains("hut") || n.Contains("building");
    }
}
