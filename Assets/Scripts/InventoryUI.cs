using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;

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
    private Image draggingIcon;

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
    private RectTransform placeholderRoot;
    private Text placeholderLabel;
    private int creaturePageIndex;
    private readonly List<Image> backdropBlurLayers = new List<Image>();

    private PlayerCreatureParty party;
    private PlayerCreatureStorage storage;

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
        EnsureCreatureTabUI();
        EnsurePlaceholderTabUI();
        ApplyPanelBackdropBlur();
        ApplyInventoryPanelLayout();
        SwitchTab(activeTab);

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
                ApplyHotbarSelection(hotbarSlots[i], i == selectedHotbarIndex);
            }
        }

        if (bagSlots != null)
        {
            for (int i = 0; i < bagSlots.Length; i++)
            {
                InventorySlot slot = inventory.GetBagSlot(i);
                bagSlots[i].SetData(slot);
                ResizeSlotVisuals(bagSlots[i]);
            }
        }

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

    public void SelectHotbar(int index)
    {
        selectedHotbarIndex = Mathf.Clamp(index, 0, inventory.hotbar.Length - 1);
        Refresh();
    }

    public void BeginDrag(InventorySlotUI slot)
    {
        if (slot == null || slot.data == null || slot.data.IsEmpty()) return;
        draggingFrom = slot;

        if (draggingIcon == null)
        {
            GameObject go = new GameObject("DraggingIcon");
            go.transform.SetParent(transform, false);
            draggingIcon = go.AddComponent<Image>();
            draggingIcon.raycastTarget = false;
        }

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
        if (draggingIcon != null)
        {
            draggingIcon.gameObject.SetActive(false);
        }

        if (draggingFrom == null) return;

        if (slot == null || slot == draggingFrom)
        {
            draggingFrom = null;
            return;
        }

        SwapSlots(draggingFrom, slot);
        draggingFrom = null;
    }

    public bool HasActiveDrag()
    {
        return draggingFrom != null;
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

        Transform gridTf = creatureTabRoot.Find("CreatureGrid");
        if (gridTf == null)
        {
            GameObject go = new GameObject("CreatureGrid", typeof(RectTransform), typeof(CanvasRenderer), typeof(GridLayoutGroup));
            go.transform.SetParent(creatureTabRoot, false);
            gridTf = go.transform;
        }
        creatureGridRoot = gridTf as RectTransform;
        GridLayoutGroup grid = gridTf.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = gridTf.gameObject.AddComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 5;
        grid.cellSize = new Vector2(slotSize, slotSize);
        grid.spacing = new Vector2(inventoryMenuSlotSpacing, inventoryMenuSlotSpacing);
        grid.childAlignment = TextAnchor.MiddleCenter;

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

            Image bg = slotTf.GetComponent<Image>();
            bg.sprite = slotSprite;
            bg.type = slotSprite != null && slotSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            bg.color = normalColor;
            view.background = bg;

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

        Transform pagerTf = creatureTabRoot.Find("CreaturePager");
        if (pagerTf == null)
        {
            GameObject go = new GameObject("CreaturePager", typeof(RectTransform), typeof(CanvasRenderer), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(creatureTabRoot, false);
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
        le.preferredWidth = 56f;
        le.preferredHeight = 34f;

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
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
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

        if (creatureTabRoot != null)
        {
            creatureTabRoot.sizeDelta = new Vector2(panelW - 48f, panelH - (tabH + 40f));
            creatureTabRoot.anchoredPosition = new Vector2(0f, -(tabH + 18f));
        }

        if (creatureGridRoot != null)
        {
            float creatureGridW = (5 * slotSize) + (4 * gap);
            float creatureGridH = (5 * slotSize) + (4 * gap);
            creatureGridRoot.anchorMin = new Vector2(0.5f, 1f);
            creatureGridRoot.anchorMax = new Vector2(0.5f, 1f);
            creatureGridRoot.pivot = new Vector2(0.5f, 1f);
            creatureGridRoot.anchoredPosition = Vector2.zero;
            creatureGridRoot.sizeDelta = new Vector2(creatureGridW, creatureGridH);

            GridLayoutGroup grid = creatureGridRoot.GetComponent<GridLayoutGroup>();
            if (grid != null)
            {
                grid.cellSize = new Vector2(slotSize, slotSize);
                grid.spacing = new Vector2(gap, gap);
            }
        }

        if (creaturePagerRoot != null)
        {
            creaturePagerRoot.anchorMin = new Vector2(0.5f, 1f);
            creaturePagerRoot.anchorMax = new Vector2(0.5f, 1f);
            creaturePagerRoot.pivot = new Vector2(0.5f, 1f);
            float y = creatureGridRoot != null ? -(creatureGridRoot.sizeDelta.y + 12f) : -12f;
            creaturePagerRoot.anchoredPosition = new Vector2(0f, y);
            creaturePagerRoot.sizeDelta = new Vector2(350f, 38f);
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

        int pageCount = storage != null ? Mathf.Max(1, storage.pageCount) : 30;
        creaturePageIndex = Mathf.Clamp(creaturePageIndex, 0, pageCount - 1);
        int baseIndex = creaturePageIndex * CreatureSlotsPerPage;

        for (int i = 0; i < creatureSlots.Length; i++)
        {
            CreatureStorageSlotUI slot = creatureSlots[i];
            if (slot == null) continue;
            CreatureInstance instance = storage != null ? storage.GetAt(baseIndex + i) : null;
            ApplyCreatureSlot(slot, instance);
        }

        if (creaturePageLabel != null)
        {
            int displayPage = creaturePageIndex + 1;
            creaturePageLabel.text = "Page " + displayPage + " / " + pageCount;
        }

        if (creaturePrevPageButton != null) creaturePrevPageButton.interactable = creaturePageIndex > 0;
        if (creatureNextPageButton != null) creatureNextPageButton.interactable = creaturePageIndex < pageCount - 1;
    }

    void ApplyCreatureSlot(CreatureStorageSlotUI slot, CreatureInstance instance)
    {
        if (slot == null) return;
        if (slot.background != null)
        {
            slot.background.sprite = slotSprite;
            slot.background.type = slotSprite != null && slotSprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            slot.background.color = normalColor;
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
        if (ui != null)
        {
            if (isHotbar)
            {
                ui.SelectHotbar(index);
            }
        }
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

public class CreatureStorageSlotUI : MonoBehaviour
{
    public Image background;
    public Image icon;
    public Text nameLabel;
    public Text levelLabel;
}
