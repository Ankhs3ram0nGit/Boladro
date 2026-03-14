using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class MiniMapController : MonoBehaviour
{
    [Header("Panel")]
    public Vector2 panelSize = new Vector2(190f, 190f);
    public Vector2 panelOffset = new Vector2(-18f, -18f);
    public float panelPadding = 8f;
    public Color panelColor = new Color(0f, 0f, 0f, 1f);

    [Header("Map Camera")]
    public float minimapOrthographicSize = 14f;
    public float minimapCameraZ = -10f;
    [Min(64)] public int renderTextureSize = 256;
    public Color minimapClearColor = new Color(0f, 0f, 0f, 0f);

    [Header("Performance")]
    [Min(1f)] public float minimapRenderFps = 10f;
    [Min(0f)] public float minimapRenderMoveThreshold = 0.04f;

    [Header("Player Marker")]
    public float markerOuterSize = 16f;
    public float markerInnerSize = 10f;
    public Color markerOuterColor = Color.black;
    public Color markerInnerColor = Color.white;

    [Header("Active Creature Marker")]
    [Range(0.2f, 1f)] public float activeCreatureDotScaleVsPlayer = 0.5f;
    public Color activeCreatureDotColor = Color.red;

    [Header("Creature Blips")]
    public float creatureBlipSize = 18f;
    [Min(0.1f)] public float creatureScanInterval = 0.45f;
    public Color creatureBlipTint = Color.white;
    public bool hideDuringEngagedBattle = true;

    private const string RootName = "__MiniMapUI";
    private const string CameraName = "__MiniMapCamera";

    private Camera minimapCamera;
    private RenderTexture minimapTexture;
    private RectTransform rootRect;
    private RectTransform mapRectTransform;
    private RectTransform creatureBlipRoot;
    private RectTransform activeCreatureMarker;
    private RawImage mapImage;
    private bool initialized;
    private bool createdCanvasAtRuntime;
    private Canvas canvas;
    private Sprite circleSprite;
    private float nextCreatureScanAt;
    private float nextMinimapRenderAt;
    private Vector2 lastRenderedPlayerPos;
    private bool hasRenderedMinimapFrame;
    private bool minimapVisible = true;

    private class CreatureBlip
    {
        public int key;
        public Transform target;
        public SpriteRenderer sourceRenderer;
        public Image image;
    }

    private readonly Dictionary<int, CreatureBlip> creatureBlips = new Dictionary<int, CreatureBlip>();
    private readonly List<int> staleCreatureKeys = new List<int>();
    private readonly List<WorldSpawnMarker> markerBuffer = new List<WorldSpawnMarker>(64);

    void OnEnable()
    {
        EnsureInitialized();
    }

    void LateUpdate()
    {
        if (!initialized)
        {
            EnsureInitialized();
        }

        if (hideDuringEngagedBattle && BattleSystem.IsEngagedBattleActive)
        {
            SetMiniMapVisible(false);
            return;
        }
        SetMiniMapVisible(true);

        if (minimapCamera == null) return;
        RenderMinimapIfNeeded(false);
        UpdateCreatureBlips();
        UpdateActiveCreatureMarker();
    }

    void OnDestroy()
    {
        if (minimapCamera != null)
        {
            if (Application.isPlaying) Destroy(minimapCamera.gameObject);
            else DestroyImmediate(minimapCamera.gameObject);
        }

        if (rootRect != null)
        {
            if (Application.isPlaying) Destroy(rootRect.gameObject);
            else DestroyImmediate(rootRect.gameObject);
        }

        if (minimapTexture != null)
        {
            if (Application.isPlaying) Destroy(minimapTexture);
            else DestroyImmediate(minimapTexture);
        }

        if (circleSprite != null && createdCanvasAtRuntime)
        {
            if (Application.isPlaying) Destroy(circleSprite);
            else DestroyImmediate(circleSprite);
        }
    }

    void EnsureInitialized()
    {
        EnsureCanvas();
        if (canvas == null) return;

        EnsureMinimapCamera();
        EnsureMinimapUi();

        initialized = minimapCamera != null && mapImage != null;
    }

    void EnsureCanvas()
    {
        if (canvas != null) return;
        canvas = FindFirstObjectByType<Canvas>();
        if (canvas != null) return;

        GameObject go = new GameObject("HUDCanvas");
        canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        go.AddComponent<CanvasScaler>();
        go.AddComponent<GraphicRaycaster>();
        createdCanvasAtRuntime = true;
    }

    void EnsureMinimapCamera()
    {
        if (minimapCamera == null)
        {
            GameObject camGo = new GameObject(CameraName);
            minimapCamera = camGo.AddComponent<Camera>();
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = Mathf.Max(1f, minimapOrthographicSize);
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = minimapClearColor;
            minimapCamera.cullingMask = ~LayerMask.GetMask("UI");
            minimapCamera.depth = -50f;
            minimapCamera.allowHDR = false;
            minimapCamera.allowMSAA = false;
            minimapCamera.enabled = false; // Manual render at throttled cadence.
        }

        if (minimapTexture == null ||
            minimapTexture.width != renderTextureSize ||
            minimapTexture.height != renderTextureSize)
        {
            if (minimapTexture != null)
            {
                if (Application.isPlaying) Destroy(minimapTexture);
                else DestroyImmediate(minimapTexture);
            }

            minimapTexture = new RenderTexture(renderTextureSize, renderTextureSize, 16, RenderTextureFormat.ARGB32);
            minimapTexture.name = "MiniMapRT";
            minimapTexture.filterMode = FilterMode.Point;
            minimapTexture.wrapMode = TextureWrapMode.Clamp;
            minimapTexture.Create();
        }

        minimapCamera.targetTexture = minimapTexture;
        RequestMinimapRender();
    }

    void EnsureMinimapUi()
    {
        if (canvas == null) return;

        Transform existing = canvas.transform.Find(RootName);
        if (existing == null)
        {
            GameObject root = new GameObject(RootName);
            rootRect = root.AddComponent<RectTransform>();
            root.transform.SetParent(canvas.transform, false);
            Image panel = root.AddComponent<Image>();
            panel.color = new Color(panelColor.r, panelColor.g, panelColor.b, 1f);
        }
        else
        {
            rootRect = existing.GetComponent<RectTransform>();
            if (rootRect == null) rootRect = existing.gameObject.AddComponent<RectTransform>();
            Image panel = existing.GetComponent<Image>();
            if (panel == null) panel = existing.gameObject.AddComponent<Image>();
            panel.color = new Color(panelColor.r, panelColor.g, panelColor.b, 1f);
        }

        rootRect.anchorMin = new Vector2(1f, 1f);
        rootRect.anchorMax = new Vector2(1f, 1f);
        rootRect.pivot = new Vector2(1f, 1f);
        rootRect.anchoredPosition = panelOffset;
        rootRect.sizeDelta = panelSize;

        Transform map = rootRect.Find("Map");
        RectTransform mapRect;
        if (map == null)
        {
            GameObject mapGo = new GameObject("Map");
            mapRect = mapGo.AddComponent<RectTransform>();
            mapGo.transform.SetParent(rootRect, false);
            mapImage = mapGo.AddComponent<RawImage>();
        }
        else
        {
            mapRect = map.GetComponent<RectTransform>();
            if (mapRect == null) mapRect = map.gameObject.AddComponent<RectTransform>();
            mapImage = map.GetComponent<RawImage>();
            if (mapImage == null) mapImage = map.gameObject.AddComponent<RawImage>();
        }

        mapRect.anchorMin = Vector2.zero;
        mapRect.anchorMax = Vector2.one;
        float effectivePadding = Mathf.Max(1f, panelPadding * 0.5f);
        mapRect.offsetMin = new Vector2(effectivePadding, effectivePadding);
        mapRect.offsetMax = new Vector2(-effectivePadding, -effectivePadding);
        mapImage.texture = minimapTexture;
        mapImage.color = Color.white;
        mapImage.raycastTarget = false;
        mapRectTransform = mapRect;
        EnsureCreatureBlipLayer(mapRectTransform);

        EnsurePlayerMarker(rootRect);
        EnsureActiveCreatureMarker(mapRectTransform);
        RequestMinimapRender();
    }

    void EnsureCreatureBlipLayer(RectTransform mapRect)
    {
        if (mapRect == null) return;
        Transform t = mapRect.Find("CreatureBlips");
        if (t == null)
        {
            GameObject go = new GameObject("CreatureBlips");
            creatureBlipRoot = go.AddComponent<RectTransform>();
            go.transform.SetParent(mapRect, false);
        }
        else
        {
            creatureBlipRoot = t.GetComponent<RectTransform>();
            if (creatureBlipRoot == null) creatureBlipRoot = t.gameObject.AddComponent<RectTransform>();
        }

        creatureBlipRoot.anchorMin = Vector2.zero;
        creatureBlipRoot.anchorMax = Vector2.one;
        creatureBlipRoot.pivot = new Vector2(0.5f, 0.5f);
        creatureBlipRoot.offsetMin = Vector2.zero;
        creatureBlipRoot.offsetMax = Vector2.zero;
    }

    void UpdateCreatureBlips()
    {
        if (creatureBlipRoot == null || mapRectTransform == null) return;
        Transform activeCreature = GetActiveCreatureTransform();

        if (Time.time >= nextCreatureScanAt)
        {
            nextCreatureScanAt = Time.time + Mathf.Max(0.1f, creatureScanInterval);
            RefreshCreatureBlipTargets();
        }

        float halfHeight = Mathf.Max(0.01f, minimapOrthographicSize);
        Rect rect = mapRectTransform.rect;
        float mapAspect = Mathf.Max(0.01f, rect.width / Mathf.Max(1f, rect.height));
        float halfWidth = halfHeight * mapAspect;
        float halfUiW = rect.width * 0.5f;
        float halfUiH = rect.height * 0.5f;
        Vector2 playerPos = transform.position;

        foreach (KeyValuePair<int, CreatureBlip> pair in creatureBlips)
        {
            CreatureBlip blip = pair.Value;
            if (blip == null || blip.target == null || blip.image == null)
            {
                continue;
            }

            if (activeCreature != null && blip.target == activeCreature)
            {
                blip.image.enabled = false;
                continue;
            }

            Vector2 delta = (Vector2)blip.target.position - playerPos;
            bool visible = Mathf.Abs(delta.x) <= halfWidth && Mathf.Abs(delta.y) <= halfHeight;
            blip.image.enabled = visible;
            if (!visible) continue;

            float nx = delta.x / halfWidth;
            float ny = delta.y / halfHeight;
            blip.image.rectTransform.anchoredPosition = new Vector2(nx * halfUiW, ny * halfUiH);
            blip.image.rectTransform.sizeDelta = new Vector2(creatureBlipSize, creatureBlipSize);

            Sprite source = blip.sourceRenderer != null ? blip.sourceRenderer.sprite : null;
            if (source != null) blip.image.sprite = source;
            blip.image.color = creatureBlipTint;
        }
    }

    void UpdateActiveCreatureMarker()
    {
        if (activeCreatureMarker == null || mapRectTransform == null) return;

        Transform active = GetActiveCreatureTransform();
        if (active == null)
        {
            activeCreatureMarker.gameObject.SetActive(false);
            return;
        }

        float halfHeight = Mathf.Max(0.01f, minimapOrthographicSize);
        Rect rect = mapRectTransform.rect;
        float mapAspect = Mathf.Max(0.01f, rect.width / Mathf.Max(1f, rect.height));
        float halfWidth = halfHeight * mapAspect;
        float halfUiW = rect.width * 0.5f;
        float halfUiH = rect.height * 0.5f;
        Vector2 playerPos = transform.position;
        Vector2 delta = (Vector2)active.position - playerPos;

        bool visible = Mathf.Abs(delta.x) <= halfWidth && Mathf.Abs(delta.y) <= halfHeight;
        activeCreatureMarker.gameObject.SetActive(visible);
        if (!visible) return;

        float nx = delta.x / halfWidth;
        float ny = delta.y / halfHeight;
        activeCreatureMarker.anchoredPosition = new Vector2(nx * halfUiW, ny * halfUiH);

        float size = Mathf.Max(3f, markerOuterSize * Mathf.Clamp(activeCreatureDotScaleVsPlayer, 0.2f, 1f));
        activeCreatureMarker.sizeDelta = new Vector2(size, size);
    }

    void RefreshCreatureBlipTargets()
    {
        staleCreatureKeys.Clear();
        foreach (KeyValuePair<int, CreatureBlip> pair in creatureBlips)
        {
            staleCreatureKeys.Add(pair.Key);
        }

        markerBuffer.Clear();
        foreach (WorldSpawnMarker marker in WorldSpawnMarker.ActiveMarkers)
        {
            if (marker == null) continue;
            markerBuffer.Add(marker);
        }

        for (int i = 0; i < markerBuffer.Count; i++)
        {
            WorldSpawnMarker marker = markerBuffer[i];
            if (marker == null || marker.transform == null) continue;
            if (!marker.gameObject.activeInHierarchy) continue;

            int key = marker.GetInstanceID();
            staleCreatureKeys.Remove(key);

            if (!creatureBlips.TryGetValue(key, out CreatureBlip blip) || blip == null)
            {
                blip = CreateCreatureBlip(key);
                creatureBlips[key] = blip;
            }

            blip.target = marker.transform;
            if (blip.sourceRenderer == null)
            {
                blip.sourceRenderer = marker.GetComponent<SpriteRenderer>();
                if (blip.sourceRenderer == null)
                {
                    blip.sourceRenderer = marker.GetComponentInChildren<SpriteRenderer>(true);
                }
            }
            if (blip.sourceRenderer != null && blip.sourceRenderer.sprite != null)
            {
                blip.image.sprite = blip.sourceRenderer.sprite;
            }
        }

        for (int i = 0; i < staleCreatureKeys.Count; i++)
        {
            int key = staleCreatureKeys[i];
            if (!creatureBlips.TryGetValue(key, out CreatureBlip blip) || blip == null) continue;
            if (blip.image != null)
            {
                if (Application.isPlaying) Destroy(blip.image.gameObject);
                else DestroyImmediate(blip.image.gameObject);
            }
            creatureBlips.Remove(key);
        }
        staleCreatureKeys.Clear();
    }

    void RenderMinimapIfNeeded(bool force)
    {
        if (minimapCamera == null || minimapTexture == null) return;
        if (!minimapVisible) return;

        Vector2 playerPos = transform.position;
        float threshold = Mathf.Max(0f, minimapRenderMoveThreshold);
        float thresholdSqr = threshold * threshold;
        bool moved = !hasRenderedMinimapFrame || ((playerPos - lastRenderedPlayerPos).sqrMagnitude >= thresholdSqr);

        float fps = Mathf.Max(1f, minimapRenderFps);
        bool timeReady = Time.unscaledTime >= nextMinimapRenderAt;

        if (!force && !moved && !timeReady) return;

        minimapCamera.orthographicSize = Mathf.Max(1f, minimapOrthographicSize);
        minimapCamera.transform.position = new Vector3(playerPos.x, playerPos.y, minimapCameraZ);
        minimapCamera.Render();

        hasRenderedMinimapFrame = true;
        lastRenderedPlayerPos = playerPos;
        nextMinimapRenderAt = Time.unscaledTime + (1f / fps);
    }

    void RequestMinimapRender()
    {
        hasRenderedMinimapFrame = false;
        nextMinimapRenderAt = 0f;
    }

    CreatureBlip CreateCreatureBlip(int key)
    {
        GameObject go = new GameObject("CreatureBlip_" + key);
        RectTransform rt = go.AddComponent<RectTransform>();
        go.transform.SetParent(creatureBlipRoot, false);
        Image img = go.AddComponent<Image>();
        img.raycastTarget = false;
        img.preserveAspect = true;
        img.sprite = GetCircleSprite();
        img.color = creatureBlipTint;

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(creatureBlipSize, creatureBlipSize);

        return new CreatureBlip
        {
            key = key,
            image = img
        };
    }

    void EnsurePlayerMarker(RectTransform parent)
    {
        if (parent == null) return;
        Sprite markerSprite = GetCircleSprite();

        RectTransform outer = EnsureMarkerLayer(parent, "PlayerMarkerOuter", markerSprite, markerOuterColor, markerOuterSize);
        EnsureMarkerLayer(outer, "PlayerMarkerInner", markerSprite, markerInnerColor, markerInnerSize);
    }

    void EnsureActiveCreatureMarker(RectTransform parent)
    {
        if (parent == null) return;
        activeCreatureMarker = EnsureMarkerLayer(parent, "ActiveCreatureMarker", GetCircleSprite(), activeCreatureDotColor, markerOuterSize * 0.5f);
        if (activeCreatureMarker != null)
        {
            activeCreatureMarker.SetAsLastSibling();
            Image img = activeCreatureMarker.GetComponent<Image>();
            if (img != null)
            {
                img.color = activeCreatureDotColor;
            }
        }
    }

    Transform GetActiveCreatureTransform()
    {
        if (ActivePartyFollowerController.Instance == null) return null;
        return ActivePartyFollowerController.Instance.CurrentFollowerTransform;
    }

    RectTransform EnsureMarkerLayer(Transform parent, string name, Sprite sprite, Color color, float size)
    {
        Transform t = parent.Find(name);
        RectTransform rect;
        Image img;
        if (t == null)
        {
            GameObject go = new GameObject(name);
            rect = go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            img = go.AddComponent<Image>();
        }
        else
        {
            rect = t.GetComponent<RectTransform>();
            if (rect == null) rect = t.gameObject.AddComponent<RectTransform>();
            img = t.GetComponent<Image>();
            if (img == null) img = t.gameObject.AddComponent<Image>();
        }

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(size, size);

        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return rect;
    }

    Sprite GetCircleSprite()
    {
        if (circleSprite != null) return circleSprite;

        const int size = 32;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = size * 0.5f - 1f;
        float radiusSq = radius * radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x - center.x;
                float dy = y - center.y;
                bool inside = (dx * dx + dy * dy) <= radiusSq;
                tex.SetPixel(x, y, inside ? Color.white : new Color(0f, 0f, 0f, 0f));
            }
        }
        tex.Apply(false, false);
        circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        return circleSprite;
    }

    void SetMiniMapVisible(bool visible)
    {
        bool wasVisible = minimapVisible;
        minimapVisible = visible;
        if (minimapCamera != null) minimapCamera.enabled = false;
        if (rootRect != null && rootRect.gameObject.activeSelf != visible)
        {
            rootRect.gameObject.SetActive(visible);
        }

        if (visible && !wasVisible)
        {
            RequestMinimapRender();
            RenderMinimapIfNeeded(true);
        }
    }
}
