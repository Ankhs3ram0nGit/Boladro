using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(Tilemap))]
public class MapPainter : MonoBehaviour
{
    private const string OverlayTilemapObjectName = "GroundSpringOverlay";

    [Header("Map Layout")]
    public int width = 128;
    public int height = 96;
    public int seed = 27;
    public bool autoGenerateOnEnable = false;
    public bool useSpringOverlay = false;

    [Header("Primary Tile Source")]
    public Texture2D springTileset;
    [Min(8)] public int tilePixels = 16;
    [Min(8f)] public float tilePixelsPerUnit = 16f;

    [Header("Props")]
    public Transform propsRoot;
    public int treeCount = 58;
    public int bushCount = 84;
    public int rockCount = 46;

    [Header("Grass Scatter")]
    [Range(0f, 1f)] public float grassDensityOnGreenTiles = 0.75f;
    [Range(0f, 0.49f)] public float grassPositionJitter = 0.22f;
    public bool scatterGrassOnGreenTiles = true;

    [Header("Rock Source")]
    public Texture2D txPropsTexture;
    public List<Sprite> rockSprites = new List<Sprite>();

    [Header("Legacy Fallback Tiles")]
    public TileBase grass;
    public TileBase flower;
    public TileBase path;
    public TileBase wall;

    private const string SpringTilesetPath = "Assets/Farm RPG FREE 16x16 - Tiny Asset Pack/Tileset/Tileset Spring.png";
    private const string TxPropsPath = "Assets/Cainos/Pixel Art Top Down - Basic/Texture/TX Props.png";

    private static readonly string[] RequestedRockNames =
    {
        "TX Props - Stone 02",
        "TX Props - Stone 03",
        "TX Props - Stone 04",
        "TX Props - Stone 05",
        "TX Props - Stone 06"
    };

    private static readonly string[] GrassPrefabPaths =
    {
        "Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Grass 01.prefab",
        "Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Grass 02.prefab",
        "Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Grass 03.prefab",
        "Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Grass 04.prefab",
        "Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Grass 05.prefab",
        "Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Grass 06.prefab",
        "Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Grass 07.prefab",
        "Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Grass 08.prefab",
        "Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Grass 09.prefab",
        "Assets/Cainos/Pixel Art Top Down - Basic/Prefab/Plant/PF Plant - Grass 10.prefab"
    };

    private TileBase springGrassA;
    private TileBase springGrassB;
    private TileBase springGrassC;
    private TileBase springPathA;
    private TileBase springPathB;
    private TileBase springPathC;
    private TileBase springDarkA;
    private TileBase springDarkB;
    private TileBase springRiverA;
    private readonly List<GameObject> grassPrefabLibrary = new List<GameObject>();
    private readonly Dictionary<int, bool> greenSpriteCache = new Dictionary<int, bool>();

    void OnEnable()
    {
        // Keep heavy map/procedural prop generation in edit mode only, and never
        // during play mode transitions.
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
#endif
        if (autoGenerateOnEnable && !Application.isPlaying)
        {
            Paint();
        }
    }

    [ContextMenu("Paint")]
    public void Paint()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
#endif
        if (Application.isPlaying) return;

        EnsureSources();
        if (springTileset == null && grass == null)
        {
            Debug.LogWarning("[MapPainter] Missing tileset sources. Assign springTileset or legacy grass tile.");
            return;
        }

        Tilemap map = GetComponent<Tilemap>();
        if (map == null) return;
        Tilemap overlay = useSpringOverlay ? EnsureOverlayTilemap(map) : null;
        if (!useSpringOverlay)
        {
            Transform existingOverlay = map.transform.Find(OverlayTilemapObjectName);
            if (existingOverlay != null)
            {
                Tilemap staleOverlay = existingOverlay.GetComponent<Tilemap>();
                if (staleOverlay != null) staleOverlay.ClearAllTiles();
            }
        }

