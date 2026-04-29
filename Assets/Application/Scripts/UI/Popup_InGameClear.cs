using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 게임 종료 시 표시되는 클리어 팝업.
/// Final Score 자릿수별 캐스케이드 카운트업, Final Level 단순 카운트업,
/// Best/Last/Total 펀치 갱신, Restart 버튼 처리.
/// </summary>
public class Popup_InGameClear : MonoBehaviour
{
    [Header("Final Score / Level")]
    [SerializeField] private TextMeshProUGUI txt_FinalScore;
    [Tooltip("Final Score TMP 포맷. {0}에 점수가 들어감")]
    [SerializeField] private string finalScoreFormat = "FINAL SCORE : {0:0,000,000}";
    [SerializeField] private TextMeshProUGUI txt_FinalLevel;
    [Tooltip("Final Level TMP 포맷. {0}에 레벨이 들어감")]
    [SerializeField] private string finalLevelFormat = "FINAL LEVEL : {0:000}";

    [Header("Record History")]
    [SerializeField] private TextMeshProUGUI txt_BestScore;
    [SerializeField] private string bestScoreFormat = "BEST SCORE : {0:000,000}";
    [SerializeField] private TextMeshProUGUI txt_LastPlay;
    [SerializeField] private string lastPlayFormat = "LAST PLAY : {0:000,000}";
    [SerializeField] private TextMeshProUGUI txt_TotalPlay;
    [SerializeField] private string totalPlayFormat = "TOTAL PLAY : {0:0000}";

