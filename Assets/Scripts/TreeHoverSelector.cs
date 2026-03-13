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
    private const int FixedTreeMaxHealth = 100;
    private const int FixedPickaxeTreeDamage = 10;

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
    public int woodDropAmount = 1;
    public string woodSpriteAssetPath = "Assets/Resources/Wood.png";
    public float woodDropYOffset = 0.12f;
    public float woodPickupDistance = 1f;
    [Range(0.01f, 1f)] public float woodDropScale = 0.1f;
    public float woodDropFallDistanceMin = 0.28f;
    public float woodDropFallDistanceMax = 0.68f;
    public float woodDropFallDurationMin = 0.14f;
    public float woodDropFallDurationMax = 0.26f;
    public float woodDropArcHeight = 0.16f;

    [Header("Wood Collect Animation")]
    public float woodCollectMoveDuration = 0.18f;
    public float woodCollectExpandDuration = 0.08f;
    public float woodCollectShrinkDuration = 0.10f;
    public float woodCollectExpandScale = 1.35f;
    public float woodCollectTargetYOffset = 0.45f;
    public int woodCollectSortingBoost = 60;

    class TreeEntry
    {
        public Transform root;
        public SpriteRenderer[] renderers;
    }

    class WoodDropEntry
    {
        public Transform root;
        public SpriteRenderer renderer;
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

    struct TreeSquashState
    {
        public Transform root;
        public SpriteRenderer[] renderers;
        public Vector3 baseLocalScale;
        public float baseBottomY;
        public float baseWorldHeight;
        public bool includeShadow;
    }

    private readonly List<TreeEntry> treeEntries = new List<TreeEntry>();
    private PlayerToolController toolController;
    private Camera mainCam;
    private Grid grid;
    private float animTime;
    private float nextRefreshTime;
    private SpriteRenderer selectorRenderer;
    private Transform selectorTransform;
    private TreeEntry activeHoveredTree;
    private PlayerToolController boundToolController;
    private readonly Dictionary<int, Coroutine> activeSquashRoutines = new Dictionary<int, Coroutine>();
    private readonly Dictionary<int, int> treeHealth = new Dictionary<int, int>();
    private readonly List<WoodDropEntry> woodDrops = new List<WoodDropEntry>();
    private Sprite cachedWoodSprite;
    private InventoryModel inventory;
    private InventoryItemData runtimeWoodItem;
    private SpriteRenderer playerSpriteRenderer;

    void Awake()
    {
        treeMaxHealth = FixedTreeMaxHealth;
        pickaxeDamageToTree = FixedPickaxeTreeDamage;
        toolController = GetComponent<PlayerToolController>();
        inventory = GetComponent<InventoryModel>();
        BindToolController(toolController);
        mainCam = Camera.main;
        grid = FindFirstObjectByType<Grid>();
        EnsureFramesLoaded();
        EnsureSelectorObject();
        RefreshTreeEntries();
        SetSelectorVisible(false);
    }

    void OnDisable()
    {
        BindToolController(null);
        StopAllSquashRoutines();
        SetSelectorVisible(false);
        activeHoveredTree = null;
    }

    void OnDestroy()
    {
        if (runtimeWoodItem == null) return;
        if (Application.isPlaying) Destroy(runtimeWoodItem);
        else DestroyImmediate(runtimeWoodItem);
        runtimeWoodItem = null;
    }

    void Update()
    {
        if (toolController == null) toolController = GetComponent<PlayerToolController>();
        if (inventory == null) inventory = GetComponent<InventoryModel>();
        if (playerSpriteRenderer == null) playerSpriteRenderer = GetComponent<SpriteRenderer>();
        if (toolController != boundToolController) BindToolController(toolController);
        if (mainCam == null) mainCam = Camera.main;
        if (grid == null) grid = FindFirstObjectByType<Grid>();

        if (Time.time >= nextRefreshTime)
        {
            nextRefreshTime = Time.time + 1.5f;
            RefreshTreeEntries();
            EnsureFramesLoaded();
        }

        bool hasMouseWorld = TryGetMouseWorldPoint(out Vector2 mouseWorld);
        ProcessWoodDrops(hasMouseWorld, mouseWorld);

        if (toolController == null || !toolController.IsPickaxeEquipped())
        {
            SetSelectorVisible(false);
            activeHoveredTree = null;
            return;
        }

        if (mainCam == null || selectorFrames == null || selectorFrames.Length == 0)
        {
            SetSelectorVisible(false);
            activeHoveredTree = null;
            return;
        }

        if (!hasMouseWorld)
        {
            SetSelectorVisible(false);
            activeHoveredTree = null;
            return;
        }

        if (!TryGetHoveredTree(mouseWorld, out TreeEntry hovered, out Bounds hoveredBounds))
        {
            SetSelectorVisible(false);
            activeHoveredTree = null;
            return;
        }

        if (!IsWithinTileDistance(hoveredBounds))
        {
            SetSelectorVisible(false);
            activeHoveredTree = null;
            return;
        }

        RenderSelector(hovered, hoveredBounds);
        activeHoveredTree = hovered;
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
        if (activeHoveredTree == null || activeHoveredTree.root == null) return;

        if (!ApplyPickaxeDamageToTree(activeHoveredTree))
        {
            return;
        }

        StartTreeSquash(activeHoveredTree);
    }

    void StartTreeSquash(TreeEntry tree)
    {
        if (tree == null || tree.root == null) return;
        int key = tree.root.GetInstanceID();

        if (activeSquashRoutines.TryGetValue(key, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
        }
        activeSquashRoutines[key] = StartCoroutine(PlayTreeSquashRoutine(key, tree));
    }

    IEnumerator PlayTreeSquashRoutine(int key, TreeEntry tree)
    {
        if (!TryCaptureTreeSquashState(tree, out TreeSquashState state))
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
            ApplyTreeSquash(state, u);
            yield return null;
        }

        t = 0f;
        while (t < up)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / up);
            ApplyTreeSquash(state, 1f - u);
            yield return null;
        }

        RestoreTreeSquash(state);
        activeSquashRoutines.Remove(key);
    }

    bool TryCaptureTreeSquashState(TreeEntry tree, out TreeSquashState state)
    {
        state = default;
        if (tree == null || tree.root == null) return false;

        float height = 1f;
        float bottomY = tree.root.position.y;
        if (TryGetCombinedBounds(tree.renderers, includeShadowInSquash, out Bounds bounds))
        {
            height = Mathf.Max(0.001f, bounds.size.y);
            bottomY = bounds.min.y;
        }

        state = new TreeSquashState
        {
            root = tree.root,
            renderers = tree.renderers,
            baseLocalScale = tree.root.localScale,
            baseBottomY = bottomY,
            baseWorldHeight = height,
            includeShadow = includeShadowInSquash
        };
        return true;
    }

    void ApplyTreeSquash(TreeSquashState state, float squashAmount)
    {
        if (state.root == null) return;
        float sy = Mathf.Lerp(1f, Mathf.Clamp(hitSquashScaleY, 0.4f, 1f), squashAmount);
        // Keep horizontal scale stable to avoid visible seam splitting on multi-part tree sprites.
        float sx = 1f;

        Vector3 local = state.baseLocalScale;
        state.root.localScale = new Vector3(local.x * sx, local.y * sy, local.z);

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

    void RestoreTreeSquash(TreeSquashState state)
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
        if (mouse == null) return false;

        Vector2 screen = mouse.position.ReadValue();
        float z = Mathf.Abs(mainCam.transform.position.z);
        Vector3 wp = mainCam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, z));
        world = new Vector2(wp.x, wp.y);
        return true;
    }

    void RefreshTreeEntries()
    {
        treeEntries.Clear();
        HashSet<int> presentIds = new HashSet<int>();
        FadeableSprite[] fades = FindObjectsByType<FadeableSprite>(includeInactiveTrees ? FindObjectsInactive.Include : FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < fades.Length; i++)
        {
            FadeableSprite fade = fades[i];
            if (fade == null) continue;
            Transform root = fade.transform;
            if (root == null) continue;

            string rootName = root.name.ToLowerInvariant();
            if (!rootName.Contains("tree")) continue;

            SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers == null || renderers.Length == 0) continue;

            TreeEntry entry = new TreeEntry
            {
                root = root,
                renderers = renderers
            };
            treeEntries.Add(entry);

            int id = root.GetInstanceID();
            presentIds.Add(id);
            if (!treeHealth.ContainsKey(id))
            {
                treeHealth[id] = Mathf.Max(1, treeMaxHealth);
            }
        }

        List<int> toRemove = new List<int>();
        foreach (KeyValuePair<int, int> kv in treeHealth)
        {
            if (!presentIds.Contains(kv.Key)) toRemove.Add(kv.Key);
        }
        for (int i = 0; i < toRemove.Count; i++)
        {
            treeHealth.Remove(toRemove[i]);
        }
    }

    bool TryGetHoveredTree(Vector2 mouseWorld, out TreeEntry hovered, out Bounds hoveredBounds)
    {
        hovered = null;
        hoveredBounds = default;
        float bestDistance = float.MaxValue;

        Vector3 probe = new Vector3(mouseWorld.x, mouseWorld.y, 0f);
        for (int i = 0; i < treeEntries.Count; i++)
        {
            TreeEntry entry = treeEntries[i];
            if (entry == null || entry.root == null) continue;
            if (!entry.root.gameObject.activeInHierarchy && !includeInactiveTrees) continue;

            if (!TryGetCombinedBounds(entry.renderers, out Bounds b)) continue;

            bool contains = probe.x >= b.min.x && probe.x <= b.max.x && probe.y >= b.min.y && probe.y <= b.max.y;
            if (!contains) continue;

            float centerDist = Vector2.Distance(mouseWorld, new Vector2(b.center.x, b.center.y));
            if (centerDist < bestDistance)
            {
                bestDistance = centerDist;
                hovered = entry;
                hoveredBounds = b;
            }
        }

        return hovered != null;
    }

    bool TryGetCombinedBounds(SpriteRenderer[] renderers, out Bounds bounds)
    {
        return TryGetCombinedBounds(renderers, true, out bounds);
    }

    bool TryGetCombinedBounds(SpriteRenderer[] renderers, bool includeShadow, out Bounds bounds)
    {
        bounds = default;
        bool hasAny = false;
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

    bool IsWithinTileDistance(Bounds treeBounds)
    {
        float tileSize = fallbackTileWorldSize;
        if (grid != null)
        {
            tileSize = Mathf.Max(0.01f, Mathf.Max(grid.cellSize.x, grid.cellSize.y));
        }

        float maxDistance = Mathf.Max(0.01f, maxHoverTiles * tileSize);
        Vector3 p = transform.position;
        Vector3 closest = treeBounds.ClosestPoint(p);
        return Vector2.Distance(new Vector2(p.x, p.y), new Vector2(closest.x, closest.y)) <= maxDistance;
    }

    void RenderSelector(TreeEntry tree, Bounds treeBounds)
    {
        EnsureSelectorObject();
        if (selectorRenderer == null || selectorFrames == null || selectorFrames.Length == 0) return;
        EnsureSelectorHasNoCollision();

        animTime += Time.deltaTime * Mathf.Max(1f, animationFps);
        int frameIndex = Mathf.FloorToInt(animTime) % selectorFrames.Length;
        Sprite frame = selectorFrames[frameIndex];
        if (frame == null) return;

        selectorRenderer.sprite = frame;
        selectorTransform.position = new Vector3(
            treeBounds.center.x + selectorOffset.x,
            treeBounds.center.y + selectorOffset.y,
            0f);

        Vector2 frameSize = frame.bounds.size;
        float sx = frameSize.x > 0.0001f ? (treeBounds.size.x * selectorPadding) / frameSize.x : 1f;
        float sy = frameSize.y > 0.0001f ? (treeBounds.size.y * selectorPadding) / frameSize.y : 1f;
        sx *= Mathf.Max(0.01f, selectorScaleMultiplier.x);
        sy *= Mathf.Max(0.01f, selectorScaleMultiplier.y);
        selectorTransform.localScale = new Vector3(sx, sy, 1f);

        int highestOrder = 0;
        int layerId = 0;
        bool hasLayer = false;
        for (int i = 0; i < tree.renderers.Length; i++)
        {
            SpriteRenderer sr = tree.renderers[i];
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

    void EnsureFramesLoaded()
    {
        if (selectorFrames != null && selectorFrames.Length > 0) return;

#if UNITY_EDITOR
        Dictionary<int, Sprite> selected = new Dictionary<int, Sprite>();
        Object[] loaded = AssetDatabase.LoadAllAssetsAtPath(selectorAsepritePath);
        for (int i = 0; i < loaded.Length; i++)
        {
            Sprite s = loaded[i] as Sprite;
            if (s == null) continue;
            for (int f = 0; f < SelectorFrameNames.Length; f++)
            {
                if (!string.Equals(s.name, SelectorFrameNames[f], System.StringComparison.OrdinalIgnoreCase)) continue;
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

    bool ApplyPickaxeDamageToTree(TreeEntry tree)
    {
        if (tree == null || tree.root == null) return false;

        int id = tree.root.GetInstanceID();
        if (!treeHealth.TryGetValue(id, out int hp))
        {
            hp = Mathf.Max(1, treeMaxHealth);
        }

        hp -= Mathf.Max(1, pickaxeDamageToTree);
        treeHealth[id] = hp;
        if (hp > 0) return true;

        DestroyTreeAndDropWood(tree);
        return false;
    }

    void DestroyTreeAndDropWood(TreeEntry tree)
    {
        if (tree == null || tree.root == null) return;

        Transform root = tree.root;
        int id = root.GetInstanceID();

        Bounds b;
        if (!TryGetCombinedBounds(tree.renderers, out b))
        {
            b = new Bounds(root.position, new Vector3(1f, 1f, 0f));
        }

        Vector3 dropPos = new Vector3(b.center.x, b.min.y + woodDropYOffset, 0f);
        SpawnWoodDrop(dropPos, tree);

        if (activeSquashRoutines.TryGetValue(id, out Coroutine running) && running != null)
        {
            StopCoroutine(running);
            activeSquashRoutines.Remove(id);
        }
        treeHealth.Remove(id);

        if (activeHoveredTree != null && activeHoveredTree.root == root)
        {
            activeHoveredTree = null;
            SetSelectorVisible(false);
        }

        treeEntries.RemoveAll(e => e == null || e.root == null || e.root == root);
        Destroy(root.gameObject);
    }

    void SpawnWoodDrop(Vector3 worldPos, TreeEntry sourceTree)
    {
        GameObject drop = new GameObject("WoodDrop");
        drop.transform.position = worldPos;
        drop.layer = LayerMask.NameToLayer("Ignore Raycast");

        SpriteRenderer sr = drop.AddComponent<SpriteRenderer>();
        Sprite wood = ResolveWoodSprite();
        if (wood != null) sr.sprite = wood;

        int highestOrder = 0;
        int sortingLayerId = 0;
        bool hasLayer = false;
        if (sourceTree != null && sourceTree.renderers != null)
        {
            for (int i = 0; i < sourceTree.renderers.Length; i++)
            {
                SpriteRenderer r = sourceTree.renderers[i];
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

        float side = Random.value < 0.5f ? -1f : 1f;
        float sideDistance = Random.Range(
            Mathf.Max(0.01f, woodDropFallDistanceMin),
            Mathf.Max(Mathf.Max(0.01f, woodDropFallDistanceMin), woodDropFallDistanceMax));
        Vector3 fallTarget = worldPos + new Vector3(side * sideDistance, Random.Range(-0.04f, 0.08f), 0f);
        float fallDuration = Random.Range(
            Mathf.Max(0.02f, woodDropFallDurationMin),
            Mathf.Max(Mathf.Max(0.02f, woodDropFallDurationMin), woodDropFallDurationMax));

        Vector3 dropScale = Vector3.one * Mathf.Max(0.01f, woodDropScale * 2f);
        drop.transform.localScale = dropScale;

        woodDrops.Add(new WoodDropEntry
        {
            root = drop.transform,
            renderer = sr,
            amount = Mathf.Max(1, woodDropAmount),
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

    void ProcessWoodDrops(bool hasMouseWorld, Vector2 mouseWorld)
    {
        if (woodDrops.Count == 0) return;

        for (int i = woodDrops.Count - 1; i >= 0; i--)
        {
            WoodDropEntry drop = woodDrops[i];
            if (drop == null || drop.root == null)
            {
                woodDrops.RemoveAt(i);
                continue;
            }

            if (drop.isCollecting)
            {
                bool done = UpdateWoodDropCollectAnimation(drop);
                if (done)
                {
                    if (drop.root != null) Destroy(drop.root.gameObject);
                    woodDrops.RemoveAt(i);
                }
                continue;
            }

            if (drop.isFalling)
            {
                UpdateWoodDropFalling(drop);
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
            if (!TryPickupWood(drop)) continue;
            StartWoodDropCollectAnimation(drop);
        }
    }

    void UpdateWoodDropFalling(WoodDropEntry drop)
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

    void StartWoodDropCollectAnimation(WoodDropEntry drop)
    {
        if (drop == null || drop.root == null || drop.renderer == null) return;

        drop.isCollecting = true;
        drop.collectElapsed = 0f;
        drop.collectStart = drop.root.position;
        drop.collectTarget = ResolveCurrentWoodCollectTarget(drop);
        SetWoodDropCollectSorting(drop);
    }

    bool UpdateWoodDropCollectAnimation(WoodDropEntry drop)
    {
        if (drop == null || drop.root == null || drop.renderer == null) return true;

        float moveDuration = Mathf.Max(0.01f, woodCollectMoveDuration);
        float expandDuration = Mathf.Max(0.01f, woodCollectExpandDuration);
        float shrinkDuration = Mathf.Max(0.01f, woodCollectShrinkDuration);
        float expandScale = Mathf.Max(1f, woodCollectExpandScale);

        drop.collectElapsed += Time.deltaTime;
        float t = drop.collectElapsed;

        drop.collectTarget = ResolveCurrentWoodCollectTarget(drop);
        SetWoodDropCollectSorting(drop);

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

    Vector3 ResolveCurrentWoodCollectTarget(WoodDropEntry drop)
    {
        float z = drop != null && drop.root != null ? drop.root.position.z : 0f;
        return new Vector3(
            transform.position.x,
            transform.position.y + woodCollectTargetYOffset,
            z);
    }

    void SetWoodDropCollectSorting(WoodDropEntry drop)
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

    bool TryPickupWood(WoodDropEntry drop)
    {
        if (inventory == null) return false;
        EnsureRuntimeWoodItem();
        if (runtimeWoodItem == null) return false;

        return inventory.TryAddItem(runtimeWoodItem, Mathf.Max(1, drop.amount));
    }

    void EnsureRuntimeWoodItem()
    {
        if (runtimeWoodItem != null) return;
        runtimeWoodItem = ScriptableObject.CreateInstance<InventoryItemData>();
        runtimeWoodItem.hideFlags = HideFlags.HideAndDontSave;
        runtimeWoodItem.itemId = "wood";
        runtimeWoodItem.displayName = "Wood";
        runtimeWoodItem.icon = ResolveWoodSprite();
    }
}
