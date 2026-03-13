using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    public CreatureHealth health;
    public Vector3 worldOffset = new Vector3(0f, 0.08f, 0f);

    public Canvas canvas;
    public Image fillImage;
    public Text levelText;
    public Texture2D barTexture;
    public Texture2D fillTexture;

    private SpriteRenderer sr;
    private static Sprite solidWhiteSprite;

    void Awake()
    {
        if (health == null) health = GetComponent<CreatureHealth>();
        sr = GetComponent<SpriteRenderer>();

        if (canvas == null) canvas = GetComponentInChildren<Canvas>(true);
        if (fillImage == null)
        {
            Transform t = transform.Find("EnemyUI/HealthBarBG/HealthBarFill");
            if (t != null) fillImage = t.GetComponent<Image>();
        }
        if (levelText == null)
        {
            Transform t = transform.Find("EnemyUI/LevelText");
            if (t != null) levelText = t.GetComponent<Text>();
        }

        if (barTexture != null)
        {
            Image bg = null;
            Transform t = transform.Find("EnemyUI/HealthBarBG");
            if (t != null) bg = t.GetComponent<Image>();
            if (bg != null) bg.sprite = Sprite.Create(barTexture, new Rect(0, 0, barTexture.width, barTexture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        if (fillTexture != null && fillImage != null)
        {
            fillImage.sprite = Sprite.Create(fillTexture, new Rect(0, 0, fillTexture.width, fillTexture.height), new Vector2(0.5f, 0.5f), 100f);
        }

        EnsureRedFillStyle();
    }

    void LateUpdate()
    {
        if (canvas != null)
        {
            Vector3 pos = transform.position;
            if (sr != null) pos.y = sr.bounds.max.y;
            canvas.transform.position = pos + worldOffset;
        }

        if (health != null && fillImage != null)
        {
            float ratio = health.maxHealth > 0 ? (float)health.currentHealth / health.maxHealth : 0f;
            fillImage.fillAmount = Mathf.Clamp01(ratio);
        }

        if (levelText != null)
        {
            int lvl = health != null ? health.level : 1;
            levelText.text = "Lv " + lvl;
        }
    }

    void EnsureRedFillStyle()
    {
        if (fillImage == null) return;

        if (solidWhiteSprite == null)
        {
            Texture2D t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            solidWhiteSprite = Sprite.Create(t, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 100f);
        }

        fillImage.sprite = solidWhiteSprite;
        fillImage.color = new Color(0.88f, 0.14f, 0.14f, 1f);
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.fillOrigin = 0;
        fillImage.fillAmount = 1f;
    }
}
