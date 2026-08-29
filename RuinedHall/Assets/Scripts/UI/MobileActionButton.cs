using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] Image background;
    Color _baseColor;
    bool _pressed;

    public event Action Pressed;

    public void Configure(Image image)
    {
        background = image;
        _baseColor = image.color;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (_pressed)
            return;

        _pressed = true;
        if (background != null)
            background.color = _baseColor * 0.72f;
        Pressed?.Invoke();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
        if (background != null)
            background.color = _baseColor;
    }

    void OnDisable()
    {
        _pressed = false;
        if (background != null)
            background.color = _baseColor;
    }
}
