using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class TreeHoverSelector : MonoBehaviour
{
    private const string SelectorObjectName = "__HoverSelectorFX";
    private static readonly string[] SelectorFrameNames = { "Frame_0", "Frame_1", "Frame_2", "Frame_3" };
    private const string PickaxeHitSfxBasePath = "Assets/JDSherbert - Ultimate UI SFX Pack (FREE)/New Folder/DSGNMisc_HIT-Spell Hit_HY_PC-";
    private const string PickaxeHitSfxSuffix = ".wav";
    private const int PickaxeHitSfxStart = 1;
    private const int PickaxeHitSfxEnd = 6;

    private const string DefaultStoneSpriteAssetPath = "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Props.png";
    private const string DefaultStoneSpriteName = "TX Props - Stone 06";

    [Header("Selector Source")]
    public string selectorAsepritePath = "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Aseprite/UI_FlatAnimated.aseprite";
    public Sprite[] selectorFrames;
    public float animationFps = 12f;

    [Header("Selection Rules")]
    public float maxHoverTiles = 3f;
    public float fallbackTileWorldSize = 1f;
    public float selectorPadding = 1.08f;
    public bool includeInactiveTrees;

    [Header("Rendering")]
    public int sortingOrderBoost = 300;

    [Header("Manual Tuning")]
    public Vector2 selectorOffset = Vector2.zero;
    public Vector2 selectorScaleMultiplier = Vector2.one;
    public Vector2 treeSelectorOffset = Vector2.zero;
    public Vector2 stoneSelectorOffset = Vector2.zero;
    public Vector2 treeSelectorScaleMultiplier = Vector2.one;
    public Vector2 stoneSelectorScaleMultiplier = Vector2.one;

    [Header("Pickaxe Hit Squash")]
    public bool registerPickaxeHits = true;
    public bool includeShadowInSquash = false;
    [Range(0.4f, 1f)] public float hitSquashScaleY = 0.82f;
    [Range(1f, 1.6f)] public float hitStretchScaleX = 1.08f;
    [Min(0.01f)] public float hitCompressDuration = 0.06f;
    [Min(0.01f)] public float hitRecoverDuration = 0.12f;

    [Header("Tree Harvest")]
    public int treeMaxHealth = 100;
    public int pickaxeDamageToTree = 10;
    public int woodDropAmountMin = 10;
    public int woodDropAmountMax = 20;
    public string woodSpriteAssetPath = "Assets/Resources/Wood.png";
    public float woodDropYOffset = 0.12f;

    [Header("Stone Harvest")]
    public int stoneMaxHealth = 100;
    public int pickaxeDamageToStone = 10;
    public int stoneDropAmountMin = 2;
    public int stoneDropAmountMax = 5;
    public string stoneSpriteAssetPath = DefaultStoneSpriteAssetPath;
    public string stoneSpriteName = DefaultStoneSpriteName;
    public float stoneDropYOffset = 0.08f;

    [Header("Harvest XP")]
    [Min(0)] public int treeBreakXp = 8;
    [Min(0)] public int stoneBreakXp = 5;
    [Min(0.1f)] public float levelUpFloatingTextDuration = 1.0f;

    [Header("Drop Behavior")]
    public float woodPickupDistance = 1f;
    [Range(0.01f, 1f)] public float woodDropScale = 0.1f;
    public float woodDropFallDistanceMin = 0.28f;
    public float woodDropFallDistanceMax = 0.68f;
    public float woodDropFallDurationMin = 0.14f;
    public float woodDropFallDurationMax = 0.26f;
    public float woodDropArcHeight = 0.16f;

    [Header("Drop Collect Animation")]
    public float woodCollectMoveDuration = 0.18f;
    public float woodCollectExpandDuration = 0.08f;
    public float woodCollectShrinkDuration = 0.10f;
    public float woodCollectExpandScale = 1.35f;
    public float woodCollectTargetYOffset = 0.45f;
    public int woodCollectSortingBoost = 60;

    [Header("Audio")]
    public AudioClip[] pickaxeHitSfx;
    [Range(0f, 1f)] public float pickaxeHitSfxVolume = 0.78f;

    enum HarvestKind
    {
        Tree,
        Stone
    }

    class HarvestEntry
    {
        public Transform root;
        public SpriteRenderer[] renderers;
        public HarvestKind kind;
    }

    class ResourceDropEntry
    {
        public Transform root;
        public SpriteRenderer renderer;
        public string itemId;
        public string displayName;
        public int amount;
        public Vector3 baseScale;
        public int baseSortingLayerId;
        public int baseSortingOrder;
        public Vector3 fallStart;
        public Vector3 fallTarget;
        public float fallDuration;
        public float fallElapsed;
        public bool isFalling;
        public Vector3 collectStart;
        public Vector3 collectTarget;
        public float collectElapsed;
        public bool isCollecting;
    }

    struct HarvestSquashState
    {
        public Transform root;
        public SpriteRenderer[] renderers;
        public Vector3 baseLocalScale;
        public float baseBottomY;
        public bool includeShadow;
    }

    private readonly List<HarvestEntry> harvestEntries = new List<HarvestEntry>();
    private readonly Dictionary<int, int> harvestHealth = new Dictionary<int, int>();
    private readonly Dictionary<int, Coroutine> activeSquashRoutines = new Dictionary<int, Coroutine>();
    private readonly List<ResourceDropEntry> resourceDrops = new List<ResourceDropEntry>();

    private PlayerToolController toolController;
    private PlayerToolController boundToolController;
    private Camera mainCam;
    private Grid grid;
    private float animTime;
    private float nextRefreshTime;

    private SpriteRenderer selectorRenderer;
    private Transform selectorTransform;
    private HarvestEntry activeHoveredTarget;
    private readonly HashSet<int> nonReadableTextureIds = new HashSet<int>();
    private readonly List<Vector2> spritePhysicsPoints = new List<Vector2>(32);

    private Sprite cachedWoodSprite;
    private Sprite cachedStoneSprite;
    private AudioSource pickaxeHitSfxSource;
    private bool attemptedPickaxeHitSfxLoad;

    private InventoryModel inventory;
    private PlayerCreatureParty creatureParty;
    private InventoryItemData runtimeWoodItem;
    private InventoryItemData runtimeStoneItem;
    private SpriteRenderer playerSpriteRenderer;

    void Awake()
    {
        if (Mathf.Abs(pickaxeHitSfxVolume - 0.9f) <= 0.0001f)
        {
            pickaxeHitSfxVolume = 0.78f;
        }

        toolController = GetComponent<PlayerToolController>();
        inventory = GetComponent<InventoryModel>();
        BindToolController(toolController);
        mainCam = Camera.main;
        grid = FindFirstObjectByType<Grid>();

        EnsureFramesLoaded();
        EnsureSelectorObject();
        EnsurePickaxeHitAudio();
        RefreshHarvestEntries();
        SetSelectorVisible(false);
    }

    void OnDisable()
    {
        BindToolController(null);
        StopAllSquashRoutines();
        SetSelectorVisible(false);
        activeHoveredTarget = null;
    }

    void OnDestroy()
    {
        if (runtimeWoodItem != null)
        {
            if (Application.isPlaying) Destroy(runtimeWoodItem);
            else DestroyImmediate(runtimeWoodItem);
            runtimeWoodItem = null;
        }

        if (runtimeStoneItem != null)
        {
            if (Application.isPlaying) Destroy(runtimeStoneItem);
            else DestroyImmediate(runtimeStoneItem);
            runtimeStoneItem = null;
        }
    }

    void Update()
    {
        if (toolController == null) toolController = GetComponent<PlayerToolController>();
        if (inventory == null) inventory = GetComponent<InventoryModel>();
        if (creatureParty == null) creatureParty = GetComponent<PlayerCreatureParty>();
        if (playerSpriteRenderer == null) playerSpriteRenderer = GetComponent<SpriteRenderer>();
        if (toolController != boundToolController) BindToolController(toolController);
        if (mainCam == null) mainCam = Camera.main;
        if (grid == null) grid = FindFirstObjectByType<Grid>();

        if (Time.time >= nextRefreshTime)
        {
            nextRefreshTime = Time.time + 1.5f;
            RefreshHarvestEntries();
            EnsureFramesLoaded();
        }

        bool hasMouseWorld = TryGetMouseWorldPoint(out Vector2 mouseWorld);
        ProcessResourceDrops(hasMouseWorld, mouseWorld);

        if (toolController == null || !toolController.IsPickaxeEquipped())
        {
            SetSelectorVisible(false);
            activeHoveredTarget = null;
            return;
        }

        if (mainCam == null || selectorFrames == null || selectorFrames.Length == 0)
        {
            SetSelectorVisible(false);
            activeHoveredTarget = null;
            return;
        }

        if (!hasMouseWorld)
        {
            SetSelectorVisible(false);
            activeHoveredTarget = null;
            return;
        }

        if (!TryGetHoveredTarget(mouseWorld, out HarvestEntry hovered, out Bounds hoveredBounds))
        {
            SetSelectorVisible(false);
            activeHoveredTarget = null;
            return;
        }

        if (!IsWithinTileDistance(hoveredBounds))
        {
            SetSelectorVisible(false);
            activeHoveredTarget = null;
            return;
        }

        RenderSelector(hovered, hoveredBounds);
        activeHoveredTarget = hovered;
    }

    void EnsureSelectorObject()
    {
        if (selectorRenderer != null && selectorTransform != null) return;

        Transform existing = transform.Find(SelectorObjectName);
        if (existing == null)
        {
            Transform legacy = transform.Find("__TreeHoverSelectorFX");
            if (legacy != null)
            {
                existing = legacy;
            }
        }
        if (existing == null)
        {
            GameObject go = new GameObject(SelectorObjectName);
            existing = go.transform;
            existing.SetParent(null, true);
        }
        else
        {
            existing.name = SelectorObjectName;
        }

        selectorTransform = existing;
        selectorRenderer = existing.GetComponent<SpriteRenderer>();
        if (selectorRenderer == null)
        {
            selectorRenderer = existing.gameObject.AddComponent<SpriteRenderer>();
        }
        selectorTransform.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        selectorRenderer.color = Color.white;
        selectorRenderer.flipX = false;
        selectorRenderer.flipY = false;
        EnsureSelectorHasNoCollision();
    }

    void SetSelectorVisible(bool visible)
    {
        if (selectorRenderer == null) return;
        selectorRenderer.enabled = visible;
    }

    void BindToolController(PlayerToolController controller)
    {
        if (boundToolController != null)
        {
            boundToolController.OnPickaxeSwing -= HandlePickaxeSwing;
        }

        boundToolController = controller;
        if (boundToolController != null)
        {
            boundToolController.OnPickaxeSwing += HandlePickaxeSwing;
        }
    }

    void HandlePickaxeSwing()
    {
        if (!registerPickaxeHits) return;
        if (selectorRenderer == null || !selectorRenderer.enabled) return;
        if (activeHoveredTarget == null || activeHoveredTarget.root == null) return;
        if (!TryGetMouseWorldPoint(out Vector2 mouseWorld)) return;
        if (!IsPointInsideEntryInteraction(activeHoveredTarget, mouseWorld, out _)) return;

        PlayRandomPickaxeHitSfx();
        if (!ApplyPickaxeDamageToTarget(activeHoveredTarget))
        {
            return;
        }

        StartTargetSquash(activeHoveredTarget);
    }

    void StartTargetSquash(HarvestEntry target)
    {
        if (target == null || target.root == null) return;
        int key = target.root.GetInstanceID();

        if (activeSquashRoutines.TryGetValue(key, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
        }
        activeSquashRoutines[key] = StartCoroutine(PlayTargetSquashRoutine(key, target));
    }

    IEnumerator PlayTargetSquashRoutine(int key, HarvestEntry target)
    {
        if (!TryCaptureSquashState(target, out HarvestSquashState state))
        {
            activeSquashRoutines.Remove(key);
            yield break;
        }

        float down = Mathf.Max(0.01f, hitCompressDuration);
        float up = Mathf.Max(0.01f, hitRecoverDuration);

        float t = 0f;
        while (t < down)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / down);
            ApplySquash(state, u);
            yield return null;
        }

        t = 0f;
        while (t < up)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / up);
            ApplySquash(state, 1f - u);
            yield return null;
        }

        RestoreSquash(state);
        activeSquashRoutines.Remove(key);
    }

    bool TryCaptureSquashState(HarvestEntry target, out HarvestSquashState state)
    {
        state = default;
        if (target == null || target.root == null) return false;

        float bottomY = target.root.position.y;
        if (TryGetCombinedBounds(target.renderers, includeShadowInSquash, out Bounds bounds))
        {
            bottomY = bounds.min.y;
        }

        state = new HarvestSquashState
        {
            root = target.root,
            renderers = target.renderers,
            baseLocalScale = target.root.localScale,
            baseBottomY = bottomY,
            includeShadow = includeShadowInSquash
        };
        return true;
    }

    void ApplySquash(HarvestSquashState state, float squashAmount)
    {
        if (state.root == null) return;
        float sy = Mathf.Lerp(1f, Mathf.Clamp(hitSquashScaleY, 0.4f, 1f), squashAmount);

        Vector3 local = state.baseLocalScale;
        state.root.localScale = new Vector3(local.x, local.y * sy, local.z);

        if (TryGetCombinedBounds(state.renderers, state.includeShadow, out Bounds scaledBounds))
        {
            float deltaY = state.baseBottomY - scaledBounds.min.y;
            if (Mathf.Abs(deltaY) > 0.0001f)
            {
                Vector3 p = state.root.position;
                p.y += deltaY;
                state.root.position = p;
            }
        }
    }

    void RestoreSquash(HarvestSquashState state)
    {
        if (state.root == null) return;
        state.root.localScale = state.baseLocalScale;
        if (TryGetCombinedBounds(state.renderers, state.includeShadow, out Bounds restored))
        {
            float deltaY = state.baseBottomY - restored.min.y;
            if (Mathf.Abs(deltaY) > 0.0001f)
            {
                Vector3 p = state.root.position;
                p.y += deltaY;
                state.root.position = p;
            }
        }
    }

    void StopAllSquashRoutines()
    {
        foreach (KeyValuePair<int, Coroutine> pair in activeSquashRoutines)
        {
            if (pair.Value != null) StopCoroutine(pair.Value);
        }
        activeSquashRoutines.Clear();
    }

    void EnsureSelectorHasNoCollision()
    {
        if (selectorTransform == null) return;

        Collider2D[] cols = selectorTransform.GetComponents<Collider2D>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            Destroy(cols[i]);
        }

        Rigidbody2D rb = selectorTransform.GetComponent<Rigidbody2D>();
        if (rb != null) Destroy(rb);
    }

    bool TryGetMouseWorldPoint(out Vector2 world)
    {
        world = Vector2.zero;
        Mouse mouse = Mouse.current;
        if (mouse == null || mainCam == null) return false;

        Vector2 screen = mouse.position.ReadValue();
        float z = Mathf.Abs(mainCam.transform.position.z);
        Vector3 wp = mainCam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, z));
        world = new Vector2(wp.x, wp.y);
        return true;
    }

    void RefreshHarvestEntries()
    {
        harvestEntries.Clear();

        HashSet<int> presentIds = new HashSet<int>();
        HashSet<int> seenRootIds = new HashSet<int>();

        FindObjectsInactive inactiveMode = includeInactiveTrees ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;

        FadeableSprite[] fades = FindObjectsByType<FadeableSprite>(inactiveMode, FindObjectsSortMode.None);
        for (int i = 0; i < fades.Length; i++)
        {
            FadeableSprite fade = fades[i];
            if (fade == null || fade.transform == null) continue;
            TryAddHarvestEntry(fade.transform, seenRootIds, presentIds);
        }

        FootColliderMarker[] markers = FindObjectsByType<FootColliderMarker>(inactiveMode, FindObjectsSortMode.None);
        for (int i = 0; i < markers.Length; i++)
        {
            FootColliderMarker marker = markers[i];
            if (marker == null || marker.transform == null) continue;
            TryAddHarvestEntry(marker.transform, seenRootIds, presentIds);
        }

        List<int> toRemove = new List<int>();
        foreach (KeyValuePair<int, int> kv in harvestHealth)
        {
            if (!presentIds.Contains(kv.Key)) toRemove.Add(kv.Key);
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            harvestHealth.Remove(toRemove[i]);
        }
    }

    void TryAddHarvestEntry(Transform t, HashSet<int> seenRootIds, HashSet<int> presentIds)
    {
        if (t == null) return;

        Transform root = ResolveHarvestRoot(t);
        if (root == null) return;
        int id = root.GetInstanceID();
        if (seenRootIds.Contains(id)) return;

        if (!TryResolveHarvestKind(root, out HarvestKind kind)) return;

        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        if (!root.gameObject.activeInHierarchy && !includeInactiveTrees) return;

        harvestEntries.Add(new HarvestEntry
        {
            root = root,
            renderers = renderers,
            kind = kind
        });

        seenRootIds.Add(id);
        presentIds.Add(id);

        if (!harvestHealth.ContainsKey(id))
        {
            harvestHealth[id] = GetMaxHealthForKind(kind);
        }
    }

    Transform ResolveHarvestRoot(Transform candidate)
    {
        if (candidate == null) return null;

        Transform current = candidate;
        while (current != null)
        {
            if (LooksLikeDirectHarvestTarget(current))
            {
                return current;
            }
            current = current.parent;
        }

        return null;
    }

    bool LooksLikeDirectHarvestTarget(Transform t)
    {
        if (t == null) return false;

        FootColliderMarker marker = t.GetComponent<FootColliderMarker>();
        if (IsRockMarker(marker) || IsTreeMarker(marker))
        {
            return true;
        }

        Collider2D collider = t.GetComponent<Collider2D>();
        if (collider == null) return false;

        string n = t.name.ToLowerInvariant();
        if (n.Contains("rock") || n.Contains("stone") || n.Contains("tree"))
        {
            return true;
        }

        return false;
    }

    bool TryResolveHarvestKind(Transform root, out HarvestKind kind)
    {
        kind = HarvestKind.Tree;
        if (root == null) return false;

        FootColliderMarker directMarker = root.GetComponent<FootColliderMarker>();
        if (IsRockMarker(directMarker))
        {
            kind = HarvestKind.Stone;
            return true;
        }
        if (IsTreeMarker(directMarker))
        {
            kind = HarvestKind.Tree;
            return true;
        }

        if (root.GetComponent<Collider2D>() == null) return false;

        string n = root.name.ToLowerInvariant();
        if (n.Contains("rock") || n.Contains("stone"))
        {
            kind = HarvestKind.Stone;
            return true;
        }
        if (n.Contains("tree"))
        {
            kind = HarvestKind.Tree;
            return true;
        }

        return false;
    }

    static bool IsRockMarker(FootColliderMarker marker)
    {
        if (marker == null || string.IsNullOrWhiteSpace(marker.obstacleKind)) return false;
        string kind = marker.obstacleKind.Trim();
        return string.Equals(kind, "Rock", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(kind, "Stone", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsTreeMarker(FootColliderMarker marker)
    {
        if (marker == null || string.IsNullOrWhiteSpace(marker.obstacleKind)) return false;
        string kind = marker.obstacleKind.Trim();
        return string.Equals(kind, "Tree", StringComparison.OrdinalIgnoreCase);
    }

    bool TryGetHoveredTarget(Vector2 mouseWorld, out HarvestEntry hovered, out Bounds hoveredBounds)
    {
        hovered = null;
        hoveredBounds = default;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < harvestEntries.Count; i++)
        {
            HarvestEntry entry = harvestEntries[i];
            if (entry == null || entry.root == null) continue;
            if (!entry.root.gameObject.activeInHierarchy && !includeInactiveTrees) continue;

            if (!TryGetSelectorDisplayBounds(entry, out Bounds displayBounds)) continue;
            if (!IsPointInsideEntryInteraction(entry, mouseWorld, out float distance)) continue;

            if (distance < bestDistance)
            {
                bestDistance = distance;
                hovered = entry;
                hoveredBounds = displayBounds;
            }
        }

        return hovered != null;
    }

    bool IsPointInsideEntryInteraction(HarvestEntry entry, Vector2 worldPoint, out float distanceMetric)
    {
        distanceMetric = float.MaxValue;
        if (entry == null || entry.renderers == null) return false;

        bool hitAny = false;
        for (int i = 0; i < entry.renderers.Length; i++)
        {
            SpriteRenderer sr = entry.renderers[i];
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            if (IsShadowRenderer(sr)) continue;
            if (!IsPointInsideRenderer(sr, worldPoint)) continue;

            hitAny = true;
            Vector2 c = new Vector2(sr.bounds.center.x, sr.bounds.center.y);
            float d = Vector2.Distance(worldPoint, c);
            if (d < distanceMetric) distanceMetric = d;
        }

        return hitAny;
    }

    bool IsPointInsideRenderer(SpriteRenderer sr, Vector2 worldPoint)
    {
        if (sr == null || sr.sprite == null || !sr.enabled) return false;

        Bounds wb = sr.bounds;
        if (worldPoint.x < wb.min.x || worldPoint.x > wb.max.x || worldPoint.y < wb.min.y || worldPoint.y > wb.max.y)
        {
            return false;
        }

        Sprite sp = sr.sprite;
        Texture2D tex = sp.texture;
        if (tex == null) return true;

        int texId = tex.GetInstanceID();
        if (nonReadableTextureIds.Contains(texId))
        {
            return true;
        }

        try
        {
            Vector3 local3 = sr.transform.InverseTransformPoint(new Vector3(worldPoint.x, worldPoint.y, sr.transform.position.z));
            Bounds sb = sp.bounds;
            if (local3.x < sb.min.x || local3.x > sb.max.x || local3.y < sb.min.y || local3.y > sb.max.y)
            {
                return false;
            }

            Vector2 local2 = new Vector2(local3.x, local3.y);
            if (IsPointInsideSpritePhysicsShape(sp, local2))
            {
                return true;
            }

            float u = Mathf.InverseLerp(sb.min.x, sb.max.x, local3.x);
            float v = Mathf.InverseLerp(sb.min.y, sb.max.y, local3.y);
            if (sr.flipX) u = 1f - u;
            if (sr.flipY) v = 1f - v;

            Rect tr = sp.textureRect;
            int minX = Mathf.FloorToInt(tr.xMin);
            int maxX = Mathf.Max(minX, Mathf.CeilToInt(tr.xMax) - 1);
            int minY = Mathf.FloorToInt(tr.yMin);
            int maxY = Mathf.Max(minY, Mathf.CeilToInt(tr.yMax) - 1);

            int px = Mathf.Clamp(Mathf.RoundToInt(minX + (u * (maxX - minX))), minX, maxX);
            int py = Mathf.Clamp(Mathf.RoundToInt(minY + (v * (maxY - minY))), minY, maxY);
            Color c = tex.GetPixel(px, py);
            return c.a > 0.05f;
        }
        catch (UnityException)
        {
            nonReadableTextureIds.Add(texId);
            return true;
        }
    }

    bool IsPointInsideSpritePhysicsShape(Sprite sprite, Vector2 localPoint)
    {
        if (sprite == null) return false;
        int shapeCount = sprite.GetPhysicsShapeCount();
        if (shapeCount <= 0) return false;

        for (int shapeIndex = 0; shapeIndex < shapeCount; shapeIndex++)
        {
            spritePhysicsPoints.Clear();
            sprite.GetPhysicsShape(shapeIndex, spritePhysicsPoints);
            if (spritePhysicsPoints.Count < 3) continue;
            if (IsPointInPolygon(localPoint, spritePhysicsPoints)) return true;
        }

        return false;
    }

    static bool IsPointInPolygon(Vector2 p, List<Vector2> polygon)
    {
        bool inside = false;
        int j = polygon.Count - 1;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 pi = polygon[i];
            Vector2 pj = polygon[j];
            bool intersects = ((pi.y > p.y) != (pj.y > p.y)) &&
                              (p.x < ((pj.x - pi.x) * (p.y - pi.y) / Mathf.Max(0.000001f, (pj.y - pi.y)) + pi.x));
            if (intersects) inside = !inside;
            j = i;
        }
        return inside;
    }

    bool TryGetCombinedBounds(SpriteRenderer[] renderers, out Bounds bounds)
    {
        return TryGetCombinedBounds(renderers, true, out bounds);
    }

    bool TryGetCombinedBounds(SpriteRenderer[] renderers, bool includeShadow, out Bounds bounds)
    {
        bounds = default;
        bool hasAny = false;
        if (renderers == null) return false;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || !sr.enabled || sr.sprite == null) continue;
            if (!includeShadow && IsShadowRenderer(sr)) continue;
            if (!hasAny)
            {
                bounds = sr.bounds;
                hasAny = true;
            }
            else
            {
                bounds.Encapsulate(sr.bounds);
            }
        }
        return hasAny;
    }

    static bool IsShadowRenderer(SpriteRenderer sr)
    {
        if (sr == null) return false;
        string n = sr.gameObject.name.ToLowerInvariant();
        return n.Contains("shadow");
    }

    bool IsWithinTileDistance(Bounds targetBounds)
    {
        float tileSize = fallbackTileWorldSize;
        if (grid != null)
        {
            tileSize = Mathf.Max(0.01f, Mathf.Max(grid.cellSize.x, grid.cellSize.y));
        }

        float maxDistance = Mathf.Max(0.01f, maxHoverTiles * tileSize);
        Vector3 p = transform.position;
        Vector3 closest = targetBounds.ClosestPoint(p);
        return Vector2.Distance(new Vector2(p.x, p.y), new Vector2(closest.x, closest.y)) <= maxDistance;
    }

    void RenderSelector(HarvestEntry target, Bounds targetBounds)
    {
        EnsureSelectorObject();
        if (selectorRenderer == null || selectorFrames == null || selectorFrames.Length == 0) return;
        EnsureSelectorHasNoCollision();

        animTime += Time.deltaTime * Mathf.Max(1f, animationFps);
        int frameIndex = Mathf.FloorToInt(animTime) % selectorFrames.Length;
        Sprite frame = selectorFrames[frameIndex];
        if (frame == null) return;

        Vector2 kindOffset = target.kind == HarvestKind.Stone ? stoneSelectorOffset : treeSelectorOffset;
        Vector2 kindScale = target.kind == HarvestKind.Stone ? stoneSelectorScaleMultiplier : treeSelectorScaleMultiplier;
        Vector2 totalOffset = selectorOffset + kindOffset;
        Bounds renderBounds = targetBounds;
        if (TryGetSelectorDisplayBounds(target, out Bounds display))
        {
            renderBounds = display;
        }

        selectorRenderer.sprite = frame;
        selectorTransform.position = new Vector3(
            renderBounds.center.x + totalOffset.x,
            renderBounds.center.y + totalOffset.y,
            0f);

        Vector2 frameSize = frame.bounds.size;
        float sx = frameSize.x > 0.0001f ? (renderBounds.size.x * selectorPadding) / frameSize.x : 1f;
        float sy = frameSize.y > 0.0001f ? (renderBounds.size.y * selectorPadding) / frameSize.y : 1f;
        sx *= Mathf.Max(0.01f, selectorScaleMultiplier.x * kindScale.x);
        sy *= Mathf.Max(0.01f, selectorScaleMultiplier.y * kindScale.y);
        selectorTransform.localScale = new Vector3(sx, sy, 1f);

        int highestOrder = 0;
        int layerId = 0;
        bool hasLayer = false;
        for (int i = 0; i < target.renderers.Length; i++)
        {
            SpriteRenderer sr = target.renderers[i];
            if (sr == null) continue;
            if (!hasLayer)
            {
                layerId = sr.sortingLayerID;
                hasLayer = true;
            }
            if (sr.sortingOrder > highestOrder) highestOrder = sr.sortingOrder;
        }

        if (hasLayer) selectorRenderer.sortingLayerID = layerId;
        selectorRenderer.sortingOrder = highestOrder + sortingOrderBoost;
        selectorRenderer.enabled = true;
    }

    bool TryGetSelectorDisplayBounds(HarvestEntry entry, out Bounds bounds)
    {
        bounds = default;
        if (entry == null || entry.renderers == null) return false;

        if (entry.kind == HarvestKind.Tree)
        {
            // Keep tree selector around the tree body (exclude shadow footprint).
            return TryGetCombinedBounds(entry.renderers, false, out bounds);
        }

        return TryGetCombinedBounds(entry.renderers, out bounds);
    }

    void EnsureFramesLoaded()
    {
        if (selectorFrames != null && selectorFrames.Length > 0) return;

#if UNITY_EDITOR
        Dictionary<int, Sprite> selected = new Dictionary<int, Sprite>();
        UnityEngine.Object[] loaded = AssetDatabase.LoadAllAssetsAtPath(selectorAsepritePath);
        for (int i = 0; i < loaded.Length; i++)
        {
            Sprite s = loaded[i] as Sprite;
            if (s == null) continue;
            for (int f = 0; f < SelectorFrameNames.Length; f++)
            {
                if (!string.Equals(s.name, SelectorFrameNames[f], StringComparison.OrdinalIgnoreCase)) continue;
                selected[f] = s;
                break;
            }
        }

        List<Sprite> frames = new List<Sprite>(4);
        for (int i = 0; i < SelectorFrameNames.Length; i++)
        {
            if (selected.TryGetValue(i, out Sprite s) && s != null)
            {
                frames.Add(s);
            }
        }

        selectorFrames = frames.ToArray();
#endif
    }

    bool ApplyPickaxeDamageToTarget(HarvestEntry target)
    {
        if (target == null || target.root == null) return false;

        int id = target.root.GetInstanceID();
        if (!harvestHealth.TryGetValue(id, out int hp))
        {
            hp = GetMaxHealthForKind(target.kind);
        }

        hp -= GetDamageForKind(target.kind);
        harvestHealth[id] = hp;
        if (hp > 0) return true;

        DestroyTargetAndDropResources(target);
        return false;
    }

    int GetMaxHealthForKind(HarvestKind kind)
    {
        return kind == HarvestKind.Stone
            ? Mathf.Max(1, stoneMaxHealth)
            : Mathf.Max(1, treeMaxHealth);
    }

    int GetDamageForKind(HarvestKind kind)
    {
        return kind == HarvestKind.Stone
            ? Mathf.Max(1, pickaxeDamageToStone)
            : Mathf.Max(1, pickaxeDamageToTree);
    }

    void DestroyTargetAndDropResources(HarvestEntry target)
    {
        if (target == null || target.root == null) return;

        Transform root = target.root;
        int id = root.GetInstanceID();

        Bounds b;
        if (!TryGetCombinedBounds(target.renderers, out b))
        {
            b = new Bounds(root.position, new Vector3(1f, 1f, 0f));
        }

        if (target.kind == HarvestKind.Stone)
        {
            Vector3 stoneDropPos = new Vector3(b.center.x, b.min.y + stoneDropYOffset, 0f);
            int amount = ResolveRandomAmount(stoneDropAmountMin, stoneDropAmountMax);
            SpawnResourceDrop(stoneDropPos, target, "stone", "Stone", ResolveStoneSprite(), amount);
        }
        else
        {
            Vector3 woodDropPos = new Vector3(b.center.x, b.min.y + woodDropYOffset, 0f);
            int amount = ResolveRandomAmount(woodDropAmountMin, woodDropAmountMax);
            SpawnResourceDrop(woodDropPos, target, "wood", "Wood", ResolveWoodSprite(), amount);
        }

        GrantHarvestExperience(target.kind);

        if (activeSquashRoutines.TryGetValue(id, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
            activeSquashRoutines.Remove(id);
        }
        harvestHealth.Remove(id);

        if (activeHoveredTarget != null && activeHoveredTarget.root == root)
        {
            activeHoveredTarget = null;
            SetSelectorVisible(false);
        }

        harvestEntries.RemoveAll(e => e == null || e.root == null || e.root == root);
        Destroy(root.gameObject);
    }

    static int ResolveRandomAmount(int minInclusive, int maxInclusive)
    {
        int min = Mathf.Max(1, minInclusive);
        int max = Mathf.Max(min, maxInclusive);
        return UnityEngine.Random.Range(min, max + 1);
    }

    void GrantHarvestExperience(HarvestKind kind)
    {
        if (creatureParty == null) creatureParty = GetComponent<PlayerCreatureParty>();
        if (creatureParty == null || creatureParty.ActiveCreatures == null || creatureParty.ActiveCreatures.Count == 0) return;

        int activeIndex = Mathf.Clamp(creatureParty.ActivePartyIndex, 0, creatureParty.ActiveCreatures.Count - 1);
        CreatureInstance active = creatureParty.ActiveCreatures[activeIndex];
        if (active == null || active.currentHP <= 0)
        {
            int firstAlive = creatureParty.FindFirstAlivePartyIndex();
            if (firstAlive < 0) return;
            active = creatureParty.ActiveCreatures[firstAlive];
            if (active == null) return;
        }

        CreatureRegistry.Initialize();
        CreatureDefinition def = CreatureRegistry.Get(active.definitionID);
        if (def == null) return;

        int gainAmount = kind == HarvestKind.Stone ? Mathf.Max(0, stoneBreakXp) : Mathf.Max(0, treeBreakXp);
        if (gainAmount <= 0) return;

        ExperienceGainResult gain = CreatureExperienceSystem.AddExperience(active, def, gainAmount);

        if (gain.leveledUp)
        {
            CreatureLevelUpSignal.Notify(active);
            string levelMsg = active.DisplayName + " Lv " + gain.newLevel + "!";
            Vector3 textPos = new Vector3(transform.position.x, transform.position.y + 0.9f, transform.position.z);
            WorldFloatingText.Spawn(levelMsg, textPos, new Color(0.45f, 0.87f, 1f, 1f), levelUpFloatingTextDuration);
        }

        if (creatureParty != null)
        {
            creatureParty.NotifyPartyChanged();
        }

        CreatureCombatant followerCombatant = ActivePartyFollowerController.Instance != null
            ? ActivePartyFollowerController.Instance.CurrentFollowerCombatant
            : null;
        if (followerCombatant != null && ReferenceEquals(followerCombatant.Instance, active))
        {
            followerCombatant.InitFromDefinition(def, active);
        }
    }

    void SpawnResourceDrop(Vector3 worldPos, HarvestEntry sourceTarget, string itemId, string displayName, Sprite dropSprite, int amount)
    {
        GameObject drop = new GameObject(displayName + "Drop");
        drop.transform.position = worldPos;
        drop.layer = LayerMask.NameToLayer("Ignore Raycast");

        SpriteRenderer sr = drop.AddComponent<SpriteRenderer>();
        if (dropSprite != null) sr.sprite = dropSprite;

        int highestOrder = 0;
        int sortingLayerId = 0;
        bool hasLayer = false;
        if (sourceTarget != null && sourceTarget.renderers != null)
        {
            for (int i = 0; i < sourceTarget.renderers.Length; i++)
            {
                SpriteRenderer r = sourceTarget.renderers[i];
                if (r == null) continue;
                if (!hasLayer)
                {
                    sortingLayerId = r.sortingLayerID;
                    hasLayer = true;
                }
                if (r.sortingOrder > highestOrder) highestOrder = r.sortingOrder;
            }
        }
        if (hasLayer) sr.sortingLayerID = sortingLayerId;
        sr.sortingOrder = highestOrder + 2;

        float side = UnityEngine.Random.value < 0.5f ? -1f : 1f;
        float sideDistance = UnityEngine.Random.Range(
            Mathf.Max(0.01f, woodDropFallDistanceMin),
            Mathf.Max(Mathf.Max(0.01f, woodDropFallDistanceMin), woodDropFallDistanceMax));
        Vector3 fallTarget = worldPos + new Vector3(side * sideDistance, UnityEngine.Random.Range(-0.04f, 0.08f), 0f);
        float fallDuration = UnityEngine.Random.Range(
            Mathf.Max(0.02f, woodDropFallDurationMin),
            Mathf.Max(Mathf.Max(0.02f, woodDropFallDurationMin), woodDropFallDurationMax));

        float typeScaleBoost = string.Equals(itemId, "stone", StringComparison.OrdinalIgnoreCase) ? 3f : 1f;
        Vector3 dropScale = Vector3.one * Mathf.Max(0.01f, woodDropScale * 2f * typeScaleBoost);
        drop.transform.localScale = dropScale;

        resourceDrops.Add(new ResourceDropEntry
        {
            root = drop.transform,
            renderer = sr,
            itemId = itemId,
            displayName = displayName,
            amount = Mathf.Max(1, amount),
            baseScale = dropScale,
            baseSortingLayerId = sr.sortingLayerID,
            baseSortingOrder = sr.sortingOrder,
            fallStart = worldPos,
            fallTarget = fallTarget,
            fallDuration = fallDuration,
            fallElapsed = 0f,
            isFalling = true,
            collectElapsed = 0f,
            isCollecting = false
        });
    }

    Sprite ResolveWoodSprite()
    {
        if (cachedWoodSprite != null) return cachedWoodSprite;

        cachedWoodSprite = Resources.Load<Sprite>("Wood");
#if UNITY_EDITOR
        if (cachedWoodSprite == null)
        {
            cachedWoodSprite = AssetDatabase.LoadAssetAtPath<Sprite>(woodSpriteAssetPath);
        }
#endif
        return cachedWoodSprite;
    }

    Sprite ResolveStoneSprite()
    {
        if (cachedStoneSprite != null) return cachedStoneSprite;

#if UNITY_EDITOR
        if (!string.IsNullOrWhiteSpace(stoneSpriteAssetPath))
        {
            if (stoneSpriteAssetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                stoneSpriteAssetPath.EndsWith(".aseprite", StringComparison.OrdinalIgnoreCase))
            {
                UnityEngine.Object[] all = AssetDatabase.LoadAllAssetsAtPath(stoneSpriteAssetPath);
                string wanted = string.IsNullOrWhiteSpace(stoneSpriteName) ? DefaultStoneSpriteName : stoneSpriteName.Trim();

                for (int i = 0; i < all.Length; i++)
                {
                    Sprite s = all[i] as Sprite;
                    if (s == null) continue;
                    if (string.Equals(s.name, wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        cachedStoneSprite = s;
                        break;
                    }
                }

                if (cachedStoneSprite == null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        Sprite s = all[i] as Sprite;
                        if (s == null) continue;
                        string n = s.name.ToLowerInvariant();
                        if (n.Contains("stone") && n.Contains("06"))
                        {
                            cachedStoneSprite = s;
                            break;
                        }
                    }
                }

                if (cachedStoneSprite == null)
                {
                    for (int i = 0; i < all.Length; i++)
                    {
                        Sprite s = all[i] as Sprite;
                        if (s != null)
                        {
                            cachedStoneSprite = s;
                            break;
                        }
                    }
                }
            }
            else
            {
                cachedStoneSprite = AssetDatabase.LoadAssetAtPath<Sprite>(stoneSpriteAssetPath);
            }
        }
#endif

        if (cachedStoneSprite == null)
        {
            cachedStoneSprite = Resources.Load<Sprite>("Stone 06");
        }

        return cachedStoneSprite;
    }

    void ProcessResourceDrops(bool hasMouseWorld, Vector2 mouseWorld)
    {
        if (resourceDrops.Count == 0) return;

        for (int i = resourceDrops.Count - 1; i >= 0; i--)
        {
            ResourceDropEntry drop = resourceDrops[i];
            if (drop == null || drop.root == null)
            {
                resourceDrops.RemoveAt(i);
                continue;
            }

            if (drop.isCollecting)
            {
                bool done = UpdateDropCollectAnimation(drop);
                if (done)
                {
                    if (drop.root != null) Destroy(drop.root.gameObject);
                    resourceDrops.RemoveAt(i);
                }
                continue;
            }

            if (drop.isFalling)
            {
                UpdateDropFalling(drop);
                continue;
            }

            bool nearPlayer = Vector2.Distance(
                new Vector2(transform.position.x, transform.position.y),
                new Vector2(drop.root.position.x, drop.root.position.y))
                <= Mathf.Max(0.1f, woodPickupDistance);

            bool hovered = false;
            if (hasMouseWorld && drop.renderer != null && drop.renderer.enabled && drop.renderer.sprite != null)
            {
                hovered = drop.renderer.bounds.Contains(new Vector3(mouseWorld.x, mouseWorld.y, drop.root.position.z));
            }

            if (!nearPlayer && !hovered) continue;
            if (!TryPickupResource(drop)) continue;
            StartDropCollectAnimation(drop);
        }
    }

    void UpdateDropFalling(ResourceDropEntry drop)
    {
        if (drop == null || drop.root == null) return;

        drop.fallElapsed += Time.deltaTime;
        float t = Mathf.Clamp01(drop.fallDuration > 0.0001f ? drop.fallElapsed / drop.fallDuration : 1f);
        float eased = t * t * (3f - (2f * t));
        Vector3 pos = Vector3.Lerp(drop.fallStart, drop.fallTarget, eased);
        pos.y += Mathf.Sin(t * Mathf.PI) * Mathf.Max(0f, woodDropArcHeight);
        drop.root.position = pos;

        if (t < 1f) return;

        drop.root.position = drop.fallTarget;
        drop.root.localScale = drop.baseScale;
        drop.isFalling = false;
    }

    void StartDropCollectAnimation(ResourceDropEntry drop)
    {
        if (drop == null || drop.root == null || drop.renderer == null) return;

        drop.isCollecting = true;
        drop.collectElapsed = 0f;
        drop.collectStart = drop.root.position;
        drop.collectTarget = ResolveCurrentCollectTarget(drop);
        SetDropCollectSorting(drop);
    }

    bool UpdateDropCollectAnimation(ResourceDropEntry drop)
    {
        if (drop == null || drop.root == null || drop.renderer == null) return true;

        float moveDuration = Mathf.Max(0.01f, woodCollectMoveDuration);
        float expandDuration = Mathf.Max(0.01f, woodCollectExpandDuration);
        float shrinkDuration = Mathf.Max(0.01f, woodCollectShrinkDuration);
        float expandScale = Mathf.Max(1f, woodCollectExpandScale);

        drop.collectElapsed += Time.deltaTime;
        float t = drop.collectElapsed;

        drop.collectTarget = ResolveCurrentCollectTarget(drop);
        SetDropCollectSorting(drop);

        if (t <= moveDuration)
        {
            float u = t / moveDuration;
            float eased = 1f - Mathf.Pow(1f - u, 3f);
            drop.root.position = Vector3.Lerp(drop.collectStart, drop.collectTarget, eased);
            drop.root.localScale = drop.baseScale;
            return false;
        }

        drop.root.position = drop.collectTarget;
        t -= moveDuration;
        if (t <= expandDuration)
        {
            float u = t / expandDuration;
            float s = Mathf.Lerp(1f, expandScale, u);
            drop.root.localScale = drop.baseScale * s;
            return false;
        }

        t -= expandDuration;
        if (t <= shrinkDuration)
        {
            float u = t / shrinkDuration;
            float s = Mathf.Lerp(expandScale, 0f, u);
            drop.root.localScale = drop.baseScale * s;
            return false;
        }

        return true;
    }

    Vector3 ResolveCurrentCollectTarget(ResourceDropEntry drop)
    {
        float z = drop != null && drop.root != null ? drop.root.position.z : 0f;
        return new Vector3(
            transform.position.x,
            transform.position.y + woodCollectTargetYOffset,
            z);
    }

    void SetDropCollectSorting(ResourceDropEntry drop)
    {
        if (drop == null || drop.renderer == null) return;

        if (playerSpriteRenderer != null)
        {
            drop.renderer.sortingLayerID = playerSpriteRenderer.sortingLayerID;
            drop.renderer.sortingOrder = playerSpriteRenderer.sortingOrder + Mathf.Max(1, woodCollectSortingBoost);
            return;
        }

        drop.renderer.sortingLayerID = drop.baseSortingLayerId;
        drop.renderer.sortingOrder = drop.baseSortingOrder + Mathf.Max(1, woodCollectSortingBoost);
    }

    bool TryPickupResource(ResourceDropEntry drop)
    {
        if (inventory == null || drop == null) return false;

        EnsureRuntimeItem(drop.itemId);
        InventoryItemData data = ResolveRuntimeItem(drop.itemId);
        if (data == null) return false;

        return inventory.TryAddItem(data, Mathf.Max(1, drop.amount));
    }

    void EnsureRuntimeItem(string itemId)
    {
        string normalized = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim().ToLowerInvariant();

        if (normalized == "stone")
        {
            if (runtimeStoneItem != null) return;
            runtimeStoneItem = ScriptableObject.CreateInstance<InventoryItemData>();
            runtimeStoneItem.hideFlags = HideFlags.HideAndDontSave;
            runtimeStoneItem.itemId = "stone";
            runtimeStoneItem.displayName = "Stone";
            runtimeStoneItem.icon = ResolveStoneSprite();
            return;
        }

        if (runtimeWoodItem != null) return;
        runtimeWoodItem = ScriptableObject.CreateInstance<InventoryItemData>();
        runtimeWoodItem.hideFlags = HideFlags.HideAndDontSave;
        runtimeWoodItem.itemId = "wood";
        runtimeWoodItem.displayName = "Wood";
        runtimeWoodItem.icon = ResolveWoodSprite();
    }

    InventoryItemData ResolveRuntimeItem(string itemId)
    {
        string normalized = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim().ToLowerInvariant();
        if (normalized == "stone") return runtimeStoneItem;
        return runtimeWoodItem;
    }

    void EnsurePickaxeHitAudio()
    {
        if (pickaxeHitSfxSource != null) return;
        pickaxeHitSfxSource = gameObject.AddComponent<AudioSource>();
        pickaxeHitSfxSource.playOnAwake = false;
        pickaxeHitSfxSource.loop = false;
        pickaxeHitSfxSource.spatialBlend = 0f;
    }

    void PlayRandomPickaxeHitSfx()
    {
        EnsurePickaxeHitAudio();
        EnsurePickaxeHitSfxLoaded();

        if (pickaxeHitSfxSource == null || pickaxeHitSfx == null || pickaxeHitSfx.Length == 0) return;

        List<AudioClip> valid = null;
        for (int i = 0; i < pickaxeHitSfx.Length; i++)
        {
            if (pickaxeHitSfx[i] == null) continue;
            if (valid == null) valid = new List<AudioClip>(pickaxeHitSfx.Length);
            valid.Add(pickaxeHitSfx[i]);
        }

        if (valid == null || valid.Count == 0) return;
        AudioClip clip = valid[UnityEngine.Random.Range(0, valid.Count)];
        if (clip == null) return;
        pickaxeHitSfxSource.PlayOneShot(clip, Mathf.Clamp01(pickaxeHitSfxVolume));
    }

    void EnsurePickaxeHitSfxLoaded()
    {
        if (attemptedPickaxeHitSfxLoad) return;
        attemptedPickaxeHitSfxLoad = true;

        if (pickaxeHitSfx != null)
        {
            for (int i = 0; i < pickaxeHitSfx.Length; i++)
            {
                if (pickaxeHitSfx[i] != null) return;
            }
        }

#if UNITY_EDITOR
        List<AudioClip> loaded = new List<AudioClip>(PickaxeHitSfxEnd - PickaxeHitSfxStart + 1);
        for (int i = PickaxeHitSfxStart; i <= PickaxeHitSfxEnd; i++)
        {
            string path = PickaxeHitSfxBasePath + i.ToString("000") + PickaxeHitSfxSuffix;
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) loaded.Add(clip);
        }

        if (loaded.Count > 0)
        {
            pickaxeHitSfx = loaded.ToArray();
        }
#endif
    }
}

