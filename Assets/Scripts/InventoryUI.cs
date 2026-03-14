using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;
using System.Text;

public class InventoryUI : MonoBehaviour
{
    enum InventoryTab
    {
        Items = 0,
        Creatures = 1,
        Skills = 2,
        Crafting = 3,
        Player = 4
    }

    enum CreatureDetailSubTab
    {
        Summary = 0,
        Attacks = 1,
        SoulTraits = 2
    }

    enum DragMode
    {
        None = 0,
        Item = 1,
        CreatureStorage = 2,
        CreatureParty = 3
    }

    const int MinRecommendedSlotSize = 90;
    const int DefaultRecommendedIconSize = 80;
    const float IconDisplayScale = 0.9f;
    const float SlotFillOpacity = 0.35f;
    const int CreatureSlotsPerPage = 25;
    static readonly Color DefaultSlotNormalColor = new Color(0.75f, 0.75f, 0.78f, 1f);
    static readonly Color DefaultEmptySlotFillColor = new Color(0f, 0f, 0f, SlotFillOpacity);

    public InventoryModel inventory;
    public RectTransform hotbarRoot;
    public RectTransform bagRoot;
    public RectTransform panelRoot;
    public int slotSize = 90;
    public int iconSize = 80;
    public int spacing = 2;
    [Tooltip("Spacing between bag/inventory menu slots (Tab menu only).")]
    public int inventoryMenuSlotSpacing = 10;
    public Sprite slotSprite;
    public Sprite selectedSprite;
    public Color normalColor = new Color(0.75f, 0.75f, 0.78f, 1f);
    public Color selectedColor = new Color(1f, 1f, 0.8f, 1f);
    public Color emptySlotFillColor = new Color(0f, 0f, 0f, SlotFillOpacity);
    public int slotInnerPadding = 6;
    public int selectedHotbarIndex = 0;

    private InventorySlotUI[] hotbarSlots;
    private InventorySlotUI[] bagSlots;
    private InventorySlotUI draggingFrom;
    private InventorySlotUI selectedInventorySlot;
    private Image draggingIcon;
    private DragMode dragMode = DragMode.None;

    private InventoryTab activeTab = InventoryTab.Items;
    private RectTransform tabsRoot;
    private Button[] tabButtons;
    private Text[] tabButtonLabels;
    private RectTransform creatureTabRoot;
    private RectTransform creatureGridRoot;
    private RectTransform creaturePagerRoot;
    private CreatureStorageSlotUI[] creatureSlots;
    private Button creaturePrevPageButton;
    private Button creatureNextPageButton;
    private Text creaturePageLabel;
    private RectTransform creatureRightPanelRoot;
    private RectTransform creatureDetailsRoot;
    private RectTransform creatureSubTabsRoot;
    private RectTransform creatureDetailContentRoot;
    private Image creatureDetailSprite;
    private Text creatureDetailNameText;
    private Text creatureDetailTypesText;
    private Image creatureDetailHpBg;
    private Image creatureDetailHpFill;
    private Image creatureDetailXpBg;
    private Image creatureDetailXpFill;
    private Text creatureDetailHpText;
    private Text creatureDetailXpText;
    private Text creatureDetailBodyText;
    private Button creatureEvolveButton;
    private Text creatureEvolveButtonLabel;
    private Button creatureSummaryTabButton;
    private Button creatureAttacksTabButton;
    private Button creatureSoulTraitsTabButton;

    private RectTransform itemDetailsRoot;
    private Image itemDetailsIcon;
    private Text itemDetailsNameText;
    private Text itemDetailsCountText;
    private Button itemUseButton;
    private Button itemDropButton;
    private Button itemDiscardButton;

    private RectTransform placeholderRoot;
    private Text placeholderLabel;
    private int creaturePageIndex;
    private int selectedCreatureStorageIndex = -1;
    private int selectedCreaturePartyIndex = -1;
    private bool selectedCreatureFromParty;
    private CreatureDetailSubTab activeCreatureDetailSubTab = CreatureDetailSubTab.Summary;
    private int draggingCreatureStorageIndex = -1;
    private int draggingCreaturePartyIndex = -1;
    private readonly List<Image> backdropBlurLayers = new List<Image>();

    private PlayerCreatureParty party;
    private PlayerCreatureStorage storage;

    public bool IsPanelOpen
    {
        get { return panelRoot != null && panelRoot.gameObject.activeSelf; }
    }

    public bool IsCreaturesTabOpen
    {
        get { return IsPanelOpen && activeTab == InventoryTab.Creatures; }
    }

    void Awake()
    {
        NormalizeVisualSettings();
        EnsureEventSystem();
        EnsureCreatureSources();
        if (inventory == null) inventory = FindFirstObjectByType<InventoryModel>();
        if (panelRoot == null) panelRoot = transform.Find("InventoryPanel") as RectTransform;
        if (hotbarRoot == null) hotbarRoot = transform.Find("Hotbar") as RectTransform;
        if (bagRoot == null && panelRoot != null) bagRoot = panelRoot.Find("BagGrid") as RectTransform;

        EnsureSlotSprites();

        BuildUI();
        Refresh();
    }

    void OnEnable()
    {
        EnsureCreatureSources();
        BindCreatureEvents();
        if (inventory == null) inventory = FindFirstObjectByType<InventoryModel>();
        if (inventory != null)
        {
            inventory.OnChanged += Refresh;
        }
        Refresh();
    }

    void OnDisable()
    {
        UnbindCreatureEvents();
        if (inventory != null)
        {
            inventory.OnChanged -= Refresh;
        }
    }

