using UnityEngine;

[DisallowMultipleComponent]
public class FadeableSprite : MonoBehaviour
{
    public float fadedAlpha = 0.4f;
    public float fadeSpeed = 12f;

    private SpriteRenderer[] renderers;
    private float targetAlpha = 1f;

    void Awake()
    {
        CacheRenderers();
    }

    private void OnEnable()
    {
        CacheRenderers();
    }

    private void CacheRenderers()
    {
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private static bool ShouldIgnoreFade(SpriteRenderer sr)
    {
        return sr != null && sr.GetComponent<IgnoreOcclusionFade>() != null;
    }

    public void FadeOut()
    {
        targetAlpha = Mathf.Clamp01(fadedAlpha);
    }

    public void FadeIn()
    {
        targetAlpha = 1f;
    }

    void Update()
    {
        if (renderers == null || renderers.Length == 0)
        {
            CacheRenderers();
        }
        if (renderers == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null) continue;
            if (ShouldIgnoreFade(sr)) continue;
            Color c = sr.color;
            c.a = Mathf.MoveTowards(c.a, targetAlpha, fadeSpeed * Time.deltaTime);
            sr.color = c;
        }
    }

    public Bounds GetBounds()
    {
        if (renderers == null || renderers.Length == 0)
        {
            CacheRenderers();
            if (renderers == null || renderers.Length == 0)
            {
                return new Bounds(transform.position, Vector3.zero);
            }
        }

        Bounds b = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || ShouldIgnoreFade(sr)) continue;
            if (!hasBounds)
            {
                b = sr.bounds;
                hasBounds = true;
            }
            else
            {
                b.Encapsulate(sr.bounds);
            }
        }

        if (!hasBounds)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null) continue;
                if (!hasBounds)
                {
                    b = sr.bounds;
                    hasBounds = true;
                }
                else
                {
                    b.Encapsulate(sr.bounds);
                }
            }
        }

        if (!hasBounds)
        {
            return new Bounds(transform.position, Vector3.zero);
        }

        return b;
    }
}
