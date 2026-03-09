using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 버튼 눌림·텍스트 팝 등 범용 UI 스케일 효과. IPointerDown/Up으로 버튼, PlayPopEffect()로 이벤트 연출.
/// </summary>
public class UIBounceEffect : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Button Press Settings")]
    [Tooltip("버튼 눌림 시 축소 효과 사용 여부")]
    public bool usePressEffect = true;

    [Tooltip("눌렸을 때 스케일")]
    public float pressScale = 0.85f;

    [Tooltip("눌림/복귀 애니메이션 시간")]
    public float pressDuration = 0.05f;

    [Header("Pop Effect Settings (For Text/Events)")]
    [Tooltip("이벤트 시 일시적으로 커지는 스케일")]
    public float popScale = 1.3f;

    [Tooltip("커졌다가 돌아오는 한 구간당 시간")]
    public float popDuration = 0.15f;

    private Vector3 originalScale;
    private Coroutine currentCoroutine;

    private void Awake()
    {
        originalScale = transform.localScale;
    }

    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (!usePressEffect) return;
        StopCurrentAndRun(LerpScale(originalScale, originalScale * pressScale, pressDuration));
    }

    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData)
    {
        if (!usePressEffect) return;
        StopCurrentAndRun(LerpScale(transform.localScale, originalScale, pressDuration));
    }

    /// <summary>이벤트용 팝 연출: popScale로 커졌다가 originalScale로 복귀.</summary>
    public void PlayPopEffect()
    {
        StopCurrentAndRun(PopEffectRoutine());
    }

    private void StopCurrentAndRun(IEnumerator routine)
    {
        if (currentCoroutine != null)
        {
            StopCoroutine(currentCoroutine);
            currentCoroutine = null;
        }
        currentCoroutine = StartCoroutine(Runner(routine));
    }

    private IEnumerator Runner(IEnumerator routine)
    {
        yield return routine;
        currentCoroutine = null;
    }

    private IEnumerator LerpScale(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        transform.localScale = to;
    }

    private IEnumerator PopEffectRoutine()
    {
        Vector3 pop = new Vector3(popScale, popScale, 1f);
        yield return LerpScale(originalScale, pop, popDuration);
        yield return LerpScale(pop, originalScale, popDuration);
    }
}
