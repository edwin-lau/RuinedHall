using UnityEngine;
using UnityEngine.UI;

// 世界空间血条：跟在角色头顶，面向相机，精英更宽、更黄。
public sealed class WorldHealthBar : MonoBehaviour
{
    const float WorldWidth = 1.35f;
    const float EliteWorldWidth = 2.15f;
    const float WorldHeight = 0.16f;
    const float EliteWorldHeight = 0.4f;
    const float HeadPadding = 0.22f;

    [SerializeField] float visibleDistance = 48f;

    CharacterCombatAgent _owner;
    CharacterController _controller;
    Image _fill;
    Text _title;
    Text _amount;
    CanvasGroup _group;
    Canvas _canvas;
    Transform _cameraTransform;
    bool _elite;

    // 给战斗单位挂血条；已有则复用并重新绑定。
    public static WorldHealthBar Attach(CharacterCombatAgent owner)
    {
        if (owner == null)
            return null;

        var existing = owner.GetComponentInChildren<WorldHealthBar>(true);
        if (existing == null)
        {
            foreach (var candidate in FindObjectsByType<WorldHealthBar>(FindObjectsInactive.Include))
            {
                if (candidate._owner == owner)
                {
                    existing = candidate;
                    break;
                }
            }
        }

        if (existing != null)
        {
            existing.Bind(owner);
            return existing;
        }

        var go = new GameObject(
            owner.name + " HealthBar",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(WorldHealthBar));
        var bar = go.GetComponent<WorldHealthBar>();
        bar.Bind(owner);
        return bar;
    }

    // 订阅血量变化，按是否精英建 UI 并对准头顶。
    public void Bind(CharacterCombatAgent owner)
    {
        if (_owner != null)
            _owner.HealthChanged -= OnHealthChanged;

        _owner = owner;
        _controller = owner != null ? owner.GetComponent<CharacterController>() : null;
        if (_owner == null)
            return;

        transform.SetParent(null, true);
        _owner.HealthChanged += OnHealthChanged;
        _elite = _owner.IsElite;
        EnsureBuilt();
        SetHealth(_owner.CurrentHealth, _owner.MaxHealth);
        ApplyTitle();
        enabled = true;
        AlignToHead();
        FaceCamera();
    }

    // 隐藏血条并停止跟随。
    public void Hide()
    {
        if (_group != null)
            _group.alpha = 0f;
        enabled = false;
    }

    // 显示血条并恢复跟随。
    public void Show()
    {
        enabled = true;
        if (_group != null)
            _group.alpha = 1f;
    }

    // 解绑血量事件。
    void OnDestroy()
    {
        if (_owner != null)
            _owner.HealthChanged -= OnHealthChanged;
    }

    // 每帧跟头顶、朝向相机；主人死亡或丢失则隐藏 / 销毁。
    void LateUpdate()
    {
        if (_owner == null)
        {
            Destroy(gameObject);
            return;
        }

        if (_owner.IsDead)
        {
            Hide();
            return;
        }

        AlignToHead();
        FaceCamera();
        UpdateVisibility();
    }