    void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.tabKey.wasPressedThisFrame && panelRoot != null)
            {
                panelRoot.gameObject.SetActive(!panelRoot.gameObject.activeSelf);
            }
        }

        if (draggingIcon != null)
        {
            UpdateDraggingIconPosition(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
        }

        HandleHotbarSelection();
    }

    void EnsureEventSystem()
    {
        EventSystem es = EventSystem.current;
        if (es == null)
        {
            GameObject go = new GameObject("EventSystem");
            es = go.AddComponent<EventSystem>();
        }

        bool hasAnyModule =
            es.GetComponent<BaseInputModule>() != null;

        if (hasAnyModule) return;

#if ENABLE_INPUT_SYSTEM
        es.gameObject.AddComponent<InputSystemUIInputModule>();
#else
        es.gameObject.AddComponent<StandaloneInputModule>();
#endif
    }

    void UpdateDraggingIconPosition(Vector2 screenPosition)
    {
        if (draggingIcon == null || draggingIcon.canvas == null) return;
        RectTransform canvasRect = draggingIcon.canvas.transform as RectTransform;
        if (canvasRect == null) return;

        Vector2 pos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            draggingIcon.canvas.worldCamera,
            out pos);
        draggingIcon.rectTransform.anchoredPosition = pos;
    }

    void EnsureDraggingIcon()
    {
        if (draggingIcon != null) return;
        GameObject go = new GameObject("DraggingIcon");
        go.transform.SetParent(transform, false);
        draggingIcon = go.AddComponent<Image>();
        draggingIcon.raycastTarget = false;
        draggingIcon.preserveAspect = true;
    }

    void BuildUI()
    {
        NormalizeVisualSettings();
        EnsureCreatureSources();
        if (inventory == null) return;
        if (hotbarRoot == null) return;
        inventory.EnsureSlots();

        EnsureLayout(hotbarRoot, true);

        if (hotbarRoot != null)
        {
            Image bg = hotbarRoot.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = new Color(0f, 0f, 0f, 0.45f);
                bg.raycastTarget = false;
            }
        }

        hotbarSlots = BuildSlotRow(hotbarRoot, inventory.hotbar.Length, true);
        if (bagRoot != null)
        {
            EnsureLayout(bagRoot, false);
            bagSlots = BuildSlotGrid(bagRoot, inventory.bag.Length, inventory.bagColumns);
        }

        EnsureTabBarUI();
        EnsureItemDetailsUI();
        EnsureCreatureTabUI();
        EnsurePlaceholderTabUI();
        ApplyPanelBackdropBlur();
        ApplyInventoryPanelLayout();
        SwitchTab(activeTab);

        if (selectedInventorySlot == null && hotbarSlots != null && hotbarSlots.Length > 0)
        {
            selectedInventorySlot = hotbarSlots[Mathf.Clamp(selectedHotbarIndex, 0, hotbarSlots.Length - 1)];
        }

        if (panelRoot != null)
        {
            panelRoot.gameObject.SetActive(false);
        }
    }

    void EnsureLayout(RectTransform root, bool horizontal)
    {
        if (root == null) return;

        HorizontalLayoutGroup h = root.GetComponent<HorizontalLayoutGroup>();
        GridLayoutGroup g = root.GetComponent<GridLayoutGroup>();

        if (horizontal)
        {
            if (h == null) h = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            if (g != null) DestroyImmediate(g);
            h.spacing = spacing;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
        }
        else
        {
            if (g == null) g = root.gameObject.AddComponent<GridLayoutGroup>();
            if (h != null) DestroyImmediate(h);
            g.cellSize = new Vector2(slotSize, slotSize);
            int inventorySpacing = Mathf.Max(0, inventoryMenuSlotSpacing);
            g.spacing = new Vector2(inventorySpacing, inventorySpacing);
            g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            g.constraintCount = inventory.bagColumns;
            g.childAlignment = TextAnchor.MiddleCenter;
        }
    }

    InventorySlotUI[] BuildSlotRow(RectTransform root, int count, bool isHotbar)
    {
        if (root == null) return null;
        ClearChildren(root);

        InventorySlotUI[] result = new InventorySlotUI[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = CreateSlot(root, isHotbar, i);
        }
        return result;
    }

    InventorySlotUI[] BuildSlotGrid(RectTransform root, int count, int columns)
    {
        if (root == null) return null;
        ClearChildren(root);

        InventorySlotUI[] result = new InventorySlotUI[count];
        for (int i = 0; i < count; i++)
        {
            result[i] = CreateSlot(root, false, i);
        }
        return result;
    }

    void ClearChildren(RectTransform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
            {
                Destroy(root.GetChild(i).gameObject);
            }
            else
            {
                DestroyImmediate(root.GetChild(i).gameObject);
            }
        }
    }

    InventorySlotUI CreateSlot(RectTransform parent, bool isHotbar, int index)
    {
        GameObject slotObj = new GameObject(isHotbar ? "HotbarSlot" + (index + 1) : "BagSlot" + (index + 1));
        slotObj.transform.SetParent(parent, false);

        Image bg = slotObj.AddComponent<Image>();
        bg.sprite = slotSprite;
        bg.raycastTarget = true;

        RectTransform rt = slotObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(slotSize, slotSize);

        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(slotObj.transform, false);
        Image fill = fillObj.AddComponent<Image>();
        fill.raycastTarget = false;
        fill.sprite = CreateSolidSprite();
        fill.color = emptySlotFillColor;
        RectTransform frt = fillObj.GetComponent<RectTransform>();
        frt.anchorMin = new Vector2(0f, 0f);
        frt.anchorMax = new Vector2(1f, 1f);
        frt.pivot = new Vector2(0.5f, 0.5f);

        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(slotObj.transform, false);
        Image icon = iconObj.AddComponent<Image>();
        icon.raycastTarget = false;
        icon.preserveAspect = true;
        RectTransform irt = iconObj.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 0.5f);
        irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        int displayIconSize = ResolveDisplayIconSize();
        irt.sizeDelta = new Vector2(displayIconSize, displayIconSize);

        GameObject countObj = new GameObject("Count");
        countObj.transform.SetParent(slotObj.transform, false);
        Text countText = countObj.AddComponent<Text>();
        countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        countText.fontSize = 14;
        countText.alignment = TextAnchor.LowerRight;
        countText.color = Color.white;
        RectTransform crt = countObj.GetComponent<RectTransform>();
        crt.anchorMin = new Vector2(0, 0);
        crt.anchorMax = new Vector2(1, 1);
        crt.offsetMin = new Vector2(2, 2);
        crt.offsetMax = new Vector2(-2, -2);

        InventorySlotUI slotUi = slotObj.AddComponent<InventorySlotUI>();
        slotUi.ui = this;
        slotUi.isHotbar = isHotbar;
        slotUi.index = index;
        slotUi.background = bg;
        slotUi.fill = fill;
        slotUi.icon = icon;
        slotUi.countText = countText;

        return slotUi;
    }

    public void Refresh()
    {
        NormalizeVisualSettings();
        EnsureCreatureSources();
        if (inventory == null) return;

        if (hotbarSlots != null)
        {
            for (int i = 0; i < hotbarSlots.Length; i++)
            {
                InventorySlot slot = inventory.GetHotbarSlot(i);
                hotbarSlots[i].SetData(slot);
                ResizeSlotVisuals(hotbarSlots[i]);
                bool isSelectedSlot = ReferenceEquals(hotbarSlots[i], selectedInventorySlot);
                ApplyHotbarSelection(hotbarSlots[i], isSelectedSlot);
            }
        }

        if (bagSlots != null)
        {
            for (int i = 0; i < bagSlots.Length; i++)
            {
                InventorySlot slot = inventory.GetBagSlot(i);
                bagSlots[i].SetData(slot);
                ResizeSlotVisuals(bagSlots[i]);
                bool isSelectedSlot = ReferenceEquals(bagSlots[i], selectedInventorySlot);
                ApplyHotbarSelection(bagSlots[i], isSelectedSlot);
            }
        }

        RefreshSelectedItemDetails();
        RefreshCreatureTab();
        ApplyInventoryPanelLayout();
        UpdateTabButtonStates();
    }

    void ApplyHotbarSelection(InventorySlotUI slot, bool selected)
    {
        if (slot == null || slot.background == null) return;
        slot.background.color = selected ? selectedColor : normalColor;
        Sprite bgSprite = selected ? selectedSprite : slotSprite;
        if (bgSprite != null) slot.background.sprite = bgSprite;
        slot.background.type = bgSprite != null && bgSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        slot.background.preserveAspect = false;
    }

    public void OnInventorySlotClicked(InventorySlotUI slot)
    {
        if (slot == null) return;
        selectedInventorySlot = slot;
        if (slot.isHotbar)
        {
            selectedHotbarIndex = Mathf.Clamp(slot.index, 0, Mathf.Max(0, inventory != null && inventory.hotbar != null ? inventory.hotbar.Length - 1 : 0));
        }
        Refresh();
    }

    public void SelectHotbar(int index)
    {
        selectedHotbarIndex = Mathf.Clamp(index, 0, inventory.hotbar.Length - 1);
        if (hotbarSlots != null && hotbarSlots.Length > 0)
        {
            selectedInventorySlot = hotbarSlots[selectedHotbarIndex];
        }
        Refresh();
    }

    public void BeginDrag(InventorySlotUI slot)
    {
        if (dragMode != DragMode.None && dragMode != DragMode.Item) return;
        if (slot == null || slot.data == null || slot.data.IsEmpty()) return;
        draggingFrom = slot;
        dragMode = DragMode.Item;
        EnsureDraggingIcon();

        draggingIcon.transform.SetAsLastSibling();
        draggingIcon.sprite = slot.icon.sprite;
        draggingIcon.color = Color.white;
        int displayIconSize = ResolveDisplayIconSize();
        draggingIcon.rectTransform.sizeDelta = new Vector2(displayIconSize, displayIconSize);
        draggingIcon.gameObject.SetActive(true);
        UpdateDraggingIconPosition(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
    }

    public void EndDrag(InventorySlotUI slot)
    {
        if (dragMode != DragMode.Item) return;
        if (draggingIcon != null)
        {
            draggingIcon.gameObject.SetActive(false);
        }

        if (draggingFrom == null) return;

        if (slot == null || slot == draggingFrom)
        {
            draggingFrom = null;
            dragMode = DragMode.None;
            return;
        }

        SwapSlots(draggingFrom, slot);
        draggingFrom = null;
        dragMode = DragMode.None;
        Refresh();
    }

    public bool HasActiveDrag()
    {
        return dragMode == DragMode.Item && draggingFrom != null;
    }

    public InventorySlotUI GetDragSource()
    {
        return draggingFrom;
    }

    public void UpdateDragVisual(PointerEventData eventData)
    {
        if (draggingIcon == null || eventData == null) return;
        UpdateDraggingIconPosition(eventData.position);
    }

    void SwapSlots(InventorySlotUI from, InventorySlotUI to)
    {
        if (inventory == null) return;

        InventorySlot a = from.data;
        InventorySlot b = to.data;

        InventoryItemData tempItem = a.item;
        int tempCount = a.count;
        a.item = b.item;
        a.count = b.count;
        b.item = tempItem;
        b.count = tempCount;

        inventory.NotifyChanged();
    }

    void HandleHotbarSelection()
    {
        if (inventory == null || inventory.hotbar == null || inventory.hotbar.Length == 0) return;

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.01f)
            {
                int next = (selectedHotbarIndex - 1 + inventory.hotbar.Length) % inventory.hotbar.Length;
                SelectHotbar(next);
                return;
            }
            if (scroll < -0.01f)
            {
                int next = (selectedHotbarIndex + 1) % inventory.hotbar.Length;
                SelectHotbar(next);
                return;
            }
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        for (int i = 0; i < inventory.hotbar.Length && i < 9; i++)
        {
            Key key = Key.Digit1 + i;
            if (kb[key].wasPressedThisFrame)
            {
                SelectHotbar(i);
                break;
            }
        }
    }

    Sprite CreateSlotSprite(Color32 border)
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color32 clear = new Color32(0, 0, 0, 0);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                tex.SetPixel(x, y, edge ? border : clear);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16);
    }

    Sprite CreateSolidSprite()
    {
        const int size = 2;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                tex.SetPixel(x, y, Color.white);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    void ResizeSlotVisuals(InventorySlotUI slot)
    {
        if (slot == null) return;
        if (slot.background != null)
        {
            RectTransform bgRt = slot.background.rectTransform;
            if (bgRt != null)
            {
                bgRt.sizeDelta = new Vector2(slotSize, slotSize);
            }
        }

        if (slot.fill != null)
        {
            slot.fill.color = emptySlotFillColor;
            RectTransform fillRt = slot.fill.rectTransform;
            if (fillRt != null)
            {
                int padding = Mathf.Clamp(slotInnerPadding, 0, slotSize / 2);
                fillRt.offsetMin = new Vector2(padding, padding);
                fillRt.offsetMax = new Vector2(-padding, -padding);
            }
        }

        if (slot.icon != null)
        {
            RectTransform iconRt = slot.icon.rectTransform;
            if (iconRt != null)
            {
                int displayIconSize = ResolveDisplayIconSize();
                iconRt.sizeDelta = new Vector2(displayIconSize, displayIconSize);
            }
        }
    }

    void EnsureItemDetailsUI()
    {
        if (panelRoot == null) return;

        Transform existing = panelRoot.Find("ItemDetailsRoot");
        if (existing == null)
        {
            GameObject go = new GameObject("ItemDetailsRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(panelRoot, false);
            existing = go.transform;
        }

        itemDetailsRoot = existing as RectTransform;
        Image bg = existing.GetComponent<Image>();
        bg.sprite = slotSprite;
        bg.type = slotSprite != null && slotSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        bg.color = new Color(0f, 0f, 0f, 0.45f);
        bg.raycastTarget = false;

        Transform iconTf = itemDetailsRoot.Find("ItemIcon");
        if (iconTf == null)
        {
            GameObject go = new GameObject("ItemIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(itemDetailsRoot, false);
            iconTf = go.transform;
        }
        itemDetailsIcon = iconTf.GetComponent<Image>();
        itemDetailsIcon.preserveAspect = true;
        itemDetailsIcon.color = Color.white;

        Transform nameTf = itemDetailsRoot.Find("ItemName");
        if (nameTf == null)
        {
            GameObject go = new GameObject("ItemName", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(itemDetailsRoot, false);
            nameTf = go.transform;
        }
        itemDetailsNameText = nameTf.GetComponent<Text>();
        itemDetailsNameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        itemDetailsNameText.fontSize = 20;
        itemDetailsNameText.fontStyle = FontStyle.Bold;
        itemDetailsNameText.alignment = TextAnchor.UpperLeft;
        itemDetailsNameText.color = Color.white;

        Transform countTf = itemDetailsRoot.Find("ItemCount");
        if (countTf == null)
        {
            GameObject go = new GameObject("ItemCount", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(itemDetailsRoot, false);
            countTf = go.transform;
        }
        itemDetailsCountText = countTf.GetComponent<Text>();
        itemDetailsCountText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        itemDetailsCountText.fontSize = 16;
        itemDetailsCountText.alignment = TextAnchor.UpperLeft;
        itemDetailsCountText.color = Color.white;

        itemUseButton = EnsureItemActionButton(itemDetailsRoot, "UseButton", "Use", OnUseSelectedItem);
        itemDropButton = EnsureItemActionButton(itemDetailsRoot, "DropButton", "Drop", OnDropSelectedItem);
        itemDiscardButton = EnsureItemActionButton(itemDetailsRoot, "DiscardButton", "Discard", OnDiscardSelectedItem);
    }

    Button EnsureItemActionButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction action)
    {
        Transform existing = parent.Find(name);
        if (existing == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            existing = go.transform;
        }

        Image bg = existing.GetComponent<Image>();
        bg.sprite = slotSprite;
        bg.type = slotSprite != null && slotSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        bg.color = new Color(0.18f, 0.18f, 0.2f, 0.95f);

        LayoutElement le = existing.GetComponent<LayoutElement>();
        le.preferredWidth = 118f;
        le.preferredHeight = 36f;

        Button button = existing.GetComponent<Button>();
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);

        Transform textTf = existing.Find("Text");
        if (textTf == null)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(existing, false);
            textTf = go.transform;
        }
        Text t = textTf.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 16;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = label;
        RectTransform trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        return button;
    }

    void RefreshSelectedItemDetails()
    {
        if (itemDetailsRoot == null) return;

        bool show = activeTab == InventoryTab.Items;
        itemDetailsRoot.gameObject.SetActive(show);
        if (!show) return;

        InventorySlot slot = selectedInventorySlot != null ? selectedInventorySlot.data : null;
        bool hasItem = slot != null && !slot.IsEmpty() && slot.item != null;

        if (itemDetailsIcon != null)
        {
            itemDetailsIcon.sprite = hasItem ? slot.item.icon : null;
            itemDetailsIcon.enabled = itemDetailsIcon.sprite != null;
        }
        if (itemDetailsNameText != null)
        {
            itemDetailsNameText.text = hasItem ? slot.item.displayName : "No item selected";
        }
        if (itemDetailsCountText != null)
        {
            itemDetailsCountText.text = hasItem ? ("x " + Mathf.Max(0, slot.count)) : string.Empty;
        }

        if (itemUseButton != null) itemUseButton.interactable = hasItem;
        if (itemDropButton != null) itemDropButton.interactable = hasItem;
        if (itemDiscardButton != null) itemDiscardButton.interactable = hasItem;
    }

    void OnUseSelectedItem()
    {
        InventorySlot slot = selectedInventorySlot != null ? selectedInventorySlot.data : null;
        if (slot == null || slot.IsEmpty() || slot.item == null) return;
        Debug.Log("InventoryUI: Use action selected for " + slot.item.displayName + " (not implemented).");
    }

    void OnDropSelectedItem()
    {
        InventorySlot slot = selectedInventorySlot != null ? selectedInventorySlot.data : null;
        if (slot == null || slot.IsEmpty()) return;
        slot.count = Mathf.Max(0, slot.count - 1);
        if (slot.count <= 0) slot.Clear();
        inventory.NotifyChanged();
    }

    void OnDiscardSelectedItem()
    {
        InventorySlot slot = selectedInventorySlot != null ? selectedInventorySlot.data : null;
        if (slot == null || slot.IsEmpty()) return;
        slot.Clear();
        inventory.NotifyChanged();
    }

    public bool HasActiveCreatureDrag()
    {
        return dragMode == DragMode.CreatureStorage || dragMode == DragMode.CreatureParty;
    }

    public bool CanAddCreatureToParty()
    {
        EnsureCreatureSources();
        return party != null && party.HasSpaceInParty();
    }

    public void BeginCreatureDragFromStorage(CreatureStorageSlotUI slot)
    {
        if (slot == null) return;
        if (dragMode != DragMode.None && dragMode != DragMode.CreatureStorage && dragMode != DragMode.CreatureParty) return;
        EnsureCreatureSources();
        if (storage == null) return;

        int globalIndex = (creaturePageIndex * CreatureSlotsPerPage) + slot.pageLocalIndex;
        CreatureInstance moving = storage.GetAt(globalIndex);
        if (moving == null) return;

        dragMode = DragMode.CreatureStorage;
        draggingCreatureStorageIndex = globalIndex;
        draggingCreaturePartyIndex = -1;

        EnsureDraggingIcon();
        draggingIcon.transform.SetAsLastSibling();
        draggingIcon.sprite = slot.icon != null ? slot.icon.sprite : null;
        draggingIcon.color = Color.white;
        draggingIcon.rectTransform.sizeDelta = new Vector2(Mathf.RoundToInt(slotSize * 0.9f), Mathf.RoundToInt(slotSize * 0.9f));
        draggingIcon.gameObject.SetActive(draggingIcon.sprite != null);
        UpdateDraggingIconPosition(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
    }

    public void BeginCreatureDragFromParty(CreaturePartySlotUI slot)
    {
        if (slot == null) return;
        BeginCreatureDragFromPartyIndex(slot.partyIndex, slot.icon != null ? slot.icon.sprite : null);
    }

    public void BeginCreatureDragFromPartyIndex(int partyIndex, Sprite dragSprite)
    {
        if (dragMode != DragMode.None && dragMode != DragMode.CreatureStorage && dragMode != DragMode.CreatureParty) return;
        EnsureCreatureSources();
        if (party == null) return;

        CreatureInstance moving = party.GetCreatureAt(partyIndex);
        if (moving == null) return;

        dragMode = DragMode.CreatureParty;
        draggingCreaturePartyIndex = partyIndex;
        draggingCreatureStorageIndex = -1;

        EnsureDraggingIcon();
        draggingIcon.transform.SetAsLastSibling();
        draggingIcon.sprite = dragSprite;
        draggingIcon.color = Color.white;
        draggingIcon.rectTransform.sizeDelta = new Vector2(Mathf.RoundToInt(slotSize * 0.9f), Mathf.RoundToInt(slotSize * 0.9f));
        draggingIcon.gameObject.SetActive(draggingIcon.sprite != null);
        UpdateDraggingIconPosition(Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero);
    }

    public void DropCreatureOnStorageSlot(int pageLocalIndex)
    {
        if (!HasActiveCreatureDrag()) return;
        EnsureCreatureSources();
        if (storage == null || party == null)
        {
            EndCreatureDrag();
            return;
        }

        int targetStorageIndex = (creaturePageIndex * CreatureSlotsPerPage) + pageLocalIndex;
        if (targetStorageIndex < 0 || targetStorageIndex >= storage.Capacity)
        {
            EndCreatureDrag();
            return;
        }

        if (dragMode == DragMode.CreatureStorage)
        {
            int sourceIndex = draggingCreatureStorageIndex;
            if (sourceIndex == targetStorageIndex)
            {
                EndCreatureDrag();
                return;
            }

            CreatureInstance moving;
            if (!storage.TryTakeAt(sourceIndex, out moving) || moving == null)
            {
                EndCreatureDrag();
                return;
            }

            CreatureInstance replaced;
            if (!storage.TrySetAt(targetStorageIndex, moving, out replaced))
            {
                storage.TrySetAt(sourceIndex, moving, out _);
                EndCreatureDrag();
                return;
            }

            if (replaced != null)
            {
                storage.TrySetAt(sourceIndex, replaced, out _);
            }

            selectedCreatureFromParty = false;
            selectedCreatureStorageIndex = targetStorageIndex;
            selectedCreaturePartyIndex = -1;
        }
        else if (dragMode == DragMode.CreatureParty)
        {
            int sourcePartyIndex = draggingCreaturePartyIndex;
            CreatureInstance moving;
            if (!party.TryTakeCreatureAtSlot(sourcePartyIndex, out moving) || moving == null)
            {
                EndCreatureDrag();
                return;
            }

            CreatureInstance replaced;
            if (!storage.TrySetAt(targetStorageIndex, moving, out replaced))
            {
                party.TrySetCreatureAtSlot(sourcePartyIndex, moving, out _);
                EndCreatureDrag();
                return;
            }

            if (replaced != null)
            {
                CreatureInstance displaced;
                if (!party.TrySetCreatureAtSlot(sourcePartyIndex, replaced, out displaced))
                {
                    storage.TryStoreCreature(replaced);
                }
                else if (displaced != null)
                {
                    storage.TryStoreCreature(displaced);
                }
            }

            selectedCreatureFromParty = false;
            selectedCreatureStorageIndex = targetStorageIndex;
            selectedCreaturePartyIndex = -1;
        }

        EndCreatureDrag();
        RefreshCreatureTab();
    }

    public void DropCreatureOnPartySlot(int targetPartyIndex)
    {
        if (!HasActiveCreatureDrag()) return;
        EnsureCreatureSources();
        if (storage == null || party == null)
        {
            EndCreatureDrag();
            return;
        }

        if (targetPartyIndex < 0 || targetPartyIndex >= PlayerCreatureParty.MaxPartySize)
        {
            EndCreatureDrag();
            return;
        }

        if (dragMode == DragMode.CreatureStorage)
        {
            int sourceStorageIndex = draggingCreatureStorageIndex;
            CreatureInstance moving;
            if (!storage.TryTakeAt(sourceStorageIndex, out moving) || moving == null)
            {
                EndCreatureDrag();
                return;
            }

            CreatureInstance replaced;
            if (!party.TrySetCreatureAtSlot(targetPartyIndex, moving, out replaced))
            {
                storage.TrySetAt(sourceStorageIndex, moving, out _);
                EndCreatureDrag();
                return;
            }

            if (replaced != null)
            {
                storage.TrySetAt(sourceStorageIndex, replaced, out _);
            }

            selectedCreatureFromParty = true;
            selectedCreaturePartyIndex = Mathf.Clamp(targetPartyIndex, 0, PlayerCreatureParty.MaxPartySize - 1);
            selectedCreatureStorageIndex = -1;
        }
        else if (dragMode == DragMode.CreatureParty)
        {
            int sourcePartyIndex = draggingCreaturePartyIndex;
            if (sourcePartyIndex == targetPartyIndex)
            {
                EndCreatureDrag();
                return;
            }

            CreatureInstance moving;
            if (!party.TryTakeCreatureAtSlot(sourcePartyIndex, out moving) || moving == null)
            {
                EndCreatureDrag();
                return;
            }

            CreatureInstance replaced;
            if (!party.TrySetCreatureAtSlot(targetPartyIndex, moving, out replaced))
            {
                party.TrySetCreatureAtSlot(sourcePartyIndex, moving, out _);
                EndCreatureDrag();
                return;
            }

            if (replaced != null)
            {
                CreatureInstance displaced;
                if (!party.TrySetCreatureAtSlot(sourcePartyIndex, replaced, out displaced))
                {
                    storage.TryStoreCreature(replaced);
                }
                else if (displaced != null)
                {
                    storage.TryStoreCreature(displaced);
                }
            }

            selectedCreatureFromParty = true;
            selectedCreaturePartyIndex = Mathf.Clamp(targetPartyIndex, 0, PlayerCreatureParty.MaxPartySize - 1);
            selectedCreatureStorageIndex = -1;
        }

        EndCreatureDrag();
        RefreshCreatureTab();
    }

    public void DropCreatureIntoNewPartySlot()
    {
        if (!HasActiveCreatureDrag()) return;
        EnsureCreatureSources();
        if (party == null)
        {
            EndCreatureDrag();
            return;
        }

        int nextSlot = party.FindFirstEmptyPartySlotIndex();
        if (nextSlot < 0 || nextSlot >= PlayerCreatureParty.MaxPartySize)
        {
            EndCreatureDrag();
            return;
        }

        DropCreatureOnPartySlot(nextSlot);
    }

    public void EndCreatureDrag()
    {
        if (!HasActiveCreatureDrag()) return;
        dragMode = DragMode.None;
        draggingCreatureStorageIndex = -1;
        draggingCreaturePartyIndex = -1;
        if (draggingIcon != null) draggingIcon.gameObject.SetActive(false);
    }

    void EnsureCreatureSources()
    {
        if (party != null && storage != null) return;

        PlayerMover mover = FindFirstObjectByType<PlayerMover>();
        if (mover == null) return;

        if (party == null)
        {
            party = mover.GetComponent<PlayerCreatureParty>();
            if (party == null) party = mover.gameObject.AddComponent<PlayerCreatureParty>();
        }

        if (storage == null)
        {
            storage = mover.GetComponent<PlayerCreatureStorage>();
            if (storage == null) storage = mover.gameObject.AddComponent<PlayerCreatureStorage>();
        }

        if (storage != null)
        {
            storage.EnsureInitialized(party);
        }
    }

    void BindCreatureEvents()
    {
        if (party != null)
        {
            party.PartyChanged -= HandleCreatureDataChanged;
            party.PartyChanged += HandleCreatureDataChanged;
        }
        if (storage != null)
        {
            storage.StorageChanged -= HandleCreatureDataChanged;
            storage.StorageChanged += HandleCreatureDataChanged;
        }
    }

    void UnbindCreatureEvents()
    {
        if (party != null)
        {
            party.PartyChanged -= HandleCreatureDataChanged;
        }
        if (storage != null)
        {
            storage.StorageChanged -= HandleCreatureDataChanged;
        }
    }

    void HandleCreatureDataChanged()
    {
        RefreshCreatureTab();
    }

    void EnsureTabBarUI()
    {
        if (panelRoot == null) return;

        Transform existing = panelRoot.Find("TabsRoot");
        if (existing == null)
        {
            GameObject go = new GameObject("TabsRoot", typeof(RectTransform), typeof(CanvasRenderer), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(panelRoot, false);
            existing = go.transform;
        }

        tabsRoot = existing as RectTransform;
        if (tabsRoot == null) return;
        tabsRoot.anchorMin = new Vector2(0.5f, 1f);
        tabsRoot.anchorMax = new Vector2(0.5f, 1f);
        tabsRoot.pivot = new Vector2(0.5f, 1f);

        HorizontalLayoutGroup layout = tabsRoot.GetComponent<HorizontalLayoutGroup>();
        if (layout == null) layout = tabsRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        string[] labels = { "Items", "Creatures", "Skills", "Crafting", "Player" };
        if (tabButtons == null || tabButtons.Length != labels.Length)
        {
            tabButtons = new Button[labels.Length];
            tabButtonLabels = new Text[labels.Length];
        }

        for (int i = 0; i < labels.Length; i++)
        {
            Transform t = tabsRoot.Find("Tab_" + labels[i]);
            if (t == null)
            {
                GameObject btnGo = new GameObject("Tab_" + labels[i], typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
                btnGo.transform.SetParent(tabsRoot, false);
                t = btnGo.transform;
            }

            RectTransform rt = t as RectTransform;
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(120f, 38f);
            }

            LayoutElement le = t.GetComponent<LayoutElement>();
            if (le != null)
            {
                le.preferredWidth = 120f;
                le.preferredHeight = 38f;
            }

            Image bg = t.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = slotSprite;
                bg.type = slotSprite != null && slotSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                bg.color = new Color(0.20f, 0.20f, 0.20f, 0.92f);
            }

            Button button = t.GetComponent<Button>();
            int tabIndex = i;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SwitchTab((InventoryTab)tabIndex));

            Transform labelTf = t.Find("Text");
            Text label;
            if (labelTf == null)
            {
                GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
                textGo.transform.SetParent(t, false);
                label = textGo.GetComponent<Text>();
            }
            else
            {
                label = labelTf.GetComponent<Text>();
            }

            if (label != null)
            {
                RectTransform lrt = label.rectTransform;
                lrt.anchorMin = Vector2.zero;
                lrt.anchorMax = Vector2.one;
                lrt.offsetMin = Vector2.zero;
                lrt.offsetMax = Vector2.zero;
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                label.alignment = TextAnchor.MiddleCenter;
                label.fontStyle = FontStyle.Bold;
                label.fontSize = 18;
                label.text = labels[i];
                label.color = Color.white;
            }

            tabButtons[i] = button;
            tabButtonLabels[i] = label;
        }

        // Cleanup any stray controls accidentally parented into tab row from previous runtime layouts.
        Transform strayEvolve = tabsRoot.Find("EvolveButton");
        if (strayEvolve != null)
        {
            if (Application.isPlaying) Destroy(strayEvolve.gameObject);
            else DestroyImmediate(strayEvolve.gameObject);
        }
    }

    void EnsureCreatureTabUI()
    {
        if (panelRoot == null) return;

        Transform tabTf = panelRoot.Find("CreatureTabRoot");
        if (tabTf == null)
        {
            GameObject go = new GameObject("CreatureTabRoot", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(panelRoot, false);
            tabTf = go.transform;
        }
        creatureTabRoot = tabTf as RectTransform;
        if (creatureTabRoot == null) return;
        creatureTabRoot.anchorMin = new Vector2(0.5f, 1f);
        creatureTabRoot.anchorMax = new Vector2(0.5f, 1f);
        creatureTabRoot.pivot = new Vector2(0.5f, 1f);

        Transform leftColumnTf = creatureTabRoot.Find("LeftColumn");
        if (leftColumnTf == null)
        {
            GameObject go = new GameObject("LeftColumn", typeof(RectTransform), typeof(CanvasRenderer), typeof(VerticalLayoutGroup));
            go.transform.SetParent(creatureTabRoot, false);
            leftColumnTf = go.transform;
        }
        RectTransform leftColumnRt = leftColumnTf as RectTransform;
        VerticalLayoutGroup leftLayout = leftColumnTf.GetComponent<VerticalLayoutGroup>();
        leftLayout.spacing = 10f;
        leftLayout.childAlignment = TextAnchor.UpperLeft;
        leftLayout.childControlWidth = false;
        leftLayout.childControlHeight = false;
        leftLayout.childForceExpandWidth = false;
        leftLayout.childForceExpandHeight = false;

        Transform rightPanelTf = creatureTabRoot.Find("RightPanel");
        if (rightPanelTf == null)
        {
            GameObject go = new GameObject("RightPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(creatureTabRoot, false);
            rightPanelTf = go.transform;
        }
        creatureRightPanelRoot = rightPanelTf as RectTransform;
        Image rightPanelBg = rightPanelTf.GetComponent<Image>();
        rightPanelBg.sprite = slotSprite;
        rightPanelBg.type = slotSprite != null && slotSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        rightPanelBg.color = new Color(0f, 0f, 0f, 0.38f);

        Transform gridTf = leftColumnTf.Find("CreatureGrid");
        if (gridTf == null)
        {
            GameObject go = new GameObject("CreatureGrid", typeof(RectTransform), typeof(CanvasRenderer), typeof(GridLayoutGroup));
            go.transform.SetParent(leftColumnTf, false);
            gridTf = go.transform;
        }
        creatureGridRoot = gridTf as RectTransform;
        GridLayoutGroup grid = gridTf.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = gridTf.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.cellSize = new Vector2(slotSize, slotSize);
        grid.spacing = new Vector2(inventoryMenuSlotSpacing, inventoryMenuSlotSpacing);
        grid.childAlignment = TextAnchor.UpperLeft;

        if (creatureSlots == null || creatureSlots.Length != CreatureSlotsPerPage)
        {
            creatureSlots = new CreatureStorageSlotUI[CreatureSlotsPerPage];
        }

        for (int i = 0; i < CreatureSlotsPerPage; i++)
        {
            Transform slotTf = gridTf.Find("CreatureSlot" + (i + 1));
            if (slotTf == null)
            {
                GameObject slotGo = new GameObject("CreatureSlot" + (i + 1), typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                slotGo.transform.SetParent(gridTf, false);
                slotTf = slotGo.transform;
            }

            CreatureStorageSlotUI view = slotTf.GetComponent<CreatureStorageSlotUI>();
            if (view == null) view = slotTf.gameObject.AddComponent<CreatureStorageSlotUI>();
            view.ui = this;
            view.pageLocalIndex = i;

            Image bg = slotTf.GetComponent<Image>();
            bg.sprite = slotSprite;
            bg.type = slotSprite != null && slotSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            bg.color = normalColor;
            view.background = bg;

            Button slotButton = slotTf.GetComponent<Button>();
            if (slotButton == null) slotButton = slotTf.gameObject.AddComponent<Button>();
            slotButton.onClick.RemoveAllListeners();
            int capturedIndex = i;
            slotButton.onClick.AddListener(() => OnCreatureStorageSlotClicked(capturedIndex));

            Transform iconTf = slotTf.Find("Icon");
            if (iconTf == null)
            {
                GameObject go = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(slotTf, false);
                iconTf = go.transform;
            }
            Image icon = iconTf.GetComponent<Image>();
            icon.preserveAspect = true;
            icon.color = Color.white;
            RectTransform iconRt = icon.rectTransform;
            iconRt.anchorMin = new Vector2(0.5f, 0.55f);
            iconRt.anchorMax = new Vector2(0.5f, 0.55f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.sizeDelta = new Vector2(ResolveDisplayIconSize(), ResolveDisplayIconSize());
            view.icon = icon;

            Transform nameTf = slotTf.Find("Name");
            if (nameTf == null)
            {
                GameObject go = new GameObject("Name", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
                go.transform.SetParent(slotTf, false);
                nameTf = go.transform;
            }
            Text name = nameTf.GetComponent<Text>();
            RectTransform nameRt = name.rectTransform;
            nameRt.anchorMin = new Vector2(0f, 0f);
            nameRt.anchorMax = new Vector2(1f, 0.28f);
            nameRt.offsetMin = new Vector2(2f, 0f);
            nameRt.offsetMax = new Vector2(-2f, 0f);
            name.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            name.alignment = TextAnchor.UpperCenter;
            name.fontSize = 10;
            name.color = Color.white;
            view.nameLabel = name;

            Transform levelTf = slotTf.Find("Level");
            if (levelTf == null)
            {
                GameObject go = new GameObject("Level", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
                go.transform.SetParent(slotTf, false);
                levelTf = go.transform;
            }
            Text lvl = levelTf.GetComponent<Text>();
            RectTransform levelRt = lvl.rectTransform;
            levelRt.anchorMin = new Vector2(0f, 0.76f);
            levelRt.anchorMax = new Vector2(1f, 1f);
            levelRt.offsetMin = new Vector2(2f, 0f);
            levelRt.offsetMax = new Vector2(-2f, -2f);
            lvl.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            lvl.alignment = TextAnchor.UpperRight;
            lvl.fontSize = 12;
            lvl.color = Color.white;
            view.levelLabel = lvl;

            creatureSlots[i] = view;
        }

        Transform pagerTf = leftColumnTf.Find("CreaturePager");
        if (pagerTf == null)
        {
            GameObject go = new GameObject("CreaturePager", typeof(RectTransform), typeof(CanvasRenderer), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(leftColumnTf, false);
            pagerTf = go.transform;
        }
        creaturePagerRoot = pagerTf as RectTransform;
        HorizontalLayoutGroup pagerLayout = pagerTf.GetComponent<HorizontalLayoutGroup>();
        pagerLayout.childAlignment = TextAnchor.MiddleCenter;
        pagerLayout.spacing = 12f;
        pagerLayout.childControlWidth = false;
        pagerLayout.childControlHeight = false;
        pagerLayout.childForceExpandWidth = false;
        pagerLayout.childForceExpandHeight = false;

        creaturePrevPageButton = EnsurePagerButton(pagerTf, "Prev", "<", OnPrevCreaturePage);
        creaturePageLabel = EnsurePagerLabel(pagerTf, "PageLabel");
        creatureNextPageButton = EnsurePagerButton(pagerTf, "Next", ">", OnNextCreaturePage);

        Transform legacyPartyTf = creatureRightPanelRoot.Find("PartySlotsRoot");
        if (legacyPartyTf != null) legacyPartyTf.gameObject.SetActive(false);
        EnsureCreatureDetailsUI();
    }

    void EnsureCreatureDetailsUI()
    {
        if (creatureRightPanelRoot == null) return;

        Transform detailsTf = creatureRightPanelRoot.Find("DetailsRoot");
        if (detailsTf == null)
        {
            GameObject go = new GameObject("DetailsRoot", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(creatureRightPanelRoot, false);
            detailsTf = go.transform;
        }
        creatureDetailsRoot = detailsTf as RectTransform;

        Transform spriteTf = creatureDetailsRoot.Find("Sprite");
        if (spriteTf == null)
        {
            GameObject go = new GameObject("Sprite", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(creatureDetailsRoot, false);
            spriteTf = go.transform;
        }
        creatureDetailSprite = spriteTf.GetComponent<Image>();
        creatureDetailSprite.preserveAspect = true;
        creatureDetailSprite.color = Color.white;

        creatureDetailNameText = EnsureDetailText(creatureDetailsRoot, "Name", 18, TextAnchor.UpperLeft, FontStyle.Bold);
        creatureDetailTypesText = EnsureDetailText(creatureDetailsRoot, "Types", 14, TextAnchor.UpperLeft, FontStyle.Normal);
        creatureDetailHpText = EnsureDetailText(creatureDetailsRoot, "HPText", 13, TextAnchor.MiddleLeft, FontStyle.Bold);
        creatureDetailXpText = EnsureDetailText(creatureDetailsRoot, "XPText", 13, TextAnchor.MiddleLeft, FontStyle.Bold);
        Transform contentTf = creatureDetailsRoot.Find("ContentRoot");
        if (contentTf == null)
        {
            GameObject go = new GameObject("ContentRoot", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(creatureDetailsRoot, false);
            contentTf = go.transform;
        }
        creatureDetailContentRoot = contentTf as RectTransform;

        creatureDetailBodyText = EnsureDetailText(creatureDetailContentRoot, "BodyText", 13, TextAnchor.UpperLeft, FontStyle.Normal);
        creatureDetailBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
        creatureDetailBodyText.verticalOverflow = VerticalWrapMode.Overflow;

        creatureEvolveButton = EnsureCreatureEvolveButton(creatureDetailsRoot);
        if (creatureEvolveButton != null)
        {
            Transform labelTf = creatureEvolveButton.transform.Find("Text");
            if (labelTf != null) creatureEvolveButtonLabel = labelTf.GetComponent<Text>();
        }

        EnsureCreatureDetailBars();
        EnsureCreatureDetailSubTabs();
    }

    Button EnsureCreatureEvolveButton(Transform parent)
    {
        Transform tf = parent.Find("EvolveButton");
        if (tf == null)
        {
            GameObject go = new GameObject("EvolveButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            tf = go.transform;
        }

        Image bg = tf.GetComponent<Image>();
        bg.sprite = slotSprite;
        bg.type = slotSprite != null && slotSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        bg.color = new Color(0.20f, 0.20f, 0.24f, 0.95f);

        LayoutElement le = tf.GetComponent<LayoutElement>();
        le.preferredWidth = 132f;
        le.preferredHeight = 26f;

        Button b = tf.GetComponent<Button>();
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(OnEvolveSelectedCreatureClicked);

        Transform labelTf = tf.Find("Text");
        if (labelTf == null)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(tf, false);
            labelTf = go.transform;
        }

        Text t = labelTf.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 13;
        t.alignment = TextAnchor.MiddleCenter;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        t.text = "Evolve";

        RectTransform trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        return b;
    }

    Text EnsureDetailText(RectTransform parent, string name, int fontSize, TextAnchor anchor, FontStyle style)
    {
        Transform tf = parent.Find(name);
        if (tf == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            tf = go.transform;
        }
        Text t = tf.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = fontSize;
        t.alignment = anchor;
        t.fontStyle = style;
        t.color = Color.white;
        return t;
    }

    void EnsureCreatureDetailBars()
    {
        Transform hpBgTf = creatureDetailsRoot.Find("HPBarBG");
        if (hpBgTf == null)
        {
            GameObject go = new GameObject("HPBarBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(creatureDetailsRoot, false);
            hpBgTf = go.transform;
        }
        creatureDetailHpBg = hpBgTf.GetComponent<Image>();
        creatureDetailHpBg.sprite = CreateSolidSprite();
        creatureDetailHpBg.type = Image.Type.Simple;
        creatureDetailHpBg.color = new Color(0f, 0f, 0f, 0.85f);

        Transform hpFillTf = hpBgTf.Find("Fill");
        if (hpFillTf == null)
        {
            GameObject go = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(hpBgTf, false);
            hpFillTf = go.transform;
        }
        creatureDetailHpFill = hpFillTf.GetComponent<Image>();
        creatureDetailHpFill.sprite = CreateSolidSprite();
        creatureDetailHpFill.type = Image.Type.Filled;
        creatureDetailHpFill.fillMethod = Image.FillMethod.Horizontal;
        creatureDetailHpFill.fillOrigin = 0;

        Transform xpBgTf = creatureDetailsRoot.Find("XPBarBG");
        if (xpBgTf == null)
        {
            GameObject go = new GameObject("XPBarBG", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(creatureDetailsRoot, false);
            xpBgTf = go.transform;
        }
        creatureDetailXpBg = xpBgTf.GetComponent<Image>();
        creatureDetailXpBg.sprite = CreateSolidSprite();
        creatureDetailXpBg.type = Image.Type.Simple;
        creatureDetailXpBg.color = new Color(0f, 0f, 0f, 0.85f);

        Transform xpFillTf = xpBgTf.Find("Fill");
        if (xpFillTf == null)
        {
            GameObject go = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(xpBgTf, false);
            xpFillTf = go.transform;
        }
        creatureDetailXpFill = xpFillTf.GetComponent<Image>();
        creatureDetailXpFill.sprite = CreateSolidSprite();
        creatureDetailXpFill.type = Image.Type.Filled;
        creatureDetailXpFill.fillMethod = Image.FillMethod.Horizontal;
        creatureDetailXpFill.fillOrigin = 0;
        creatureDetailXpFill.color = new Color(0.20f, 0.63f, 0.96f, 1f);
    }

    void EnsureCreatureDetailSubTabs()
    {
        Transform tabsTf = creatureDetailsRoot.Find("SubTabs");
        if (tabsTf == null)
        {
            GameObject go = new GameObject("SubTabs", typeof(RectTransform), typeof(CanvasRenderer), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(creatureDetailsRoot, false);
            tabsTf = go.transform;
        }
        creatureSubTabsRoot = tabsTf as RectTransform;
        HorizontalLayoutGroup layout = tabsTf.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        creatureSummaryTabButton = EnsureCreatureSubTabButton(tabsTf, "SummaryTab", "Summary", CreatureDetailSubTab.Summary);
        creatureAttacksTabButton = EnsureCreatureSubTabButton(tabsTf, "AttacksTab", "Attacks", CreatureDetailSubTab.Attacks);
        creatureSoulTraitsTabButton = EnsureCreatureSubTabButton(tabsTf, "SoulTraitsTab", "Soul Traits", CreatureDetailSubTab.SoulTraits);
    }

    Button EnsureCreatureSubTabButton(Transform parent, string name, string label, CreatureDetailSubTab tab)
    {
        Transform tf = parent.Find(name);
        if (tf == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            tf = go.transform;
        }

        Image bg = tf.GetComponent<Image>();
        bg.sprite = slotSprite;
        bg.type = slotSprite != null && slotSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        bg.color = new Color(0.20f, 0.20f, 0.24f, 0.95f);

        LayoutElement le = tf.GetComponent<LayoutElement>();
        le.preferredWidth = name == "SoulTraitsTab" ? 88f : 72f;
        le.preferredHeight = 20f;

        RectTransform rt = tf as RectTransform;
        if (rt != null) rt.sizeDelta = new Vector2(le.preferredWidth, le.preferredHeight);

        Button b = tf.GetComponent<Button>();
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(() => SetCreatureDetailSubTab(tab));

        Transform labelTf = tf.Find("Text");
        if (labelTf == null)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(tf, false);
            labelTf = go.transform;
        }
        Text t = labelTf.GetComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = 12;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = label;
        RectTransform trt = t.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
        return b;
    }

    void EnsurePlaceholderTabUI()
    {
        if (panelRoot == null) return;
        Transform existing = panelRoot.Find("PlaceholderTabRoot");
        if (existing == null)
        {
            GameObject go = new GameObject("PlaceholderTabRoot", typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(panelRoot, false);
            existing = go.transform;
        }
        placeholderRoot = existing as RectTransform;
        if (placeholderRoot == null) return;
        placeholderRoot.anchorMin = new Vector2(0.5f, 1f);
        placeholderRoot.anchorMax = new Vector2(0.5f, 1f);
        placeholderRoot.pivot = new Vector2(0.5f, 1f);

        Transform labelTf = placeholderRoot.Find("Label");
        if (labelTf == null)
        {
            GameObject go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(placeholderRoot, false);
            labelTf = go.transform;
        }
        placeholderLabel = labelTf.GetComponent<Text>();
        RectTransform rt = placeholderLabel.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        placeholderLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        placeholderLabel.alignment = TextAnchor.MiddleCenter;
        placeholderLabel.fontStyle = FontStyle.Bold;
        placeholderLabel.fontSize = 24;
        placeholderLabel.color = new Color(1f, 1f, 1f, 0.86f);
    }

    Button EnsurePagerButton(Transform parent, string name, string label, UnityEngine.Events.UnityAction onClick)
    {
        Transform existing = parent.Find(name);
        if (existing == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            existing = go.transform;
        }

        Image bg = existing.GetComponent<Image>();
        bg.sprite = slotSprite;
        bg.type = slotSprite != null && slotSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
        bg.color = new Color(0.22f, 0.22f, 0.22f, 0.9f);

        LayoutElement le = existing.GetComponent<LayoutElement>();
        le.preferredWidth = 6f;
        le.preferredHeight = 4f;

        RectTransform btnRt = existing as RectTransform;
        if (btnRt != null)
        {
            btnRt.sizeDelta = new Vector2(6f, 4f);
        }

        Button b = existing.GetComponent<Button>();
        b.onClick.RemoveAllListeners();
        b.onClick.AddListener(onClick);

        Transform textTf = existing.Find("Text");
        if (textTf == null)
        {
            GameObject go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.transform.SetParent(existing, false);
            textTf = go.transform;
        }

        Text t = textTf.GetComponent<Text>();
        RectTransform trt = t.rectTransform;
        trt.anchorMin = new Vector2(0.5f, 0.5f);
        trt.anchorMax = new Vector2(0.5f, 0.5f);
        trt.pivot = new Vector2(0.5f, 0.5f);
        trt.anchoredPosition = Vector2.zero;
        trt.sizeDelta = new Vector2(28f, 28f);
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = 18;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        t.text = label;
        return b;
    }

    Text EnsurePagerLabel(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing == null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(LayoutElement), typeof(Outline));
            go.transform.SetParent(parent, false);
            existing = go.transform;
        }

        LayoutElement le = existing.GetComponent<LayoutElement>();
        le.preferredWidth = 210f;
        le.preferredHeight = 34f;

        Text t = existing.GetComponent<Text>();
        RectTransform rt = t.rectTransform;
        rt.sizeDelta = new Vector2(210f, 34f);
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.alignment = TextAnchor.MiddleCenter;
        t.fontSize = 16;
        t.fontStyle = FontStyle.Bold;
        t.color = Color.white;
        return t;
    }

    void ApplyPanelBackdropBlur()
    {
        if (panelRoot == null) return;

        Image panelImage = panelRoot.GetComponent<Image>();
        if (panelImage != null)
        {
            panelImage.color = new Color(0f, 0f, 0f, 0f);
            panelImage.raycastTarget = true;
        }

        for (int i = 0; i < 3; i++)
        {
            while (backdropBlurLayers.Count <= i) backdropBlurLayers.Add(null);
            if (backdropBlurLayers[i] == null)
            {
                Transform existing = panelRoot.Find("BackdropBlur_" + i);
                Image img = existing != null ? existing.GetComponent<Image>() : null;
                if (img == null)
                {
                    GameObject go = new GameObject("BackdropBlur_" + i, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    go.transform.SetParent(panelRoot, false);
                    img = go.GetComponent<Image>();
                }
                backdropBlurLayers[i] = img;
            }

            Image layer = backdropBlurLayers[i];
            if (layer == null) continue;
            RectTransform rt = layer.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            layer.raycastTarget = false;
            layer.sprite = null;
            layer.type = Image.Type.Simple;
            float alpha = i == 0 ? 0.30f : (i == 1 ? 0.18f : 0.10f);
            layer.color = new Color(0f, 0f, 0f, alpha);
            layer.transform.SetSiblingIndex(i);
        }
    }

    void ApplyInventoryPanelLayout()
    {
        if (panelRoot == null) return;

        int cols = inventory != null ? Mathf.Max(1, inventory.bagColumns) : 9;
        int rows = inventory != null ? Mathf.Max(1, inventory.bagRows) : 3;
        int gap = Mathf.Max(0, inventoryMenuSlotSpacing);
        float gridW = (cols * slotSize) + ((cols - 1) * gap);
        float gridH = (rows * slotSize) + ((rows - 1) * gap);
        float extraBelow = (2f * slotSize) + gap;
        float tabH = 42f;
        float panelW = gridW + 72f;
        float panelH = tabH + 20f + gridH + extraBelow + 48f;
        panelRoot.sizeDelta = new Vector2(panelW, panelH);

        if (tabsRoot != null)
        {
            tabsRoot.anchoredPosition = new Vector2(0f, -10f);
            tabsRoot.sizeDelta = new Vector2(panelW - 28f, tabH);
        }

        if (bagRoot != null)
        {
            bagRoot.anchorMin = new Vector2(0.5f, 1f);
            bagRoot.anchorMax = new Vector2(0.5f, 1f);
            bagRoot.pivot = new Vector2(0.5f, 1f);
            bagRoot.anchoredPosition = new Vector2(0f, -(tabH + 28f));
            bagRoot.sizeDelta = new Vector2(gridW, gridH);
        }

        if (itemDetailsRoot != null)
        {
            itemDetailsRoot.anchorMin = new Vector2(0.5f, 0f);
            itemDetailsRoot.anchorMax = new Vector2(0.5f, 0f);
            itemDetailsRoot.pivot = new Vector2(0.5f, 0f);
            float detailsHeight = Mathf.Max(120f, extraBelow + 24f);
            itemDetailsRoot.anchoredPosition = new Vector2(0f, 14f);
            itemDetailsRoot.sizeDelta = new Vector2(gridW + 32f, detailsHeight);

            if (itemDetailsIcon != null)
            {
                RectTransform rt = itemDetailsIcon.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(16f, 14f);
                float iconLarge = Mathf.Max(96f, slotSize * 1.35f);
                rt.sizeDelta = new Vector2(iconLarge, iconLarge);
            }
            if (itemDetailsNameText != null)
            {
                RectTransform rt = itemDetailsNameText.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(136f, -10f);
                rt.sizeDelta = new Vector2(-152f, 34f);
            }
            if (itemDetailsCountText != null)
            {
                RectTransform rt = itemDetailsCountText.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(136f, -42f);
                rt.sizeDelta = new Vector2(-152f, 26f);
            }

            float buttonY = 20f;
            if (itemUseButton != null)
            {
                RectTransform rt = itemUseButton.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(136f, buttonY);
            }
            if (itemDropButton != null)
            {
                RectTransform rt = itemDropButton.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(264f, buttonY);
            }
            if (itemDiscardButton != null)
            {
                RectTransform rt = itemDiscardButton.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(392f, buttonY);
            }
        }

        if (creatureTabRoot != null)
        {
            creatureTabRoot.sizeDelta = new Vector2(panelW - 48f, panelH - (tabH + 40f));
            creatureTabRoot.anchoredPosition = new Vector2(0f, -(tabH + 18f));
        }

        RectTransform leftColumnRt = creatureGridRoot != null ? creatureGridRoot.transform.parent as RectTransform : null;
        if (leftColumnRt != null && creatureTabRoot != null)
        {
            leftColumnRt.anchorMin = new Vector2(0f, 1f);
            leftColumnRt.anchorMax = new Vector2(0f, 1f);
            leftColumnRt.pivot = new Vector2(0f, 1f);
            leftColumnRt.anchoredPosition = Vector2.zero;
        }

        if (creatureGridRoot != null)
        {
            float creatureGridW = (5 * slotSize) + (4 * gap);
            float creatureGridH = (5 * slotSize) + (4 * gap);
            creatureGridRoot.anchorMin = new Vector2(0f, 1f);
            creatureGridRoot.anchorMax = new Vector2(0f, 1f);
            creatureGridRoot.pivot = new Vector2(0f, 1f);
            creatureGridRoot.anchoredPosition = Vector2.zero;
            creatureGridRoot.sizeDelta = new Vector2(creatureGridW, creatureGridH);

            GridLayoutGroup grid = creatureGridRoot.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.cellSize = new Vector2(slotSize, slotSize);
                grid.spacing = new Vector2(gap, gap);
            }

            if (leftColumnRt != null)
            {
                leftColumnRt.sizeDelta = new Vector2(creatureGridW, creatureGridH + 52f);
            }
        }

        if (creaturePagerRoot != null)
        {
            creaturePagerRoot.anchorMin = new Vector2(0f, 1f);
            creaturePagerRoot.anchorMax = new Vector2(0f, 1f);
            creaturePagerRoot.pivot = new Vector2(0f, 1f);
            float y = creatureGridRoot != null ? -(creatureGridRoot.sizeDelta.y + 10f) : -10f;
            creaturePagerRoot.anchoredPosition = new Vector2(0f, y);
            creaturePagerRoot.sizeDelta = new Vector2(350f, 38f);
        }

        if (creatureRightPanelRoot != null && creatureTabRoot != null && creatureGridRoot != null)
        {
            float leftW = creatureGridRoot.sizeDelta.x;
            float rightW = Mathf.Max(260f, creatureTabRoot.sizeDelta.x - leftW - 16f);
            float rightH = Mathf.Max(200f, creatureTabRoot.sizeDelta.y);

            creatureRightPanelRoot.anchorMin = new Vector2(0f, 1f);
            creatureRightPanelRoot.anchorMax = new Vector2(0f, 1f);
            creatureRightPanelRoot.pivot = new Vector2(0f, 1f);
            creatureRightPanelRoot.anchoredPosition = new Vector2(leftW + 16f, 0f);
            creatureRightPanelRoot.sizeDelta = new Vector2(rightW, rightH);
        }

        if (creatureDetailsRoot != null && creatureRightPanelRoot != null)
        {
            creatureDetailsRoot.anchorMin = new Vector2(0f, 0f);
            creatureDetailsRoot.anchorMax = new Vector2(1f, 1f);
            creatureDetailsRoot.pivot = new Vector2(0f, 1f);
            creatureDetailsRoot.anchoredPosition = Vector2.zero;
            creatureDetailsRoot.sizeDelta = new Vector2(-20f, -20f);

            if (creatureDetailSprite != null)
            {
                RectTransform rt = creatureDetailSprite.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(0f, 0f);
                rt.sizeDelta = new Vector2(124f, 124f);
            }
            if (creatureDetailNameText != null)
            {
                RectTransform rt = creatureDetailNameText.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(132f, -2f);
                rt.sizeDelta = new Vector2(-132f, 24f);
            }
            if (creatureDetailTypesText != null)
            {
                RectTransform rt = creatureDetailTypesText.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(132f, -28f);
                rt.sizeDelta = new Vector2(-132f, 20f);
            }

            if (creatureDetailHpBg != null)
            {
                RectTransform rt = creatureDetailHpBg.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(132f, -54f);
                rt.sizeDelta = new Vector2(-132f, 12f);
                if (creatureDetailHpFill != null)
                {
                    RectTransform frt = creatureDetailHpFill.rectTransform;
                    frt.anchorMin = Vector2.zero;
                    frt.anchorMax = Vector2.one;
                    frt.offsetMin = Vector2.zero;
                    frt.offsetMax = Vector2.zero;
                }
            }
            if (creatureDetailHpText != null)
            {
                RectTransform rt = creatureDetailHpText.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(132f, -68f);
                rt.sizeDelta = new Vector2(-132f, 18f);
            }

            if (creatureDetailXpBg != null)
            {
                RectTransform rt = creatureDetailXpBg.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(132f, -90f);
                rt.sizeDelta = new Vector2(-132f, 8f);
                if (creatureDetailXpFill != null)
                {
                    RectTransform frt = creatureDetailXpFill.rectTransform;
                    frt.anchorMin = Vector2.zero;
                    frt.anchorMax = Vector2.one;
                    frt.offsetMin = Vector2.zero;
                    frt.offsetMax = Vector2.zero;
                }
            }
            if (creatureDetailXpText != null)
            {
                RectTransform rt = creatureDetailXpText.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(132f, -102f);
                rt.sizeDelta = new Vector2(-132f, 18f);
            }

            if (creatureSubTabsRoot != null)
            {
                creatureSubTabsRoot.anchorMin = new Vector2(0f, 1f);
                creatureSubTabsRoot.anchorMax = new Vector2(1f, 1f);
                creatureSubTabsRoot.pivot = new Vector2(0f, 1f);
                creatureSubTabsRoot.anchoredPosition = new Vector2(12f, -126f);
                creatureSubTabsRoot.sizeDelta = new Vector2(0f, 26f);
            }
            if (creatureDetailContentRoot != null)
            {
                creatureDetailContentRoot.anchorMin = new Vector2(0f, 0f);
                creatureDetailContentRoot.anchorMax = new Vector2(1f, 1f);
                creatureDetailContentRoot.offsetMin = new Vector2(8f, 42f);
                creatureDetailContentRoot.offsetMax = new Vector2(-8f, -158f);
            }
            if (creatureDetailBodyText != null)
            {
                RectTransform rt = creatureDetailBodyText.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.offsetMin = new Vector2(0f, 0f);
                rt.offsetMax = new Vector2(0f, 0f);
            }
            if (creatureEvolveButton != null)
            {
                RectTransform rt = creatureEvolveButton.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 0f);
                rt.pivot = new Vector2(0f, 0f);
                rt.anchoredPosition = new Vector2(8f, 8f);
                rt.sizeDelta = new Vector2(132f, 26f);
                rt.SetAsLastSibling();
            }
        }

        if (placeholderRoot != null)
        {
            placeholderRoot.anchoredPosition = new Vector2(0f, -(tabH + 28f));
            placeholderRoot.sizeDelta = new Vector2(panelW - 44f, panelH - (tabH + 50f));
        }

        for (int i = 0; i < backdropBlurLayers.Count; i++)
        {
            Image layer = backdropBlurLayers[i];
            if (layer == null) continue;
            float pad = i * 18f;
            layer.rectTransform.sizeDelta = new Vector2(panelW + pad, panelH + pad);
        }
    }

    void SwitchTab(InventoryTab tab)
    {
        activeTab = tab;

        bool showItems = tab == InventoryTab.Items;
        bool showCreatures = tab == InventoryTab.Creatures;
        bool showPlaceholder = !showItems && !showCreatures;

        if (bagRoot != null) bagRoot.gameObject.SetActive(showItems);
        if (itemDetailsRoot != null) itemDetailsRoot.gameObject.SetActive(showItems);
        if (creatureTabRoot != null) creatureTabRoot.gameObject.SetActive(showCreatures);
        if (placeholderRoot != null) placeholderRoot.gameObject.SetActive(showPlaceholder);

        if (placeholderLabel != null && showPlaceholder)
        {
            placeholderLabel.text = tab + " tab coming soon.";
        }

        if (showCreatures)
        {
            RefreshCreatureTab();
        }

        UpdateTabButtonStates();
    }

    void UpdateTabButtonStates()
    {
        if (tabButtons == null) return;
        for (int i = 0; i < tabButtons.Length; i++)
        {
            Button b = tabButtons[i];
            if (b == null) continue;
            Image bg = b.GetComponent<Image>();
            if (bg != null)
            {
                bool selected = i == (int)activeTab;
                bg.color = selected ? new Color(0.60f, 0.55f, 0.25f, 0.96f) : new Color(0.20f, 0.20f, 0.20f, 0.92f);
            }
        }
    }

    void RefreshCreatureTab()
    {
        EnsureCreatureSources();
        if (creatureSlots == null || creatureSlots.Length == 0) return;
        EnsureValidCreatureSelection();

        int pageCount = storage != null ? Mathf.Max(1, storage.pageCount) : 30;
        creaturePageIndex = Mathf.Clamp(creaturePageIndex, 0, pageCount - 1);
        int baseIndex = creaturePageIndex * CreatureSlotsPerPage;

        for (int i = 0; i < creatureSlots.Length; i++)
        {
            CreatureStorageSlotUI slot = creatureSlots[i];
            if (slot == null) continue;
            CreatureInstance instance = storage != null ? storage.GetAt(baseIndex + i) : null;
            bool isSelected = !selectedCreatureFromParty && selectedCreatureStorageIndex == (baseIndex + i);
            ApplyCreatureSlot(slot, instance, isSelected);
        }

        if (creaturePageLabel != null)
        {
            int displayPage = creaturePageIndex + 1;
            creaturePageLabel.text = "Page " + displayPage + " / " + pageCount;
        }

        if (creaturePrevPageButton != null) creaturePrevPageButton.interactable = creaturePageIndex > 0;
        if (creatureNextPageButton != null) creatureNextPageButton.interactable = creaturePageIndex < pageCount - 1;

        RefreshCreatureDetailPanel();
    }

    void EnsureValidCreatureSelection()
    {
        if (selectedCreatureFromParty)
        {
            if (party != null && party.GetCreatureAt(selectedCreaturePartyIndex) != null) return;
            selectedCreatureFromParty = false;
            selectedCreaturePartyIndex = -1;
        }

        if (!selectedCreatureFromParty)
        {
            if (storage != null && storage.GetAt(selectedCreatureStorageIndex) != null) return;
            selectedCreatureStorageIndex = -1;
        }

        if (party != null && party.PartyCount > 0)
        {
            selectedCreatureFromParty = true;
            selectedCreaturePartyIndex = Mathf.Clamp(party.ActivePartyIndex, 0, Mathf.Max(0, party.PartyCount - 1));
            return;
        }

        if (storage != null)
        {
            for (int i = 0; i < storage.Capacity; i++)
            {
                if (storage.GetAt(i) == null) continue;
                selectedCreatureFromParty = false;
                selectedCreatureStorageIndex = i;
                return;
            }
        }
    }

    void ApplyCreatureSlot(CreatureStorageSlotUI slot, CreatureInstance instance, bool isSelected)
    {
        if (slot == null) return;
        if (slot.background != null)
        {
            slot.background.sprite = isSelected ? selectedSprite : slotSprite;
            Sprite bg = isSelected ? selectedSprite : slotSprite;
            slot.background.type = bg != null && bg.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            slot.background.color = isSelected ? selectedColor : normalColor;
        }

        if (instance == null)
        {
            if (slot.icon != null)
            {
                slot.icon.sprite = null;
                slot.icon.enabled = false;
            }
            if (slot.nameLabel != null) slot.nameLabel.text = string.Empty;
            if (slot.levelLabel != null) slot.levelLabel.text = string.Empty;
            return;
        }

        CreatureDefinition def = CreatureRegistry.Get(instance.definitionID);
        if (slot.icon != null)
        {
            slot.icon.sprite = def != null ? def.sprite : null;
            slot.icon.enabled = slot.icon.sprite != null;
            slot.icon.color = Color.white;
        }

        if (slot.nameLabel != null)
        {
            string n = instance.DisplayName;
            if (n.Length > 10) n = n.Substring(0, 10);
            slot.nameLabel.text = n;
        }
        if (slot.levelLabel != null)
        {
            slot.levelLabel.text = "Lv " + Mathf.Max(1, instance.level);
        }
    }

    public void OnCreatureStorageSlotClicked(int pageLocalIndex)
    {
        EnsureCreatureSources();
        int global = (creaturePageIndex * CreatureSlotsPerPage) + pageLocalIndex;
        CreatureInstance selected = storage != null ? storage.GetAt(global) : null;
        if (selected == null) return;
        selectedCreatureFromParty = false;
        selectedCreatureStorageIndex = global;
        selectedCreaturePartyIndex = -1;
        RefreshCreatureTab();
    }

    public void OnCreaturePartySlotClicked(int partyIndex)
    {
        EnsureCreatureSources();
        CreatureInstance selected = party != null ? party.GetCreatureAt(partyIndex) : null;
        if (selected == null) return;
        selectedCreatureFromParty = true;
        selectedCreaturePartyIndex = partyIndex;
        selectedCreatureStorageIndex = -1;
        RefreshCreatureTab();
    }

    void SetCreatureDetailSubTab(CreatureDetailSubTab tab)
    {
        activeCreatureDetailSubTab = tab;
        RefreshCreatureDetailPanel();
    }

    CreatureInstance ResolveSelectedCreatureForDetails(out CreatureDefinition def)
    {
        def = null;
        EnsureCreatureSources();

        CreatureInstance instance = null;
        if (selectedCreatureFromParty)
        {
            instance = party != null ? party.GetCreatureAt(selectedCreaturePartyIndex) : null;
        }
        else
        {
            instance = storage != null ? storage.GetAt(selectedCreatureStorageIndex) : null;
        }

        if (instance == null)
        {
            if (party != null && party.PartyCount > 0)
            {
                selectedCreatureFromParty = true;
                selectedCreaturePartyIndex = 0;
                selectedCreatureStorageIndex = -1;
                instance = party.GetCreatureAt(0);
            }
            else if (storage != null)
            {
                for (int i = 0; i < storage.Capacity; i++)
                {
                    CreatureInstance c = storage.GetAt(i);
                    if (c == null) continue;
                    selectedCreatureFromParty = false;
                    selectedCreatureStorageIndex = i;
                    selectedCreaturePartyIndex = -1;
                    instance = c;
                    break;
                }
            }
        }

        if (instance != null) def = CreatureRegistry.Get(instance.definitionID);
        return instance;
    }

    void RefreshCreatureDetailPanel()
    {
        if (creatureDetailsRoot == null) return;
        CreatureDefinition def;
        CreatureInstance instance = ResolveSelectedCreatureForDetails(out def);

        Color selectedTab = new Color(0.58f, 0.52f, 0.24f, 0.95f);
        Color normalTab = new Color(0.20f, 0.20f, 0.24f, 0.95f);
        if (creatureSummaryTabButton != null) creatureSummaryTabButton.GetComponent<Image>().color = activeCreatureDetailSubTab == CreatureDetailSubTab.Summary ? selectedTab : normalTab;
        if (creatureAttacksTabButton != null) creatureAttacksTabButton.GetComponent<Image>().color = activeCreatureDetailSubTab == CreatureDetailSubTab.Attacks ? selectedTab : normalTab;
        if (creatureSoulTraitsTabButton != null) creatureSoulTraitsTabButton.GetComponent<Image>().color = activeCreatureDetailSubTab == CreatureDetailSubTab.SoulTraits ? selectedTab : normalTab;

        if (instance == null || def == null)
        {
            if (creatureDetailSprite != null) { creatureDetailSprite.sprite = null; creatureDetailSprite.enabled = false; }
            if (creatureDetailNameText != null) creatureDetailNameText.text = "No creature selected";
            if (creatureDetailTypesText != null) creatureDetailTypesText.text = string.Empty;
            if (creatureDetailHpFill != null) creatureDetailHpFill.fillAmount = 0f;
            if (creatureDetailXpFill != null) creatureDetailXpFill.fillAmount = 0f;
            if (creatureDetailHpText != null) creatureDetailHpText.text = string.Empty;
            if (creatureDetailXpText != null) creatureDetailXpText.text = string.Empty;
            if (creatureDetailBodyText != null) creatureDetailBodyText.text = string.Empty;
            if (creatureEvolveButton != null) creatureEvolveButton.interactable = false;
            if (creatureEvolveButtonLabel != null) creatureEvolveButtonLabel.text = "Evolve";
            return;
        }

        if (creatureDetailSprite != null)
        {
            creatureDetailSprite.sprite = def.sprite;
            creatureDetailSprite.enabled = creatureDetailSprite.sprite != null;
        }

        int level = Mathf.Max(1, instance.level);
        int maxHp = Mathf.Max(1, CreatureInstanceFactory.ComputeMaxHP(def, instance.soulTraits, level));
        int currHp = Mathf.Clamp(instance.currentHP, 0, maxHp);
        float hp01 = maxHp > 0 ? (float)currHp / maxHp : 0f;
        float xp01 = CreatureExperienceSystem.GetLevelProgress01(instance, def);
        int xpFloor = CreatureExperienceSystem.GetTotalXpForLevel(level, def);
        int xpCeil = CreatureExperienceSystem.GetTotalXpForLevel(Mathf.Min(CreatureExperienceSystem.MaxLevel, level + 1), def);
        int xpSpan = Mathf.Max(1, xpCeil - xpFloor);
        int xpSegment = Mathf.Clamp(instance.totalExperience - xpFloor, 0, xpSpan);

        if (creatureDetailNameText != null) creatureDetailNameText.text = instance.DisplayName + "  Lv " + level;
        if (creatureDetailTypesText != null) creatureDetailTypesText.text = string.Join(" / ", def.GetAllTypes());
        if (creatureDetailHpFill != null)
        {
            creatureDetailHpFill.fillAmount = hp01;
            creatureDetailHpFill.color = ResolveHpColor(hp01);
        }
        if (creatureDetailXpFill != null)
        {
            creatureDetailXpFill.fillAmount = xp01;
            creatureDetailXpFill.color = new Color(0.20f, 0.63f, 0.96f, 1f);
        }
        if (creatureDetailHpText != null) creatureDetailHpText.text = "HP  " + currHp + " / " + maxHp;
        if (creatureDetailXpText != null)
        {
            if (level >= CreatureExperienceSystem.MaxLevel) creatureDetailXpText.text = "XP  MAX";
            else creatureDetailXpText.text = "XP  " + xpSegment + " / " + xpSpan;
        }

        if (creatureDetailBodyText != null)
        {
            creatureDetailBodyText.text = BuildCreatureDetailBody(def, instance);
        }

        bool canEvolve = IsCreatureEligibleForEvolution(instance, def);
        if (creatureEvolveButton != null)
        {
            creatureEvolveButton.interactable = canEvolve;
            Image bg = creatureEvolveButton.GetComponent<Image>();
            if (bg != null)
            {
                bg.color = canEvolve ? new Color(0.33f, 0.58f, 0.28f, 0.95f) : new Color(0.25f, 0.25f, 0.25f, 0.75f);
            }
        }
        if (creatureEvolveButtonLabel != null)
        {
            creatureEvolveButtonLabel.text = canEvolve ? "Evolve" : "Evolve (Locked)";
        }
    }

    Color ResolveHpColor(float ratio)
    {
        if (ratio > 0.75f) return new Color(0.20f, 0.82f, 0.24f, 1f);
        if (ratio > 0.50f) return new Color(0.97f, 0.88f, 0.20f, 1f);
        if (ratio > 0.25f) return new Color(1.00f, 0.62f, 0.16f, 1f);
        return new Color(0.90f, 0.18f, 0.18f, 1f);
    }

    string BuildCreatureDetailBody(CreatureDefinition def, CreatureInstance instance)
    {
        if (def == null || instance == null) return string.Empty;
        StringBuilder sb = new StringBuilder(512);
        int level = Mathf.Max(1, instance.level);

        if (activeCreatureDetailSubTab == CreatureDetailSubTab.Summary)
        {
            int atk = CreatureInstanceFactory.ComputeAttack(def, instance.soulTraits, level);
            int deff = CreatureInstanceFactory.ComputeDefense(def, instance.soulTraits, level);
            int spd = CreatureInstanceFactory.ComputeSpeed(def, instance.soulTraits, level);
            sb.AppendLine(def.description);
            sb.AppendLine();
            sb.AppendLine("Rarity: " + def.rarityTier);
            sb.AppendLine("Stage: " + def.evolutionStage);
            sb.AppendLine("Atk: " + atk + "  Def: " + deff + "  Spd: " + spd);
            sb.AppendLine("Battles: " + Mathf.Max(0, instance.totalBattles));
        }
        else if (activeCreatureDetailSubTab == CreatureDetailSubTab.Attacks)
        {
            for (int i = 0; i < 4; i++)
            {
                MoveDefinition move = def.GetMoveForSlot(i);
                if (move == null)
                {
                    sb.AppendLine((i + 1) + ". ---");
                    continue;
                }

                int ppCurrent = instance.currentPP != null && i < instance.currentPP.Length ? Mathf.Max(0, instance.currentPP[i]) : move.maxPP;
                sb.Append((i + 1) + ". ");
                sb.Append(move.displayName);
                sb.Append("  ");
                sb.Append(move.baseDamage <= 0 ? "Status" : ("Pow " + move.baseDamage));
                sb.Append("  ");
                sb.Append("PP " + ppCurrent + "/" + Mathf.Max(0, move.maxPP));
                sb.Append("  ");
                sb.Append(move.moveType);
                sb.AppendLine();
            }
        }
        else
        {
            SoulTraitValues st = instance.soulTraits;
            sb.AppendLine("Vitality Spark: " + st.vitalitySpark);
            sb.AppendLine("Strike Essence: " + st.strikeEssence);
            sb.AppendLine("Ward Essence: " + st.wardEssence);
            sb.AppendLine("Gale Essence: " + st.galeEssence);
            sb.AppendLine("Focus Essence: " + st.focusEssence);
            sb.AppendLine("Soul Depth: " + st.soulDepth);
        }

        return sb.ToString().TrimEnd();
    }

    bool IsCreatureEligibleForEvolution(CreatureInstance instance, CreatureDefinition def)
    {
        if (instance == null || def == null) return false;
        if (def.nextEvolution == null) return false;

        int level = Mathf.Max(1, instance.level);
        int battles = Mathf.Max(0, instance.totalBattles);

        switch (def.evolutionTrigger)
        {
            case EvolutionTrigger.LevelThreshold:
                return level >= Mathf.Max(1, def.evolutionLevel);
            case EvolutionTrigger.LevelPlusCondition:
                return level >= Mathf.Max(1, def.evolutionLevel) && battles >= Mathf.Max(0, def.evolutionBattleCount);
            case EvolutionTrigger.SpecialItem:
                return HasEvolutionRelic(def.evolutionItem);
            default:
                return false;
        }
    }

    bool HasEvolutionRelic(EvolutionRelic relic)
    {
        if (relic == EvolutionRelic.None) return true;
        if (inventory == null) return false;

        string token = NormalizeItemToken(relic.ToString());
        if (ContainsItemToken(inventory.hotbar, token)) return true;
        if (ContainsItemToken(inventory.bag, token)) return true;
        return false;
    }

    bool ConsumeEvolutionRelic(EvolutionRelic relic)
    {
        if (relic == EvolutionRelic.None) return true;
        if (inventory == null) return false;

        string token = NormalizeItemToken(relic.ToString());
        if (TryConsumeItemToken(inventory.hotbar, token)) { inventory.NotifyChanged(); return true; }
        if (TryConsumeItemToken(inventory.bag, token)) { inventory.NotifyChanged(); return true; }
        return false;
    }

    bool ContainsItemToken(InventorySlot[] slots, string token)
    {
        if (slots == null) return false;
        for (int i = 0; i < slots.Length; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.IsEmpty() || slot.item == null) continue;
            if (ItemMatchesToken(slot.item, token)) return true;
        }
        return false;
    }

    bool TryConsumeItemToken(InventorySlot[] slots, string token)
    {
        if (slots == null) return false;
        for (int i = 0; i < slots.Length; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.IsEmpty() || slot.item == null) continue;
            if (!ItemMatchesToken(slot.item, token)) continue;
            slot.count = Mathf.Max(0, slot.count - 1);
            if (slot.count <= 0) slot.Clear();
            return true;
        }
        return false;
    }

    bool ItemMatchesToken(InventoryItemData item, string token)
    {
        if (item == null) return false;
        string id = NormalizeItemToken(item.itemId);
        string name = NormalizeItemToken(item.displayName);
        return id == token || name == token;
    }

    string NormalizeItemToken(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;
        StringBuilder sb = new StringBuilder(raw.Length);
        for (int i = 0; i < raw.Length; i++)
        {
            char c = raw[i];
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    void OnEvolveSelectedCreatureClicked()
    {
        CreatureDefinition def;
        CreatureInstance instance = ResolveSelectedCreatureForDetails(out def);
        if (instance == null || def == null || def.nextEvolution == null) return;
        if (!IsCreatureEligibleForEvolution(instance, def)) return;

        if (def.evolutionTrigger == EvolutionTrigger.SpecialItem && !ConsumeEvolutionRelic(def.evolutionItem))
        {
            return;
        }

        CreatureDefinition next = def.nextEvolution;
        int level = Mathf.Max(1, instance.level);
        int oldMax = Mathf.Max(1, CreatureInstanceFactory.ComputeMaxHP(def, instance.soulTraits, level));
        int oldCurrent = Mathf.Clamp(instance.currentHP, 0, oldMax);
        float hpRatio = oldMax > 0 ? (float)oldCurrent / oldMax : 1f;

        instance.definitionID = CreatureRegistry.CanonicalizeCreatureID(next.creatureID);
        CreatureExperienceSystem.EnsureExperienceBaseline(instance, next);
        int newMax = Mathf.Max(1, CreatureInstanceFactory.ComputeMaxHP(next, instance.soulTraits, level));
        if (oldCurrent <= 0) instance.currentHP = 0;
        else instance.currentHP = Mathf.Clamp(Mathf.RoundToInt(newMax * hpRatio), 1, newMax);
        CreatureInstanceFactory.RefillPP(next, instance);

        if (party != null) party.NotifyPartyChanged();
        Refresh();
    }

    void OnPrevCreaturePage()
    {
        int maxPages = storage != null ? Mathf.Max(1, storage.pageCount) : 30;
        creaturePageIndex = Mathf.Clamp(creaturePageIndex - 1, 0, maxPages - 1);
        RefreshCreatureTab();
    }

    void OnNextCreaturePage()
    {
        int maxPages = storage != null ? Mathf.Max(1, storage.pageCount) : 30;
        creaturePageIndex = Mathf.Clamp(creaturePageIndex + 1, 0, maxPages - 1);
        RefreshCreatureTab();
    }

    int ResolveDisplayIconSize()
    {
        return Mathf.Max(1, Mathf.RoundToInt(iconSize * IconDisplayScale));
    }

    void NormalizeVisualSettings()
    {
        // Auto-migrate legacy scene values so old serialized data can't keep the hotbar tiny/white.
        if (slotSize < MinRecommendedSlotSize) slotSize = MinRecommendedSlotSize;

        int maxIcon = Mathf.Max(1, slotSize - 8);
        if (iconSize <= 0 || iconSize > maxIcon)
        {
            iconSize = Mathf.Min(DefaultRecommendedIconSize, maxIcon);
        }

        if (IsNearlyWhite(normalColor))
        {
            normalColor = DefaultSlotNormalColor;
        }

        if (spacing < 0) spacing = 0;
        if (inventoryMenuSlotSpacing < 0) inventoryMenuSlotSpacing = 0;
        if (slotInnerPadding < 0) slotInnerPadding = 0;
        // Keep slot interior consistently black at the requested opacity.
        emptySlotFillColor = DefaultEmptySlotFillColor;
        EnsureSlotSprites();
    }

    bool IsNearlyWhite(Color c)
    {
        return c.a > 0.95f && c.r > 0.95f && c.g > 0.95f && c.b > 0.95f;
    }

    void EnsureSlotSprites()
    {
        // Always use transparent-center slot sprites so the inner fill controls opacity.
        slotSprite = CreateSlotSprite(new Color32(150, 150, 150, 255));
        selectedSprite = CreateSlotSprite(new Color32(255, 215, 90, 255));
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        NormalizeVisualSettings();
    }
#endif
}

public class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public InventoryUI ui;
    public bool isHotbar;
    public int index;
    public Image background;
    public Image fill;
    public Image icon;
    public Text countText;
    public InventorySlot data;

    public void SetData(InventorySlot slot)
    {
        data = slot;
        if (slot == null || slot.IsEmpty())
        {
            if (icon != null)
            {
                icon.sprite = null;
                icon.color = Color.white;
                icon.enabled = false;
            }
            if (countText != null) countText.text = "";
        }
        else
        {
            if (icon != null)
            {
                icon.sprite = slot.item != null ? slot.item.icon : null;
                icon.color = Color.white;
                icon.enabled = icon.sprite != null;
            }
            if (countText != null) countText.text = slot.count > 1 ? slot.count.ToString() : "";
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (ui == null) return;
        ui.OnInventorySlotClicked(this);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (ui == null) return;
        ui.BeginDrag(this);
        ui.UpdateDragVisual(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ui == null || !ui.HasActiveDrag()) return;
        ui.UpdateDragVisual(eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (ui == null || !ui.HasActiveDrag()) return;
        if (ui.GetDragSource() == this) return;
        ui.EndDrag(this);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ui == null || !ui.HasActiveDrag()) return;

        InventorySlotUI dropTarget = null;
        if (eventData != null && eventData.pointerCurrentRaycast.gameObject != null)
        {
            dropTarget = eventData.pointerCurrentRaycast.gameObject.GetComponentInParent<InventorySlotUI>();
        }
        ui.EndDrag(dropTarget);
    }
}

public class CreatureStorageSlotUI : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public InventoryUI ui;
    public int pageLocalIndex;
    public Image background;
    public Image icon;
    public Text nameLabel;
    public Text levelLabel;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (ui == null) return;
        ui.OnCreatureStorageSlotClicked(pageLocalIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (ui == null) return;
        ui.BeginCreatureDragFromStorage(this);
        ui.UpdateDragVisual(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ui == null) return;
        if (!ui.HasActiveCreatureDrag()) return;
        ui.UpdateDragVisual(eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (ui == null) return;
        if (!ui.HasActiveCreatureDrag()) return;
        ui.DropCreatureOnStorageSlot(pageLocalIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ui == null) return;
        if (!ui.HasActiveCreatureDrag()) return;

        if (eventData != null && eventData.pointerCurrentRaycast.gameObject != null)
        {
            GameObject go = eventData.pointerCurrentRaycast.gameObject;
            CreatureStorageSlotUI storageTarget = go.GetComponentInParent<CreatureStorageSlotUI>();
            if (storageTarget != null)
            {
                ui.DropCreatureOnStorageSlot(storageTarget.pageLocalIndex);
                return;
            }

            PartySidebarSlotDragUI partyTarget = go.GetComponentInParent<PartySidebarSlotDragUI>();
            if (partyTarget != null)
            {
                ui.DropCreatureOnPartySlot(partyTarget.slotIndex);
                return;
            }

            AddPartyDropZoneUI addZone = go.GetComponentInParent<AddPartyDropZoneUI>();
            if (addZone != null)
            {
                ui.DropCreatureIntoNewPartySlot();
                return;
            }
        }

        ui.EndCreatureDrag();
    }
}

public class CreaturePartySlotUI : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public InventoryUI ui;
    public int partyIndex;
    public Image background;
    public Image icon;
    public Text levelText;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (ui == null) return;
        ui.OnCreaturePartySlotClicked(partyIndex);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (ui == null) return;
        ui.BeginCreatureDragFromParty(this);
        ui.UpdateDragVisual(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (ui == null) return;
        if (!ui.HasActiveCreatureDrag()) return;
        ui.UpdateDragVisual(eventData);
    }

    public void OnDrop(PointerEventData eventData)
    {
        if (ui == null) return;
        if (!ui.HasActiveCreatureDrag()) return;
        ui.DropCreatureOnPartySlot(partyIndex);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (ui == null) return;
        if (!ui.HasActiveCreatureDrag()) return;

        if (eventData != null && eventData.pointerCurrentRaycast.gameObject != null)
        {
            GameObject go = eventData.pointerCurrentRaycast.gameObject;
            CreaturePartySlotUI partyTarget = go.GetComponentInParent<CreaturePartySlotUI>();
            if (partyTarget != null)
            {
                ui.DropCreatureOnPartySlot(partyTarget.partyIndex);
                return;
            }

            CreatureStorageSlotUI storageTarget = go.GetComponentInParent<CreatureStorageSlotUI>();
            if (storageTarget != null)
            {
                ui.DropCreatureOnStorageSlot(storageTarget.pageLocalIndex);
                return;
            }
        }

        ui.EndCreatureDrag();
    }
}
