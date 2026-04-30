using System.Collections;
using UnityEngine;

/// <summary>
/// 스타트업 인트로: 로고 프리팹 표시 → 일정 시간 후 메인 Canvas로 전환.
/// Logo 프리팹과 메인 Canvas 어느 쪽에도 속하지 않은 GameObject에 부착할 것.
/// </summary>
public class LogoIntro : MonoBehaviour
{
    [Header("References")]
    [Tooltip("표시할 Logo 프리팹 인스턴스 (씬에 배치된 것)")]
    [SerializeField] private GameObject logoRoot;

    [Tooltip("로고 종료 후 켤 메인 Canvas (Tap to Start UI)")]
    [SerializeField] private GameObject mainCanvas;

    [Tooltip("(선택) 페이드아웃용 CanvasGroup. logoRoot에 없으면 자동 추가")]
    [SerializeField] private CanvasGroup logoCanvasGroup;

    [Header("Timing")]
    [Tooltip("로고 표시 시간 (초)")]
    [SerializeField] private float displayDuration = 1.5f;

    [Tooltip("페이드아웃 시간 (초). 0이면 즉시 전환")]
    [SerializeField] private float fadeDuration = 0.4f;

    private IEnumerator Start()
    {
        if (mainCanvas != null) mainCanvas.SetActive(false);

        if (logoRoot == null) yield break;

        logoRoot.SetActive(true);

        bool useFade = fadeDuration > 0f;
        if (useFade)
        {
            if (logoCanvasGroup == null)
                logoCanvasGroup = logoRoot.GetComponent<CanvasGroup>();
            if (logoCanvasGroup == null)
                logoCanvasGroup = logoRoot.AddComponent<CanvasGroup>();
            logoCanvasGroup.alpha = 1f;
        }

        yield return new WaitForSeconds(displayDuration);

        if (useFade && logoCanvasGroup != null)
        {
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.deltaTime;
                logoCanvasGroup.alpha = 1f - (t / fadeDuration);
                yield return null;
            }
            logoCanvasGroup.alpha = 0f;
        }

        logoRoot.SetActive(false);
        if (mainCanvas != null) mainCanvas.SetActive(true);
    }
}
