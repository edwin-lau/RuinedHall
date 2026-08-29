using UnityEngine;
using UnityEngine.EventSystems;

public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] RectTransform handleRoot;
    [SerializeField] RectTransform knob;
    [SerializeField] float radius = 92f;

    RectTransform _zone;
    Vector2 _restAnchoredPosition;
    int _pointerId = int.MinValue;

    public Vector2 Value { get; private set; }
    public bool IsHeld => _pointerId != int.MinValue;

    public void Configure(RectTransform root, RectTransform knobTransform, float handleRadius)
    {
        handleRoot = root;
        knob = knobTransform;
        radius = handleRadius;
        CacheRestPosition();
    }

    void Awake()
    {
        _zone = transform as RectTransform;
        CacheRestPosition();
    }

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

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId)
            return;

        UpdateKnob(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != _pointerId)
            return;

        ResetStick();
    }

    void OnDisable()
    {
        ResetStick();
    }

    void CacheRestPosition()
    {
        if (handleRoot != null)
            _restAnchoredPosition = handleRoot.anchoredPosition;
    }

    void UpdateKnob(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                handleRoot, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        Vector2 clamped = Vector2.ClampMagnitude(local, radius);
        knob.anchoredPosition = clamped;
        Value = radius > 0.01f ? clamped / radius : Vector2.zero;
    }

    Vector2 ClampHandleToZone(Vector2 local)
    {
        Vector2 min = new Vector2(handleRoot.rect.width * 0.5f, handleRoot.rect.height * 0.5f);
        Vector2 max = _zone.rect.size - min;
        return new Vector2(Mathf.Clamp(local.x, min.x, max.x), Mathf.Clamp(local.y, min.y, max.y));
    }

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
