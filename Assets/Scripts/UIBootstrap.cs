using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class UIBootstrap : MonoBehaviour
{
    const float HotbarHorizontalPadding = 24f;
    const float HotbarVerticalPadding = 20f;

    public RectTransform canvasRect;
    public RectTransform hudRect;
    public RectTransform heartsRect;
    public RectTransform hotbarRect;
    public RectTransform inventoryPanelRect;
    public RectTransform bagRect;
    public HealthUI healthUI;
    public MonoBehaviour inventoryUI;

    public Vector2 heartsOffset = new Vector2(16, -16);
    public Vector2 hotbarOffset = new Vector2(0, 20);
    public Vector2 hotbarSize = new Vector2(420, 80);
    public Vector2 inventoryPanelSize = new Vector2(560, 380);
    public Vector2 bagSize = new Vector2(520, 220);

    void Awake()
    {
        ApplyLayout();
    }

    void OnEnable()
    {
        ApplyLayout();
    }

    void LateUpdate()
    {
        if (Application.isPlaying)
        {
            ApplyLayout();
        }
    }

    void ApplyLayout()
    {
        if (canvasRect == null) canvasRect = GetComponent<RectTransform>();
        if (hudRect == null)
        {
            Transform hud = transform.Find("HUD");
            if (hud != null) hudRect = hud.GetComponent<RectTransform>();
        }
        if (heartsRect == null && hudRect != null)
        {
            Transform hearts = hudRect.Find("Hearts");
            if (hearts != null) heartsRect = hearts.GetComponent<RectTransform>();
        }
        if (hotbarRect == null && hudRect != null)
        {
            Transform hotbarTf = hudRect.Find("Hotbar");
            if (hotbarTf != null) hotbarRect = hotbarTf.GetComponent<RectTransform>();
        }
        if (inventoryPanelRect == null && hudRect != null)
        {
            Transform inv = hudRect.Find("InventoryPanel");
            if (inv != null) inventoryPanelRect = inv.GetComponent<RectTransform>();
        }
        if (bagRect == null && inventoryPanelRect != null)
        {
            Transform bag = inventoryPanelRect.Find("BagGrid");
            if (bag != null) bagRect = bag.GetComponent<RectTransform>();
        }
        if (healthUI == null && hudRect != null)
        {
            healthUI = hudRect.GetComponent<HealthUI>();
        }
        if (inventoryUI == null && hudRect != null)
        {
            inventoryUI = hudRect.GetComponent("InventoryUI") as MonoBehaviour;
        }

        if (healthUI != null)
        {
            healthUI.heartSize = Mathf.Max(healthUI.heartSize, 56);
            healthUI.spacing = 2;
        }

        NormalizeCanvas();
        NormalizeHud();
        NormalizeHearts();
        AutoSizeHotbarFromInventory();
        NormalizeHotbar();
        NormalizeInventoryPanel();
        NormalizeBag();
        MakeHudTransparent();
    }

    void Start()
    {
        if (healthUI != null)
        {
            healthUI.ForceRebuild();
        }
    }

    void NormalizeCanvas()
    {
        if (canvasRect == null) return;
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.anchoredPosition = Vector2.zero;
        canvasRect.sizeDelta = Vector2.zero;
        canvasRect.localScale = Vector3.one;
    }

    void NormalizeHud()
    {
        if (hudRect == null) return;
        hudRect.anchorMin = Vector2.zero;
        hudRect.anchorMax = Vector2.one;
        hudRect.anchoredPosition = Vector2.zero;
        hudRect.sizeDelta = Vector2.zero;
        hudRect.localScale = Vector3.one;
    }

    void NormalizeHearts()
    {
        if (heartsRect == null) return;
        heartsRect.anchorMin = new Vector2(0, 1);
        heartsRect.anchorMax = new Vector2(0, 1);
        heartsRect.pivot = new Vector2(0, 1);
        heartsRect.anchoredPosition = heartsOffset;
        if (heartsRect.sizeDelta == Vector2.zero)
        {
            heartsRect.sizeDelta = new Vector2(400, 48);
        }
        heartsRect.localScale = Vector3.one;
    }

    void NormalizeHotbar()
    {
        if (hotbarRect == null) return;
        HorizontalLayoutGroup layout = hotbarRect.GetComponent<HorizontalLayoutGroup>();
        if (layout != null)
        {
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }

        hotbarRect.anchorMin = new Vector2(0.5f, 0);
        hotbarRect.anchorMax = new Vector2(0.5f, 0);
        hotbarRect.pivot = new Vector2(0.5f, 0);
        hotbarRect.anchoredPosition = hotbarOffset;
        hotbarRect.sizeDelta = hotbarSize;
        hotbarRect.localScale = Vector3.one;
    }

    void AutoSizeHotbarFromInventory()
    {
        InventoryUI typedInventoryUI = inventoryUI as InventoryUI;
        if (typedInventoryUI == null && hudRect != null)
        {
            typedInventoryUI = hudRect.GetComponent<InventoryUI>();
        }
        if (typedInventoryUI == null) return;

        int slotSize = Mathf.Max(1, typedInventoryUI.slotSize);
        int spacing = Mathf.Max(0, typedInventoryUI.spacing);
        int slotCount = ResolveHotbarSlotCount(typedInventoryUI);

        float computedWidth = (slotCount * slotSize) + (Mathf.Max(0, slotCount - 1) * spacing) + HotbarHorizontalPadding;
        float computedHeight = slotSize + HotbarVerticalPadding;
        hotbarSize = new Vector2(computedWidth, computedHeight);
    }

    int ResolveHotbarSlotCount(InventoryUI typedInventoryUI)
    {
        if (typedInventoryUI == null) return 9;
        InventoryModel model = typedInventoryUI.inventory;
        if (model != null)
        {
            model.EnsureSlots();
            if (model.hotbar != null && model.hotbar.Length > 0)
            {
                return model.hotbar.Length;
            }

            if (model.hotbarSize > 0)
            {
                return model.hotbarSize;
            }
        }

        return 9;
    }

    void NormalizeInventoryPanel()
    {
        if (inventoryPanelRect == null) return;
        inventoryPanelRect.anchorMin = new Vector2(0.5f, 0.5f);
        inventoryPanelRect.anchorMax = new Vector2(0.5f, 0.5f);
        inventoryPanelRect.pivot = new Vector2(0.5f, 0.5f);
        inventoryPanelRect.anchoredPosition = Vector2.zero;
        inventoryPanelRect.sizeDelta = inventoryPanelSize;
        inventoryPanelRect.localScale = Vector3.one;
    }

    void NormalizeBag()
    {
        if (bagRect == null) return;
        bagRect.anchorMin = new Vector2(0.5f, 0.5f);
        bagRect.anchorMax = new Vector2(0.5f, 0.5f);
        bagRect.pivot = new Vector2(0.5f, 0.5f);
        bagRect.anchoredPosition = new Vector2(0, -10);
        bagRect.sizeDelta = bagSize;
        bagRect.localScale = Vector3.one;
    }

    void MakeHudTransparent()
    {
        if (hudRect == null) return;
        Image img = hudRect.GetComponent<Image>();
        if (img != null)
        {
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = false;
        }

        if (hotbarRect != null)
        {
            Image hotbarImg = hotbarRect.GetComponent<Image>();
            if (hotbarImg != null)
            {
                hotbarImg.raycastTarget = false;
            }
        }

        if (inventoryPanelRect != null)
        {
            Image panelImg = inventoryPanelRect.GetComponent<Image>();
            if (panelImg != null)
            {
                panelImg.color = new Color(0f, 0f, 0f, 0.6f);
                panelImg.raycastTarget = true;
            }
        }
    }
}
