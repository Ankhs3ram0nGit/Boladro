using UnityEngine;

[DisallowMultipleComponent]
public class FeetAnchorAuto : MonoBehaviour
{
    public Transform feetAnchor;
    public float yOffset = 0f;

    void Awake()
    {
        EnsureFeetAnchor();
    }

    void OnValidate()
    {
        EnsureFeetAnchor();
    }

    void EnsureFeetAnchor()
    {
        if (feetAnchor == null)
        {
            Transform existing = transform.Find("Feet");
            if (existing != null) feetAnchor = existing;
        }

        if (feetAnchor == null)
        {
            GameObject go = new GameObject("Feet");
            go.transform.SetParent(transform, false);
            feetAnchor = go.transform;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            float localY = sr.bounds.min.y - transform.position.y;
            Vector3 local = new Vector3(0f, localY + yOffset, 0f);
            feetAnchor.localPosition = local;
        }
    }
}
