using UnityEngine;

public class Popup_Pause : MonoBehaviour
{
    private void OnEnable()
    {
        Time.timeScale = 0f; // 팝업 열리면 게임 일시정지
    }

    private void OnDisable()
    {
        Time.timeScale = 1f; // 팝업 닫히면 게임 재개
    }

    public void Close()
    {
        UIManager.Instance.ClosePopup();
    }
}
