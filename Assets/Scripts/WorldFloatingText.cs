using UnityEngine;

[DisallowMultipleComponent]
public class WorldFloatingText : MonoBehaviour
{
    [Min(0.1f)] public float duration = 1.0f;
    [Min(0f)] public float riseDistance = 0.45f;

    private TextMesh mainText;
    private TextMesh shadowText;
    private Color mainColor;
    private Color shadowColor;
    private Vector3 startPos;
    private float elapsed;

    public static void Spawn(string text, Vector3 worldPosition, Color color, float durationSeconds = 1.0f)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        GameObject go = new GameObject("WorldFloatingText");
        go.transform.position = worldPosition;
        WorldFloatingText floating = go.AddComponent<WorldFloatingText>();
        floating.duration = Mathf.Max(0.1f, durationSeconds);
        floating.Initialize(text.Trim(), color);
    }

    private void Initialize(string text, Color color)
    {
        startPos = transform.position;
        mainText = BuildTextMesh("Main", Vector3.zero, color);
        shadowText = BuildTextMesh("Shadow", new Vector3(0.02f, -0.02f, 0f), new Color(0f, 0f, 0f, 0.95f));

        mainText.text = text;
        shadowText.text = text;
        mainColor = mainText.color;
        shadowColor = shadowText.color;
    }

    private TextMesh BuildTextMesh(string name, Vector3 localPos, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one;

        TextMesh mesh = go.AddComponent<TextMesh>();
        mesh.text = string.Empty;
        mesh.anchor = TextAnchor.MiddleCenter;
        mesh.alignment = TextAlignment.Center;
        mesh.characterSize = 0.08f;
        mesh.fontSize = 56;
        mesh.color = color;

        Font f = TryGetBuiltinFont("LegacyRuntime.ttf");
        if (f == null) f = TryGetBuiltinFont("Arial.ttf");
        if (f != null)
        {
            mesh.font = f;
            MeshRenderer r = mesh.GetComponent<MeshRenderer>();
            if (r != null)
            {
                r.sharedMaterial = f.material;
                r.sortingOrder = 1500;
            }
        }

        return mesh;
    }

    private static Font TryGetBuiltinFont(string resourceName)
    {
        try
        {
            return Resources.GetBuiltinResource<Font>(resourceName);
        }
        catch
        {
            return null;
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Mathf.Max(0.1f, duration));
        float eased = 1f - Mathf.Pow(1f - t, 3f);

        transform.position = startPos + new Vector3(0f, riseDistance * eased, 0f);
        float alpha = 1f - t;
        if (mainText != null)
        {
            Color c = mainColor;
            c.a *= alpha;
            mainText.color = c;
        }
        if (shadowText != null)
        {
            Color c = shadowColor;
            c.a *= alpha;
            shadowText.color = c;
        }

        Camera cam = Camera.main;
        if (cam != null)
        {
            transform.rotation = cam.transform.rotation;
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }
}