    // 懒创建世界空间 Canvas：底板、填充、称号与数值。
    void EnsureBuilt()
    {
        if (_fill != null)
            return;

        _canvas = GetComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        _canvas.worldCamera = Camera.main;
        _canvas.sortingOrder = 200;
        _canvas.overrideSorting = true;

        var rt = transform as RectTransform;
        float width = BarWidth();
        float height = _elite ? EliteWorldHeight : WorldHeight;
        rt.sizeDelta = new Vector2(100f, 100f * height / width);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.localScale = Vector3.one * (width / 100f);

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        Sprite white = Resources.Load<Sprite>("ui/white");
        if (white == null)
            white = UiSprites.White();

        var bgGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bgGo.transform.SetParent(transform, false);
        Stretch(bgGo.GetComponent<RectTransform>());
        var bg = bgGo.GetComponent<Image>();
        bg.sprite = white;
        bg.color = new Color(0.06f, 0.06f, 0.06f, 0.88f);
        bg.raycastTarget = false;

        var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fillGo.transform.SetParent(transform, false);
        var fillRt = fillGo.GetComponent<RectTransform>();
        Stretch(fillRt);
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(-2f, -2f);
        _fill = fillGo.GetComponent<Image>();
        _fill.sprite = white;
        _fill.type = Image.Type.Filled;
        _fill.fillMethod = Image.FillMethod.Horizontal;
        _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
        _fill.color = _elite
            ? new Color(0.93f, 0.72f, 0.18f, 1f)
            : new Color(0.86f, 0.18f, 0.16f, 1f);
        _fill.raycastTarget = false;

        var titleGo = new GameObject("Title", typeof(RectTransform), typeof(Text));
        titleGo.transform.SetParent(transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 0f);
        titleRt.sizeDelta = new Vector2(0f, 42f);
        titleRt.anchoredPosition = new Vector2(0f, 2f);
        _title = titleGo.GetComponent<Text>();
        _title.alignment = TextAnchor.LowerCenter;
        _title.color = new Color(1f, 0.86f, 0.35f, 1f);
        _title.fontSize = 28;
        _title.raycastTarget = false;
        _title.font = Font.CreateDynamicFontFromOSFont(
            new[] { "PingFang SC", "Heiti SC", "Arial Unicode MS", "Arial" },
            28);
        if (_title.font == null)
            _title.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var amountGo = new GameObject("Amount", typeof(RectTransform), typeof(Text));
        amountGo.transform.SetParent(transform, false);
        var amountRt = amountGo.GetComponent<RectTransform>();
        amountRt.anchorMin = new Vector2(0f, 0f);
        amountRt.anchorMax = new Vector2(1f, 1f);
        amountRt.offsetMin = Vector2.zero;
        amountRt.offsetMax = Vector2.zero;
        _amount = amountGo.GetComponent<Text>();
        _amount.alignment = TextAnchor.MiddleCenter;
        _amount.color = Color.white;
        _amount.fontSize = 22;
        _amount.raycastTarget = false;
        _amount.font = _title.font;
        ApplyTitle();
    }

    // 精英显示称号，训练木桩显示“肉桩”。
    void ApplyTitle()
    {
        if (_title == null)
            return;

        bool show = _owner != null &&
            ((_owner.IsElite && !string.IsNullOrWhiteSpace(_owner.EliteTitle)) || _owner.IsTrainingDummy);
        _title.enabled = show;
        if (show)
            _title.text = string.IsNullOrWhiteSpace(_owner.EliteTitle) ? "肉桩" : _owner.EliteTitle;
    }

    // 血量变化回调。
    void OnHealthChanged(int current, int max)
    {
        SetHealth(current, max);
    }

    // 刷新填充比例；木桩额外显示数字。
    void SetHealth(int current, int max)
    {
        max = Mathf.Max(1, max);
        if (_fill != null)
            _fill.fillAmount = Mathf.Clamp01(current / (float)max);
        if (_amount != null)
        {
            bool showAmount = _owner != null && _owner.IsTrainingDummy;
            _amount.enabled = showAmount;
            if (showAmount)
                _amount.text = current + " / " + max;
        }
    }

    // 把血条放到碰撞体头顶上方。
    void AlignToHead()
    {
        Vector3 origin = _owner.transform.position;
        float topY = origin.y + 1.6f;

        if (_controller == null)
            _controller = _owner.GetComponent<CharacterController>();
        if (_controller != null)
        {
            Bounds bounds = _controller.bounds;
            origin.x = bounds.center.x;
            origin.z = bounds.center.z;
            topY = bounds.max.y;
        }

        transform.position = new Vector3(origin.x, topY + HeadPadding, origin.z);
        transform.localScale = Vector3.one * (BarWidth() / 100f);
    }

    // 精英用更宽的血条。
    float BarWidth()
    {
        return _elite ? EliteWorldWidth : WorldWidth;
    }

    // 让血条面向主相机。
    void FaceCamera()
    {
        if (_cameraTransform == null && Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
            if (_canvas != null)
                _canvas.worldCamera = Camera.main;
        }

        if (_cameraTransform == null)
            return;

        transform.rotation = _cameraTransform.rotation * Quaternion.Euler(0f, 180f, 0f);
    }

    // 超过可视距离则透明隐藏。
    void UpdateVisibility()
    {
        if (_group == null)
            return;

        if (_cameraTransform == null)
        {
            _group.alpha = 1f;
            return;
        }

        float distance = Vector3.Distance(transform.position, _cameraTransform.position);
        _group.alpha = distance <= visibleDistance ? 1f : 0f;
    }

    // 把 RectTransform 拉满父节点。
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
}
