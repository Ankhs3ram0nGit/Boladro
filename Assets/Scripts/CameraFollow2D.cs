using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    public Transform target;
    public float smoothTime = 0.15f;
    public Vector3 offset = new Vector3(0f, 0f, -10f);
    public float damageShakeDuration = 0.16f;
    public float damageShakeMagnitude = 0.22f;

    private Vector3 velocity;
    private float shakeTimer;
    private float shakeInitialDuration;
    private float shakeMagnitude;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = target.position + offset;
        Vector3 position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);

        if (shakeTimer > 0f)
        {
            shakeTimer -= Time.deltaTime;
            float t = shakeInitialDuration > 0.0001f ? Mathf.Clamp01(shakeTimer / shakeInitialDuration) : 0f;
            float dampedMagnitude = shakeMagnitude * t;
            Vector2 jitter = Random.insideUnitCircle * dampedMagnitude;
            position += new Vector3(jitter.x, jitter.y, 0f);
        }

        transform.position = position;
    }

    public void TriggerDamageShake(float intensityMultiplier = 1f)
    {
        float duration = Mathf.Max(0.01f, damageShakeDuration);
        float magnitude = Mathf.Max(0.01f, damageShakeMagnitude * Mathf.Max(0.25f, intensityMultiplier));

        if (shakeTimer < duration)
        {
            shakeTimer = duration;
            shakeInitialDuration = duration;
        }
        shakeMagnitude = Mathf.Max(shakeMagnitude, magnitude);
    }
}
