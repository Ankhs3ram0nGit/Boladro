using UnityEngine;

[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteFromTexture : MonoBehaviour
{
    public Texture2D texture;
    public float pixelsPerUnit = 128f;

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
        if (texture == null) return;

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        Rect rect = new Rect(0, 0, texture.width, texture.height);
        Vector2 pivot = new Vector2(0.5f, 0.5f);
        Sprite sprite = Sprite.Create(texture, rect, pivot, pixelsPerUnit);
        sr.sprite = sprite;
    }
}
