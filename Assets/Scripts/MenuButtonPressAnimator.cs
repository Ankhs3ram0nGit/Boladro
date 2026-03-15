using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class MenuButtonPressAnimator : MonoBehaviour, IPointerClickHandler
{
    [Range(0.6f, 0.98f)] public float pressedScale = 0.86f;
    [Range(1f, 1.25f)] public float overshootScale = 1.08f;
    [Min(0.01f)] public float shrinkDuration = 0.06f;
    [Min(0.01f)] public float growDuration = 0.07f;
    [Min(0.01f)] public float settleDuration = 0.06f;

    private RectTransform rectTransform;
    private Vector3 baseScale = Vector3.one;
    private Coroutine animRoutine;

    void Awake()
    {
        rectTransform = transform as RectTransform;
        if (rectTransform != null) baseScale = rectTransform.localScale;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!isActiveAndEnabled) return;
        if (rectTransform == null) rectTransform = transform as RectTransform;
        if (rectTransform == null) return;

        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(PlayPressAnim());
    }

    private IEnumerator PlayPressAnim()
    {
        Vector3 down = baseScale * Mathf.Clamp(pressedScale, 0.6f, 0.99f);
        Vector3 up = baseScale * Mathf.Max(1f, overshootScale);

        float t = 0f;
        float s = Mathf.Max(0.01f, shrinkDuration);
        while (t < s)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / s);
            rectTransform.localScale = Vector3.Lerp(baseScale, down, p);
            yield return null;
        }

        t = 0f;
        float g = Mathf.Max(0.01f, growDuration);
        while (t < g)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / g);
            rectTransform.localScale = Vector3.Lerp(down, up, p);
            yield return null;
        }

        t = 0f;
        float e = Mathf.Max(0.01f, settleDuration);
        while (t < e)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / e);
            rectTransform.localScale = Vector3.Lerp(up, baseScale, p);
            yield return null;
        }

        rectTransform.localScale = baseScale;
        animRoutine = null;
    }
}
