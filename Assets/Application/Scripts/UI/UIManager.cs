using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Popup Prefabs")]
    [SerializeField] private GameObject popupPausePrefab;
    [SerializeField] private GameObject popupWinPrefab;
    [SerializeField] private GameObject popupFailPrefab;

    [Header("Canvas")]
    [SerializeField] private Transform popupRoot; // 팝업이 생성될 부모 (Canvas)

    private GameObject _currentPopup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

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

        // 이미 열린 팝업이 있으면 닫고 새로 열기
        ClosePopup();

        _currentPopup = Instantiate(prefab, popupRoot);
    }
}
