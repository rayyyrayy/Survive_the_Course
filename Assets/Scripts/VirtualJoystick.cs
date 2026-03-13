using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Simple on-screen joystick. Attach to the JoystickBackground image.
/// Exposes a normalized InputVector for movement.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
{
    public static VirtualJoystick Instance { get; private set; }

    [SerializeField] private RectTransform handle;

    public Vector2 InputVector { get; private set; }

    private RectTransform _backgroundRect;
    private float _radius;

    private void Awake()
    {
        Instance = this;
        _backgroundRect = GetComponent<RectTransform>();
        if (_backgroundRect != null)
        {
            // Use half the smaller dimension as the joystick radius.
            _radius = Mathf.Min(_backgroundRect.sizeDelta.x, _backgroundRect.sizeDelta.y) * 0.5f;
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        OnDrag(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_backgroundRect == null || handle == null)
            return;

        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _backgroundRect,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint))
        {
            return;
        }

        // Convert from local rect space into a -1..1 range based on radius.
        var v = localPoint / _radius;
        if (v.magnitude > 1f)
            v = v.normalized;

        InputVector = v;
        handle.anchoredPosition = v * _radius;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        InputVector = Vector2.zero;
        if (handle != null)
            handle.anchoredPosition = Vector2.zero;
    }
}

