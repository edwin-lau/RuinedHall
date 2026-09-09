using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 手机动作键：按下触发一次，按住可查询 IsHeld（连招用）。
/// </summary>
public class MobileActionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField] Image background;
    Color _baseColor;
    bool _pressed;

    public event Action Pressed;
    public bool IsHeld => _pressed;

    /// <summary>记下按钮底色，方便按下变暗。</summary>
    void Awake()
    {
        if (background != null)
            _baseColor = background.color;
    }

    /// <summary>HUD 动态创建按钮后绑定 Image。</summary>
    public void Configure(Image image)
    {
        background = image;
        _baseColor = image.color;
    }

    /// <summary>按下：变暗并抛出 Pressed。</summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        if (_pressed)
            return;

        _pressed = true;
        if (background != null)
            background.color = _baseColor * 0.72f;
        Pressed?.Invoke();
    }

    /// <summary>抬起松开。</summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        Release();
    }

    /// <summary>滑出按钮区域也松开，避免卡住。</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        Release();
    }

    /// <summary>组件关掉时清按住状态。</summary>
    void OnDisable()
    {
        Release();
    }

    /// <summary>恢复颜色和按住标记。</summary>
    void Release()
    {
        _pressed = false;
        if (background != null)
            background.color = _baseColor;
    }
}
