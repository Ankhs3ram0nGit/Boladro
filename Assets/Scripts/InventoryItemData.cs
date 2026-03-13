using UnityEngine;

[CreateAssetMenu(menuName = "Boladro/Inventory Item", fileName = "NewInventoryItem")]
public class InventoryItemData : ScriptableObject
{
    public string itemId = "item";
    public string displayName = "Item";
    public Sprite icon;
}
