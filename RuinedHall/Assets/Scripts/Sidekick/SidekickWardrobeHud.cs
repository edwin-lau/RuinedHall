using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Sidekick 换装预览 HUD：快捷套装、体型滑条与各槽位部件按钮。
public class SidekickWardrobeHud : MonoBehaviour
{
    SidekickWardrobe _wardrobe;
    readonly Dictionary<string, Image> _buttons = new Dictionary<string, Image>();
    Sprite _white;

    static readonly Color Idle = new Color(0.16f, 0.17f, 0.2f, 0.94f);
    static readonly Color Equipped = new Color(0.78f, 0.52f, 0.16f, 0.96f);
    static readonly Color DefaultWorn = new Color(0.24f, 0.32f, 0.4f, 0.94f);

    // 创建 Overlay Canvas 并立刻拼出换装面板。
    public static SidekickWardrobeHud Create(SidekickWardrobe wardrobe)
    {
        EnsureEventSystem();

        var canvasGo = new GameObject(
            "WardrobeHud",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster),
            typeof(SidekickWardrobeHud));

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var hud = canvasGo.GetComponent<SidekickWardrobeHud>();
        hud._wardrobe = wardrobe;
        hud.Build();
        return hud;
    }

    // 启用时重新订阅衣柜变化。
    void OnEnable()
    {
        BindWardrobe();
    }

    // 停用时取消衣柜变化订阅。
    void OnDisable()
    {
        if (_wardrobe != null)
            _wardrobe.Changed -= Refresh;
    }

    // 订阅衣柜 Changed，先解绑再绑以免重复。
    void BindWardrobe()
    {
        if (_wardrobe == null)
            return;
        _wardrobe.Changed -= Refresh;
        _wardrobe.Changed += Refresh;
    }

    // 拼出左侧面板：标题、快捷套装、体型滑条、槽位列表与保存按钮。
    void Build()
    {
        _white = WhiteSprite();
        Font font = ResolveFont(28);

        // 左侧半透明面板与标题
        var panel = Create("Panel", transform);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 0.5f);
        panelRt.sizeDelta = new Vector2(470f, 0f);
        panelRt.anchoredPosition = Vector2.zero;
        var panelImage = panel.AddComponent<Image>();
        panelImage.sprite = _white;
        panelImage.color = new Color(0.05f, 0.055f, 0.07f, 0.86f);

        var title = CreateText(panel.transform, "Title", "换装预览", 34, FontStyle.Bold);
        var titleRt = title.rectTransform;
        titleRt.anchorMin = new Vector2(0f, 1f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.pivot = new Vector2(0.5f, 1f);
        titleRt.sizeDelta = new Vector2(-32f, 48f);
        titleRt.anchoredPosition = new Vector2(0f, -16f);
        title.alignment = TextAnchor.MiddleLeft;

        var hint = CreateText(panel.transform, "Hint", "上面选脸型，刚柔改轮廓。胡子在列表里加。", 20, FontStyle.Normal);
        var hintRt = hint.rectTransform;
        hintRt.anchorMin = new Vector2(0f, 1f);
        hintRt.anchorMax = new Vector2(1f, 1f);
        hintRt.pivot = new Vector2(0.5f, 1f);
        hintRt.sizeDelta = new Vector2(-32f, 28f);
        hintRt.anchoredPosition = new Vector2(0f, -62f);
        hint.alignment = TextAnchor.MiddleLeft;
        hint.color = new Color(0.78f, 0.78f, 0.8f, 0.9f);

        BuildQuickRow(panel.transform, font);
        BuildBodySliders(panel.transform, font);

        // 可滚动的槽位部件列表
        var scrollGo = Create("Scroll", panel.transform);
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.anchorMin = new Vector2(0f, 0f);
        scrollRt.anchorMax = new Vector2(1f, 1f);
        scrollRt.offsetMin = new Vector2(16f, 78f);
        scrollRt.offsetMax = new Vector2(-10f, -368f);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        var viewport = Create("Viewport", scrollGo.transform);
        var viewportRt = viewport.GetComponent<RectTransform>();
        Stretch(viewportRt);
        viewport.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
        viewport.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = viewportRt;

        var content = Create("Content", viewport.transform);
        var contentRt = content.GetComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = new Vector2(0f, 0f);
        var layout = content.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = contentRt;

        for (int i = 0; i < SidekickWardrobe.UiSlotOrder.Length; i++)
        {
            string slot = SidekickWardrobe.UiSlotOrder[i];
            IReadOnlyList<WardrobePart> parts = _wardrobe.PartsForSlot(slot);
            if (parts.Count == 0)
                continue;
            BuildSlotSection(content.transform, slot, parts, font);
        }

        // 底部保存到正式关卡
        var save = Create("SaveToGame", panel.transform);
        var saveRt = save.GetComponent<RectTransform>();
        saveRt.anchorMin = new Vector2(0f, 0f);
        saveRt.anchorMax = new Vector2(1f, 0f);
        saveRt.pivot = new Vector2(0.5f, 0f);
        saveRt.sizeDelta = new Vector2(-32f, 48f);
        saveRt.anchoredPosition = new Vector2(0f, 18f);
        var saveImage = save.AddComponent<Image>();
        saveImage.sprite = _white;
        saveImage.color = new Color(0.28f, 0.46f, 0.32f, 0.96f);
        var saveButton = save.AddComponent<Button>();
        saveButton.targetGraphic = saveImage;
        var saveLabel = CreateText(save.transform, "Label", "保存到游戏（HelpOthers）", 22, FontStyle.Bold);
        Stretch(saveLabel.rectTransform);
        saveLabel.alignment = TextAnchor.MiddleCenter;
        saveLabel.font = font;
        saveButton.onClick.AddListener(() =>
        {
            _wardrobe.SaveLookForGame();
            saveLabel.text = "已保存，可打开 HelpOthers";
        });

        BindWardrobe();
        Refresh();
    }

    // 顶部快捷套装按钮行。
    void BuildQuickRow(Transform parent, Font font)
    {
        var row = Create("Quick", parent);
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(0.5f, 1f);
        rowRt.sizeDelta = new Vector2(-32f, 56f);
        rowRt.anchoredPosition = new Vector2(0f, -100f);
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        MakeQuick(row.transform, "骑士全身", () => _wardrobe.EquipSet("SK_FANT_KNGT_17"), font);
        MakeQuick(row.transform, "科幻全身", () => _wardrobe.EquipSet("SK_SCFI_CIVL_09"), font);
        MakeQuick(row.transform, "恐怖全身", () => _wardrobe.EquipSet("SK_HORR_VILN"), font);
        MakeQuick(row.transform, "全部还原", () => _wardrobe.RestoreAll(), font);
    }

    // 创建一个快捷套装按钮。
    void MakeQuick(Transform parent, string label, UnityEngine.Events.UnityAction action, Font font)
    {
        var go = Create(label, parent);
        var image = go.AddComponent<Image>();
        image.sprite = _white;
        image.color = new Color(0.22f, 0.28f, 0.36f, 0.95f);
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        var text = CreateText(go.transform, "Label", label, 22, FontStyle.Bold);
        Stretch(text.rectTransform);
        text.alignment = TextAnchor.MiddleCenter;
        text.font = font;
    }

    // 脸型按钮与刚柔 / 瘦 / 壮 / 肌肉滑条。
    void BuildBodySliders(Transform parent, Font font)
    {
        var box = Create("Body", parent);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0f, 1f);
        boxRt.anchorMax = new Vector2(1f, 1f);
        boxRt.pivot = new Vector2(0.5f, 1f);
        boxRt.sizeDelta = new Vector2(-32f, 192f);
        boxRt.anchoredPosition = new Vector2(0f, -164f);
        var layout = box.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        IReadOnlyList<WardrobePart> heads = _wardrobe.PartsForSlot("01HEAD");
        if (heads.Count > 0)
        {
            var faceRow = Create("FaceTypes", box.transform);
            var faceLe = faceRow.AddComponent<LayoutElement>();
            faceLe.minHeight = 40f;
            faceLe.preferredHeight = 40f;
            var faceLayout = faceRow.AddComponent<HorizontalLayoutGroup>();
            faceLayout.spacing = 8f;
            faceLayout.childAlignment = TextAnchor.MiddleCenter;
            faceLayout.childControlWidth = true;
            faceLayout.childControlHeight = true;
            faceLayout.childForceExpandWidth = true;
            faceLayout.childForceExpandHeight = true;

            var faceTitle = CreateText(faceRow.transform, "FaceLabel", "脸型", 20, FontStyle.Bold);
            faceTitle.alignment = TextAnchor.MiddleLeft;
            var titleLe = faceTitle.gameObject.AddComponent<LayoutElement>();
            titleLe.minWidth = 48f;
            titleLe.preferredWidth = 48f;
            titleLe.flexibleWidth = 0f;

            for (int i = 0; i < heads.Count; i++)
            {
                WardrobePart head = heads[i];
                var go = Create(head.Name, faceRow.transform);
                var image = go.AddComponent<Image>();
                image.sprite = _white;
                image.color = Idle;
                var button = go.AddComponent<Button>();
                button.targetGraphic = image;
                string nameCopy = head.Name;
                button.onClick.AddListener(() => _wardrobe.EquipFace(nameCopy));
                var text = CreateText(go.transform, "Label", head.Label, 18, FontStyle.Normal);
                Stretch(text.rectTransform);
                text.alignment = TextAnchor.MiddleCenter;
                text.font = font;
                _buttons["01HEAD/" + head.Name] = image;
            }
        }

        MakeSlider(box.transform, "刚柔", -80f, 80f, _wardrobe.FaceBlend, font,
            value => _wardrobe.SetFaceBlend(value));
        MakeSlider(box.transform, "瘦", 0f, 100f, _wardrobe.Skinny, font,
            value => _wardrobe.SetBody(value, _wardrobe.Heavy, _wardrobe.Muscles));
        MakeSlider(box.transform, "壮", 0f, 100f, _wardrobe.Heavy, font,
            value => _wardrobe.SetBody(_wardrobe.Skinny, value, _wardrobe.Muscles));
        MakeSlider(box.transform, "肌肉", -20f, 80f, _wardrobe.Muscles, font,
            value => _wardrobe.SetBody(_wardrobe.Skinny, _wardrobe.Heavy, value));
    }

    // 创建一条整数步进的体型滑条。
    void MakeSlider(Transform parent, string label, float min, float max, float value, Font font, System.Action<float> apply)
    {
        var row = Create(label, parent);
        var rowLe = row.AddComponent<LayoutElement>();
        rowLe.minHeight = 30f;
        rowLe.preferredHeight = 30f;
        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 8f;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;

        var text = CreateText(row.transform, "Label", label, 20, FontStyle.Normal);
        text.alignment = TextAnchor.MiddleLeft;
        text.font = font;
        var textLe = text.gameObject.AddComponent<LayoutElement>();
        textLe.minWidth = 56f;
        textLe.preferredWidth = 56f;

        var sliderGo = Create("Slider", row.transform);
        var sliderLe = sliderGo.AddComponent<LayoutElement>();
        sliderLe.flexibleWidth = 1f;
        sliderLe.minWidth = 160f;
        var slider = sliderGo.AddComponent<Slider>();
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = value;
        slider.wholeNumbers = true;

        var bg = Create("Background", sliderGo.transform);
        Stretch(bg.GetComponent<RectTransform>());
        var bgImage = bg.AddComponent<Image>();
        bgImage.sprite = _white;
        bgImage.color = new Color(0.12f, 0.13f, 0.16f, 0.95f);
        slider.targetGraphic = bgImage;

        var fillArea = Create("Fill Area", sliderGo.transform);
        var fillAreaRt = fillArea.GetComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRt.offsetMin = Vector2.zero;
        fillAreaRt.offsetMax = Vector2.zero;
        var fill = Create("Fill", fillArea.transform);
        Stretch(fill.GetComponent<RectTransform>());
        var fillImage = fill.AddComponent<Image>();
        fillImage.sprite = _white;
        fillImage.color = new Color(0.72f, 0.5f, 0.2f, 0.95f);
        slider.fillRect = fill.GetComponent<RectTransform>();

        var handleArea = Create("Handle Slide Area", sliderGo.transform);
        Stretch(handleArea.GetComponent<RectTransform>());
        var handle = Create("Handle", handleArea.transform);
        var handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(16f, 0f);
        var handleImage = handle.AddComponent<Image>();
        handleImage.sprite = _white;
        handleImage.color = Color.white;
        slider.handleRect = handleRt;

        int last = Mathf.RoundToInt(value);
        slider.onValueChanged.AddListener(next =>
        {
            int quantized = Mathf.RoundToInt(next);
            if (quantized == last)
                return;
            last = quantized;
            apply(quantized);
        });
    }

    // 为一个槽位创建标题和两列部件按钮网格。
    void BuildSlotSection(Transform parent, string slot, IReadOnlyList<WardrobePart> parts, Font font)
    {
        var section = Create("Slot_" + slot, parent);
        var sectionLayout = section.AddComponent<VerticalLayoutGroup>();
        sectionLayout.spacing = 6f;
        sectionLayout.childAlignment = TextAnchor.UpperLeft;
        sectionLayout.childControlWidth = true;
        sectionLayout.childControlHeight = true;
        sectionLayout.childForceExpandWidth = true;
        sectionLayout.childForceExpandHeight = false;

        var header = CreateText(section.transform, "Header", SidekickWardrobe.SlotLabel(slot), 24, FontStyle.Bold);
        header.alignment = TextAnchor.MiddleLeft;
        var headerLe = header.gameObject.AddComponent<LayoutElement>();
        headerLe.minHeight = 28f;
        headerLe.preferredHeight = 28f;

        var gridGo = Create("Grid", section.transform);
        var grid = gridGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(208f, 46f);
        grid.spacing = new Vector2(8f, 8f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.startAxis = GridLayoutGroup.Axis.Horizontal;
        var gridFitter = gridGo.AddComponent<ContentSizeFitter>();
        gridFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        for (int i = 0; i < parts.Count; i++)
        {
            WardrobePart part = parts[i];
            var go = Create(part.Name, gridGo.transform);
            var image = go.AddComponent<Image>();
            image.sprite = _white;
            image.color = Idle;
            var button = go.AddComponent<Button>();
            button.targetGraphic = image;
            string slotCopy = slot;
            string nameCopy = part.Name;
            button.onClick.AddListener(() => _wardrobe.Toggle(slotCopy, nameCopy));

            var text = CreateText(go.transform, "Label", part.Label, 20, FontStyle.Normal);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            text.font = font;
            _buttons[slot + "/" + part.Name] = image;
        }
    }

    // 按当前装备高亮按钮：默认件用蓝灰，自定义件用金色。
    void Refresh()
    {
        if (_wardrobe == null)
            return;

        foreach (KeyValuePair<string, Image> pair in _buttons)
        {
            int slash = pair.Key.IndexOf('/');
            string slot = pair.Key.Substring(0, slash);
            string name = pair.Key.Substring(slash + 1);
            _wardrobe.Equipped.TryGetValue(slot, out string equipped);
            bool on = string.Equals(equipped, name, System.StringComparison.Ordinal);
            if (!on)
            {
                pair.Value.color = Idle;
                continue;
            }

            pair.Value.color = _wardrobe.IsDefault(slot) ? DefaultWorn : Equipped;
        }
    }

    // 场景没有 EventSystem 时补一个 Input System 模块。
    static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        var eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        eventGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    // 创建带 RectTransform 的 UI 子物体。
    static GameObject Create(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    // 创建一段不接收射线的文字。
    Text CreateText(Transform parent, string objectName, string value, int size, FontStyle style)
    {
        var go = Create(objectName, parent);
        var text = go.AddComponent<Text>();
        text.text = value;
        text.font = ResolveFont(size);
        text.fontSize = size;
        text.fontStyle = style;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    // 把 RectTransform 拉满父节点。
    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // 优先用中文字体，找不到再退回内置字体。
    static Font ResolveFont(int size)
    {
        Font osFont = Font.CreateDynamicFontFromOSFont(
            new[] { "PingFang SC", "Heiti SC", "Arial Unicode MS", "Arial" },
            size);
        return osFont != null ? osFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    // 生成 4x4 纯白 Sprite，用作按钮底图。
    static Sprite WhiteSprite()
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 4; x++)
                texture.SetPixel(x, y, Color.white);
        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0f, 0f, 4f, 4f), new Vector2(0.5f, 0.5f), 4f);
    }
}
