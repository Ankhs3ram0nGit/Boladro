using UnityEngine;

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
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup < nextScanAt) return;
        nextScanAt = Time.realtimeSinceStartup + 1.0f;
        ApplyFootColliders();
    }

    private static void ApplyFootColliders()
    {
        bool hasTemplate = TryGetTreeTemplateCollider(out Vector2 treeTemplateSize, out Vector2 treeTemplateOffset);

        SpriteRenderer[] renderers = FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || sr.sprite == null) continue;
            bool isObstacle = TryClassifyObstacle(sr, out string obstacleKind, out float widthRatio, out float heightRatio);

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

        string n = (sr.gameObject.name + " " + sr.transform.root.name).ToLowerInvariant();

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
}
