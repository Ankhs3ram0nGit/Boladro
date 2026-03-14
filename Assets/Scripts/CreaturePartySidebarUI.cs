using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CreaturePartySidebarUI : MonoBehaviour
{
    private const string LevelUpPulseSfxAssetPath = "Assets/JDSherbert - Ultimate UI SFX Pack (FREE)/New Folder/DSGNMisc_STEP-Bit Step_HY_PC-005.wav";

    private sealed class PartySlotView : MonoBehaviour
    {
        public int slotIndex;
        public RectTransform slotRect;
        public LayoutElement layout;
        public Image background;
        public RectTransform iconRect;
        public Image icon;
        public RectTransform levelUpArrowRect;
        public Image levelUpArrow;
        public RectTransform faintedOverlayRect;
        public Image faintedOverlay;
        public Image glass;
        public Outline glassOutline;
        public RectTransform infoRoot;
        public Text nameText;
        public Text levelText;
        public Text hpText;
        public RectTransform hpBarRect;
        public RectTransform xpBarRect;
        public Image hpFill;
        public Image xpFill;
        public CreatureInstance instance;
        public CreatureDefinition definition;
        public bool isActive;
        public float expandT;
    }

    [Header("Data")]
    public PlayerCreatureParty partySource;
    [Range(1, 6)] public int maxVisibleSlots = 6;

    [Header("Layout")]
    public Vector2 anchoredPosition = new Vector2(16f, -120f);
    public Vector2 collapsedSlotSize = new Vector2(72f, 72f);
    public Vector2 expandedSlotSize = new Vector2(230f, 84f);
    public Vector2 collapsedIconSize = new Vector2(52f, 52f);
    public Vector2 expandedIconSize = new Vector2(60f, 60f);
    public float spacing = 6f;
    [Min(0.5f)] public float expandAnimationSpeed = 6f;
    [Range(0.1f, 1f)] public float holdExpandSpeedMultiplier = 0.28f;
    [Range(0f, 1f)] public float expandedInfoThreshold = 0.6f;

    [Header("Interaction")]
    public Key expandAllHoldKey = Key.Q;
    [Min(0.01f)] public float expandAllHoldDelay = 0.24f;

    [Header("Visuals")]
    public Sprite markerBackgroundSprite;
    public Sprite glassOverlaySprite;
    public Sprite faintedCrossSprite;
    public Sprite levelUpArrowSprite;
    public Color markerColor = Color.white;
    public Color activeMarkerColor = Color.white;
    public Color glassColor = new Color(1f, 1f, 1f, 0.9f);
    public Color glassOutlineColor = new Color(0f, 0f, 0f, 1f);
    [Range(0.2f, 4f)] public float glassOutlineThickness = 1.2f;
    public Color levelUpArrowColor = new Color(1f, 1f, 1f, 0.95f);
    public Vector2 levelUpArrowSize = new Vector2(24f, 24f);

    [Header("Active Info")]
    public Color nameColor = Color.white;
    public Color levelColor = Color.white;
    public Color hpTextColor = Color.white;
    public Color hpBarBackColor = new Color(0f, 0f, 0f, 0.55f);
    public Color hpBarFillColor = new Color(0.18f, 0.92f, 0.22f, 1f);
    public Color xpBarBackColor = new Color(0f, 0f, 0f, 0.55f);
    public Color xpBarFillColor = new Color(0.30f, 0.75f, 1f, 1f);
    public Color barOutlineColor = Color.black;
    [Range(0.5f, 2f)] public float barOutlineThickness = 1f;
    [Range(0f, 1f)] public float levelUpPulseSfxVolume = 0.9f;
    public AudioClip levelUpPulseSfx;

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
    private Sprite neutralFillSprite;
    private InventoryUI inventoryUI;
    private RectTransform addPartyDropRect;
    private Image addPartyDropBg;
    private Text addPartyDropPlus;
    private Text addPartyDropLabel;
    private AddPartyDropZoneUI addPartyDropZone;
    private bool uiDirty = true;
    private int lastRenderedCount = -1;
    private bool expandAllHeld;
    private bool expandHoldKeyDown;
    private float expandHoldKeyDownTime;
    private AudioSource levelUpPulseAudioSource;
    private readonly HashSet<string> activePulseSfxKeys = new HashSet<string>();
    private static readonly Color HpGreen = new Color(0.20f, 0.82f, 0.24f, 1f);
    private static readonly Color HpYellow = new Color(0.97f, 0.88f, 0.20f, 1f);
    private static readonly Color HpOrange = new Color(1.00f, 0.62f, 0.16f, 1f);
    private static readonly Color HpRed = new Color(0.90f, 0.18f, 0.18f, 1f);

    void Awake()
    {
        EnsureSprites();
        EnsureLevelUpPulseAudio();
        EnsurePartySource();
        EnsureRoot();
        RebuildUI();
    }

    void OnEnable()
    {
        uiDirty = true;
        expandAllHeld = false;
        EnsurePartySource();
        BindPartyEvents();
        if (!Application.isPlaying || builtSlots.Count == 0)
        {
            RebuildUI();
        }
    }

    void LateUpdate()
    {
        UpdateExpandHoldState();
        if (!Application.isPlaying)
        {
            TickCardAnimations();
            return;
        }

        EnsurePartySource();
        int desiredCount = 0;
        if (partySource != null && partySource.ActiveCreatures != null)
        {
            desiredCount = Mathf.Min(maxVisibleSlots, partySource.ActiveCreatures.Count);
        }

        bool hierarchyMismatch = builtSlots.Count != desiredCount;
        if (uiDirty || lastRenderedCount != desiredCount || hierarchyMismatch)
        {
            RebuildUI();
        }
        else
        {
            RefreshAddPartyDropZone();
        }

        RefreshLiveSlotData();
        TickCardAnimations();
    }

    void OnDisable()
    {
        UnbindPartyEvents();
        activePulseSfxKeys.Clear();
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
        if (neutralFillSprite != null)
        {
            Destroy(neutralFillSprite);
            neutralFillSprite = null;
        }
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

        if (inventoryUI == null)
        {
            inventoryUI = GetComponent<InventoryUI>();
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

        float maxWidth = Mathf.Max(collapsedSlotSize.x, expandedSlotSize.x);
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
                if (child == null) continue;
                if (child.GetComponent<PartySlotView>() == null) continue;
                builtSlots.Add(child.gameObject);
            }
        }

        EnsureAddPartyDropZone();
    }

    private void RebuildUI()
    {
        EnsureSprites();
        EnsureLevelUpPulseAudio();
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

            activePulseSfxKeys.Clear();
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

        activePulseSfxKeys.Clear();
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

        RefreshAddPartyDropZone();
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
        view.slotIndex = index;
        view.slotRect = slot.GetComponent<RectTransform>();
        view.layout = slot.GetComponent<LayoutElement>();
        view.background = slot.GetComponent<Image>();
        view.expandT = 0f;

        view.background.sprite = markerBackgroundSprite;
        view.background.color = markerColor;
        view.background.raycastTarget = true;
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

        GameObject levelUpArrowGO = new GameObject("LevelUpArrow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        levelUpArrowGO.transform.SetParent(slot.transform, false);
        view.levelUpArrowRect = levelUpArrowGO.GetComponent<RectTransform>();
        view.levelUpArrowRect.anchorMin = new Vector2(1f, 0.5f);
        view.levelUpArrowRect.anchorMax = new Vector2(1f, 0.5f);
        view.levelUpArrowRect.pivot = new Vector2(0f, 0.5f);
        view.levelUpArrowRect.anchoredPosition = new Vector2(8f, 0f);
        view.levelUpArrowRect.sizeDelta = levelUpArrowSize;
        view.levelUpArrowRect.localRotation = Quaternion.Euler(0f, 0f, 90f);
        view.levelUpArrow = levelUpArrowGO.GetComponent<Image>();
        view.levelUpArrow.sprite = levelUpArrowSprite;
        view.levelUpArrow.color = levelUpArrowColor;
        view.levelUpArrow.preserveAspect = true;
        view.levelUpArrow.raycastTarget = false;
        view.levelUpArrow.enabled = false;

        GameObject faintedGO = new GameObject("FaintedOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        faintedGO.transform.SetParent(slot.transform, false);
        view.faintedOverlayRect = faintedGO.GetComponent<RectTransform>();
        view.faintedOverlay = faintedGO.GetComponent<Image>();
        view.faintedOverlay.sprite = faintedCrossSprite;
        view.faintedOverlay.color = new Color(1f, 1f, 1f, 0.25f);
        view.faintedOverlay.preserveAspect = true;
        view.faintedOverlay.raycastTarget = false;
        view.faintedOverlay.enabled = false;

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
        view.hpBarRect = hpBack.rectTransform;
        view.hpFill = CreateFill(hpBack.transform as RectTransform, hpBarFillColor);

        Image xpBack = CreateBar("XPBarBG", view.infoRoot, xpBarBackColor, new Vector2(0f, 0.14f), new Vector2(1f, 0.28f));
        view.xpBarRect = xpBack.rectTransform;
        view.xpFill = CreateFill(xpBack.transform as RectTransform, xpBarFillColor);

        PartySidebarSlotDragUI dragView = slot.GetComponent<PartySidebarSlotDragUI>();
        if (dragView == null) dragView = slot.AddComponent<PartySidebarSlotDragUI>();
        dragView.sidebar = this;
        dragView.slotIndex = index;
        dragView.iconSource = view.icon;

        return slot;
    }

    private void EnsureAddPartyDropZone()
    {
        if (listRoot == null) return;

        Transform existing = listRoot.Find("AddPartyDropZone");
        if (existing == null)
        {
            GameObject go = new GameObject("AddPartyDropZone",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(LayoutElement),
                typeof(AddPartyDropZoneUI));
            go.transform.SetParent(listRoot, false);
            existing = go.transform;
        }

        addPartyDropRect = existing as RectTransform;
        addPartyDropBg = existing.GetComponent<Image>();
        if (addPartyDropBg != null)
        {
            addPartyDropBg.color = new Color(0f, 0f, 0f, 0.35f);
            addPartyDropBg.raycastTarget = true;
        }

        LayoutElement le = existing.GetComponent<LayoutElement>();
        if (le != null)
        {
            le.preferredWidth = expandedSlotSize.x;
            le.preferredHeight = Mathf.Max(64f, collapsedSlotSize.y);
            le.minWidth = le.preferredWidth;
            le.minHeight = le.preferredHeight;
        }

        Transform plusTf = existing.Find("Plus");
        if (plusTf == null)
        {
            GameObject go = new GameObject("Plus", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(existing, false);
            plusTf = go.transform;
        }

        addPartyDropPlus = plusTf.GetComponent<Text>();
        addPartyDropPlus.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        addPartyDropPlus.fontStyle = FontStyle.Bold;
        addPartyDropPlus.fontSize = 46;
        addPartyDropPlus.alignment = TextAnchor.MiddleLeft;
        addPartyDropPlus.color = new Color(0.88f, 0.88f, 0.88f, 1f);
        addPartyDropPlus.text = "+";
        RectTransform plusRt = addPartyDropPlus.rectTransform;
        plusRt.anchorMin = new Vector2(0f, 0f);
        plusRt.anchorMax = new Vector2(0f, 1f);
        plusRt.pivot = new Vector2(0f, 0.5f);
        plusRt.anchoredPosition = new Vector2(10f, 0f);
        plusRt.sizeDelta = new Vector2(44f, 0f);

        Transform labelTf = existing.Find("Label");
        if (labelTf == null)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(existing, false);
            labelTf = go.transform;
        }

        addPartyDropLabel = labelTf.GetComponent<Text>();
        addPartyDropLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        addPartyDropLabel.fontStyle = FontStyle.Bold;
        addPartyDropLabel.fontSize = 26;
        addPartyDropLabel.alignment = TextAnchor.MiddleLeft;
        addPartyDropLabel.color = new Color(0.86f, 0.86f, 0.86f, 1f);
        addPartyDropLabel.text = "Add to Party";
        RectTransform labelRt = addPartyDropLabel.rectTransform;
        labelRt.anchorMin = new Vector2(0f, 0f);
        labelRt.anchorMax = new Vector2(1f, 1f);
        labelRt.pivot = new Vector2(0f, 0.5f);
        labelRt.offsetMin = new Vector2(58f, 0f);
        labelRt.offsetMax = new Vector2(-10f, 0f);

        addPartyDropZone = existing.GetComponent<AddPartyDropZoneUI>();
        if (addPartyDropZone != null)
        {
            addPartyDropZone.sidebar = this;
        }
    }

    private void RefreshAddPartyDropZone()
    {
        EnsureAddPartyDropZone();
        if (addPartyDropRect == null) return;

        bool canShow = CanAcceptPartyAddDrop();
        if (addPartyDropRect.gameObject.activeSelf != canShow)
        {
            addPartyDropRect.gameObject.SetActive(canShow);
        }

        if (!canShow) return;

        addPartyDropRect.SetSiblingIndex(builtSlots.Count);
        if (addPartyDropZone != null) addPartyDropZone.sidebar = this;
        if (addPartyDropBg != null) addPartyDropBg.color = new Color(0f, 0f, 0f, 0.35f);
        if (addPartyDropLabel != null) addPartyDropLabel.color = new Color(0.86f, 0.86f, 0.86f, 1f);
        if (addPartyDropPlus != null) addPartyDropPlus.color = new Color(0.90f, 0.90f, 0.90f, 1f);
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
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
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

        Outline outline = go.GetComponent<Outline>();
        outline.effectColor = barOutlineColor;
        outline.effectDistance = new Vector2(barOutlineThickness, barOutlineThickness);
        outline.useGraphicAlpha = true;

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
        img.sprite = GetNeutralFillSprite();
        img.color = color;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
        img.preserveAspect = false;
        img.material = null;
        img.raycastTarget = false;

        return img;
    }

    private void ApplySlotVisual(GameObject slotObj, CreatureInstance instance, CreatureDefinition def, bool isActive)
    {
        if (slotObj == null) return;

        PartySlotView view = slotObj.GetComponent<PartySlotView>();
        if (view == null) return;

        view.instance = instance;
        view.definition = def;
        view.isActive = isActive;
        bool shouldExpand = isActive || expandAllHeld;
        view.expandT = Mathf.Clamp01(shouldExpand ? Mathf.Max(view.expandT, 0.9f) : Mathf.Min(view.expandT, 0.1f));

        view.background.color = isActive ? activeMarkerColor : markerColor;
        view.background.sprite = markerBackgroundSprite;
        view.background.type = markerBackgroundSprite != null && markerBackgroundSprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;

        view.icon.sprite = ResolveHeadSprite(def);
        view.icon.enabled = view.icon.sprite != null;

        string displayName = instance != null ? instance.DisplayName : (def != null ? def.displayName : "Creature");
        int level = instance != null ? Mathf.Max(1, instance.level) : 1;
        int maxHp = Mathf.Max(1, CreatureInstanceFactory.ComputeMaxHP(def, instance != null ? instance.soulTraits : default, level));
        int curHp = instance != null ? Mathf.Clamp(instance.currentHP, 0, maxHp) : maxHp;

        view.nameText.text = string.IsNullOrWhiteSpace(displayName) ? "Creature" : displayName;
        view.levelText.text = "Lv " + level;
        view.hpText.text = curHp + "/" + maxHp;

        float hpRatio = Mathf.Clamp01(maxHp > 0 ? (float)curHp / maxHp : 0f);
        view.hpFill.fillAmount = hpRatio;
        view.hpFill.color = ResolveHpTierColor(view.hpFill.fillAmount);
        view.xpFill.fillAmount = ComputeXpRatio(instance, def);
        view.xpFill.color = xpBarFillColor;

        view.glass.sprite = glassOverlaySprite;
        view.glass.type = glassOverlaySprite != null && glassOverlaySprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        view.glass.color = glassColor;
        view.glassOutline.effectColor = glassOutlineColor;
        view.glassOutline.effectDistance = new Vector2(glassOutlineThickness, glassOutlineThickness);

        if (view.levelUpArrow != null && view.levelUpArrowRect != null)
        {
            view.levelUpArrow.sprite = levelUpArrowSprite;
            view.levelUpArrow.color = levelUpArrowColor;
            view.levelUpArrowRect.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        LayoutText(view.nameText.rectTransform, new Vector2(0f, 0.72f), new Vector2(0.75f, 1f));
        LayoutText(view.levelText.rectTransform, new Vector2(0.75f, 0.72f), new Vector2(1f, 1f));
        LayoutText(view.hpText.rectTransform, new Vector2(0.55f, 0.28f), new Vector2(1f, 0.42f));

        if (view.faintedOverlay != null)
        {
            view.faintedOverlay.sprite = faintedCrossSprite;
            view.faintedOverlay.color = new Color(1f, 1f, 1f, 0.25f);
            view.faintedOverlay.enabled = curHp <= 0;
        }

        PartySidebarSlotDragUI dragView = slotObj.GetComponent<PartySidebarSlotDragUI>();
        if (dragView != null)
        {
            dragView.sidebar = this;
            dragView.slotIndex = view.slotIndex;
            dragView.iconSource = view.icon;
        }
    }

    private void RefreshLiveSlotData()
    {
        if (partySource == null || partySource.ActiveCreatures == null || builtSlots.Count == 0) return;

        int count = Mathf.Min(maxVisibleSlots, partySource.ActiveCreatures.Count);
        int activeIndex = Mathf.Clamp(partySource.ActivePartyIndex, 0, Mathf.Max(0, count - 1));

        for (int i = 0; i < builtSlots.Count; i++)
        {
            GameObject slotObj = builtSlots[i];
            if (slotObj == null || !slotObj.activeSelf) continue;

            PartySlotView view = slotObj.GetComponent<PartySlotView>();
            if (view == null) continue;
            if (i >= count) continue;

            CreatureInstance instance = partySource.ActiveCreatures[i];
            if (instance == null) continue;

            CreatureDefinition def = CreatureRegistry.Get(instance.definitionID);
            view.instance = instance;
            view.definition = def;
            view.isActive = i == activeIndex;

            int level = Mathf.Max(1, instance.level);
            int maxHp = Mathf.Max(1, CreatureInstanceFactory.ComputeMaxHP(def, instance.soulTraits, level));
            int curHp = Mathf.Clamp(instance.currentHP, 0, maxHp);
            float hpRatio = Mathf.Clamp01(maxHp > 0 ? (float)curHp / maxHp : 0f);
            bool levelPulseActive = CreatureLevelUpSignal.TryGetPulse01(instance, out _);

            if (levelPulseActive)
            {
                if (view.levelText != null) view.levelText.text = "Lv " + level;
                if (view.background != null)
                {
                    view.background.color = view.isActive ? activeMarkerColor : markerColor;
                }
                continue;
            }

            if (view.nameText != null)
            {
                string displayName = instance.DisplayName;
                view.nameText.text = string.IsNullOrWhiteSpace(displayName) ? "Creature" : displayName;
            }
            if (view.levelText != null) view.levelText.text = "Lv " + level;
            if (view.hpText != null) view.hpText.text = curHp + "/" + maxHp;
            if (view.hpFill != null)
            {
                view.hpFill.sprite = GetNeutralFillSprite();
                view.hpFill.material = null;
                view.hpFill.fillAmount = hpRatio;
                view.hpFill.color = ResolveHpTierColor(hpRatio);
            }
            if (view.xpFill != null)
            {
                view.xpFill.sprite = GetNeutralFillSprite();
                view.xpFill.material = null;
                view.xpFill.fillAmount = ComputeXpRatio(instance, def);
                view.xpFill.color = xpBarFillColor;
            }

            if (view.background != null)
            {
                view.background.color = view.isActive ? activeMarkerColor : markerColor;
            }

            if (view.faintedOverlay != null)
            {
                view.faintedOverlay.sprite = faintedCrossSprite;
                view.faintedOverlay.color = new Color(1f, 1f, 1f, 0.25f);
                view.faintedOverlay.enabled = curHp <= 0;
            }
        }
    }

    private Color ResolveHpTierColor(float ratio)
    {
        float clamped = Mathf.Clamp01(ratio);
        if (clamped > 0.75f) return HpGreen;
        if (clamped > 0.5f) return HpYellow;
        if (clamped > 0.25f) return HpOrange;
        return HpRed;
    }

    private void LayoutText(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void UpdateExpandHoldState()
    {
        bool held = false;
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            KeyControl key = kb[expandAllHoldKey];
            if (key != null)
            {
                if (key.wasPressedThisFrame)
                {
                    expandHoldKeyDown = true;
                    expandHoldKeyDownTime = Time.unscaledTime;
                }

                if (key.wasReleasedThisFrame)
                {
                    expandHoldKeyDown = false;
                }

                if (expandHoldKeyDown && key.isPressed)
                {
                    float heldDuration = Time.unscaledTime - expandHoldKeyDownTime;
                    held = heldDuration >= Mathf.Max(0.01f, expandAllHoldDelay);
                }
            }
        }

        if (held != expandAllHeld)
        {
            expandAllHeld = held;
            uiDirty = true;
        }
    }

    private void TickCardAnimations()
    {
        if (builtSlots.Count == 0) return;
        if (inventoryUI == null) inventoryUI = GetComponent<InventoryUI>();

        float dt = Application.isPlaying ? Time.unscaledDeltaTime : Time.deltaTime;
        float baseSpeed = Mathf.Max(0.5f, expandAnimationSpeed);
        float holdSpeed = baseSpeed * Mathf.Clamp(holdExpandSpeedMultiplier, 0.1f, 1f);
        bool forceExpandFromInventory = inventoryUI != null && inventoryUI.IsPanelOpen;

        for (int i = 0; i < builtSlots.Count; i++)
        {
            GameObject slotObj = builtSlots[i];
            if (slotObj == null || !slotObj.activeSelf) continue;

            PartySlotView view = slotObj.GetComponent<PartySlotView>();
            if (view == null) continue;

            bool expandedTarget = forceExpandFromInventory || view.isActive || expandAllHeld;
            float speed = baseSpeed;
            if (expandAllHeld && !view.isActive && expandedTarget)
            {
                speed = holdSpeed;
            }
            float step = speed * dt;
            view.expandT = Mathf.MoveTowards(view.expandT, expandedTarget ? 1f : 0f, step);

            Vector2 slotDims = Vector2.Lerp(collapsedSlotSize, expandedSlotSize, view.expandT);
            Vector2 iconDims = Vector2.Lerp(collapsedIconSize, expandedIconSize, view.expandT);

            view.slotRect.sizeDelta = slotDims;
            view.layout.preferredWidth = slotDims.x;
            view.layout.preferredHeight = slotDims.y;
            view.layout.minWidth = slotDims.x;
            view.layout.minHeight = slotDims.y;

            view.iconRect.anchorMin = new Vector2(0f, 0.5f);
            view.iconRect.anchorMax = new Vector2(0f, 0.5f);
            view.iconRect.pivot = new Vector2(0.5f, 0.5f);
            view.iconRect.sizeDelta = iconDims;

            float collapsedIconX = slotDims.x * 0.5f;
            float expandedIconX = 10f + iconDims.x * 0.5f;
            float iconX = Mathf.Lerp(collapsedIconX, expandedIconX, view.expandT);
            view.iconRect.anchoredPosition = new Vector2(iconX, 0f);

            if (view.levelUpArrowRect != null && view.levelUpArrow != null)
            {
                view.levelUpArrowRect.anchorMin = new Vector2(1f, 0.5f);
                view.levelUpArrowRect.anchorMax = new Vector2(1f, 0.5f);
                view.levelUpArrowRect.pivot = new Vector2(0f, 0.5f);
                view.levelUpArrowRect.sizeDelta = levelUpArrowSize;
                view.levelUpArrowRect.localRotation = Quaternion.Euler(0f, 0f, 90f);
                view.levelUpArrow.sprite = levelUpArrowSprite;
                bool hasPulse = CreatureLevelUpSignal.TryGetPulse01(view.instance, out float pulse01);
                SyncLevelUpPulseSfx(view.instance, hasPulse);
                if (hasPulse)
                {
                    float envelope = 1f - Mathf.Clamp01(pulse01);
                    float bob = Mathf.Sin(pulse01 * Mathf.PI * 8f) * 3f * envelope;
                    float pop = 1f + Mathf.Sin(pulse01 * Mathf.PI * 6f) * 0.16f * envelope;
                    Color pulseColor = levelUpArrowColor;
                    pulseColor.a = Mathf.Lerp(0.45f, levelUpArrowColor.a, envelope);
                    view.levelUpArrow.color = pulseColor;
                    view.levelUpArrowRect.localScale = new Vector3(pop, pop, 1f);
                    view.levelUpArrowRect.anchoredPosition = new Vector2(8f, bob);
                    view.levelUpArrow.enabled = true;

                    float cardPulse = 1f + Mathf.Sin(pulse01 * Mathf.PI * 4f) * 0.045f * envelope;
                    view.slotRect.localScale = new Vector3(cardPulse, cardPulse, 1f);
                }
                else
                {
                    view.levelUpArrowRect.localScale = Vector3.one;
                    view.levelUpArrowRect.anchoredPosition = new Vector2(8f, 0f);
                    view.levelUpArrow.enabled = false;
                    view.slotRect.localScale = Vector3.one;
                }
                view.levelUpArrowRect.SetAsLastSibling();
            }
            else
            {
                SyncLevelUpPulseSfx(view.instance, false);
                view.slotRect.localScale = Vector3.one;
            }

            if (view.faintedOverlayRect != null)
            {
                view.faintedOverlayRect.anchorMin = view.iconRect.anchorMin;
                view.faintedOverlayRect.anchorMax = view.iconRect.anchorMax;
                view.faintedOverlayRect.pivot = view.iconRect.pivot;
                view.faintedOverlayRect.sizeDelta = view.iconRect.sizeDelta;
                view.faintedOverlayRect.anchoredPosition = view.iconRect.anchoredPosition;
                view.faintedOverlayRect.SetAsLastSibling();
            }

            view.infoRoot.anchorMin = new Vector2(0f, 0f);
            view.infoRoot.anchorMax = new Vector2(1f, 1f);
            view.infoRoot.pivot = new Vector2(0.5f, 0.5f);
            view.infoRoot.offsetMin = new Vector2(iconDims.x + 18f, 8f);
            view.infoRoot.offsetMax = new Vector2(-10f, -8f);
            view.infoRoot.gameObject.SetActive(forceExpandFromInventory || view.expandT >= expandedInfoThreshold);
            LayoutInfoBars(view);

            bool glassVisible = !view.isActive && !expandAllHeld && !forceExpandFromInventory;
            view.glass.enabled = glassVisible;
            view.glassOutline.enabled = glassVisible;
        }
    }

    public void HandleSidebarSlotPointerDown(int slotIndex)
    {
        if (inventoryUI == null) inventoryUI = GetComponent<InventoryUI>();
        if (inventoryUI == null) return;
        if (!inventoryUI.IsCreaturesTabOpen) return;
        inventoryUI.OnCreaturePartySlotClicked(slotIndex);
    }

    public void BeginSidebarPartyDrag(int slotIndex, Sprite dragSprite)
    {
        if (inventoryUI == null) inventoryUI = GetComponent<InventoryUI>();
        if (inventoryUI == null) return;
        if (!inventoryUI.IsCreaturesTabOpen) return;
        inventoryUI.BeginCreatureDragFromPartyIndex(slotIndex, dragSprite);
    }

    public bool HasActiveCreatureDrag()
    {
        if (inventoryUI == null) inventoryUI = GetComponent<InventoryUI>();
        return inventoryUI != null && inventoryUI.HasActiveCreatureDrag();
    }

    public void UpdateCreatureDragVisual(PointerEventData eventData)
    {
        if (inventoryUI == null) inventoryUI = GetComponent<InventoryUI>();
        if (inventoryUI == null) return;
        inventoryUI.UpdateDragVisual(eventData);
    }

    public void DropDraggedCreatureOnSidebarSlot(int slotIndex)
    {
        if (inventoryUI == null) inventoryUI = GetComponent<InventoryUI>();
        if (inventoryUI == null) return;
        inventoryUI.DropCreatureOnPartySlot(slotIndex);
    }

    public void DropDraggedCreatureOnStorageSlot(int pageLocalIndex)
    {
        if (inventoryUI == null) inventoryUI = GetComponent<InventoryUI>();
        if (inventoryUI == null) return;
        inventoryUI.DropCreatureOnStorageSlot(pageLocalIndex);
    }

    public void EndCreatureDrag()
    {
        if (inventoryUI == null) inventoryUI = GetComponent<InventoryUI>();
        if (inventoryUI == null) return;
        inventoryUI.EndCreatureDrag();
    }

    public bool CanAcceptPartyAddDrop()
    {
        if (inventoryUI == null) inventoryUI = GetComponent<InventoryUI>();
        if (inventoryUI == null) return false;
        if (!inventoryUI.IsCreaturesTabOpen) return false;
        return inventoryUI.CanAddCreatureToParty();
    }

    public void DropDraggedCreatureIntoParty()
    {
        if (inventoryUI == null) inventoryUI = GetComponent<InventoryUI>();
        if (inventoryUI == null) return;
        inventoryUI.DropCreatureIntoNewPartySlot();
    }

    private void LayoutInfoBars(PartySlotView view)
    {
        if (view == null || view.hpBarRect == null || view.xpBarRect == null || view.infoRoot == null) return;

        const float hpHeight = 10f;
        const float xpHeight = 5f;
        const float xpGapFromHp = 2f;
        const float hpBottom = 16f;

        float xpBottom = hpBottom - xpHeight - xpGapFromHp;

        LayoutInfoBarRect(view.hpBarRect, hpBottom, hpHeight);
        LayoutInfoBarRect(view.xpBarRect, xpBottom, xpHeight);
    }

    private static void LayoutInfoBarRect(RectTransform rt, float bottom, float height)
    {
        if (rt == null) return;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.offsetMin = new Vector2(0f, bottom);
        rt.offsetMax = new Vector2(0f, bottom + height);
    }

    private void EnsureLevelUpPulseAudio()
    {
        if (levelUpPulseAudioSource != null) return;
        levelUpPulseAudioSource = gameObject.AddComponent<AudioSource>();
        levelUpPulseAudioSource.playOnAwake = false;
        levelUpPulseAudioSource.loop = false;
        levelUpPulseAudioSource.spatialBlend = 0f;
    }

    private void EnsureLevelUpPulseClipLoaded()
    {
        if (levelUpPulseSfx != null) return;
#if UNITY_EDITOR
        levelUpPulseSfx = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(LevelUpPulseSfxAssetPath);
#endif
    }

    private void SyncLevelUpPulseSfx(CreatureInstance instance, bool hasPulse)
    {
        string key = ResolvePulseKey(instance);
        if (string.IsNullOrWhiteSpace(key)) return;

        if (!hasPulse)
        {
            activePulseSfxKeys.Remove(key);
            return;
        }

        if (!activePulseSfxKeys.Add(key)) return;
        EnsureLevelUpPulseAudio();
        EnsureLevelUpPulseClipLoaded();
        if (levelUpPulseAudioSource == null || levelUpPulseSfx == null) return;
        levelUpPulseAudioSource.PlayOneShot(levelUpPulseSfx, Mathf.Clamp01(levelUpPulseSfxVolume));
    }

    private static string ResolvePulseKey(CreatureInstance instance)
    {
        if (instance == null) return string.Empty;
        if (!string.IsNullOrWhiteSpace(instance.creatureUID)) return instance.creatureUID.Trim();
        if (!string.IsNullOrWhiteSpace(instance.definitionID)) return "def:" + instance.definitionID.Trim();
        return string.Empty;
    }

    private float ComputeXpRatio(CreatureInstance instance, CreatureDefinition definition)
    {
        return CreatureExperienceSystem.GetLevelProgress01(instance, definition);
    }

    private Sprite GetNeutralFillSprite()
    {
        if (neutralFillSprite != null) return neutralFillSprite;

        Texture2D tex = Texture2D.whiteTexture;
        neutralFillSprite = Sprite.Create(
            tex,
            new Rect(0f, 0f, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f);
        neutralFillSprite.name = "PartyNeutralFill";
        return neutralFillSprite;
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

        if (faintedCrossSprite == null)
        {
            faintedCrossSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_IconCross01a.png");
        }

        if (levelUpArrowSprite == null)
        {
            levelUpArrowSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_IconArrow01a.png");
        }
#endif
    }
}

public sealed class PartySidebarSlotDragUI : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public CreaturePartySidebarUI sidebar;
    public int slotIndex;
    public Image iconSource;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (sidebar == null) return;
        sidebar.HandleSidebarSlotPointerDown(slotIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (sidebar == null) return;
        Sprite dragSprite = iconSource != null ? iconSource.sprite : null;
        sidebar.BeginSidebarPartyDrag(slotIndex, dragSprite);
        sidebar.UpdateCreatureDragVisual(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (sidebar == null) return;
        if (!sidebar.HasActiveCreatureDrag()) return;
        sidebar.UpdateCreatureDragVisual(eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (sidebar == null) return;
        if (!sidebar.HasActiveCreatureDrag()) return;
        sidebar.DropDraggedCreatureOnSidebarSlot(slotIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (sidebar == null) return;
        if (!sidebar.HasActiveCreatureDrag()) return;

        if (eventData != null && eventData.pointerCurrentRaycast.gameObject != null)
        {
            GameObject go = eventData.pointerCurrentRaycast.gameObject;
            CreatureStorageSlotUI storageTarget = go.GetComponentInParent<CreatureStorageSlotUI>();
            if (storageTarget != null)
            {
                sidebar.DropDraggedCreatureOnStorageSlot(storageTarget.pageLocalIndex);
                return;
            }

            PartySidebarSlotDragUI partyTarget = go.GetComponentInParent<PartySidebarSlotDragUI>();
            if (partyTarget != null)
            {
                sidebar.DropDraggedCreatureOnSidebarSlot(partyTarget.slotIndex);
                return;
            }

            AddPartyDropZoneUI addZone = go.GetComponentInParent<AddPartyDropZoneUI>();
            if (addZone != null)
            {
                sidebar.DropDraggedCreatureIntoParty();
                return;
            }
        }

        sidebar.EndCreatureDrag();
    }
}

public sealed class AddPartyDropZoneUI : MonoBehaviour, IDropHandler
{
    public CreaturePartySidebarUI sidebar;

    public void OnDrop(PointerEventData eventData)
    {
        if (sidebar == null) return;
        if (!sidebar.CanAcceptPartyAddDrop()) return;
        if (!sidebar.HasActiveCreatureDrag()) return;
        sidebar.DropDraggedCreatureIntoParty();
    }
}
