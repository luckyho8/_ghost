using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class UIRepeatButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private float initialDelay = 0.18f;
    [SerializeField] private float repeatInterval = 0.04f;

    private Action onPressAction;
    private Coroutine repeatCo;

    public void SetAction(Action action) => onPressAction = action;

    public void SetTiming(float initialDelay, float repeatInterval)
    {
        this.initialDelay = initialDelay;
        this.repeatInterval = repeatInterval;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (onPressAction == null) return;
        onPressAction.Invoke();
        if (repeatCo != null) StopCoroutine(repeatCo);
        repeatCo = StartCoroutine(RepeatLoop());
    }

    public void OnPointerUp(PointerEventData eventData) => StopRepeat();

    private void OnDisable() => StopRepeat();

    private void StopRepeat()
    {
        if (repeatCo != null)
        {
            StopCoroutine(repeatCo);
            repeatCo = null;
        }
    }

    private IEnumerator RepeatLoop()
    {
        yield return new WaitForSeconds(initialDelay);
        var wait = new WaitForSeconds(repeatInterval);
        while (true)
        {
            onPressAction?.Invoke();
            yield return wait;
        }
    }
}
