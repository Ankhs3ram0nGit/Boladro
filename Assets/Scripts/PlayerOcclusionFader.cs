using UnityEngine;

public class PlayerOcclusionFader : MonoBehaviour
{
    public Transform feetAnchor;
    public float checkRadius = 0.25f;
    public float fadeOutBehindMargin = 0.02f;
    public float fadeInBehindMargin = -0.04f;

    private FadeableSprite[] fadeables;
    private readonly System.Collections.Generic.Dictionary<int, bool> isFadedById = new System.Collections.Generic.Dictionary<int, bool>();

    void Awake()
    {
        if (feetAnchor == null)
        {
            Transform t = transform.Find("Feet");
            if (t != null) feetAnchor = t;
        }
    }

    void LateUpdate()
    {
        if (feetAnchor == null) return;

        if (fadeables == null || fadeables.Length == 0)
        {
            fadeables = FindObjectsByType<FadeableSprite>(FindObjectsSortMode.None);
        }

        Vector3 feet = feetAnchor.position;
        for (int i = 0; i < fadeables.Length; i++)
        {
            FadeableSprite f = fadeables[i];
            if (f == null) continue;

            Bounds visualBounds = f.GetVisualBounds();
            float minX = visualBounds.min.x - checkRadius;
            float maxX = visualBounds.max.x + checkRadius;
            float minY = visualBounds.min.y - checkRadius;
            float maxY = visualBounds.max.y + checkRadius;
            bool within = feet.x >= minX && feet.x <= maxX && feet.y >= minY && feet.y <= maxY;

            float fadeLineY = visualBounds.min.y;
            if (f.TryGetOcclusionBounds(out Bounds occlusionBounds))
            {
                // For houses/large props with foot colliders, this line defines
                // where "behind" starts while still using full visual bounds for coverage.
                fadeLineY = occlusionBounds.max.y;
            }

            int id = f.GetInstanceID();
            bool currentlyFaded = isFadedById.TryGetValue(id, out bool stored) && stored;
            float threshold = currentlyFaded ? fadeInBehindMargin : fadeOutBehindMargin;
            bool behind = feet.y > fadeLineY + threshold;

            if (within && behind)
            {
                f.FadeOut();
                isFadedById[id] = true;
            }
            else
            {
                f.FadeIn();
                isFadedById[id] = false;
            }
        }
    }
}
