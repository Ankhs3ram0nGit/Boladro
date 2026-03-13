using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class TopDownSorter : MonoBehaviour
{
    public enum SortMode
    {
        TransformY,
        FeetTransformY,
        RendererBottomY
    }

    public int orderOffset = 0;
    public int orderMultiplier = 100;
    public bool useSortingGroupIfPresent = true;
    public bool setSpriteSortPointToPivot = true;
    public bool useFixedShadowOrder = true;
    public int fixedShadowOrder = -999;
    public SortMode sortMode = SortMode.TransformY;
    public Transform feetTransform;

    private SpriteRenderer[] spriteRenderers;
    private SortingGroup sortingGroup;

    void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        sortingGroup = GetComponent<SortingGroup>();

        if (setSpriteSortPointToPivot)
        {
            foreach (var sr in spriteRenderers)
            {
                if (sr != null) sr.spriteSortPoint = SpriteSortPoint.Pivot;
            }
        }
    }

    void LateUpdate()
    {
        float y = transform.position.y;
        if (sortMode == SortMode.FeetTransformY && feetTransform != null)
        {
            y = feetTransform.position.y;
        }
        else if (sortMode == SortMode.RendererBottomY)
        {
            y = GetRendererBottomY();
        }

        int order = orderOffset - Mathf.RoundToInt(y * orderMultiplier);

        if (useSortingGroupIfPresent && sortingGroup != null)
        {
            sortingGroup.sortingOrder = order;
            return;
        }

        foreach (var sr in spriteRenderers)
        {
            if (sr == null) continue;
            string nameLower = sr.name.ToLowerInvariant();
            if (nameLower.Contains("heldtool")) continue;
            bool isShadow =
                sr.GetComponent<IgnoreOcclusionFade>() != null ||
                nameLower.Contains("shadow");
            sr.sortingOrder = (isShadow && useFixedShadowOrder) ? fixedShadowOrder : (isShadow ? order - 1 : order);
        }
    }

    private float GetRendererBottomY()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            return transform.position.y;
        }

        float minY = float.PositiveInfinity;
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer sr = spriteRenderers[i];
            if (sr == null) continue;
            minY = Mathf.Min(minY, sr.bounds.min.y);
        }

        if (float.IsPositiveInfinity(minY))
        {
            return transform.position.y;
        }

        return minY;
    }
}
