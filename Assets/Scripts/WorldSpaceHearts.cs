using UnityEngine;

public class WorldSpaceHearts : MonoBehaviour
{
    public Camera targetCamera;
    public PlayerHealth player;
    public int heartSizePixels = 32;
    public int spacingPixels = 1;
    public Vector2 screenOffsetPixels = new Vector2(16, -16);
    public int sortingOrder = 200;

    private Sprite fullHeart;
    private Sprite halfHeart;
    private Sprite emptyHeart;
    private SpriteRenderer[] renderers;

    void Awake()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (player == null) player = FindFirstObjectByType<PlayerHealth>();

        fullHeart = Resources.Load<Sprite>("UI/HeartFull");
        halfHeart = Resources.Load<Sprite>("UI/HeartHalf");
        emptyHeart = Resources.Load<Sprite>("UI/HeartEmpty");

        if (player != null)
        {
            BuildHearts(Mathf.CeilToInt(player.maxHealth * 0.5f));
            player.OnHealthChanged += OnHealthChanged;
            OnHealthChanged(player.currentHealth, player.maxHealth);
        }
    }

    void OnDestroy()
    {
        if (player != null)
        {
            player.OnHealthChanged -= OnHealthChanged;
        }
    }

    void LateUpdate()
    {
        UpdateLayout();
    }

    void BuildHearts(int count)
    {
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) Destroy(renderers[i].gameObject);
            }
        }

        renderers = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject("WSHeart" + (i + 1));
            go.transform.SetParent(transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerName = "Default";
            sr.sortingOrder = sortingOrder;
            renderers[i] = sr;
        }
    }

    void OnHealthChanged(int current, int max)
    {
        int needed = Mathf.CeilToInt(max * 0.5f);
        if (renderers == null || renderers.Length != needed)
        {
            BuildHearts(needed);
        }
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            int hpForHeart = current - (i * 2);
            if (hpForHeart >= 2) renderers[i].sprite = fullHeart;
            else if (hpForHeart == 1) renderers[i].sprite = halfHeart != null ? halfHeart : fullHeart;
            else renderers[i].sprite = emptyHeart;
        }
    }

    void UpdateLayout()
    {
        if (targetCamera == null || renderers == null) return;

        Vector3 bottomLeft = targetCamera.ViewportToWorldPoint(new Vector3(0f, 0f, targetCamera.nearClipPlane));
        Vector3 topRight = targetCamera.ViewportToWorldPoint(new Vector3(1f, 1f, targetCamera.nearClipPlane));
        Vector3 topLeft = new Vector3(bottomLeft.x, topRight.y, 0f);

        float unitsPerPixelX = (topRight.x - bottomLeft.x) / Screen.width;
        float unitsPerPixelY = (topRight.y - bottomLeft.y) / Screen.height;
        float unitsPerPixel = Mathf.Min(unitsPerPixelX, unitsPerPixelY);

        Vector3 offsetWorld = new Vector3(screenOffsetPixels.x * unitsPerPixelX, screenOffsetPixels.y * unitsPerPixelY, 0f);
        float step = (heartSizePixels + spacingPixels) * unitsPerPixelX;
        float scale = heartSizePixels * unitsPerPixel;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Vector3 pos = topLeft + offsetWorld + new Vector3(step * i, -step * 0.5f, 0f);
            pos.z = 0f;
            renderers[i].transform.position = pos;
            renderers[i].transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
