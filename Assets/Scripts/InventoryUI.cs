using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public class InventoryUI : MonoBehaviour
{
    const int MinRecommendedSlotSize = 90;
    const int DefaultRecommendedIconSize = 75;
    static readonly Color DefaultSlotNormalColor = new Color(0.75f, 0.75f, 0.78f, 1f);

    public InventoryModel inventory;
    public RectTransform hotbarRoot;
    public RectTransform bagRoot;
    public RectTransform panelRoot;
    public int slotSize = 90;
    public int iconSize = 75;
    public int spacing = 2;
    public Sprite slotSprite;
    public Sprite selectedSprite;
    public Color normalColor = new Color(0.75f, 0.75f, 0.78f, 1f);
    public Color selectedColor = new Color(1f, 1f, 0.8f, 1f);
    public int selectedHotbarIndex = 0;

    private InventorySlotUI[] hotbarSlots;
    private InventorySlotUI[] bagSlots;
    private InventorySlotUI draggingFrom;
    private Image draggingIcon;

    void Awake()
    {
        NormalizeVisualSettings();
        EnsureEventSystem();
        if (inventory == null) inventory = FindFirstObjectByType<InventoryModel>();
        if (panelRoot == null) panelRoot = transform.Find("InventoryPanel") as RectTransform;
        if (hotbarRoot == null) hotbarRoot = transform.Find("Hotbar") as RectTransform;
        if (bagRoot == null && panelRoot != null) bagRoot = panelRoot.Find("BagGrid") as RectTransform;

        if (slotSprite == null) slotSprite = CreateSlotSprite(new Color32(42, 42, 42, 255), new Color32(150, 150, 150, 255));
        if (selectedSprite == null) selectedSprite = CreateSlotSprite(new Color32(80, 80, 40, 255), new Color32(255, 215, 90, 255));

        BuildUI();
        Refresh();
    }

    void OnEnable()
    {
        if (inventory == null) inventory = FindFirstObjectByType<InventoryModel>();
        if (inventory != null)
        {
            inventory.OnChanged += Refresh;
        }
        Refresh();
    }

    void OnDisable()
    {
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
        if (inventory == null) return;
        if (hotbarRoot == null || bagRoot == null) return;
        inventory.EnsureSlots();

        EnsureLayout(hotbarRoot, true);
        EnsureLayout(bagRoot, false);

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
        bagSlots = BuildSlotGrid(bagRoot, inventory.bag.Length, inventory.bagColumns);

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
            g.spacing = new Vector2(spacing, spacing);
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

        GameObject iconObj = new GameObject("Icon");
        iconObj.transform.SetParent(slotObj.transform, false);
        Image icon = iconObj.AddComponent<Image>();
        icon.raycastTarget = false;
        RectTransform irt = iconObj.GetComponent<RectTransform>();
        irt.anchorMin = new Vector2(0.5f, 0.5f);
        irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.pivot = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(iconSize, iconSize);

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
        slotUi.icon = icon;
        slotUi.countText = countText;

        return slotUi;
    }

    public void Refresh()
    {
        NormalizeVisualSettings();
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
        draggingIcon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
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

    Sprite CreateSlotSprite(Color32 fill, Color32 border)
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool edge = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                tex.SetPixel(x, y, edge ? border : fill);
            }
        }

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 16);
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

        if (slot.icon != null)
        {
            RectTransform iconRt = slot.icon.rectTransform;
            if (iconRt != null)
            {
                iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            }
        }
    }

    void NormalizeVisualSettings()
    {
        // Auto-migrate legacy scene values so old serialized data can't keep the hotbar tiny/white.
        if (slotSize < MinRecommendedSlotSize) slotSize = MinRecommendedSlotSize;

        int maxIcon = Mathf.Max(1, slotSize - 12);
        if (iconSize <= 0 || iconSize > maxIcon)
        {
            iconSize = Mathf.Min(DefaultRecommendedIconSize, maxIcon);
        }

        if (IsNearlyWhite(normalColor))
        {
            normalColor = DefaultSlotNormalColor;
        }

        if (spacing < 0) spacing = 0;
    }

    bool IsNearlyWhite(Color c)
    {
        return c.a > 0.95f && c.r > 0.95f && c.g > 0.95f && c.b > 0.95f;
    }
}

public class InventorySlotUI : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    public InventoryUI ui;
    public bool isHotbar;
    public int index;
    public Image background;
    public Image icon;
    public Text countText;
    public InventorySlot data;

    public void SetData(InventorySlot slot)
    {
        data = slot;
        if (slot == null || slot.IsEmpty())
        {
            if (icon != null) icon.sprite = null;
            if (countText != null) countText.text = "";
        }
        else
        {
            if (icon != null) icon.sprite = slot.item != null ? slot.item.icon : null;
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
