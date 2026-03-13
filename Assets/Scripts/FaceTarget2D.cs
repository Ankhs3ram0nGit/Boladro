using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FaceTarget2D : MonoBehaviour
{
    public Transform target;
    public bool spriteFacesRight = true;

    private SpriteRenderer sr;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Update()
    {
        if (target == null) return;

        bool targetIsLeft = target.position.x < transform.position.x;
        bool flip = spriteFacesRight ? targetIsLeft : !targetIsLeft;
        sr.flipX = flip;
    }
}
