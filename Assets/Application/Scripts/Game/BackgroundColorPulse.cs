using System.Collections;
using UnityEngine;

/// <summary>
/// 배경 메시 머티리얼의 색상을 펄스 형태로 변경했다가 원본으로 천천히 돌아오게 함.
/// 콤보 발생 시 UIManager에서 호출 → 배경이 콤보 등급별 색상으로 변하고 8초 동안 페이드 복귀.
/// MaterialPropertyBlock 사용으로 머티리얼 에셋은 변경하지 않음.
/// </summary>
[RequireComponent(typeof(MeshRenderer))]
public class BackgroundColorPulse : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("색상 변경 대상 렌더러 (비워두면 같은 GameObject의 MeshRenderer)")]
    [SerializeField] private MeshRenderer targetRenderer;

    [Header("Timing")]
    [Tooltip("펀치인 시간 (초) - 원본 색상 → 펄스 색상으로 차오르는 시간. 0이면 즉시 스냅")]
    [SerializeField] private float punchInDuration = 0.08f;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock _block;
    private Color _originalColor = Color.white;
    private bool _initialized;
    private Coroutine _pulseCo;

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (_initialized) return;
        if (targetRenderer == null) targetRenderer = GetComponent<MeshRenderer>();
        if (targetRenderer == null) return;

        _block = new MaterialPropertyBlock();
        if (targetRenderer.sharedMaterial != null)
            _originalColor = targetRenderer.sharedMaterial.GetColor(BaseColorId);
        _initialized = true;
    }

    /// <summary>
    /// 배경 색상을 targetColor로 펀치인 후 fadeDuration 동안 원본 색상으로 페이드 복귀.
    /// 진행 중에 다시 호출되면 현재 색상에서 새 targetColor로 펀치인 후 새 fadeDuration 시작.
    /// </summary>
    public void Pulse(Color targetColor, float fadeDuration)
    {
        if (!_initialized) Initialize();
        if (!_initialized) return;

        if (_pulseCo != null) StopCoroutine(_pulseCo);
        _pulseCo = StartCoroutine(PulseRoutine(targetColor, fadeDuration));
    }

    /// <summary>현재 진행 중인 펄스를 즉시 취소하고 원본 색상으로 복귀</summary>
    public void ResetToOriginal()
    {
        if (!_initialized) Initialize();
        if (_pulseCo != null) StopCoroutine(_pulseCo);
        _pulseCo = null;
        ApplyColor(_originalColor);
    }

    private IEnumerator PulseRoutine(Color targetColor, float fadeDuration)
    {
        Color startColor = ReadCurrentColor();

        // 1) 펀치인 — 현재 색상 → 타겟 색상으로 빠르게
        float elapsed = 0f;
        while (elapsed < punchInDuration)
        {
            elapsed += Time.deltaTime;
            float r = Mathf.Clamp01(elapsed / punchInDuration);
            ApplyColor(Color.Lerp(startColor, targetColor, r));
            yield return null;
        }
        ApplyColor(targetColor);

        // 2) 슬로우 페이드 — 타겟 색상 → 원본 색상 (콤보 윈도우 8초)
        elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float r = Mathf.Clamp01(elapsed / fadeDuration);
            ApplyColor(Color.Lerp(targetColor, _originalColor, r));
            yield return null;
        }
        ApplyColor(_originalColor);
        _pulseCo = null;
    }

    private Color ReadCurrentColor()
    {
        if (targetRenderer == null) return _originalColor;
        targetRenderer.GetPropertyBlock(_block);
        // PropertyBlock가 비어있으면 원본 머티리얼 색상 사용
        if (_block.isEmpty)
            return _originalColor;
        // SetColor가 호출된 적 있으면 GetColor로 마지막 값 회수 가능
        return _block.GetColor(BaseColorId);
    }

    private void ApplyColor(Color c)
    {
        if (targetRenderer == null) return;
        targetRenderer.GetPropertyBlock(_block);
        _block.SetColor(BaseColorId, c);
        _block.SetColor(ColorId, c);
        targetRenderer.SetPropertyBlock(_block);
    }
}
