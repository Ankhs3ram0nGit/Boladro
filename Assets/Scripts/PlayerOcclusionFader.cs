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

            Bounds b = f.GetBounds();
            float minX = b.min.x - checkRadius;
            float maxX = b.max.x + checkRadius;
            float minY = b.min.y - checkRadius;
            float maxY = b.max.y + checkRadius;
            bool within = feet.x >= minX && feet.x <= maxX && feet.y >= minY && feet.y <= maxY;
            int id = f.GetInstanceID();
            bool currentlyFaded = isFadedById.TryGetValue(id, out bool stored) && stored;
            float threshold = currentlyFaded ? fadeInBehindMargin : fadeOutBehindMargin;
            bool behind = feet.y > b.min.y + threshold;

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
