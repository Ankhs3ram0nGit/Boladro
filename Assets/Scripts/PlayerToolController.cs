using System;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(InventoryModel))]
public class PlayerToolController : MonoBehaviour
{
    public event Action OnPickaxeSwing;

    [Header("Debug Grant")]
    public KeyCode givePickaxeKey = KeyCode.L;
    public string woodenPickaxeItemId = "wooden_pickaxe";
    public string woodenPickaxeDisplayName = "Wooden Pickaxe";
    public string woodenPickaxeSpritePath = "Assets/FREE 16x16 Pickaxes/pixelquest16-july-2025-cave.png";

    [Header("Held Tool Visual")]
    public float handOffsetX = 0.50f;
    public float handOffsetY = 0.02f;
    public float restAngleRight = -2f;
    public float swingAngle = 56f;
    public float swingDuration = 0.16f;
    public float swingRepeatDelay = 0.08f;
    public float toolScale = 8.0f;
    public bool forceHeldSpritePivot = true;
    [Range(0f, 1f)] public float heldSpritePivotX = 0.94f;
    [Range(0f, 1f)] public float heldSpritePivotY = 0.06f;
    public bool mirrorPivotXWhenFacingLeft = false;
    public float pivotExtraXOffset = 0.0f;
    public float pivotExtraYOffset = 0.0f;
    public int sortingOrderOffset = 30;
    public bool useAbsoluteTopSortingOrder = true;
    public int absoluteTopSortingOrder = 32760;

    private InventoryModel inventory;
    private InventoryUI inventoryUI;
    private InventoryHotbar inventoryHotbar;
    private PlayerMover playerMover;
    private SpriteRenderer playerRenderer;

    private InventoryItemData runtimePickaxeItem;
    private Transform heldToolPivot;
    private Transform heldToolSpriteRoot;
    private SpriteRenderer heldToolRenderer;
    private Transform cachedVisualParent;
    private Sprite cachedHeldSprite;
    private Sprite cachedHeldDisplaySprite;
    private Sprite cachedHeldDisplaySource;
    private float cachedHeldPivotX = -1f;
    private float cachedHeldPivotY = -1f;
    private bool cachedHeldForcePivot;

    private bool swinging;
    private float swingTimer;
    private float swingOffset;
    private float nextSwingAllowedTime;

    void Awake()
    {
        // Keep held tool tightly coupled to player draw order to avoid cross-layer clipping.
        useAbsoluteTopSortingOrder = false;
        if (sortingOrderOffset < 1) sortingOrderOffset = 1;

        inventory = GetComponent<InventoryModel>();
        playerMover = GetComponent<PlayerMover>();
        playerRenderer = GetComponent<SpriteRenderer>();
        inventoryUI = FindFirstObjectByType<InventoryUI>();
        inventoryHotbar = FindFirstObjectByType<InventoryHotbar>();

        if (inventory != null) inventory.EnsureSlots();
        CleanupDuplicateHeldToolPivots();
        EnsureRuntimePickaxeItem();
        EnsureHeldToolObjects();
        SetHeldToolVisible(false);
    }

    void Update()
    {
        RefreshRefs();
        HandleDebugGrantInput();

        bool equipped = IsWoodenPickaxeEquipped();

        UpdateHeldToolVisual(equipped);

        if (!equipped)
        {
            StopSwing();
            return;
        }

        HandleSwingInput();
        UpdateSwingAnimation();
    }

    void LateUpdate()
    {
        if (heldToolRenderer == null || !heldToolRenderer.enabled) return;
        ApplyHeldSorting();
    }

    void RefreshRefs()
    {
        if (inventory == null) inventory = GetComponent<InventoryModel>();
        if (playerMover == null) playerMover = GetComponent<PlayerMover>();
        if (playerRenderer == null) playerRenderer = GetComponent<SpriteRenderer>();
        if (inventoryUI == null) inventoryUI = FindFirstObjectByType<InventoryUI>();
        if (inventoryHotbar == null) inventoryHotbar = FindFirstObjectByType<InventoryHotbar>();
        if (inventory != null) inventory.EnsureSlots();
    }

    void HandleDebugGrantInput()
    {
        bool pressed = false;

        Keyboard kb = Keyboard.current;
        if (kb != null && kb.lKey.wasPressedThisFrame)
        {
            pressed = true;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pressed && Input.GetKeyDown(givePickaxeKey))
        {
            pressed = true;
        }
#endif

        if (!pressed) return;
        GiveWoodenPickaxeToInventory();
    }

