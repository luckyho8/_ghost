using UnityEngine;

/// <summary>
/// Safe area implementation for notched mobile devices. Usage:
///  (1) Add this component to the top level of any GUI panel.
///  (2) If the panel uses a full screen background image, then create an immediate child and put the component on that instead,
///      with all other elements childed below it. This will allow the background image to stretch to the full extents of the
///      screen behind the notch, which looks nicer.
///  (3) For other cases that use a mixture of full horizontal and vertical background stripes, use the Conform X & Y controls
///      on separate elements as needed.
/// </summary>
public class SafeArea : MonoBehaviour
{
    private RectTransform Panel;
    private Rect LastSafeArea = new Rect(0, 0, 0, 0);
    private Vector2Int LastScreenSize = new Vector2Int(0, 0);
    private ScreenOrientation LastOrientation = ScreenOrientation.AutoRotation;

    [SerializeField] private bool ConformX = true;
    [SerializeField] private bool ConformY = true;
    [SerializeField] private bool KeepBottom = true;
    [Range(0, 0.015f)] public float OffsetXOnIOS = 0.015f;

    private void Awake()
    {
        Panel = GetComponent<RectTransform>();

        if (Panel == null)
        {
            Debug.LogError("Cannot apply safe area - no RectTransform found on " + name);
            Destroy(gameObject);
            return;
        }

        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    private void Refresh()
    {
        Rect safeArea = Screen.safeArea;

        float offset_x = Screen.width * OffsetXOnIOS;
        float offset_y = Screen.height * OffsetXOnIOS;

        // iOS/AOS 구분 로직
        // iOS: SafeArea 좌우 대칭 → offset 적용
        // AOS: 비대칭이거나 딱 붙어있으면 → offset 미사용
        int sa_width = Mathf.FloorToInt(safeArea.width + 1f);
        int sa_x = Mathf.FloorToInt(safeArea.x);
        int diffX = Screen.width - sa_width;
        if (diffX <= sa_x || sa_x == 0)
            offset_x = 0;

        safeArea.x -= offset_x;
        safeArea.width += (offset_x + offset_x);

        if (safeArea.x <= 0)
        {
            safeArea.x = diffX;
            safeArea.width -= diffX;
        }
        else if (safeArea.x + sa_width >= Screen.width)
        {
            safeArea.width -= diffX;
        }

        int sa_height = Mathf.FloorToInt(safeArea.height + 1f);
        int sa_y = Mathf.FloorToInt(safeArea.y);
        int diffY = Screen.height - sa_height;
        if (diffY <= sa_y || sa_y == 0)
            offset_y = 0;

        safeArea.y -= offset_y;
        safeArea.height += (offset_y + offset_y);

        if (safeArea.y <= 0)
        {
            safeArea.y = diffY;
            safeArea.height -= diffY;
        }
        else if (safeArea.y + sa_height >= Screen.height)
        {
            safeArea.height -= diffY;
        }

        if (safeArea != LastSafeArea
            || Screen.width != LastScreenSize.x
            || Screen.height != LastScreenSize.y
            || Screen.orientation != LastOrientation)
        {
            LastScreenSize.x = Screen.width;
            LastScreenSize.y = Screen.height;
            LastOrientation = Screen.orientation;

            ApplySafeArea(safeArea);
        }
    }

    private void ApplySafeArea(Rect r)
    {
        LastSafeArea = r;

        if (KeepBottom)
        {
            r.height += r.y;
            r.y = 0;
        }

        if (!ConformX)
        {
            r.x = 0;
            r.width = Screen.width;
        }

        if (!ConformY)
        {
            r.y = 0;
            r.height = Screen.height;
        }

        Vector2 anchorMin = r.position;
        Vector2 anchorMax = r.position + r.size;
        anchorMin.x /= Screen.width;
        anchorMin.y /= Screen.height;
        anchorMax.x /= Screen.width;
        anchorMax.y /= Screen.height;

        Panel.anchorMin = anchorMin;
        Panel.anchorMax = anchorMax;
    }
}