    [Header("Buttons")]
    [SerializeField] private Button btn_Restart;
    [Tooltip("리스타트 시 로드할 씬 이름. 비워두면 현재 씬을 다시 로드")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Animation Timing")]
    [Tooltip("Final Score 캐스케이드 카운트업 총 시간 (초)")]
    [SerializeField] private float finalScoreDuration = 2.5f;
    [Tooltip("Final Level 카운트업 총 시간 (초)")]
    [SerializeField] private float finalLevelDuration = 0.6f;
    [Tooltip("기록 갱신 펀치 사이의 딜레이 (초)")]
    [SerializeField] private float betweenPunchDelay = 0.18f;
    [Tooltip("기록 갱신 펀치 시간 (초)")]
    [SerializeField] private float punchDuration = 0.35f;
    [Tooltip("기록 갱신 펀치 최대 스케일 배수")]
    [SerializeField] private float punchScale = 1.4f;

    private int _finalScore;
    private int _finalLevel;
    private int _prevBest;
    private int _prevLast;
    private int _prevTotal;
    private Coroutine _sequenceCo;

    /// <summary>UIManager에서 팝업을 띄우면서 호출. Init이 호출되어야 연출이 시작된다.</summary>
    public void Init(int finalScore, int finalLevel, int prevBest, int prevLast, int prevTotal)
    {
        _finalScore = finalScore;
        _finalLevel = finalLevel;
        _prevBest = prevBest;
        _prevLast = prevLast;
        _prevTotal = prevTotal;

        SetText(txt_FinalScore, finalScoreFormat, 0);
        SetText(txt_FinalLevel, finalLevelFormat, 0);
        SetText(txt_BestScore, bestScoreFormat, prevBest);
        SetText(txt_LastPlay, lastPlayFormat, prevLast);
        SetText(txt_TotalPlay, totalPlayFormat, prevTotal);

        if (_sequenceCo != null) StopCoroutine(_sequenceCo);
        _sequenceCo = StartCoroutine(PlaySequence());
    }

    private void OnEnable()
    {
        Time.timeScale = 0f;
        if (btn_Restart != null) btn_Restart.onClick.AddListener(OnRestart);
    }

    private void OnDisable()
    {
        if (btn_Restart != null) btn_Restart.onClick.RemoveListener(OnRestart);
        Time.timeScale = 1f;
    }

    // ── Sequence ─────────────────────────────────────────────

    private IEnumerator PlaySequence()
    {
        yield return CascadeCountUp(txt_FinalScore, finalScoreFormat, _finalScore, finalScoreDuration);

        yield return SimpleCountUp(txt_FinalLevel, finalLevelFormat, _finalLevel, finalLevelDuration);

        yield return new WaitForSecondsRealtime(betweenPunchDelay);

        bool isNewBest = _finalScore > _prevBest;
        if (isNewBest)
        {
            SetText(txt_BestScore, bestScoreFormat, _finalScore);
            yield return Punch(txt_BestScore != null ? txt_BestScore.transform : null);
            yield return new WaitForSecondsRealtime(betweenPunchDelay);
        }

        SetText(txt_LastPlay, lastPlayFormat, _finalScore);
        yield return Punch(txt_LastPlay != null ? txt_LastPlay.transform : null);
        yield return new WaitForSecondsRealtime(betweenPunchDelay);

        SetText(txt_TotalPlay, totalPlayFormat, _prevTotal + 1);
        yield return Punch(txt_TotalPlay != null ? txt_TotalPlay.transform : null);

        _sequenceCo = null;
    }

    // ── Animations ──────────────────────────────────────────

    /// <summary>
    /// 자릿수별 캐스케이드 카운트업.
    /// 일의자리는 0→9 풀 사이클 후 최종 자릿수로 스냅 → 그 다음 자리부터는 0→해당 자릿수 최종까지만 증가.
    /// 예: target=342335 → 0..9, 5, 15, 25, 35, 135, 235, 335, 1335, 2335, 12335, 22335, 32335, 42335, 142335, 242335, 342335
    /// </summary>
    private IEnumerator CascadeCountUp(TextMeshProUGUI tmp, string format, int target, float totalDuration)
    {
        if (tmp == null) yield break;

        var values = new List<int>();
        if (target <= 0)
        {
            values.Add(0);
        }
        else if (target < 10)
        {
            for (int v = 0; v <= target; v++) values.Add(v);
        }
        else
        {
            for (int v = 0; v <= 9; v++) values.Add(v);

            int onesFinal = target % 10;
            int locked = onesFinal;
            values.Add(locked);

            int placeMult = 10;
            while (placeMult <= target)
            {
                int digitFinal = (target / placeMult) % 10;
                for (int v = 1; v <= digitFinal; v++)
                    values.Add(locked + v * placeMult);
                locked += digitFinal * placeMult;
                placeMult *= 10;
            }
        }

        float stepDelay = totalDuration / Mathf.Max(1, values.Count);
        foreach (var v in values)
        {
            SetText(tmp, format, v);
            yield return new WaitForSecondsRealtime(stepDelay);
        }
        SetText(tmp, format, target);
    }

    private IEnumerator SimpleCountUp(TextMeshProUGUI tmp, string format, int target, float duration)
    {
        if (tmp == null) yield break;
        int steps = Mathf.Max(1, target);
        float stepDelay = duration / steps;
        for (int v = 0; v <= target; v++)
        {
            SetText(tmp, format, v);
            yield return new WaitForSecondsRealtime(stepDelay);
        }
    }

    private IEnumerator Punch(Transform t)
    {
        if (t == null) yield break;
        Vector3 origScale = t.localScale;
        Vector3 peakScale = origScale * punchScale;
        float half = Mathf.Max(0.01f, punchDuration * 0.5f);

        float elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float r = Mathf.Clamp01(elapsed / half);
            t.localScale = Vector3.Lerp(origScale, peakScale, r);
            yield return null;
        }
        elapsed = 0f;
        while (elapsed < half)
        {
            elapsed += Time.unscaledDeltaTime;
            float r = Mathf.Clamp01(elapsed / half);
            t.localScale = Vector3.Lerp(peakScale, origScale, r);
            yield return null;
        }
        t.localScale = origScale;
    }

    private void SetText(TextMeshProUGUI tmp, string format, int value)
    {
        if (tmp == null || string.IsNullOrEmpty(format)) return;
        tmp.text = string.Format(format, value);
    }

    // ── Restart ─────────────────────────────────────────────

    private void OnRestart()
    {
        Time.timeScale = 1f;
        string scene = string.IsNullOrEmpty(gameSceneName) ? SceneManager.GetActiveScene().name : gameSceneName;
        SceneManager.LoadScene(scene);
    }
}
