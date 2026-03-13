using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class HealthUI : MonoBehaviour
{
    public RectTransform heartsRoot;
    public PlayerHealth player;
    public int heartSize = 32;
    public int spacing = 1;
    public Color32 fullColor = new Color32(220, 50, 60, 255);
    public Color32 emptyOutlineColor = new Color32(255, 255, 255, 255);
    public Sprite fullHeartSprite;
    public Sprite halfHeartSprite;
    public Sprite emptyHeartSprite;
    public Texture2D heartSpriteSheet32;

    private Image[] hearts;
    private Sprite fullHeart;
    private Sprite halfHeart;
    private Sprite emptyHeart;

    void Awake()
    {
#if UNITY_EDITOR
        if (heartSpriteSheet32 == null)
        {
            heartSpriteSheet32 = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Heart Icon/Pixel Heart Sprite Sheet 32x32.png");
        }
#endif
        if (heartSpriteSheet32 == null)
        {
            heartSpriteSheet32 = Resources.Load<Texture2D>("UI/PixelHeartSpriteSheet32x32");
        }

        if (heartSpriteSheet32 != null)
        {
            BuildHeartsFromSheet();
        }

        if (fullHeartSprite == null)
        {
            fullHeartSprite = Resources.Load<Sprite>("UI/HeartFull");
        }
        if (halfHeartSprite == null)
        {
            halfHeartSprite = Resources.Load<Sprite>("UI/HeartHalf");
        }
        if (emptyHeartSprite == null)
        {
            emptyHeartSprite = Resources.Load<Sprite>("UI/HeartEmpty");
        }

        if (fullHeart == null) fullHeart = fullHeartSprite != null ? fullHeartSprite : CreateHeartSprite(fullColor);
        if (halfHeart == null) halfHeart = halfHeartSprite != null ? halfHeartSprite : CreateHeartSprite(fullColor);
        if (emptyHeart == null) emptyHeart = emptyHeartSprite != null ? emptyHeartSprite : CreateHeartSprite(emptyOutlineColor, true);
    }

    void Start()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerHealth>();
        }
        if (heartsRoot == null || player == null) return;

        if (!heartsRoot.gameObject.activeSelf)
        {
            heartsRoot.gameObject.SetActive(true);
        }
        BuildHearts(player.maxHealth);
        player.OnHealthChanged += OnHealthChanged;
        OnHealthChanged(player.currentHealth, player.maxHealth);
    }

    void OnEnable()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerHealth>();
        }
        if (player != null && heartsRoot != null)
        {
            int needed = Mathf.CeilToInt(player.maxHealth * 0.5f);
            if (hearts == null || hearts.Length != needed)
            {
                BuildHearts(player.maxHealth);
            }
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

    void BuildHearts(int count)
    {
        int heartCount = Mathf.Max(1, Mathf.CeilToInt(count * 0.5f));

        for (int i = heartsRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = heartsRoot.GetChild(i);
            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        hearts = new Image[heartCount];
        for (int i = 0; i < heartCount; i++)
        {
            GameObject go = new GameObject("Heart" + (i + 1));
            go.transform.SetParent(heartsRoot, false);
            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(heartSize, heartSize);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.preferredWidth = heartSize;
            le.preferredHeight = heartSize;
            le.flexibleWidth = 0f;
            le.flexibleHeight = 0f;
            hearts[i] = img;
            hearts[i].sprite = fullHeart;
            hearts[i].color = Color.white;
        }

        HorizontalLayoutGroup layout = heartsRoot.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(0, 0, 0, 0);
        }
    }

    void OnHealthChanged(int current, int max)
    {
        if (heartsRoot == null) return;
        int needed = Mathf.CeilToInt(max * 0.5f);
        if (hearts == null || hearts.Length != needed)
        {
            BuildHearts(max);
        }
        if (hearts == null) return;

        for (int i = 0; i < hearts.Length; i++)
        {
            int hpForHeart = current - (i * 2);
            if (hpForHeart >= 2)
            {
                hearts[i].sprite = fullHeart;
            }
            else if (hpForHeart == 1)
            {
                hearts[i].sprite = halfHeart != null ? halfHeart : fullHeart;
            }
            else
            {
                hearts[i].sprite = emptyHeart;
            }
            hearts[i].color = Color.white;
        }
    }

    public void ForceRebuild()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerHealth>();
        }
        if (player == null || heartsRoot == null) return;

        BuildHearts(player.maxHealth);
        OnHealthChanged(player.currentHealth, player.maxHealth);
    }

    Sprite CreateHeartSprite(Color32 color, bool outlineOnly = false)
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        bool[,] mask = new bool[size, size]
        {
            {false,false,false,false,true ,true ,false,false,false,false,true ,true ,false,false,false,false},
            {false,false,false,true ,true ,true ,true ,false,false,true ,true ,true ,true ,false,false,false},
            {false,false,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,false,false},
            {false,false,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,false,false},
            {false,false,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,false,false},
            {false,false,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,false,false},
            {false,false,false,true ,true ,true ,true ,true ,true ,true ,true ,true ,true ,false,false,false},
            {false,false,false,false,true ,true ,true ,true ,true ,true ,true ,true ,false,false,false,false},
            {false,false,false,false,false,true ,true ,true ,true ,true ,true ,false,false,false,false,false},
            {false,false,false,false,false,false,true ,true ,true ,true ,false,false,false,false,false,false},
            {false,false,false,false,false,false,false,true ,true ,false,false,false,false,false,false,false},
            {false,false,false,false,false,false,false,false,true ,false,false,false,false,false,false,false},
            {false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false},
            {false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false},
            {false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false},
            {false,false,false,false,false,false,false,false,false,false,false,false,false,false,false,false}
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool filled = mask[y, x];
                if (!filled)
                {
                    tex.SetPixel(x, size - 1 - y, new Color32(0, 0, 0, 0));
                    continue;
                }

                if (outlineOnly)
                {
                    bool edge = IsEdge(mask, x, y, size);
                    tex.SetPixel(x, size - 1 - y, edge ? color : new Color32(0, 0, 0, 0));
                }
                else
                {
                    tex.SetPixel(x, size - 1 - y, color);
                }
            }
        }

        tex.Apply();
        Rect rect = new Rect(0, 0, size, size);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        return Sprite.Create(tex, rect, pivot, 16);
    }

    bool IsEdge(bool[,] mask, int x, int y, int size)
    {
        if (!mask[y, x]) return false;

        for (int oy = -1; oy <= 1; oy++)
        {
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = x + ox;
                int ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= size || ny >= size) return true;
                if (!mask[ny, nx]) return true;
            }
        }
        return false;
    }

    void BuildHeartsFromSheet()
    {
        if (heartSpriteSheet32 == null) return;

        int frame = 32;
        int cols = Mathf.Max(1, heartSpriteSheet32.width / frame);
        int rows = Mathf.Max(1, heartSpriteSheet32.height / frame);
        if (cols * rows < 3) return;

        // Index mapping requested by user:
        // 0 = full, 1 = half, 2 = empty
        fullHeart = SliceFrame(heartSpriteSheet32, 0, frame);
        halfHeart = SliceFrame(heartSpriteSheet32, 1, frame);
        emptyHeart = SliceFrame(heartSpriteSheet32, 2, frame);
    }

    Sprite SliceFrame(Texture2D tex, int index, int frameSize)
    {
        if (tex == null) return null;
        int cols = Mathf.Max(1, tex.width / frameSize);
        int col = index % cols;
        int row = index / cols;
        int x = col * frameSize;
        int y = tex.height - ((row + 1) * frameSize);
        y = Mathf.Clamp(y, 0, Mathf.Max(0, tex.height - frameSize));
        Rect rect = new Rect(x, y, frameSize, frameSize);
        return Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), frameSize);
    }
}
