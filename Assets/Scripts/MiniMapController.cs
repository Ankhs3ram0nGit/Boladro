using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class MiniMapController : MonoBehaviour
{
    [Header("Panel")]
    public Vector2 panelSize = new Vector2(190f, 190f);
    public Vector2 panelOffset = new Vector2(-18f, -18f);
    public float panelPadding = 8f;
    public Color panelColor = new Color(0f, 0f, 0f, 0.45f);

    [Header("Map Camera")]
    public float minimapOrthographicSize = 14f;
    public float minimapCameraZ = -10f;
    [Min(64)] public int renderTextureSize = 256;
    public Color minimapClearColor = new Color(0f, 0f, 0f, 0f);

    [Header("Player Marker")]
    public float markerOuterSize = 16f;
    public float markerInnerSize = 10f;
    public Color markerOuterColor = Color.black;
    public Color markerInnerColor = Color.white;

    private const string RootName = "__MiniMapUI";
    private const string CameraName = "__MiniMapCamera";

    private Camera minimapCamera;
    private RenderTexture minimapTexture;
    private RectTransform rootRect;
    private RawImage mapImage;
    private bool initialized;
    private bool createdCanvasAtRuntime;
    private Canvas canvas;
    private Sprite circleSprite;

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

        if (minimapCamera == null) return;
        Vector3 p = transform.position;
        minimapCamera.transform.position = new Vector3(p.x, p.y, minimapCameraZ);
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
            panel.color = panelColor;
        }
        else
        {
            rootRect = existing.GetComponent<RectTransform>();
            if (rootRect == null) rootRect = existing.gameObject.AddComponent<RectTransform>();
            Image panel = existing.GetComponent<Image>();
            if (panel == null) panel = existing.gameObject.AddComponent<Image>();
            panel.color = panelColor;
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
        mapRect.offsetMin = new Vector2(panelPadding, panelPadding);
        mapRect.offsetMax = new Vector2(-panelPadding, -panelPadding);
        mapImage.texture = minimapTexture;
        mapImage.color = Color.white;
        mapImage.raycastTarget = false;

        EnsurePlayerMarker(rootRect);
    }

    void EnsurePlayerMarker(RectTransform parent)
    {
        if (parent == null) return;
        Sprite markerSprite = GetCircleSprite();

        RectTransform outer = EnsureMarkerLayer(parent, "PlayerMarkerOuter", markerSprite, markerOuterColor, markerOuterSize);
        EnsureMarkerLayer(outer, "PlayerMarkerInner", markerSprite, markerInnerColor, markerInnerSize);
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
}