    public void GiveWoodenPickaxeToInventory()
    {
        if (inventory == null) return;
        EnsureRuntimePickaxeItem();
        if (runtimePickaxeItem == null) return;

        bool added = inventory.TryAddItemToHotbarFirst(runtimePickaxeItem, 1);
        if (!added)
        {
            Debug.LogWarning("PlayerToolController: Could not add Wooden Pickaxe (inventory is full).");
            return;
        }

        Debug.Log("PlayerToolController: Added Wooden Pickaxe to inventory (L).");
    }

    public bool IsPickaxeEquipped()
    {
        return IsWoodenPickaxeEquipped();
    }

    bool IsWoodenPickaxeEquipped()
    {
        InventorySlot slot;
        return TryGetSelectedPickaxeSlot(out slot);
    }

    int GetSelectedHotbarIndex()
    {
        if (inventory != null && inventory.hotbar != null && inventory.hotbar.Length > 0)
        {
            if (inventoryHotbar != null)
            {
                return Mathf.Clamp(inventoryHotbar.selectedIndex, 0, inventory.hotbar.Length - 1);
            }
            if (inventoryUI != null)
            {
                return Mathf.Clamp(inventoryUI.selectedHotbarIndex, 0, inventory.hotbar.Length - 1);
            }
        }
        return 0;
    }

    void EnsureRuntimePickaxeItem()
    {
        if (runtimePickaxeItem != null) return;

        Sprite pickaxeSprite = LoadWoodenPickaxeSprite();
        runtimePickaxeItem = ScriptableObject.CreateInstance<InventoryItemData>();
        runtimePickaxeItem.hideFlags = HideFlags.HideAndDontSave;
        runtimePickaxeItem.itemId = woodenPickaxeItemId;
        runtimePickaxeItem.displayName = woodenPickaxeDisplayName;
        runtimePickaxeItem.icon = pickaxeSprite;

        cachedHeldSprite = pickaxeSprite;
    }

    Sprite LoadWoodenPickaxeSprite()
    {
        Sprite sprite = null;

#if UNITY_EDITOR
        sprite = AssetDatabase.LoadAssetAtPath<Sprite>(woodenPickaxeSpritePath);
        if (sprite == null)
        {
            UnityEngine.Object[] assetsAtPath = AssetDatabase.LoadAllAssetsAtPath(woodenPickaxeSpritePath);
            for (int i = 0; i < assetsAtPath.Length; i++)
            {
                Sprite candidate = assetsAtPath[i] as Sprite;
                if (candidate == null) continue;
                if (string.Equals(candidate.name, "pixelquest16-july-2025-cave_0", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.name, "sPickaxe1", System.StringComparison.OrdinalIgnoreCase))
                {
                    sprite = candidate;
                    break;
                }
                if (sprite == null) sprite = candidate;
            }
        }
        if (sprite == null)
        {
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(woodenPickaxeSpritePath);
            if (tex != null)
            {
                sprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0f),
                    Mathf.Max(1f, tex.width)
                );
            }
        }
