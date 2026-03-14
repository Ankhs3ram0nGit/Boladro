using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CreaturePartySidebarUI : MonoBehaviour
{
    private sealed class PartySlotView : MonoBehaviour
    {
        public RectTransform slotRect;
        public LayoutElement layout;
        public Image background;
        public RectTransform iconRect;
        public Image icon;
        public Image glass;
        public Outline glassOutline;
        public RectTransform infoRoot;
        public Text nameText;
        public Text levelText;
        public Text hpText;
        public Image hpFill;
        public Image xpFill;
    }

    [Header("Data")]
    public PlayerCreatureParty partySource;
    [Range(1, 6)] public int maxVisibleSlots = 6;

    [Header("Layout")]
    public Vector2 anchoredPosition = new Vector2(16f, -120f);
    public Vector2 inactiveSlotSize = new Vector2(72f, 72f);
    public Vector2 activeSlotSize = new Vector2(230f, 84f);
    public Vector2 inactiveIconSize = new Vector2(52f, 52f);
    public Vector2 activeIconSize = new Vector2(60f, 60f);
    public float spacing = 6f;

    [Header("Visuals")]
    public Sprite markerBackgroundSprite;
    public Sprite glassOverlaySprite;
    public Color markerColor = Color.white;
    public Color activeMarkerColor = Color.white;
    public Color glassColor = new Color(1f, 1f, 1f, 0.9f);
    public Color glassOutlineColor = new Color(0f, 0f, 0f, 1f);
    [Range(0.2f, 4f)] public float glassOutlineThickness = 1.2f;

    [Header("Active Info")]
    public Color nameColor = Color.white;
    public Color levelColor = Color.white;
    public Color hpTextColor = Color.white;
    public Color hpBarBackColor = new Color(0f, 0f, 0f, 0.55f);
    public Color hpBarFillColor = new Color(0.18f, 0.92f, 0.22f, 1f);
    public Color xpBarBackColor = new Color(0f, 0f, 0f, 0.55f);
    public Color xpBarFillColor = new Color(0.30f, 0.75f, 1f, 1f);

    [Header("Face Crop")]
    [Range(0.3f, 1f)] public float faceCropWidthRatio = 0.92f;
    [Range(0.3f, 1f)] public float faceCropHeightRatio = 0.58f;
    [Range(-0.4f, 0.4f)] public float faceCropCenterXOffsetRatio = 0f;
    [Range(0f, 1f)] public float faceCropCenterYRatio = 0.70f;

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

        float maxWidth = Mathf.Max(inactiveSlotSize.x, activeSlotSize.x);
        listRoot.anchorMin = new Vector2(0f, 1f);
        listRoot.anchorMax = new Vector2(0f, 1f);
        listRoot.pivot = new Vector2(0f, 1f);
        listRoot.anchoredPosition = anchoredPosition;
        listRoot.sizeDelta = new Vector2(maxWidth, 500f);

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
        int activeIndex = Mathf.Clamp(partySource.ActivePartyIndex, 0, Mathf.Max(0, count - 1));

        PruneInvalidSlots();

        if (Application.isPlaying && builtSlots.Count > 0)
        {
            for (int i = builtSlots.Count; i < count; i++)
            {
                builtSlots.Add(BuildSlot(i));
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
                GameObject slotObj = builtSlots[i];
                if (slotObj != null) slotObj.SetActive(show);
                if (!show || slotObj == null) continue;

                CreatureInstance instance = partySource.ActiveCreatures[i];
                CreatureDefinition def = instance != null ? CreatureRegistry.Get(instance.definitionID) : null;
                ApplySlotVisual(slotObj, instance, def, i == activeIndex);
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
            GameObject slotObj = BuildSlot(i);
            builtSlots.Add(slotObj);

            CreatureInstance instance = partySource.ActiveCreatures[i];
            CreatureDefinition def = instance != null ? CreatureRegistry.Get(instance.definitionID) : null;
            ApplySlotVisual(slotObj, instance, def, i == activeIndex);
        }

        lastRenderedCount = count;
        uiDirty = false;
    }

    private void PruneInvalidSlots()
    {
        for (int i = builtSlots.Count - 1; i >= 0; i--)
        {
            GameObject slotObj = builtSlots[i];
            if (slotObj == null || slotObj.GetComponent<PartySlotView>() == null)
            {
                builtSlots.RemoveAt(i);
                if (slotObj != null)
                {
                    if (Application.isPlaying) Destroy(slotObj);
                    else DestroyImmediate(slotObj);
                }
            }
        }
    }

    private GameObject BuildSlot(int index)
    {
        GameObject slot = new GameObject("PartySlot" + (index + 1),
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(LayoutElement),
            typeof(PartySlotView));
        slot.transform.SetParent(listRoot, false);

        PartySlotView view = slot.GetComponent<PartySlotView>();
        view.slotRect = slot.GetComponent<RectTransform>();
        view.layout = slot.GetComponent<LayoutElement>();
        view.background = slot.GetComponent<Image>();

        view.background.sprite = markerBackgroundSprite;
        view.background.color = markerColor;
        view.background.raycastTarget = false;
        view.background.type = markerBackgroundSprite != null && markerBackgroundSprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;

        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        iconGO.transform.SetParent(slot.transform, false);
        view.iconRect = iconGO.GetComponent<RectTransform>();
        view.icon = iconGO.GetComponent<Image>();
        view.icon.color = Color.white;
        view.icon.preserveAspect = true;
        view.icon.raycastTarget = false;

        GameObject glassGO = new GameObject("Glass", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        glassGO.transform.SetParent(slot.transform, false);
        RectTransform glassRt = glassGO.GetComponent<RectTransform>();
        glassRt.anchorMin = new Vector2(0f, 0f);
        glassRt.anchorMax = new Vector2(1f, 1f);
        glassRt.pivot = new Vector2(0.5f, 0.5f);
        glassRt.offsetMin = Vector2.zero;
        glassRt.offsetMax = Vector2.zero;

        view.glass = glassGO.GetComponent<Image>();
        view.glass.sprite = glassOverlaySprite;
        view.glass.color = glassColor;
        view.glass.raycastTarget = false;
        view.glass.type = glassOverlaySprite != null && glassOverlaySprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;

        view.glassOutline = glassGO.GetComponent<Outline>();
        view.glassOutline.effectColor = glassOutlineColor;
        view.glassOutline.effectDistance = new Vector2(glassOutlineThickness, glassOutlineThickness);
        view.glassOutline.useGraphicAlpha = true;

        view.infoRoot = new GameObject("ActiveInfo", typeof(RectTransform)).GetComponent<RectTransform>();
        view.infoRoot.transform.SetParent(slot.transform, false);

        view.nameText = CreateOutlinedText("Name", view.infoRoot, 16, TextAnchor.UpperLeft, nameColor);
        view.levelText = CreateOutlinedText("Level", view.infoRoot, 14, TextAnchor.UpperRight, levelColor);
        view.hpText = CreateOutlinedText("HP", view.infoRoot, 13, TextAnchor.MiddleRight, hpTextColor);

        Image hpBack = CreateBar("HPBarBG", view.infoRoot, hpBarBackColor, new Vector2(0f, 0.40f), new Vector2(1f, 0.58f));
        view.hpFill = CreateFill(hpBack.transform as RectTransform, hpBarFillColor);

        Image xpBack = CreateBar("XPBarBG", view.infoRoot, xpBarBackColor, new Vector2(0f, 0.14f), new Vector2(1f, 0.28f));
        view.xpFill = CreateFill(xpBack.transform as RectTransform, xpBarFillColor);

        return slot;
    }

    private Text CreateOutlinedText(string name, RectTransform parent, int fontSize, TextAnchor alignment, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        go.transform.SetParent(parent, false);

        Text text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;

        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        outline.effectDistance = new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;

        return text;
    }

    private Image CreateBar(string name, RectTransform parent, Color color, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        return img;
    }

    private Image CreateFill(RectTransform parent, Color color)
    {
        GameObject go = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.color = color;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
        img.raycastTarget = false;

        return img;
    }

    private void ApplySlotVisual(GameObject slotObj, CreatureInstance instance, CreatureDefinition def, bool isActive)
    {
        if (slotObj == null) return;

        PartySlotView view = slotObj.GetComponent<PartySlotView>();
        if (view == null) return;

        Vector2 slotDims = isActive ? activeSlotSize : inactiveSlotSize;
        Vector2 iconDims = isActive ? activeIconSize : inactiveIconSize;

        view.slotRect.sizeDelta = slotDims;
        view.layout.preferredWidth = slotDims.x;
        view.layout.preferredHeight = slotDims.y;
        view.layout.minWidth = slotDims.x;
        view.layout.minHeight = slotDims.y;

        view.background.color = isActive ? activeMarkerColor : markerColor;
        view.background.sprite = markerBackgroundSprite;
        view.background.type = markerBackgroundSprite != null && markerBackgroundSprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;

        view.icon.sprite = ResolveHeadSprite(def);
        view.icon.enabled = view.icon.sprite != null;
        view.iconRect.sizeDelta = iconDims;

        if (isActive)
        {
            view.iconRect.anchorMin = new Vector2(0f, 0.5f);
            view.iconRect.anchorMax = new Vector2(0f, 0.5f);
            view.iconRect.pivot = new Vector2(0.5f, 0.5f);
            view.iconRect.anchoredPosition = new Vector2(10f + iconDims.x * 0.5f, 0f);

            view.infoRoot.gameObject.SetActive(true);
            view.infoRoot.anchorMin = new Vector2(0f, 0f);
            view.infoRoot.anchorMax = new Vector2(1f, 1f);
            view.infoRoot.pivot = new Vector2(0.5f, 0.5f);
            view.infoRoot.offsetMin = new Vector2(iconDims.x + 18f, 8f);
            view.infoRoot.offsetMax = new Vector2(-10f, -8f);

            LayoutText(view.nameText.rectTransform, new Vector2(0f, 0.72f), new Vector2(0.75f, 1f));
            LayoutText(view.levelText.rectTransform, new Vector2(0.75f, 0.72f), new Vector2(1f, 1f));
            LayoutText(view.hpText.rectTransform, new Vector2(0.55f, 0.28f), new Vector2(1f, 0.42f));

            string displayName = instance != null ? instance.DisplayName : (def != null ? def.displayName : "Creature");
            int level = instance != null ? Mathf.Max(1, instance.level) : 1;
            int maxHp = Mathf.Max(1, CreatureInstanceFactory.ComputeMaxHP(def, instance != null ? instance.soulTraits : default, level));
            int curHp = instance != null ? Mathf.Clamp(instance.currentHP, 0, maxHp) : maxHp;

            view.nameText.text = string.IsNullOrWhiteSpace(displayName) ? "Creature" : displayName;
            view.levelText.text = "Lv " + level;
            view.hpText.text = curHp + "/" + maxHp;

            view.hpFill.fillAmount = Mathf.Clamp01(maxHp > 0 ? (float)curHp / maxHp : 0f);
            view.xpFill.fillAmount = ComputeXpRatio(instance);
        }
        else
        {
            view.iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            view.iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            view.iconRect.pivot = new Vector2(0.5f, 0.5f);
            view.iconRect.anchoredPosition = Vector2.zero;
            view.infoRoot.gameObject.SetActive(false);
        }

        view.glass.sprite = glassOverlaySprite;
        view.glass.type = glassOverlaySprite != null && glassOverlaySprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        view.glass.color = glassColor;
        view.glassOutline.effectColor = glassOutlineColor;
        view.glassOutline.effectDistance = new Vector2(glassOutlineThickness, glassOutlineThickness);
    }

    private void LayoutText(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private float ComputeXpRatio(CreatureInstance instance)
    {
        if (instance == null) return 0f;

        int xpToLevel = Mathf.Max(20, instance.level * 12);
        int accumulated = Mathf.Max(0, instance.totalBattles * 5);
        int currentInLevel = accumulated % xpToLevel;
        return Mathf.Clamp01((float)currentInLevel / xpToLevel);
    }

    private Sprite ResolveHeadSprite(CreatureDefinition def)
    {
        if (def == null || def.sprite == null) return null;

        string id = string.IsNullOrWhiteSpace(def.creatureID) ? def.name : def.creatureID;
        string key = id + "_" + faceCropWidthRatio.ToString("F3") + "_" + faceCropHeightRatio.ToString("F3") + "_" +
                     faceCropCenterXOffsetRatio.ToString("F3") + "_" + faceCropCenterYRatio.ToString("F3");

        if (headSpriteCache.TryGetValue(key, out Sprite cached) && cached != null)
        {
            return cached;
        }

        Sprite source = def.sprite;
        Rect src = source.textureRect;

        float cropW = Mathf.Clamp(src.width * faceCropWidthRatio, 8f, src.width);
        float cropH = Mathf.Clamp(src.height * faceCropHeightRatio, 8f, src.height);
        float centerX = src.center.x + (src.width * faceCropCenterXOffsetRatio);
        float centerY = src.yMin + (src.height * faceCropCenterYRatio);

        float x = centerX - cropW * 0.5f;
        float y = centerY - cropH * 0.5f;

        x = Mathf.Clamp(x, src.xMin, src.xMax - cropW);
        y = Mathf.Clamp(y, src.yMin, src.yMax - cropH);

        Rect crop = new Rect(
            Mathf.Round(x),
            Mathf.Round(y),
            Mathf.Round(cropW),
            Mathf.Round(cropH));

        Sprite head = Sprite.Create(source.texture, crop, new Vector2(0.5f, 0.5f), source.pixelsPerUnit);
        head.name = id + "_party_head";

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
