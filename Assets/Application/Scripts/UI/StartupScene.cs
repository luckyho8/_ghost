using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 스타트업 씬: 로딩 완료 후 "Tap to Start" 표시 → 터치 시 게임 씬 로드.
/// </summary>
public class StartupScene : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Tap to Start 텍스트")]
    [SerializeField] private TextMeshProUGUI tapToStartText;

    [Tooltip("로딩 바 (선택, 없으면 숨김)")]
    [SerializeField] private Slider loadingBar;

    [Header("Settings")]
    [Tooltip("로드할 게임 씬 이름")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Tooltip("Tap to Start 깜빡임 속도")]
    [SerializeField] private float blinkSpeed = 1.5f;

    private bool _isLoaded;
    private AsyncOperation _loadOp;

    private IEnumerator Start()
    {
        // Tap to Start 숨김
        if (tapToStartText != null)
            tapToStartText.gameObject.SetActive(false);
        if (loadingBar != null)
            loadingBar.gameObject.SetActive(true);

        // 비동기 씬 로드 (활성화 대기)
        _loadOp = SceneManager.LoadSceneAsync(gameSceneName);
        _loadOp.allowSceneActivation = false;

        // 로딩 진행
        while (_loadOp.progress < 0.9f)
        {
            if (loadingBar != null)
                loadingBar.value = _loadOp.progress / 0.9f;
            yield return null;
        }

        // 로딩 완료
        if (loadingBar != null)
        {
            loadingBar.value = 1f;
            yield return new WaitForSeconds(0.3f);
            loadingBar.gameObject.SetActive(false);
        }

        _isLoaded = true;

        // Tap to Start 표시 + 깜빡임
        if (tapToStartText != null)
        {
            tapToStartText.gameObject.SetActive(true);
            StartCoroutine(BlinkText());
        }
    }

    private void Update()
    {
        if (!_isLoaded) return;

        // 터치 또는 클릭 감지
        if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
        {
            _isLoaded = false; // 중복 방지
            _loadOp.allowSceneActivation = true;
        }
    }

    private IEnumerator BlinkText()
    {
        while (_isLoaded && tapToStartText != null)
        {
            float alpha = (Mathf.Sin(Time.time * blinkSpeed * Mathf.PI) + 1f) * 0.5f;
            Color c = tapToStartText.color;
            c.a = Mathf.Lerp(0.3f, 1f, alpha);
            tapToStartText.color = c;
            yield return null;
        }
    }
}
