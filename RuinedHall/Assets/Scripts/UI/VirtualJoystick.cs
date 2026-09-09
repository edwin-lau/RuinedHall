using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 虚拟摇杆：按下后摇杆圆心跟到触摸点，拖动输出 -1~1 的平面输入。
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] RectTransform handleRoot;
    [SerializeField] RectTransform knob;
    [SerializeField] float radius = 92f;

    RectTransform _zone;
    Vector2 _restAnchoredPosition;
    int _pointerId = int.MinValue;

    /// <summary>当前方向，长度 0–1。</summary>
    public Vector2 Value { get; private set; }
    public bool IsHeld => _pointerId != int.MinValue;

    /// <summary>HUD 创建后注入摇杆底盘、圆钮和半径。</summary>
    public void Configure(RectTransform root, RectTransform knobTransform, float handleRadius)
    {
        handleRoot = root;
        knob = knobTransform;
        radius = handleRadius;
        CacheRestPosition();
    }

    /// <summary>缓存触摸区域和摇杆默认位置。</summary>
    void Awake()
    {
        _zone = transform as RectTransform;
        CacheRestPosition();
    }

    /// <summary>按下：独占这根手指，并把底盘移到触点（限制在区域内）。</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_pointerId != int.MinValue)
            return;

        _pointerId = eventData.pointerId;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _zone, eventData.position, eventData.pressEventCamera, out Vector2 local))
        {
            handleRoot.anchoredPosition = ClampHandleToZone(local);
        }

        UpdateKnob(eventData);
    }

    /// <summary>拖动更新方向。</summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId)
            return;

        UpdateKnob(eventData);
    }

    /// <summary>松手回中。</summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId)
            return;

        ResetStick();
    }

    /// <summary>失活时强制回中，避免输入卡住。</summary>
    void OnDisable()
    {
        ResetStick();
    }

    /// <summary>记下底盘初始锚点，松手时还原。</summary>
    void CacheRestPosition()
    {
        if (handleRoot != null)
            _restAnchoredPosition = handleRoot.anchoredPosition;
    }

    /// <summary>把触点换算成圆钮位置和 Value。</summary>
    void UpdateKnob(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                handleRoot, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        Vector2 clamped = Vector2.ClampMagnitude(local, radius);
        knob.anchoredPosition = clamped;
        Value = radius > 0.01f ? clamped / radius : Vector2.zero;
    }

    /// <summary>底盘中心不要拖出触摸区，避免半个摇杆出屏。</summary>
    Vector2 ClampHandleToZone(Vector2 local)
    {
        Vector2 min = new Vector2(handleRoot.rect.width * 0.5f, handleRoot.rect.height * 0.5f);
        Vector2 max = _zone.rect.size - min;
        return new Vector2(Mathf.Clamp(local.x, min.x, max.x), Mathf.Clamp(local.y, min.y, max.y));
    }

    /// <summary>清手指、清输入、底盘和圆钮回默认。</summary>
    void ResetStick()
    {
        _pointerId = int.MinValue;
        Value = Vector2.zero;
        if (handleRoot != null)
            handleRoot.anchoredPosition = _restAnchoredPosition;
        if (knob != null)
            knob.anchoredPosition = Vector2.zero;
    }
}
