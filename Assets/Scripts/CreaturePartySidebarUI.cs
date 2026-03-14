using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CreaturePartySidebarUI : MonoBehaviour
{
    [Header("Data")]
    public PlayerCreatureParty partySource;
    [Range(1, 6)] public int maxVisibleSlots = 6;

    [Header("Layout")]
    public Vector2 anchoredPosition = new Vector2(16f, -120f);
    public Vector2 slotSize = new Vector2(72f, 72f);
    public Vector2 iconSize = new Vector2(52f, 52f);
    public float spacing = 6f;

    [Header("Visuals")]
    public Sprite markerBackgroundSprite;
    public Sprite glassOverlaySprite;
    public Color markerColor = Color.white;
    public Color glassColor = new Color(1f, 1f, 1f, 0.9f);

    private RectTransform listRoot;
    private VerticalLayoutGroup verticalLayout;
    private readonly List<GameObject> builtSlots = new List<GameObject>();
    private readonly Dictionary<string, Sprite> headSpriteCache = new Dictionary<string, Sprite>();
    private readonly List<Sprite> generatedHeadSprites = new List<Sprite>();
    private bool uiDirty = true;
    private int lastRenderedCount = -1;

    void Awake()
    {
        EnsureSprites();
        EnsurePartySource();
        EnsureRoot();
        RebuildUI();
    }

    void OnEnable()
    {
        uiDirty = true;
        EnsurePartySource();
        BindPartyEvents();
        if (!Application.isPlaying || builtSlots.Count == 0)
        {
            RebuildUI();
        }
    }

    void LateUpdate()
    {
        if (!Application.isPlaying) return;

        EnsurePartySource();
        int desiredCount = 0;
        if (partySource != null && partySource.ActiveCreatures != null)
        {
            desiredCount = Mathf.Min(maxVisibleSlots, partySource.ActiveCreatures.Count);
        }

        bool hierarchyMismatch = listRoot != null && listRoot.childCount != desiredCount;
        if (uiDirty || lastRenderedCount != desiredCount || hierarchyMismatch)
        {
            RebuildUI();
        }
    }

    void OnDisable()
    {
        UnbindPartyEvents();
    }

    void OnDestroy()
    {
        UnbindPartyEvents();
        for (int i = 0; i < generatedHeadSprites.Count; i++)
        {
            if (generatedHeadSprites[i] != null)
            {
                Destroy(generatedHeadSprites[i]);
            }
        }
        generatedHeadSprites.Clear();
        headSpriteCache.Clear();
    }

    private void EnsurePartySource()
    {
        if (partySource != null) return;

        PlayerMover mover = FindFirstObjectByType<PlayerMover>();
        if (mover != null)
        {
            partySource = mover.GetComponent<PlayerCreatureParty>();
            if (partySource == null)
            {
                partySource = mover.gameObject.AddComponent<PlayerCreatureParty>();
            }
        }
    }

    private void BindPartyEvents()
    {
        if (partySource != null)
        {
            partySource.PartyChanged -= HandlePartyChanged;
            partySource.PartyChanged += HandlePartyChanged;
        }
    }

    private void UnbindPartyEvents()
    {
        if (partySource != null)
        {
            partySource.PartyChanged -= HandlePartyChanged;
        }
    }

    private void HandlePartyChanged()
    {
        uiDirty = true;
    }

    private void EnsureRoot()
    {
        if (listRoot == null)
        {
            Transform existing = transform.Find("CreaturePartySidebar");
            if (existing != null)
            {
                listRoot = existing as RectTransform;
            }
        }

        if (listRoot == null)
        {
            GameObject go = new GameObject("CreaturePartySidebar", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            listRoot = go.GetComponent<RectTransform>();
        }

        listRoot.anchorMin = new Vector2(0f, 1f);
        listRoot.anchorMax = new Vector2(0f, 1f);
        listRoot.pivot = new Vector2(0f, 1f);
        listRoot.anchoredPosition = anchoredPosition;
        listRoot.sizeDelta = new Vector2(slotSize.x, 500f);

        verticalLayout = listRoot.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout == null) verticalLayout = listRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        verticalLayout.spacing = spacing;
        verticalLayout.childAlignment = TextAnchor.UpperLeft;
        verticalLayout.childControlWidth = false;
        verticalLayout.childControlHeight = false;
        verticalLayout.childForceExpandWidth = false;
        verticalLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = listRoot.GetComponent<ContentSizeFitter>();
        if (fitter == null) fitter = listRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        if (builtSlots.Count == 0 && listRoot.childCount > 0)
        {
            for (int i = 0; i < listRoot.childCount; i++)
            {
                Transform child = listRoot.GetChild(i);
                if (child != null) builtSlots.Add(child.gameObject);
            }
        }
    }

    private void RebuildUI()
    {
        EnsureSprites();
        EnsureRoot();
        if (partySource == null || partySource.ActiveCreatures == null) return;

        int count = Mathf.Min(maxVisibleSlots, partySource.ActiveCreatures.Count);
        PruneNullSlots();
        if (Application.isPlaying && builtSlots.Count > 0)
        {
            for (int i = builtSlots.Count; i < count; i++)
            {
                builtSlots.Add(BuildSlot(i, null));
            }

            while (builtSlots.Count > count)
            {
                int last = builtSlots.Count - 1;
                GameObject extra = builtSlots[last];
                builtSlots.RemoveAt(last);
                if (extra != null) Destroy(extra);
            }

            for (int i = 0; i < builtSlots.Count; i++)
            {
                bool show = i < count;
                if (builtSlots[i] != null) builtSlots[i].SetActive(show);
                if (!show || builtSlots[i] == null) continue;

                CreatureInstance instance = partySource.ActiveCreatures[i];
                CreatureDefinition def = instance != null ? CreatureRegistry.Get(instance.definitionID) : null;
                ApplySlotVisual(builtSlots[i], ResolveHeadSprite(def));
            }

            lastRenderedCount = count;
            uiDirty = false;
            return;
        }

        for (int i = builtSlots.Count - 1; i >= 0; i--)
        {
            if (builtSlots[i] == null) continue;
            if (Application.isPlaying) Destroy(builtSlots[i]);
            else DestroyImmediate(builtSlots[i]);
        }
        builtSlots.Clear();

        for (int i = 0; i < count; i++)
        {
            CreatureInstance instance = partySource.ActiveCreatures[i];
            CreatureDefinition def = instance != null ? CreatureRegistry.Get(instance.definitionID) : null;
            builtSlots.Add(BuildSlot(i, ResolveHeadSprite(def)));
        }

        lastRenderedCount = count;
        uiDirty = false;
    }

    private void PruneNullSlots()
    {
        for (int i = builtSlots.Count - 1; i >= 0; i--)
        {
            if (builtSlots[i] == null)
            {
                builtSlots.RemoveAt(i);
            }
        }
    }

    private GameObject BuildSlot(int index, Sprite headSprite)
    {
        GameObject slot = new GameObject("PartySlot" + (index + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(LayoutElement));
        slot.transform.SetParent(listRoot, false);

        RectTransform slotRt = slot.GetComponent<RectTransform>();
        slotRt.sizeDelta = slotSize;

        LayoutElement layoutElement = slot.GetComponent<LayoutElement>();
        layoutElement.preferredWidth = slotSize.x;
        layoutElement.preferredHeight = slotSize.y;
        layoutElement.minWidth = slotSize.x;
        layoutElement.minHeight = slotSize.y;

        Image bg = slot.GetComponent<Image>();
        bg.sprite = markerBackgroundSprite;
        bg.color = markerColor;
        bg.raycastTarget = false;
        bg.type = markerBackgroundSprite != null && markerBackgroundSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(slot.transform, false);
        RectTransform iconRt = iconGO.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = Vector2.zero;
        iconRt.sizeDelta = iconSize;

        Image icon = iconGO.GetComponent<Image>();
        icon.sprite = headSprite;
        icon.color = Color.white;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        icon.enabled = headSprite != null;

        GameObject glassGO = new GameObject("Glass", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        glassGO.transform.SetParent(slot.transform, false);
        RectTransform glassRt = glassGO.GetComponent<RectTransform>();
        glassRt.anchorMin = new Vector2(0f, 0f);
        glassRt.anchorMax = new Vector2(1f, 1f);
        glassRt.pivot = new Vector2(0.5f, 0.5f);
        glassRt.offsetMin = Vector2.zero;
        glassRt.offsetMax = Vector2.zero;

        Image glass = glassGO.GetComponent<Image>();
        glass.sprite = glassOverlaySprite;
        glass.color = glassColor;
        glass.raycastTarget = false;
        glass.type = glassOverlaySprite != null && glassOverlaySprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;

        return slot;
    }

    private void ApplySlotVisual(GameObject slot, Sprite headSprite)
    {
        if (slot == null) return;
        Transform iconTf = slot.transform.Find("Icon");
        if (iconTf == null) return;

        Image icon = iconTf.GetComponent<Image>();
        if (icon == null) return;
        icon.sprite = headSprite;
        icon.color = Color.white;
        icon.enabled = headSprite != null;
    }

    private Sprite ResolveHeadSprite(CreatureDefinition def)
    {
        if (def == null || def.sprite == null) return null;

        string key = string.IsNullOrWhiteSpace(def.creatureID) ? def.name : def.creatureID;
        if (headSpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite source = def.sprite;
        Rect src = source.rect;
        float headHeight = Mathf.Max(8f, src.height * 0.58f);
        float squareSize = Mathf.Clamp(Mathf.Min(src.width, headHeight), 8f, Mathf.Min(src.width, src.height));
        float x = src.x + (src.width - squareSize) * 0.5f;
        float y = src.y + src.height - headHeight;

        float maxY = src.yMax - squareSize;
        if (y > maxY) y = maxY;
        if (y < src.yMin) y = src.yMin;

        Rect crop = new Rect(
            Mathf.Round(x),
            Mathf.Round(y),
            Mathf.Round(squareSize),
            Mathf.Round(squareSize)
        );

        Sprite head = Sprite.Create(source.texture, crop, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        head.name = key + "_head";
        headSpriteCache[key] = head;
        generatedHeadSprites.Add(head);
        return head;
    }

    private void EnsureSprites()
    {
#if UNITY_EDITOR
        if (markerBackgroundSprite == null)
        {
            markerBackgroundSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_FrameMarker01a.png");
        }
        if (glassOverlaySprite == null)
        {
            glassOverlaySprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_FrameSlot01c.png");
        }
#endif
    }
}
