using System;
using UnityEngine;

[Serializable]
public class InventorySlot
{
    public InventoryItemData item;
    public int count;

    public bool IsEmpty()
    {
        return item == null || count <= 0;
    }

    public void Clear()
    {
        item = null;
        count = 0;
    }

    public void Set(InventoryItemData newItem, int newCount)
    {
        item = newItem;
        count = newCount;
    }
}

public class InventoryModel : MonoBehaviour
{
    public int hotbarSize = 9;
    public int bagColumns = 9;
    public int bagRows = 3;

    public InventorySlot[] hotbar;
    public InventorySlot[] bag;

    public event Action OnChanged;

    void Awake()
    {
        EnsureSlots();
    }

    public void EnsureSlots()
    {
        if (hotbar == null || hotbar.Length != hotbarSize)
        {
            hotbar = CreateSlots(hotbarSize);
        }
        if (bag == null || bag.Length != bagColumns * bagRows)
        {
            bag = CreateSlots(bagColumns * bagRows);
        }
    }

    InventorySlot[] CreateSlots(int count)
    {
        InventorySlot[] slots = new InventorySlot[count];
        for (int i = 0; i < count; i++)
        {
            slots[i] = new InventorySlot();
        }
        return slots;
    }

    public InventorySlot GetHotbarSlot(int index)
    {
        if (index < 0 || index >= hotbar.Length) return null;
        return hotbar[index];
    }

    public InventorySlot GetBagSlot(int index)
    {
        if (index < 0 || index >= bag.Length) return null;
        return bag[index];
    }

    public void NotifyChanged()
    {
        if (OnChanged != null) OnChanged.Invoke();
    }

    public bool TryAddItem(InventoryItemData item, int count = 1)
    {
        EnsureSlots();
        if (item == null || count <= 0) return false;

        int remaining = count;
        remaining = AddToMatchingSlots(hotbar, item, remaining);
        remaining = AddToMatchingSlots(bag, item, remaining);
        remaining = AddToEmptySlots(hotbar, item, remaining);
        remaining = AddToEmptySlots(bag, item, remaining);

        if (remaining != count)
        {
            NotifyChanged();
        }

        return remaining == 0;
    }

    public bool TryAddItemToHotbarFirst(InventoryItemData item, int count = 1)
    {
        EnsureSlots();
        if (item == null || count <= 0) return false;

        int remaining = count;
        remaining = AddToMatchingSlots(hotbar, item, remaining);
        remaining = AddToEmptySlots(hotbar, item, remaining);
        remaining = AddToMatchingSlots(bag, item, remaining);
        remaining = AddToEmptySlots(bag, item, remaining);

        if (remaining != count)
        {
            NotifyChanged();
        }

        return remaining == 0;
    }

    int AddToMatchingSlots(InventorySlot[] slots, InventoryItemData item, int remaining)
    {
        if (slots == null || remaining <= 0) return remaining;
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || slot.IsEmpty()) continue;
            if (!IsSameItem(slot.item, item)) continue;
            slot.count += remaining;
            remaining = 0;
        }
        return remaining;
    }

    int AddToEmptySlots(InventorySlot[] slots, InventoryItemData item, int remaining)
    {
        if (slots == null || remaining <= 0) return remaining;
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            InventorySlot slot = slots[i];
            if (slot == null || !slot.IsEmpty()) continue;
            slot.Set(item, remaining);
            remaining = 0;
        }
        return remaining;
    }

    static bool IsSameItem(InventoryItemData a, InventoryItemData b)
    {
        if (a == b) return true;
        if (a == null || b == null) return false;

        string aId = string.IsNullOrWhiteSpace(a.itemId) ? string.Empty : a.itemId.Trim().ToLowerInvariant();
        string bId = string.IsNullOrWhiteSpace(b.itemId) ? string.Empty : b.itemId.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(aId) && !string.IsNullOrEmpty(bId))
        {
            return aId == bId;
        }
        return string.Equals(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase);
    }
}
