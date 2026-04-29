using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Popup Prefabs")]
    [SerializeField] private GameObject popupPausePrefab;
    [SerializeField] private GameObject popupWinPrefab;
    [SerializeField] private GameObject popupFailPrefab;
    [Tooltip("게임 종료 시 표시되는 클리어/게임오버 팝업 (InGameClear 프리팹)")]
    [SerializeField] private GameObject popupGameOverPrefab;

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

    // ── Combo 연출 ───────────────────────────────────────────────────
    [Header("Combo - Flash")]
    [Tooltip("풀스크린 컬러 플래시용 Image (Canvas 위쪽에 풀스크린, RaycastTarget=false, alpha 0)")]
    [SerializeField] private Image comboFlashImage;

    [Tooltip("콤보 등급별 플래시 색상 (인덱스 0=콤보2, 1=콤보3, 2=콤보4, 3=콤보5+). 부족하면 마지막 색상 반복")]
    [SerializeField] private Color[] comboFlashColors = new Color[]
    {
        new Color(1f, 0.95f, 0.2f, 1f),   // 콤보 2: 노랑
        new Color(1f, 0.6f, 0.1f, 1f),    // 콤보 3: 주황
        new Color(1f, 0.25f, 0.25f, 1f),  // 콤보 4: 빨강
        new Color(0.75f, 0.25f, 1f, 1f),  // 콤보 5+: 보라
    };

    [Tooltip("플래시 최대 알파 (0~1)")]
    [Range(0f, 1f)]
    [SerializeField] private float comboFlashAlpha = 0.32f;

    [Tooltip("플래시 펀치인 시간 (초) — 콤보 발생 직후 빠르게 알파 최대로 차오르는 시간")]
    [SerializeField] private float comboFlashFadeIn = 0.04f;

    [Tooltip("플래시 페이드아웃 시간 (초) — 짧게 타격감만, 8초 콤보 타이머는 BG 색상 펄스가 담당")]
    [SerializeField] private float comboFlashFadeOut = 0.4f;

    [Header("Combo - Background Pulse")]
    [Tooltip("배경 색상 펄스 컴포넌트 (BG_Plan에 부착). 콤보 시 8초 동안 색상 페이드 복귀 → 콤보 타이머 시각화")]
    [SerializeField] private BackgroundColorPulse comboBackgroundPulse;

    [Tooltip("배경 펄스 페이드 시간 (초) — 콤보 윈도우(8초)와 동기 권장")]
    [SerializeField] private float comboBackgroundPulseDuration = 8f;

    [Header("Combo - Text Punch")]
    [Tooltip("콤보 발생 시 펀치할 RectTransform (보통 콤보 텍스트 자체 또는 그 부모)")]
    [SerializeField] private RectTransform comboPunchTarget;

    [Tooltip("펀치 시간 (초)")]
    [SerializeField] private float comboPunchDuration = 0.4f;

    [Tooltip("펀치 최대 스케일 배수")]
    [SerializeField] private float comboPunchScale = 1.6f;

    [Header("Combo - Timer Bar")]
    [Tooltip("콤보 타이머 바 (Slider). value 0~1로 남은 시간 비율 표시. 텍스트만 사용하면 비워둬도 됨")]
    [SerializeField] private Slider comboTimerBar;

    [Tooltip("타이머 바 부모 (활성/비활성 토글용, 비워두면 Slider 자체 토글)")]
    [SerializeField] private GameObject comboTimerBarRoot;

    [Header("Combo - Timer Text")]
    [Tooltip("콤보 타이머 카운트다운 텍스트 (TMP). 8.00초부터 0.00초까지 카운트다운")]
    [SerializeField] private TextMeshProUGUI comboTimerText;

    [Tooltip("타이머 텍스트 포맷. {0}=초(정수), {1}=센티초(00~99). 예: '{0}:{1:00}' → '8:00' / '00:0{0}:{1:00}' → '00:08:00'")]
    [SerializeField] private string comboTimerFormat = "{0}:{1:00}";

    private Coroutine _comboFlashCo;
    private Coroutine _comboPunchCo;

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

        // 콤보 플래시 초기 알파 0 + 비활성화
        if (comboFlashImage != null)
        {
            var c = comboFlashImage.color;
            c.a = 0f;
            comboFlashImage.color = c;
            comboFlashImage.gameObject.SetActive(false);
        }

        // 콤보 타이머 바 초기 비활성화
        SetComboTimerBarActive(false);
        if (comboTimerBar != null) comboTimerBar.value = 0f;

        // 콤보 타이머 텍스트 초기 비활성화
        if (comboTimerText != null)
            comboTimerText.gameObject.SetActive(false);
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

    // ── Combo 연출 ───────────────────────────────────────────────────

    /// <summary>
    /// 콤보 발생 시 풀스크린 짧은 타격감 플래시 + 배경 8초 색상 펄스 동시 트리거.
    /// 콤보 등급별 색상 (Combo Flash Colors[N-2]) 사용.
    /// </summary>
    public void PlayComboFlash(int comboCount)
    {
        if (comboCount < 2) return;
        if (comboFlashColors == null || comboFlashColors.Length == 0) return;

        int idx = Mathf.Clamp(comboCount - 2, 0, comboFlashColors.Length - 1);
        Color baseColor = comboFlashColors[idx];

        // 1) UI 풀스크린 플래시 — 짧은 타격감 (펀치인 0.04s + 페이드아웃 0.4s)
        if (comboFlashImage != null)
        {
            if (_comboFlashCo != null) StopCoroutine(_comboFlashCo);
            _comboFlashCo = StartCoroutine(ComboFlashRoutine(baseColor));
        }

        // 2) 배경 색상 펄스 — 8초 동안 천천히 원본 색상으로 복귀 (콤보 타이머 시각화)
        if (comboBackgroundPulse != null)
            comboBackgroundPulse.Pulse(baseColor, comboBackgroundPulseDuration);
    }

    /// <summary>외부에서 BG 펄스 시간을 명시 지정 (디폴트는 comboBackgroundPulseDuration)</summary>
    public void PlayComboFlash(int comboCount, float backgroundPulseDuration)
    {
        comboBackgroundPulseDuration = backgroundPulseDuration;
        PlayComboFlash(comboCount);
    }

    private IEnumerator ComboFlashRoutine(Color baseColor)
    {
        comboFlashImage.gameObject.SetActive(true);

        // 펀치인
        float elapsed = 0f;
        while (elapsed < comboFlashFadeIn)
        {
            elapsed += Time.deltaTime;
            float r = Mathf.Clamp01(elapsed / comboFlashFadeIn);
            comboFlashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0f, comboFlashAlpha, r));
            yield return null;
        }
        comboFlashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, comboFlashAlpha);

        // 페이드아웃 (짧게)
        elapsed = 0f;
        while (elapsed < comboFlashFadeOut)
        {
            elapsed += Time.deltaTime;
            float r = Mathf.Clamp01(elapsed / comboFlashFadeOut);
            comboFlashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(comboFlashAlpha, 0f, r));
            yield return null;
        }
        comboFlashImage.color = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        comboFlashImage.gameObject.SetActive(false);
        _comboFlashCo = null;
    }

    /// <summary>콤보 텍스트 펀치 연출</summary>
    public void PlayComboPunch()
    {
        if (comboPunchTarget == null) return;
        if (_comboPunchCo != null) StopCoroutine(_comboPunchCo);
        _comboPunchCo = StartCoroutine(ComboPunchRoutine());
    }

    private IEnumerator ComboPunchRoutine()
    {
        Vector3 origScale = Vector3.one;
        Vector3 peakScale = origScale * comboPunchScale;
        float half = Mathf.Max(0.01f, comboPunchDuration * 0.5f);

        comboPunchTarget.localScale = origScale;

        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float r = Mathf.Clamp01(elapsed / half);
            comboPunchTarget.localScale = Vector3.Lerp(origScale, peakScale, r);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.deltaTime;
            float r = Mathf.Clamp01(elapsed / half);
            comboPunchTarget.localScale = Vector3.Lerp(peakScale, origScale, r);
            yield return null;
        }
        comboPunchTarget.localScale = origScale;
        _comboPunchCo = null;
    }

    /// <summary>콤보 타이머 바 갱신 (ratio 0~1, 0 이하면 비활성화)</summary>
    public void UpdateComboTimerBar(float ratio)
    {
        if (comboTimerBar == null) return;
        bool show = ratio > 0f;
        SetComboTimerBarActive(show);
        if (show)
            comboTimerBar.value = Mathf.Clamp01(ratio);
    }

    /// <summary>콤보 타이머 텍스트 카운트다운 갱신 (남은 초, 0 이하면 비활성화)</summary>
    public void UpdateComboTimerText(float remainingSeconds)
    {
        if (comboTimerText == null) return;
        bool show = remainingSeconds > 0f;
        if (comboTimerText.gameObject.activeSelf != show)
            comboTimerText.gameObject.SetActive(show);
        if (!show) return;

        int sec = Mathf.FloorToInt(remainingSeconds);
        int centi = Mathf.FloorToInt((remainingSeconds - sec) * 100f);
        centi = Mathf.Clamp(centi, 0, 99);
        comboTimerText.text = string.Format(comboTimerFormat, sec, centi);
    }

    private void SetComboTimerBarActive(bool active)
    {
        if (comboTimerBarRoot != null)
        {
            if (comboTimerBarRoot.activeSelf != active)
                comboTimerBarRoot.SetActive(active);
        }
        else if (comboTimerBar != null)
        {
            if (comboTimerBar.gameObject.activeSelf != active)
                comboTimerBar.gameObject.SetActive(active);
        }
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

    /// <summary>게임 종료 → 클리어 팝업 오픈. Final 점수/레벨 + 이전 기록을 넘겨준다.</summary>
    public void OpenGameOver(int finalScore, int finalLevel, int prevBest, int prevLast, int prevTotal)
    {
        if (popupGameOverPrefab == null)
        {
            Debug.LogWarning("popupGameOverPrefab 미연결: UIManager Inspector에서 InGameClear 프리팹을 연결하세요.");
            return;
        }

        ClosePopup();
        _currentPopup = Instantiate(popupGameOverPrefab, popupRoot);
        var clear = _currentPopup.GetComponent<Popup_InGameClear>();
        if (clear != null)
            clear.Init(finalScore, finalLevel, prevBest, prevLast, prevTotal);
        else
            Debug.LogWarning("InGameClear 프리팹에 Popup_InGameClear 컴포넌트가 없습니다.");
    }

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
