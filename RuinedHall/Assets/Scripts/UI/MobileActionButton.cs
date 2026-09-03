using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] Image background;
    Color _baseColor;
    bool _pressed;

    public event Action Pressed;

    void Awake()
    {
        if (background != null)
            _baseColor = background.color;
    }

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
        Release();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        Release();
    }

    void OnDisable()
    {
        Release();
    }

    void Release()
    {
        _pressed = false;
        if (background != null)
            background.color = _baseColor;
    }
}
