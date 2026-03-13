using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFromAtlas : MonoBehaviour
{
    public Texture2D texture;
    public int tileSize = 16;
    public int tileX = 0;
    public int tileY = 0; // 0 = bottom row
    public float pixelsPerUnit = 16f;

    void Awake()
    {
        ApplySprite();
    }

    void OnEnable()
    {
        ApplySprite();
    }

    public void ApplySprite()
    {
        if (texture == null || tileSize <= 0) return;

        int x = Mathf.Clamp(tileX, 0, texture.width / tileSize - 1) * tileSize;
        int y = Mathf.Clamp(tileY, 0, texture.height / tileSize - 1) * tileSize;
        Rect rect = new Rect(x, y, tileSize, tileSize);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        Sprite sprite = Sprite.Create(texture, rect, pivot, pixelsPerUnit);
        GetComponent<SpriteRenderer>().sprite = sprite;
    }
}