        BuildSpringTilePalette();
        PaintLargeLayout(map, overlay);
        RebuildPropsLayout();
    }

    [ContextMenu("Scatter Grass Only")]
    public void ScatterGrassOnly()
    {
#if UNITY_EDITOR
        if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
#endif
        if (Application.isPlaying) return;

        EnsureSources();
        if (propsRoot == null) return;

        List<GameObject> grassTemplates = CollectGrassTemplates();
        if (grassTemplates.Count == 0) return;

        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();
        for (int i = propsRoot.childCount - 1; i >= 0; i--)
        {
            Transform c = propsRoot.GetChild(i);
            if (c == null) continue;
            string n = c.name.ToLowerInvariant();

            if (n.StartsWith("gen_grass_"))
            {
                SafeDestroy(c.gameObject);
                continue;
            }

            if (!c.gameObject.activeInHierarchy) continue;
            Vector3 p = c.position;
            occupied.Add(new Vector2Int(Mathf.FloorToInt(p.x), Mathf.FloorToInt(p.y)));
        }

        PlaceDenseGrassOnGreenTiles(grassTemplates, occupied);
    }

    private Tilemap EnsureOverlayTilemap(Tilemap baseMap)
    {
        if (baseMap == null) return null;

        Transform existing = baseMap.transform.Find(OverlayTilemapObjectName);
        GameObject go = existing != null ? existing.gameObject : null;
        if (go == null)
        {
            go = new GameObject(OverlayTilemapObjectName);
            go.transform.SetParent(baseMap.transform, false);
            go.layer = baseMap.gameObject.layer;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }

        Tilemap overlay = go.GetComponent<Tilemap>();
        if (overlay == null) overlay = go.AddComponent<Tilemap>();

        TilemapRenderer baseRenderer = baseMap.GetComponent<TilemapRenderer>();
        TilemapRenderer overlayRenderer = go.GetComponent<TilemapRenderer>();
        if (overlayRenderer == null) overlayRenderer = go.AddComponent<TilemapRenderer>();

        if (baseRenderer != null && overlayRenderer != null)
        {
            overlayRenderer.sortingLayerID = baseRenderer.sortingLayerID;
            overlayRenderer.sortingOrder = baseRenderer.sortingOrder + 1;
            overlayRenderer.mode = baseRenderer.mode;
        }

        return overlay;
    }

    private void EnsureSources()
    {
        if (propsRoot == null)
        {
            GameObject props = GameObject.Find("Props");
            if (props != null) propsRoot = props.transform;
        }

#if UNITY_EDITOR
        if (springTileset == null)
        {
            springTileset = AssetDatabase.LoadAssetAtPath<Texture2D>(SpringTilesetPath);
        }

        if (txPropsTexture == null)
        {
            txPropsTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(TxPropsPath);
        }

        if (rockSprites == null) rockSprites = new List<Sprite>();
        if (rockSprites.Count == 0)
        {
            LoadRequestedRockSpritesEditor();
        }

        if (grassPrefabLibrary.Count == 0)
        {
            LoadGrassPrefabsEditor();
        }
#endif
    }

    private void BuildSpringTilePalette()
    {
        int cols = springTileset != null && tilePixels > 0 ? springTileset.width / tilePixels : 0;
        int rows = springTileset != null && tilePixels > 0 ? springTileset.height / tilePixels : 0;

        // Coarse layout (64px tiles in this specific spring sheet): 3 x 5 bands.
        if (cols <= 3 && rows <= 6)
        {
            springGrassA = BuildSpringTile(topRow: 0, col: 1);
            springGrassB = BuildSpringTile(topRow: 4, col: 1);
            springGrassC = BuildSpringTile(topRow: 1, col: 1);

            springPathA = BuildSpringTile(topRow: 2, col: 1);
            springPathB = BuildSpringTile(topRow: 2, col: 2);
            springPathC = BuildSpringTile(topRow: 2, col: 0);

            springDarkA = BuildSpringTile(topRow: 1, col: 0);
            springDarkB = BuildSpringTile(topRow: 1, col: 2);

            springRiverA = BuildSpringTile(topRow: 3, col: 1);
            return;
        }

        // Fine layout (16px cells) fallback. Coordinates are chosen from fully-filled
        // spring sheet cells to avoid black/transparent seams.
        springGrassA = BuildSpringTile(topRow: 1, col: 9);
        springGrassB = BuildSpringTile(topRow: 17, col: 9);
        springGrassC = BuildSpringTile(topRow: 1, col: 9);

        springPathA = BuildSpringTile(topRow: 5, col: 9);
        springPathB = BuildSpringTile(topRow: 6, col: 9);
        springPathC = BuildSpringTile(topRow: 9, col: 9);

        springDarkA = BuildSpringTile(topRow: 4, col: 8);
        springDarkB = BuildSpringTile(topRow: 8, col: 8);

        // The provided spring sheet does not include a fully filled blue-water tile
        // in 16x16 mode, so river uses a darker ground variant.
        springRiverA = BuildSpringTile(topRow: 10, col: 8);
    }

    private TileBase BuildSpringTile(int topRow, int col)
    {
        if (springTileset == null || tilePixels <= 0) return null;

        int cols = springTileset.width / tilePixels;
        int rows = springTileset.height / tilePixels;
        if (col < 0 || col >= cols) return null;
        if (topRow < 0 || topRow >= rows) return null;

        int x = col * tilePixels;
        int y = springTileset.height - ((topRow + 1) * tilePixels);
        if (y < 0) return null;

        Sprite sprite = Sprite.Create(
            springTileset,
            new Rect(x, y, tilePixels, tilePixels),
            new Vector2(0.5f, 0.5f),
            tilePixelsPerUnit
        );

        Tile t = ScriptableObject.CreateInstance<Tile>();
        t.sprite = sprite;
        t.color = Color.white;
        t.colliderType = Tile.ColliderType.None;
        return t;
    }

    private TileBase ResolveGameplayTile(int x, int y)
    {
        TileBase baseGrass = grass != null ? grass : wall;
        if (baseGrass == null) baseGrass = path;

        if (IsMapBorder(x, y))
        {
            if (wall != null) return wall;
            return baseGrass;
        }

        if (IsRoadCell(x, y) || IsVillageCore(x, y) || IsRuinsZone(x, y))
        {
            if (path != null) return path;
            return baseGrass;
        }

        if (IsGraveyardZone(x, y))
        {
            if (wall != null) return wall;
            return baseGrass;
        }

        // Keep river visually distinct via spring overlay while preserving traversal.
        if (IsRiverCell(x, y))
        {
            return baseGrass;
        }

        if (flower != null)
        {
            float n = Mathf.PerlinNoise((x + seed) * 0.17f, (y - seed) * 0.17f);
            if (n > 0.86f) return flower;
        }

        return baseGrass;
    }

    private void PaintLargeLayout(Tilemap map, Tilemap overlay)
    {
        map.ClearAllTiles();
        if (overlay != null) overlay.ClearAllTiles();

        int minX = -width / 2;
        int maxX = minX + width - 1;
        int minY = -height / 2;
        int maxY = minY + height - 1;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                TileBase springVisual = overlay != null ? null : ResolveBaseGrassTile(x, y);

                if (IsNearRiverEdge(x, y))
                {
                    springVisual = springDarkB != null ? springDarkB : springVisual;
                }

                if (IsRiverCell(x, y))
                {
                    springVisual = springRiverA != null ? springRiverA : springVisual;
                }
                else if (IsRoadCell(x, y))
                {
                    springVisual = ResolveRoadTile(x, y);
                }
                else if (IsVillageCore(x, y))
                {
                    springVisual = ResolveVillageTile(x, y);
                }
                else if (IsRuinsZone(x, y))
                {
                    springVisual = ResolveRuinsTile(x, y);
                }
                else if (IsGraveyardZone(x, y))
                {
                    springVisual = ResolveGraveyardTile(x, y);
                }
                else if (IsMeadowZone(x, y))
                {
                    if (overlay == null)
                    {
                        springVisual = springGrassC != null ? springGrassC : springVisual;
                    }
                }

                if (IsMapBorder(x, y))
                {
                    if (overlay == null)
                    {
                        springVisual = springDarkA != null ? springDarkA : springVisual;
                    }
                }

                Vector3Int cell = new Vector3Int(x, y, 0);
                map.SetTile(cell, ResolveGameplayTile(x, y));

                if (overlay != null)
                {
                    overlay.SetTile(cell, springVisual);
                }
                else
                {
                    map.SetTile(cell, springVisual);
                }
            }
        }
    }

    private TileBase ResolveBaseGrassTile(int x, int y)
    {
        float n = Mathf.PerlinNoise((x + seed) * 0.08f, (y - seed) * 0.08f);
        if (n > 0.62f && springGrassC != null) return springGrassC;
        if (n > 0.38f && springGrassB != null) return springGrassB;
        if (springGrassA != null) return springGrassA;

        if (grass != null) return grass;
        return wall;
    }

    private TileBase ResolveRoadTile(int x, int y)
    {
        float n = Mathf.PerlinNoise((x + 0.41f * seed) * 0.21f, (y - 0.37f * seed) * 0.21f);
        if (n > 0.63f && springPathC != null) return springPathC;
        if (n > 0.35f && springPathB != null) return springPathB;
        if (springPathA != null) return springPathA;
        return path != null ? path : ResolveBaseGrassTile(x, y);
    }

    private TileBase ResolveVillageTile(int x, int y)
    {
        bool edge = Mathf.Abs(x + 8) >= 20 || Mathf.Abs(y - 4) >= 10;
        if (edge && springPathB != null) return springPathB;
        return ResolveRoadTile(x, y);
    }

    private TileBase ResolveRuinsTile(int x, int y)
    {
        bool edge = x <= 30 || x >= 52 || y <= 10 || y >= 28;
        if (edge && springDarkA != null) return springDarkA;
        return springPathB != null ? springPathB : ResolveRoadTile(x, y);
    }

    private TileBase ResolveGraveyardTile(int x, int y)
    {
        bool edge = x <= -54 || x >= -30 || y <= -36 || y >= -18;
        if (edge && springDarkA != null) return springDarkA;
        return springDarkB != null ? springDarkB : ResolveBaseGrassTile(x, y);
    }

    private bool IsMapBorder(int x, int y)
    {
        int halfW = width / 2;
        int halfH = height / 2;
        return x <= -halfW || x >= halfW - 1 || y <= -halfH || y >= halfH - 1;
    }

    private bool IsVillageCore(int x, int y)
    {
        return x >= -16 && x <= 8 && y >= 0 && y <= 14;
    }

    private bool IsRuinsZone(int x, int y)
    {
        return x >= 30 && x <= 52 && y >= 10 && y <= 28;
    }

    private bool IsGraveyardZone(int x, int y)
    {
        return x >= -54 && x <= -30 && y >= -36 && y <= -18;
    }

    private bool IsMeadowZone(int x, int y)
    {
        return x >= 22 && x <= 56 && y >= -30 && y <= -8;
    }

    private bool IsRoadCell(int x, int y)
    {
        bool mainEastWest = Mathf.Abs(y - 6) <= 2 && x >= -60 && x <= 60;
        bool lowerEastWest = Mathf.Abs(y + 12) <= 2 && x >= -56 && x <= 34;
        bool northSpine = Mathf.Abs(x - 12) <= 2 && y >= -12 && y <= 36;
        bool southSpine = Mathf.Abs(x + 22) <= 2 && y >= -42 && y <= 10;
        bool villageSpine = Mathf.Abs(x + 8) <= 2 && y >= -8 && y <= 18;
        return mainEastWest || lowerEastWest || northSpine || southSpine || villageSpine;
    }

    private float RiverCenterX(int y)
    {
        float t = (y + (height * 0.5f)) / Mathf.Max(1f, height);
        float curveA = Mathf.Sin((t * Mathf.PI * 2f) + seed * 0.17f) * 10f;
        float curveB = Mathf.Sin((t * Mathf.PI * 4f) + 1.2f) * 4f;
        return curveA + curveB + 6f;
    }

    private bool IsRiverCell(int x, int y)
    {
        float center = RiverCenterX(y);
        float wobble = Mathf.PerlinNoise((x + seed) * 0.09f, (y - seed) * 0.05f) * 1.8f;
        float halfWidth = 3.0f + wobble;
        return Mathf.Abs(x - center) <= halfWidth;
    }

    private bool IsNearRiverEdge(int x, int y)
    {
        float center = RiverCenterX(y);
        float d = Mathf.Abs(x - center);
        return d > 3.2f && d <= 4.8f;
    }

    private void RebuildPropsLayout()
    {
        if (propsRoot == null) return;

        List<GameObject> treeTemplates = new List<GameObject>();
        List<GameObject> bushTemplates = new List<GameObject>();
        List<GameObject> grassTemplates = new List<GameObject>();
        GameObject stoneTemplate = null;

        for (int i = 0; i < propsRoot.childCount; i++)
        {
            Transform c = propsRoot.GetChild(i);
            if (c == null) continue;
            string n = c.name.ToLowerInvariant();
            if (n.StartsWith("gen_"))
            {
                SafeDestroy(c.gameObject);
                i--;
                continue;
            }
            if (n.Contains("tree")) treeTemplates.Add(c.gameObject);
            else if (n.Contains("bush")) bushTemplates.Add(c.gameObject);
            else if (n.Contains("grass")) grassTemplates.Add(c.gameObject);
            else if (n.Contains("stone") && stoneTemplate == null) stoneTemplate = c.gameObject;
        }

        for (int i = 0; i < treeTemplates.Count; i++) treeTemplates[i].SetActive(false);
        for (int i = 0; i < bushTemplates.Count; i++) bushTemplates[i].SetActive(false);
        for (int i = 0; i < grassTemplates.Count; i++) grassTemplates[i].SetActive(false);
        if (stoneTemplate != null) stoneTemplate.SetActive(false);

        if (grassTemplates.Count == 0)
        {
            grassTemplates.AddRange(CollectGrassTemplates());
        }

        HashSet<Vector2Int> occupied = new HashSet<Vector2Int>();

        PlaceClonedTemplates(treeTemplates, "Gen_Tree_", treeCount, occupied, 6, 1600);
        PlaceClonedTemplates(bushTemplates, "Gen_Bush_", bushCount, occupied, 3, 2000);

        EnsureSources();
        if (rockSprites != null && rockSprites.Count > 0)
        {
            PlaceGeneratedRocks(stoneTemplate, occupied);
        }

        if (scatterGrassOnGreenTiles)
        {
            PlaceDenseGrassOnGreenTiles(grassTemplates, occupied);
        }
    }

    private void PlaceClonedTemplates(List<GameObject> templates, string prefix, int count, HashSet<Vector2Int> occupied, int minSpacing, int attempts)
    {
        if (templates == null || templates.Count == 0 || count <= 0) return;

        int placed = 0;
        int tries = 0;
        while (placed < count && tries < attempts)
        {
            tries++;
            if (!TryGetPropCell(minSpacing, occupied, out Vector2Int cell)) continue;

            GameObject src = templates[Random.Range(0, templates.Count)];
            if (src == null) continue;

            GameObject inst = Instantiate(src, propsRoot);
            inst.name = prefix + placed.ToString("D3");
            inst.SetActive(true);
            inst.transform.position = CellToWorld(cell);
            inst.transform.localScale = src.transform.localScale;

            occupied.Add(cell);
            placed++;
        }
    }

    private void PlaceGeneratedRocks(GameObject stoneTemplate, HashSet<Vector2Int> occupied)
    {
        int placed = 0;
        int tries = 0;
        int maxAttempts = Mathf.Max(rockCount * 40, 1600);

        while (placed < rockCount && tries < maxAttempts)
        {
            tries++;
            if (!TryGetPropCell(4, occupied, out Vector2Int cell)) continue;

            GameObject rock = stoneTemplate != null
                ? Instantiate(stoneTemplate, propsRoot)
                : new GameObject("Gen_Rock_" + placed.ToString("D3"));

            if (rock == null) continue;
            rock.name = "Gen_Rock_" + placed.ToString("D3");
            rock.SetActive(true);
            rock.transform.SetParent(propsRoot, true);
            rock.transform.position = CellToWorld(cell);

            SpriteRenderer sr = rock.GetComponent<SpriteRenderer>();
            if (sr == null) sr = rock.AddComponent<SpriteRenderer>();

            SpriteFromAtlas atlas = rock.GetComponent<SpriteFromAtlas>();
            if (atlas != null) atlas.enabled = false;

            Sprite sprite = rockSprites[Random.Range(0, rockSprites.Count)];
            sr.sprite = sprite;

            TopDownSorter sorter = rock.GetComponent<TopDownSorter>();
            if (sorter == null) sorter = rock.AddComponent<TopDownSorter>();
            sorter.sortMode = TopDownSorter.SortMode.RendererBottomY;

            FadeableSprite fade = rock.GetComponent<FadeableSprite>();
            if (fade == null) fade = rock.AddComponent<FadeableSprite>();

            BoxCollider2D box = rock.GetComponent<BoxCollider2D>();
            if (box == null) box = rock.AddComponent<BoxCollider2D>();
            box.isTrigger = false;
            if (sprite != null)
            {
                Bounds b = sprite.bounds;
                float w = Mathf.Max(0.18f, b.size.x * 0.45f);
                float h = Mathf.Max(0.12f, b.size.y * 0.28f);
                box.size = new Vector2(w, h);
                box.offset = new Vector2(b.center.x, b.min.y + h * 0.5f);
            }

            FootColliderMarker marker = rock.GetComponent<FootColliderMarker>();
            if (marker == null) marker = rock.AddComponent<FootColliderMarker>();
            marker.obstacleKind = "Rock";

            occupied.Add(cell);
            placed++;
        }
    }

    private bool TryGetPropCell(int minSpacing, HashSet<Vector2Int> occupied, out Vector2Int cell)
    {
        int minX = -width / 2 + 2;
        int maxX = width / 2 - 3;
        int minY = -height / 2 + 2;
        int maxY = height / 2 - 3;

        for (int attempt = 0; attempt < 12; attempt++)
        {
            int x = Random.Range(minX, maxX + 1);
            int y = Random.Range(minY, maxY + 1);
            cell = new Vector2Int(x, y);

            if (IsRoadCell(x, y)) continue;
            if (IsRiverCell(x, y)) continue;
            if (IsVillageCore(x, y) || IsRuinsZone(x, y) || IsGraveyardZone(x, y)) continue;
            if (Mathf.Abs(x) < 8 && Mathf.Abs(y) < 6) continue; // keep player start area open.

            bool tooClose = false;
            foreach (Vector2Int o in occupied)
            {
                if (Mathf.Abs(o.x - x) <= minSpacing && Mathf.Abs(o.y - y) <= minSpacing)
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            return true;
        }

        cell = Vector2Int.zero;
        return false;
    }

    private static Vector3 CellToWorld(Vector2Int cell)
    {
        return new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0f);
    }

    private List<GameObject> CollectGrassTemplates()
    {
        List<GameObject> templates = new List<GameObject>();

        if (propsRoot != null)
        {
            for (int i = 0; i < propsRoot.childCount; i++)
            {
                Transform c = propsRoot.GetChild(i);
                if (c == null) continue;
                string n = c.name.ToLowerInvariant();
                if (n.StartsWith("gen_")) continue;
                if (!n.Contains("grass")) continue;
                templates.Add(c.gameObject);
            }
        }

        if (templates.Count == 0 && grassPrefabLibrary.Count > 0)
        {
            templates.AddRange(grassPrefabLibrary);
        }

        return templates;
    }

    private void PlaceDenseGrassOnGreenTiles(List<GameObject> grassTemplates, HashSet<Vector2Int> occupied)
    {
        if (propsRoot == null) return;
        if (grassTemplates == null || grassTemplates.Count == 0) return;

        Tilemap map = GetComponent<Tilemap>();
        if (map == null) return;

        float density = Mathf.Clamp01(grassDensityOnGreenTiles);
        if (density <= 0f) return;

        greenSpriteCache.Clear();
        BoundsInt bounds = map.cellBounds;
        int placed = 0;

        for (int y = bounds.yMin; y < bounds.yMax; y++)
        {
            for (int x = bounds.xMin; x < bounds.xMax; x++)
            {
                Vector3Int cell = new Vector3Int(x, y, 0);
                TileBase tile = map.GetTile(cell);
                if (tile == null) continue;
                if (!IsFullGreenTile(map, cell, tile)) continue;
                if (Random.value > density) continue;

                Vector2Int key = new Vector2Int(x, y);
                if (occupied.Contains(key)) continue;

                GameObject source = grassTemplates[Random.Range(0, grassTemplates.Count)];
                if (source == null) continue;

                GameObject inst = Instantiate(source, propsRoot);
                inst.name = "Gen_Grass_" + placed.ToString("D4");
                inst.SetActive(true);

                Vector3 world = CellToWorld(key);
                world.x += Random.Range(-grassPositionJitter, grassPositionJitter);
                world.y += Random.Range(-grassPositionJitter, grassPositionJitter);
                inst.transform.position = world;
                inst.transform.localScale = source.transform.localScale * Random.Range(0.92f, 1.08f);

                occupied.Add(key);
                placed++;
            }
        }
    }

    private bool IsFullGreenTile(Tilemap map, Vector3Int cell, TileBase tile)
    {
        if (map == null || tile == null) return false;

        string tileName = (tile.name ?? string.Empty).ToLowerInvariant();
        if (tileName.Contains("path") ||
            tileName.Contains("road") ||
            tileName.Contains("river") ||
            tileName.Contains("water") ||
            tileName.Contains("stone") ||
            tileName.Contains("wall") ||
            tileName.Contains("roof") ||
            tileName.Contains("house"))
        {
            return false;
        }

        Sprite sprite = map.GetSprite(cell);
        if (sprite == null) return false;

        int sid = sprite.GetInstanceID();
        if (greenSpriteCache.TryGetValue(sid, out bool cached))
        {
            return cached;
        }

        bool isGreen = TrySampleSpriteCenterColor(sprite, out Color c) &&
                       c.g > 0.35f &&
                       c.g > c.r * 1.15f &&
                       c.g > c.b * 1.15f;

        greenSpriteCache[sid] = isGreen;
        return isGreen;
    }

    private static bool TrySampleSpriteCenterColor(Sprite sprite, out Color color)
    {
        color = Color.clear;
        if (sprite == null || sprite.texture == null) return false;

        Rect r = sprite.textureRect;
        Texture2D tex = sprite.texture;
        int cx = Mathf.Clamp(Mathf.RoundToInt(r.x + (r.width * 0.5f)), 0, tex.width - 1);
        int cy = Mathf.Clamp(Mathf.RoundToInt(r.y + (r.height * 0.5f)), 0, tex.height - 1);

        try
        {
            color = tex.GetPixel(cx, cy);
            return true;
        }
        catch
        {
            // Texture is likely not marked Read/Write. Sample through a temporary RenderTexture instead.
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Texture2D probe = null;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                probe = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                probe.ReadPixels(new Rect(cx, cy, 1, 1), 0, 0, false);
                probe.Apply(false, false);
                color = probe.GetPixel(0, 0);
                return true;
            }
            finally
            {
                if (probe != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(probe);
                    else Destroy(probe);
#else
                    Destroy(probe);
#endif
                }
                RenderTexture.active = prev;
                RenderTexture.ReleaseTemporary(rt);
            }
        }
    }

#if UNITY_EDITOR
    private void LoadGrassPrefabsEditor()
    {
        grassPrefabLibrary.Clear();

        for (int i = 0; i < GrassPrefabPaths.Length; i++)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(GrassPrefabPaths[i]);
            if (p != null) grassPrefabLibrary.Add(p);
        }
    }

    private void LoadRequestedRockSpritesEditor()
    {
        rockSprites.Clear();
        if (txPropsTexture == null) return;

        Object[] all = AssetDatabase.LoadAllAssetsAtPath(TxPropsPath);
        for (int i = 0; i < RequestedRockNames.Length; i++)
        {
            string target = RequestedRockNames[i];
            for (int j = 0; j < all.Length; j++)
            {
                Sprite s = all[j] as Sprite;
                if (s == null) continue;
                if (!string.Equals(s.name, target, System.StringComparison.OrdinalIgnoreCase)) continue;
                rockSprites.Add(s);
                break;
            }
        }
    }
#endif

    private static void SafeDestroy(GameObject go)
    {
        if (go == null) return;
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DestroyImmediate(go);
            return;
        }
#endif
        Destroy(go);
    }
}
