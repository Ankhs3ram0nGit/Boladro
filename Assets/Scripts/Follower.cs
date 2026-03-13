using UnityEngine;
using System.Collections;

public class Follower : MonoBehaviour
{
    public Transform target;
    public float followDistance = 0.6f;
    public float hopTilesPerMove = 2f;
    [Tooltip("1.0 = original speed, 0.33 = one-third speed.")]
    public float movementSpeedMultiplier = 0.33333334f;
    public float hopCrouchTime = 0.14f;
    public float hopAirTime = 0.5f;
    public float hopLandTime = 0.12f;
    public float hopPauseTime = 0.10f;
    public float hopArcHeight = 0.18f;
    public bool spriteFacesRight = false;

    private SpriteRenderer sr;
    private Vector3 baseScale = Vector3.one;
    private bool hopping;
    private Grid grid;

    public bool IsHopping => hopping;

    void Start()
    {
        hopTilesPerMove = 2f;
        if (GetComponent<CreatureGroundShadow>() == null)
        {
            gameObject.AddComponent<CreatureGroundShadow>();
        }
        FaceTarget2D faceTarget = GetComponent<FaceTarget2D>();
        if (faceTarget != null)
        {
            // Preserve authored orientation metadata, but movement-facing should come from Follower logic.
            spriteFacesRight = faceTarget.spriteFacesRight;
            faceTarget.enabled = false;
        }
        sr = GetComponent<SpriteRenderer>();
        if (sr != null) baseScale = sr.transform.localScale;
        grid = FindAnyObjectByType<Grid>();
        WildCreatureAI wild = FindAnyObjectByType<WildCreatureAI>();
        if (wild != null)
        {
            movementSpeedMultiplier = Mathf.Max(0.01f, wild.movementSpeedMultiplier);
            hopCrouchTime = wild.hopCrouchTime;
            hopAirTime = wild.hopAirTime;
            hopLandTime = wild.hopLandTime;
            hopPauseTime = wild.hopPauseTime;
        }

        if (target == null)
        {
            GameObject player = GameObject.Find("Player");
            if (player != null)
            {
                target = player.transform;
            }
        }
    }

    void Update()
    {
        if (target == null) return;
        if (hopping) return;

        Vector2 current = transform.position;
        Vector2 goal = target.position;
        Vector2 toTarget = goal - current;

        float dist = toTarget.magnitude;
        if (dist <= followDistance)
        {
            return;
        }

        Vector2 dir = toTarget.normalized;
        FaceMove(dir);
        float step = GetHopDistanceWorld();
        float available = dist - followDistance;
        if (available < step * 0.9f)
        {
            return;
        }
        StartCoroutine(HopTowards(SnapDirection(dir), step));
    }

    IEnumerator HopTowards(Vector2 dir, float step)
    {
        hopping = true;
        Vector3 startScale = baseScale;
        float speedScale = 1f / Mathf.Max(0.01f, movementSpeedMultiplier);
        float crouchTime = hopCrouchTime * speedScale;
        float airTime = hopAirTime * speedScale;
        float landTime = hopLandTime * speedScale;
        float pauseTime = hopPauseTime * speedScale;

        yield return AnimateScale(startScale, new Vector3(startScale.x * 1.08f, startScale.y * 0.86f, startScale.z), crouchTime);

        Vector3 startPos = transform.position;
        Vector3 endPos = new Vector3(startPos.x + dir.x * step, startPos.y + dir.y * step, startPos.z);
        Vector3 stretch = new Vector3(startScale.x * 0.92f, startScale.y * 1.10f, startScale.z);

        float t = 0f;
        while (t < airTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / airTime);
            float eased = Mathf.SmoothStep(0f, 1f, u);
            Vector3 flat = Vector3.Lerp(startPos, endPos, eased);
            float arc = 4f * u * (1f - u) * hopArcHeight;
            transform.position = new Vector3(flat.x, flat.y + arc, flat.z);
            if (sr != null) sr.transform.localScale = Vector3.Lerp(startScale, stretch, Mathf.Sin(u * Mathf.PI));
            yield return null;
        }
        transform.position = endPos;

        yield return AnimateScale(sr != null ? sr.transform.localScale : startScale, new Vector3(startScale.x * 1.05f, startScale.y * 0.90f, startScale.z), landTime);
        yield return AnimateScale(sr != null ? sr.transform.localScale : startScale, startScale, pauseTime);

        hopping = false;
    }

    IEnumerator AnimateScale(Vector3 from, Vector3 to, float duration)
    {
        if (sr == null || duration <= 0f) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float u = Mathf.Clamp01(t / duration);
            sr.transform.localScale = Vector3.Lerp(from, to, u);
            yield return null;
        }
        sr.transform.localScale = to;
    }

    float GetHopDistanceWorld()
    {
        float tile = 1f;
        if (grid == null) grid = FindAnyObjectByType<Grid>();
        if (grid != null)
        {
            tile = Mathf.Abs(grid.cellSize.x * grid.transform.lossyScale.x);
            if (tile < 0.0001f) tile = 1f;
        }
        return Mathf.Max(0.01f, hopTilesPerMove) * tile;
    }

    Vector2 SnapDirection(Vector2 raw)
    {
        if (raw.sqrMagnitude <= 0.0001f) return Vector2.right;
        return raw.normalized;
    }

    void FaceMove(Vector2 delta)
    {
        if (sr == null) return;
        if (Mathf.Abs(delta.x) <= 0.01f) return;
        bool movingLeft = delta.x < 0f;
        sr.flipX = spriteFacesRight ? movingLeft : !movingLeft;
    }
}
