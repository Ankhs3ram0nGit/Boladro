using UnityEngine;

public static class ExperienceOrbVisuals
{
    private static Sprite coreSprite;
    private static Sprite glowSprite;

    public static Sprite CoreSprite => coreSprite != null ? coreSprite : (coreSprite = CreateRadialSprite("XPOrbCore", 20, 0.30f));
    public static Sprite GlowSprite => glowSprite != null ? glowSprite : (glowSprite = CreateRadialSprite("XPOrbGlow", 40, 0.55f));

    private static Sprite CreateRadialSprite(string name, int size, float softEdgeStart)
    {
        int texSize = Mathf.Clamp(size, 8, 128);
        Texture2D tex = new Texture2D(texSize, texSize, TextureFormat.RGBA32, false);
        tex.name = name + "_Tex";
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.hideFlags = HideFlags.HideAndDontSave;

        Vector2 center = new Vector2((texSize - 1) * 0.5f, (texSize - 1) * 0.5f);
        float radius = texSize * 0.5f;
        float falloffStart = Mathf.Clamp01(softEdgeStart) * radius;

        for (int y = 0; y < texSize; y++)
        {
            for (int x = 0; x < texSize; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), center);
                if (d > radius)
                {
                    tex.SetPixel(x, y, Color.clear);
                    continue;
                }

                float alpha = 1f;
                if (d > falloffStart)
                {
                    float t = Mathf.InverseLerp(radius, falloffStart, d);
                    alpha = Mathf.Clamp01(t);
                }

                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply(false, true);
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = name;
        return sprite;
    }
}
