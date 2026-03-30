using System.Collections;
using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Popup Prefabs")]
    [SerializeField] private GameObject popupPausePrefab;
    [SerializeField] private GameObject popupWinPrefab;
    [SerializeField] private GameObject popupFailPrefab;

    [Header("Canvas")]
    [SerializeField] private Transform popupRoot;

    // ── Plus Score 연출 ─────────────────────────────────────────────
    [Header("Plus Score 연출")]
    [Tooltip("획득 점수 표시 TMP (Plus_Score 하위의 Score_Txt)")]
    [SerializeField] private TextMeshProUGUI plusScore_Txt;

    [Tooltip("Plus_Score 펀치 연출 시간 (커졌다 돌아오는 전체 시간)")]
    [SerializeField] private float plusScorePunchDuration = 0.25f;

    [Tooltip("Plus_Score 펀치 최대 스케일")]
    [SerializeField] private float plusScorePunchScale = 1.3f;

    // ── Description 연출 ────────────────────────────────────────────
    [Header("Description 연출")]
    [Tooltip("점수 획득 사유 표시 TMP (예: 1 Line Clear!!)")]
    [SerializeField] private TextMeshProUGUI scoreDesc_Txt;

    [Tooltip("Description 페이드인 시간")]
    [SerializeField] private float descFadeInDuration = 0.15f;

    [Tooltip("Description 표시 유지 시간")]
    [SerializeField] private float descHoldDuration = 0.5f;

    [Tooltip("Description 페이드아웃 시간")]
    [SerializeField] private float descFadeOutDuration = 0.2f;

    [Tooltip("Description 펀치 스케일 (1이면 펀치 없음)")]
    [SerializeField] private float descPunchScale = 1.15f;

    // ── Ghost Toggle UI ─────────────────────────────────────────────
    [Header("Ghost Toggle UI")]
    [Tooltip("Ghost_On 오브젝트 (고스트 활성 시 표시)")]
    [SerializeField] private GameObject ghostOnObj;
    [Tooltip("Ghost_Off 오브젝트 (고스트 비활성 시 표시)")]
    [SerializeField] private GameObject ghostOffObj;

    private GameObject _currentPopup;
    private Coroutine _plusScoreCoroutine;
    private Coroutine _scoreDescCoroutine;
    private Vector3 _plusScoreOriginalScale;
    private Vector3 _scoreDescOriginalScale;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (plusScore_Txt != null)
        {
            _plusScoreOriginalScale = plusScore_Txt.transform.localScale;
            SetTMPAlpha(plusScore_Txt, 0f);
        }
        if (scoreDesc_Txt != null)
        {
            _scoreDescOriginalScale = scoreDesc_Txt.transform.localScale;
            SetTMPAlpha(scoreDesc_Txt, 0f);
        }
    }

    // ── Plus Score 연출 ─────────────────────────────────────────────

    /// <summary>획득 점수와 설명 텍스트를 연출과 함께 표시한다.</summary>
    public void ShowPlusScore(int score, string description = null)
    {
        // Plus Score: 펀치 + 알파 페이드
        if (plusScore_Txt != null)
        {
            plusScore_Txt.text = $"+ {score}";
            if (_plusScoreCoroutine != null) StopCoroutine(_plusScoreCoroutine);
            _plusScoreCoroutine = StartCoroutine(PunchAlphaRoutine(
                plusScore_Txt, _plusScoreOriginalScale, plusScorePunchScale, plusScorePunchDuration));
        }

        // Description: 별도 연출 (빠른 획득 시 즉시 리셋 후 재시작)
        if (scoreDesc_Txt != null && !string.IsNullOrEmpty(description))
        {
            scoreDesc_Txt.text = description;
            if (_scoreDescCoroutine != null)
            {
                StopCoroutine(_scoreDescCoroutine);
                // 즉시 리셋
                scoreDesc_Txt.transform.localScale = _scoreDescOriginalScale;
                SetTMPAlpha(scoreDesc_Txt, 0f);
            }
            _scoreDescCoroutine = StartCoroutine(DescriptionRoutine());
        }
    }

    /// <summary>Plus_Score 전용: 스케일 펀치 + 알파 페이드(0→1→0)</summary>
    private IEnumerator PunchAlphaRoutine(TextMeshProUGUI tmp, Vector3 origScale, float punchScale, float duration)
    {
        Transform t = tmp.transform;
        float half = duration * 0.5f;
        Vector3 peakScale = origScale * punchScale;

        // Phase 1: 커지면서 알파 0→1
        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / half);
            t.localScale = Vector3.Lerp(origScale, peakScale, ratio);
            SetTMPAlpha(tmp, ratio);
            yield return null;
        }
        t.localScale = peakScale;
        SetTMPAlpha(tmp, 1f);

        // Phase 2: 원래 크기로 돌아가면서 알파 1→0
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / half);
            t.localScale = Vector3.Lerp(peakScale, origScale, ratio);
            SetTMPAlpha(tmp, 1f - ratio);
            yield return null;
        }
        t.localScale = origScale;
        SetTMPAlpha(tmp, 0f);
    }

    /// <summary>Description 전용: 페이드인 → 유지 → 페이드아웃 (펀치 옵션)</summary>
    private IEnumerator DescriptionRoutine()
    {
        Transform t = scoreDesc_Txt.transform;
        Vector3 origScale = _scoreDescOriginalScale;
        Vector3 peakScale = origScale * descPunchScale;

        // Phase 1: 페이드인 + 살짝 커짐
        float elapsed = 0f;
        while (elapsed < descFadeInDuration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / descFadeInDuration);
            SetTMPAlpha(scoreDesc_Txt, ratio);
            t.localScale = Vector3.Lerp(origScale, peakScale, ratio);
            yield return null;
        }
        SetTMPAlpha(scoreDesc_Txt, 1f);
        t.localScale = peakScale;

        // Phase 2: 유지 (펀치 복귀)
        elapsed = 0f;
        float punchReturnTime = Mathf.Min(0.1f, descHoldDuration);
        while (elapsed < punchReturnTime)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / punchReturnTime);
            t.localScale = Vector3.Lerp(peakScale, origScale, ratio);
            yield return null;
        }
        t.localScale = origScale;

        // 나머지 유지 시간
        float remainHold = descHoldDuration - punchReturnTime;
        if (remainHold > 0f)
            yield return new WaitForSeconds(remainHold);

        // Phase 3: 페이드아웃
        elapsed = 0f;
        while (elapsed < descFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float ratio = Mathf.Clamp01(elapsed / descFadeOutDuration);
            SetTMPAlpha(scoreDesc_Txt, 1f - ratio);
            yield return null;
        }
        SetTMPAlpha(scoreDesc_Txt, 0f);
        t.localScale = origScale;
    }

    private void SetTMPAlpha(TextMeshProUGUI tmp, float alpha)
    {
        Color c = tmp.color;
        c.a = alpha;
        tmp.color = c;
    }

    // ── Ghost Toggle UI ─────────────────────────────────────────────

    /// <summary>고스트 ON/OFF 상태에 따라 UI 오브젝트를 전환한다.</summary>
    public void UpdateGhostToggleUI(bool isGhostActive)
    {
        if (ghostOnObj != null) ghostOnObj.SetActive(isGhostActive);
        if (ghostOffObj != null) ghostOffObj.SetActive(!isGhostActive);
    }

    // ── Popup ───────────────────────────────────────────────────────

    public void OpenPause()  => OpenPopup(popupPausePrefab);
    public void OpenWin()    => OpenPopup(popupWinPrefab);
    public void OpenFail()   => OpenPopup(popupFailPrefab);

    public void ClosePopup()
    {
        if (_currentPopup != null)
        {
            Destroy(_currentPopup);
            _currentPopup = null;
        }
    }

    private void OpenPopup(GameObject prefab)
    {
        if (prefab == null)
        {
            Debug.LogWarning("팝업 프리팹이 연결되지 않았습니다.");
            return;
        }

        ClosePopup();
        _currentPopup = Instantiate(prefab, popupRoot);
    }
}