#endif

        if (sprite == null)
        {
            sprite = Resources.Load<Sprite>("Items/sPickaxe1");
        }

        if (sprite == null)
        {
            Debug.LogWarning("PlayerToolController: Could not load Wooden Pickaxe sprite at " + woodenPickaxeSpritePath);
        }

        return sprite;
    }

    void EnsureHeldToolObjects()
    {
        if (heldToolPivot == null)
        {
            Transform existing = FindChildRecursiveByName(transform, "__HeldToolPivot");
            if (existing == null)
            {
                GameObject go = new GameObject("__HeldToolPivot");
                existing = go.transform;
                existing.SetParent(transform, false);
            }
            heldToolPivot = existing;
        }

        if (heldToolSpriteRoot == null)
        {
            Transform existing = FindChildRecursiveByName(heldToolPivot, "__HeldToolSprite");
            if (existing == null)
            {
                GameObject go = new GameObject("__HeldToolSprite");
                existing = go.transform;
                existing.SetParent(heldToolPivot, false);
            }
            heldToolSpriteRoot = existing;
        }

        if (heldToolRenderer == null && heldToolSpriteRoot != null)
        {
            heldToolRenderer = heldToolSpriteRoot.GetComponent<SpriteRenderer>();
            if (heldToolRenderer == null)
            {
                heldToolRenderer = heldToolSpriteRoot.gameObject.AddComponent<SpriteRenderer>();
            }
        }

        if (heldToolRenderer != null && cachedHeldSprite == null)
        {
            cachedHeldSprite = LoadWoodenPickaxeSprite();
        }
    }

    void UpdateHeldToolVisual(bool visible)
    {
        EnsureHeldToolObjects();
        if (heldToolPivot == null || heldToolSpriteRoot == null || heldToolRenderer == null) return;

        Transform visualParent = playerMover != null ? playerMover.GetVisualRoot() : transform;
        if (visualParent == null) visualParent = transform;
        if (cachedVisualParent != visualParent || heldToolPivot.parent != visualParent)
        {
            heldToolPivot.SetParent(visualParent, false);
            cachedVisualParent = visualParent;
        }

        if (!visible)
        {
            SetHeldToolVisible(false);
            return;
        }

        if (cachedHeldSprite == null)
        {
            cachedHeldSprite = LoadWoodenPickaxeSprite();
        }

        InventorySlot selectedPickaxeSlot;
        if (TryGetSelectedPickaxeSlot(out selectedPickaxeSlot) && selectedPickaxeSlot != null && selectedPickaxeSlot.item != null && selectedPickaxeSlot.item.icon != null)
        {
            cachedHeldSprite = selectedPickaxeSlot.item.icon;
        }

        bool facingLeft = playerRenderer != null && playerRenderer.flipX;
        heldToolRenderer.sprite = ResolveHeldDisplaySprite(cachedHeldSprite, facingLeft);
        SetHeldToolVisible(heldToolRenderer.sprite != null);
        if (!heldToolRenderer.enabled) return;

        float side = facingLeft ? -1f : 1f;
        heldToolPivot.localPosition = new Vector3(handOffsetX * side, handOffsetY, 0f);

        // True mirror with flipX uses negated angle, not (180-angle).
        float totalAngle = facingLeft
            ? (-restAngleRight + swingOffset)
            : (restAngleRight + swingOffset);
        heldToolPivot.localRotation = Quaternion.Euler(0f, 0f, totalAngle);

        float absScale = Mathf.Max(0.01f, Mathf.Abs(toolScale));
        heldToolSpriteRoot.localScale = new Vector3(absScale, absScale, 1f);
        heldToolSpriteRoot.localPosition = new Vector3(
            pivotExtraXOffset * side,
            pivotExtraYOffset,
            0f);
        heldToolRenderer.flipX = facingLeft;
        heldToolRenderer.flipY = false;

        ApplyHeldSorting();
        heldToolRenderer.color = Color.white;
    }

    void SetHeldToolVisible(bool visible)
    {
        if (heldToolRenderer == null) return;
        heldToolRenderer.enabled = visible;
    }

    void HandleSwingInput()
    {
        bool wantsSwing = false;

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
        {
            wantsSwing = true;
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!wantsSwing && Input.GetMouseButton(0))
        {
            wantsSwing = true;
        }
#endif

        if (wantsSwing && !swinging && Time.time >= nextSwingAllowedTime)
        {
            swinging = true;
            swingTimer = 0f;
            if (OnPickaxeSwing != null) OnPickaxeSwing.Invoke();
        }
    }

    void UpdateSwingAnimation()
    {
        if (!swinging)
        {
            swingOffset = 0f;
            return;
        }

        swingTimer += Time.deltaTime;
        float duration = Mathf.Max(0.02f, swingDuration);
        float t = Mathf.Clamp01(swingTimer / duration);
        float pulse = t < 0.5f ? (t * 2f) : ((1f - t) * 2f);
        bool facingLeft = playerRenderer != null && playerRenderer.flipX;
        float dir = facingLeft ? 1f : -1f;
        swingOffset = dir * swingAngle * pulse;

        if (t >= 1f)
        {
            swinging = false;
            swingOffset = 0f;
            nextSwingAllowedTime = Time.time + Mathf.Max(0f, swingRepeatDelay);
        }
    }

    void StopSwing()
    {
        swinging = false;
        swingTimer = 0f;
        swingOffset = 0f;
    }

    static string NormalizeId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        return id.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).ToLowerInvariant();
    }

    bool IsLikelyPickaxeItem(InventoryItemData item)
    {
        if (item == null) return false;

        string id = NormalizeId(item.itemId);
        if (!string.IsNullOrEmpty(id))
        {
            if (id == NormalizeId(woodenPickaxeItemId)) return true;
            if (id.Contains("pickaxe")) return true;
        }

        string name = NormalizeId(item.displayName);
        if (!string.IsNullOrEmpty(name) && name.Contains("pickaxe")) return true;

        if (item.icon != null)
        {
            string iconName = NormalizeId(item.icon.name);
            if (!string.IsNullOrEmpty(iconName) && iconName.Contains("pickaxe")) return true;
        }

        return false;
    }

    void ApplyHeldSorting()
    {
        if (heldToolRenderer == null || playerRenderer == null) return;
        heldToolRenderer.sortingLayerID = playerRenderer.sortingLayerID;
        int effectiveOffset = Mathf.Max(1, sortingOrderOffset);
        heldToolRenderer.sortingOrder = Mathf.Clamp(playerRenderer.sortingOrder + effectiveOffset, -32768, 32767);
    }

    Sprite ResolveHeldDisplaySprite(Sprite source, bool facingLeft)
    {
        if (source == null) return null;
        if (!forceHeldSpritePivot)
        {
            ReleaseCachedHeldDisplaySprite();
            return source;
        }

        float pivotX = Mathf.Clamp01(heldSpritePivotX);
        if (facingLeft && mirrorPivotXWhenFacingLeft)
        {
            pivotX = 1f - pivotX;
        }
        float pivotY = Mathf.Clamp01(heldSpritePivotY);
        bool pivotUnchanged =
            Mathf.Abs(cachedHeldPivotX - pivotX) < 0.0001f &&
            Mathf.Abs(cachedHeldPivotY - pivotY) < 0.0001f &&
            cachedHeldForcePivot == forceHeldSpritePivot;

        if (cachedHeldDisplaySprite != null && cachedHeldDisplaySource == source && pivotUnchanged)
        {
            return cachedHeldDisplaySprite;
        }

        ReleaseCachedHeldDisplaySprite();

        Texture2D tex = source.texture;
        if (tex == null)
        {
            return source;
        }

        Rect rect = source.rect;
        Vector2 pivot = new Vector2(pivotX, pivotY);
        cachedHeldDisplaySprite = Sprite.Create(
            tex,
            rect,
            pivot,
            source.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect,
            source.border);
        cachedHeldDisplaySprite.name = source.name + "__HeldPivot";
        cachedHeldDisplaySprite.hideFlags = HideFlags.HideAndDontSave;
        cachedHeldDisplaySource = source;
        cachedHeldPivotX = pivotX;
        cachedHeldPivotY = pivotY;
        cachedHeldForcePivot = forceHeldSpritePivot;
        return cachedHeldDisplaySprite;
    }

    void ReleaseCachedHeldDisplaySprite()
    {
        if (cachedHeldDisplaySprite == null) return;
        if (Application.isPlaying)
        {
            Destroy(cachedHeldDisplaySprite);
        }
        else
        {
            DestroyImmediate(cachedHeldDisplaySprite);
        }
        cachedHeldDisplaySprite = null;
        cachedHeldDisplaySource = null;
        cachedHeldPivotX = -1f;
        cachedHeldPivotY = -1f;
        cachedHeldForcePivot = false;
    }

    bool TryGetSelectedPickaxeSlot(out InventorySlot selectedSlot)
    {
        selectedSlot = null;
        if (inventory == null || inventory.hotbar == null || inventory.hotbar.Length == 0) return false;

        int len = inventory.hotbar.Length;
        int[] candidates = new int[3];
        int count = 0;

        candidates[count++] = Mathf.Clamp(GetSelectedHotbarIndex(), 0, len - 1);
        if (inventoryUI != null) candidates[count++] = Mathf.Clamp(inventoryUI.selectedHotbarIndex, 0, len - 1);
        if (inventoryHotbar != null) candidates[count++] = Mathf.Clamp(inventoryHotbar.selectedIndex, 0, len - 1);

        for (int i = 0; i < count; i++)
        {
            int idx = candidates[i];
            bool alreadyChecked = false;
            for (int j = 0; j < i; j++)
            {
                if (candidates[j] == idx)
                {
                    alreadyChecked = true;
                    break;
                }
            }
            if (alreadyChecked) continue;

            InventorySlot slot = inventory.GetHotbarSlot(idx);
            if (slot == null || slot.IsEmpty() || slot.item == null) continue;
            if (!IsLikelyPickaxeItem(slot.item)) continue;

            selectedSlot = slot;
            return true;
        }

        return false;
    }

    void CleanupDuplicateHeldToolPivots()
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        if (all == null || all.Length == 0) return;

        Transform keeper = null;
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.name != "__HeldToolPivot") continue;
            if (keeper == null)
            {
                keeper = t;
                continue;
            }
            Destroy(t.gameObject);
        }
    }

    static Transform FindChildRecursiveByName(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Transform found = FindChildRecursiveByName(child, name);
            if (found != null) return found;
        }
        return null;
    }

    void OnDestroy()
    {
        ReleaseCachedHeldDisplaySprite();
    }
}
