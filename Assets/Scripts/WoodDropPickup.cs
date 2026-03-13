using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class WoodDropPickup : MonoBehaviour
{
    public string itemId = "wood";
    public string displayName = "Wood";
    public int amount = 1;
    public float pickupDistance = 1.2f;
    public string spriteAssetPath = "Assets/Resources/Wood.png";

    private SpriteRenderer sr;
    private Camera mainCam;
    private Transform player;
    private InventoryModel inventory;
    private InventoryItemData runtimeItem;
    private Sprite cachedSprite;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        mainCam = Camera.main;
        EnsureNoCollision();
        EnsureSprite();
    }

    void Update()
    {
        if (mainCam == null) mainCam = Camera.main;
        ResolvePlayerInventory();

        if (inventory == null) return;
        if (!CanPickupNow()) return;
        TryPickup();
    }

    void EnsureNoCollision()
    {
        Collider2D[] cols = GetComponents<Collider2D>();
        for (int i = 0; i < cols.Length; i++)
        {
            if (cols[i] == null) continue;
            Destroy(cols[i]);
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null) Destroy(rb);
    }

    void EnsureSprite()
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;
        if (sr.sprite != null) return;

        cachedSprite = ResolveSprite();
        if (cachedSprite != null)
        {
            sr.sprite = cachedSprite;
        }
    }

    Sprite ResolveSprite()
    {
        if (cachedSprite != null) return cachedSprite;

        cachedSprite = Resources.Load<Sprite>("Wood");
#if UNITY_EDITOR
        if (cachedSprite == null)
        {
            cachedSprite = AssetDatabase.LoadAssetAtPath<Sprite>(spriteAssetPath);
        }
#endif
        return cachedSprite;
    }

    void ResolvePlayerInventory()
    {
        if (player != null && inventory != null) return;

        GameObject p = GameObject.Find("Player");
        if (p == null) return;

        player = p.transform;
        inventory = p.GetComponent<InventoryModel>();
    }

    bool CanPickupNow()
    {
        if (player != null)
        {
            float d = Vector2.Distance(new Vector2(transform.position.x, transform.position.y), new Vector2(player.position.x, player.position.y));
            if (d <= Mathf.Max(0.1f, pickupDistance)) return true;
        }
        return IsMouseHovering();
    }

    bool IsMouseHovering()
    {
        if (mainCam == null || sr == null || sr.sprite == null) return false;
        Mouse mouse = Mouse.current;
        if (mouse == null) return false;

        Vector2 screen = mouse.position.ReadValue();
        float z = Mathf.Abs(mainCam.transform.position.z - transform.position.z);
        Vector3 wp = mainCam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, z));
        return sr.bounds.Contains(new Vector3(wp.x, wp.y, transform.position.z));
    }

    void TryPickup()
    {
        if (inventory == null) return;
        EnsureItemData();
        if (runtimeItem == null) return;

        bool added = inventory.TryAddItem(runtimeItem, Mathf.Max(1, amount));
        if (!added) return;
        Destroy(gameObject);
    }

    void EnsureItemData()
    {
        if (runtimeItem != null) return;

        runtimeItem = ScriptableObject.CreateInstance<InventoryItemData>();
        runtimeItem.hideFlags = HideFlags.HideAndDontSave;
        runtimeItem.itemId = itemId;
        runtimeItem.displayName = displayName;
        runtimeItem.icon = ResolveSprite();
    }

    void OnDestroy()
    {
        if (runtimeItem == null) return;
        if (Application.isPlaying) Destroy(runtimeItem);
        else DestroyImmediate(runtimeItem);
        runtimeItem = null;
    }
}
