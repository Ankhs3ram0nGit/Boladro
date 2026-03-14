using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class InventoryHotbar : MonoBehaviour
{
    public int slotCount = 5;
    public int slotSize = 90;
    public int iconSize = 75;
    public int spacing = 6;
    public int selectedIndex = 0;
    public Color normalColor = new Color(0.75f, 0.75f, 0.78f, 1f);
    public Color selectedColor = new Color(1f, 0.9f, 0.6f, 1f);
    public Sprite slotSprite;
    public Sprite selectedSprite;

    private Image[] slots;

    void Awake()
    {
#if UNITY_EDITOR
        EnsureSprites();
#endif
        BuildIfNeeded();
        ApplySelection();
    }

    void Update()
    {
        int keyIndex = ReadSlotKey();
        if (keyIndex >= 0)
        {
            SelectSlot(keyIndex);
        }

        Keyboard kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.qKey.wasPressedThisFrame)
            {
                SelectSlot((selectedIndex - 1 + slotCount) % slotCount);
            }
            // Reserve E for creature engagement; use R for hotbar forward cycling.
            if (kb.rKey.wasPressedThisFrame)
            {
                SelectSlot((selectedIndex + 1) % slotCount);
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll > 0.01f)
            {
                SelectSlot((selectedIndex - 1 + slotCount) % slotCount);
            }
            else if (scroll < -0.01f)
            {
                SelectSlot((selectedIndex + 1) % slotCount);
            }
        }
    }

    int ReadSlotKey()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return -1;

        if (kb.digit1Key.wasPressedThisFrame) return 0;
        if (kb.digit2Key.wasPressedThisFrame) return 1;
        if (kb.digit3Key.wasPressedThisFrame) return 2;
        if (kb.digit4Key.wasPressedThisFrame) return 3;
        if (kb.digit5Key.wasPressedThisFrame) return 4;

        return -1;
    }

    void BuildIfNeeded()
    {
        HorizontalLayoutGroup layout = GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
        {
            layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        }
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        if (transform.childCount != slotCount)
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                if (Application.isPlaying)
                {
                    Destroy(transform.GetChild(i).gameObject);
                }
                else
                {
                    DestroyImmediate(transform.GetChild(i).gameObject);
                }
            }

            for (int i = 0; i < slotCount; i++)
            {
                GameObject slot = new GameObject("Slot" + (i + 1));
                slot.transform.SetParent(transform, false);
                Image img = slot.AddComponent<Image>();
                img.raycastTarget = false;
                img.sprite = slotSprite;
                RectTransform rt = slot.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(slotSize, slotSize);
            }
        }

        slots = new Image[slotCount];
        for (int i = 0; i < slotCount; i++)
        {
            Transform child = transform.GetChild(i);
            slots[i] = child.GetComponent<Image>();
        }
    }

    void ApplySelection()
    {
        if (slots == null) return;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] == null) continue;
            bool selected = i == selectedIndex;
            slots[i].color = selected ? selectedColor : normalColor;
            if (selected && selectedSprite != null)
            {
                slots[i].sprite = selectedSprite;
            }
            else if (slotSprite != null)
            {
                slots[i].sprite = slotSprite;
            }
        }
    }

    public void SelectSlot(int index)
    {
        if (index < 0 || index >= slotCount) return;
        selectedIndex = index;
        ApplySelection();
    }

    public void SetSlotIcon(int index, Sprite icon)
    {
        if (index < 0 || index >= slotCount) return;
        Transform child = transform.GetChild(index);
        Image iconImage = child.Find("Icon")?.GetComponent<Image>();
        if (iconImage == null)
        {
            GameObject iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(child, false);
            iconImage = iconObj.AddComponent<Image>();
            iconImage.raycastTarget = false;
            RectTransform rt = iconObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
        }

        RectTransform iconRt = iconImage.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0.5f, 0.5f);
        iconRt.anchorMax = new Vector2(0.5f, 0.5f);
        iconRt.pivot = new Vector2(0.5f, 0.5f);
        iconRt.anchoredPosition = Vector2.zero;
        iconRt.sizeDelta = new Vector2(iconSize, iconSize);

        iconImage.sprite = icon;
        iconImage.color = Color.white;
    }

#if UNITY_EDITOR
    void EnsureSprites()
    {
        if (slotSprite == null)
        {
            slotSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_FrameSlot01a.png");
        }
        if (selectedSprite == null)
        {
            selectedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Complete_UI_Essential_Pack_Free/01_Flat_Theme/Sprites/UI_Flat_FrameSlot01c.png");
        }
    }

    void OnValidate()
    {
        EnsureSprites();
    }
#endif
}
