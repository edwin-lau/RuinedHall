using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class GameHud : MonoBehaviour
{
    const string HeroPortraitResource = "characters/hero/imges/college";
    const string CircleSpriteResource = "ui/circle";
    const string WhiteSpriteResource = "ui/white";

    [SerializeField] VirtualJoystick joystick;
    [SerializeField] MobileActionButton punchButton;
    [SerializeField] MobileActionButton jumpButton;
    [SerializeField] Image healthFill;
    [SerializeField] Text healthText;
    [SerializeField] Image runStaminaFill;
    [SerializeField] Image portrait;
    [SerializeField] Text goldText;
    [SerializeField] Text eliteText;
    [SerializeField] Text reviveStaminaText;
    [SerializeField] Image eliteIcon;
    [SerializeField] Image goldIcon;
    [SerializeField] Image reviveStaminaIcon;
    [SerializeField] GameObject revivePanel;
    [SerializeField] Text reviveMessage;
    [SerializeField] Button reviveConfirm;
    [SerializeField] Button reviveCancel;
    [SerializeField] Button addStaminaButton;

    public VirtualJoystick Joystick => joystick;
    public MobileActionButton PunchButton => punchButton;
    public MobileActionButton JumpButton => jumpButton;

    HeroController _hero;

    public static GameHud Create()
    {
        EnsureEventSystem();

        var canvasGo = new GameObject("GameHud", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(GameHud));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;
        canvas.sortingOrder = 100;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        var hud = canvasGo.GetComponent<GameHud>();
        hud.Build();
        return hud;
    }

    public void BindHero(HeroController hero)
    {
        if (_hero != null)
        {
            _hero.HealthChanged -= OnHeroHealth;
            _hero.RunStaminaChanged -= OnHeroRunStamina;
            _hero.Died -= OnHeroDied;
        }

        _hero = hero;
        if (_hero == null)
            return;

        _hero.HealthChanged += OnHeroHealth;
        _hero.RunStaminaChanged += OnHeroRunStamina;
        _hero.Died += OnHeroDied;
        SetHealth(_hero.CurrentHealth, _hero.MaxHealth);
        EnsureRunStaminaBar();
        SetRunStamina(_hero.RunStamina);
        EnsureProgressHud();
        EnsureReviveDialog();
        EnsureAddStaminaButton();
        RefreshProgress();
        if (_hero.IsDead)
            ShowReviveDialog();
        else
            HideReviveDialog();
    }

    void OnEnable()
    {
        PlayerProgress.Changed += RefreshProgress;
        EnsureProgressHud();
        EnsureAddStaminaButton();
        RefreshProgress();
    }

    void OnDisable()
    {
        PlayerProgress.Changed -= RefreshProgress;
    }

    void OnDestroy()
    {
        if (_hero != null)
        {
            _hero.HealthChanged -= OnHeroHealth;
            _hero.RunStaminaChanged -= OnHeroRunStamina;
            _hero.Died -= OnHeroDied;
        }

        PlayerProgress.Changed -= RefreshProgress;
    }

    public void Build()
    {
        Sprite circle = LoadCircleSprite();

        BuildHeroStatus(circle);
        EnsureProgressHud();
        EnsureReviveDialog();
        EnsureAddStaminaButton();
        joystick = BuildJoystick(circle);
        punchButton = BuildActionButton(
            "PunchButton",
            "拳",
            new Vector2(-210f, 210f),
            200f,
            new Color(0.92f, 0.38f, 0.22f, 0.92f),
            circle);
        jumpButton = BuildActionButton(
            "JumpButton",
            "跳",
            new Vector2(-430f, 320f),
            150f,
            new Color(0.22f, 0.72f, 0.86f, 0.92f),
            circle);
    }

    void BuildHeroStatus(Sprite circle)
    {
        var root = CreateUiObject("HeroStatus", transform);
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0f, 1f);
        rootRt.anchorMax = new Vector2(0f, 1f);
        rootRt.pivot = new Vector2(0f, 1f);
        rootRt.sizeDelta = new Vector2(560f, 148f);
        rootRt.anchoredPosition = new Vector2(36f, -28f);

        var frameGo = CreateUiObject("AvatarFrame", rootRt);
        var frameRt = frameGo.GetComponent<RectTransform>();
        frameRt.anchorMin = new Vector2(0f, 0.5f);
        frameRt.anchorMax = new Vector2(0f, 0.5f);
        frameRt.pivot = new Vector2(0f, 0.5f);
        frameRt.sizeDelta = new Vector2(112f, 112f);
        frameRt.anchoredPosition = Vector2.zero;
        var frameImage = frameGo.AddComponent<Image>();
        frameImage.sprite = circle;
        frameImage.color = new Color(0.08f, 0.08f, 0.08f, 0.82f);
        frameImage.raycastTarget = false;

        var maskGo = CreateUiObject("AvatarMask", frameRt);
        var maskRt = maskGo.GetComponent<RectTransform>();
        maskRt.anchorMin = new Vector2(0.5f, 0.5f);
        maskRt.anchorMax = new Vector2(0.5f, 0.5f);
        maskRt.pivot = new Vector2(0.5f, 0.5f);
        maskRt.sizeDelta = new Vector2(96f, 96f);
        maskRt.anchoredPosition = Vector2.zero;
        var maskImage = maskGo.AddComponent<Image>();
        maskImage.sprite = circle;
        maskImage.raycastTarget = false;
        var mask = maskGo.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        var portraitGo = CreateUiObject("Portrait", maskRt);
        Stretch(portraitGo.GetComponent<RectTransform>());
        portrait = portraitGo.AddComponent<Image>();
        portrait.preserveAspect = true;
        portrait.raycastTarget = false;
        portrait.sprite = LoadHeroPortrait();
        portrait.color = portrait.sprite != null
            ? Color.white
            : new Color(0.75f, 0.62f, 0.42f, 1f);

        var infoGo = CreateUiObject("Info", rootRt);
        var infoRt = infoGo.GetComponent<RectTransform>();
        infoRt.anchorMin = new Vector2(0f, 0.5f);
        infoRt.anchorMax = new Vector2(0f, 0.5f);
        infoRt.pivot = new Vector2(0f, 0.5f);
        infoRt.sizeDelta = new Vector2(400f, 108f);
        infoRt.anchoredPosition = new Vector2(128f, 0f);

        var nameGo = CreateUiObject("Name", infoRt);
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 1f);
        nameRt.anchorMax = new Vector2(1f, 1f);
        nameRt.pivot = new Vector2(0f, 1f);
        nameRt.sizeDelta = new Vector2(0f, 36f);
        nameRt.anchoredPosition = Vector2.zero;
        var nameText = nameGo.AddComponent<Text>();
        nameText.text = "Hero";
        nameText.alignment = TextAnchor.MiddleLeft;
        nameText.color = Color.white;
        nameText.fontSize = 28;
        nameText.raycastTarget = false;
        nameText.font = ResolveUiFont(28);

        var barBgGo = CreateUiObject("HealthBar", infoRt);
        var barBgRt = barBgGo.GetComponent<RectTransform>();
        barBgRt.anchorMin = new Vector2(0f, 0f);
        barBgRt.anchorMax = new Vector2(1f, 0f);
        barBgRt.pivot = new Vector2(0f, 0f);
        barBgRt.sizeDelta = new Vector2(0f, 28f);
        barBgRt.anchoredPosition = new Vector2(0f, 8f);
        var barBg = barBgGo.AddComponent<Image>();
        barBg.sprite = LoadWhiteSprite();
        barBg.color = new Color(0f, 0f, 0f, 0.55f);
        barBg.raycastTarget = false;

        var fillGo = CreateUiObject("Fill", barBgRt);
        Stretch(fillGo.GetComponent<RectTransform>());
        fillGo.GetComponent<RectTransform>().offsetMin = new Vector2(3f, 3f);
        fillGo.GetComponent<RectTransform>().offsetMax = new Vector2(-3f, -3f);
        healthFill = fillGo.AddComponent<Image>();
        healthFill.sprite = LoadWhiteSprite();
        healthFill.type = Image.Type.Filled;
        healthFill.fillMethod = Image.FillMethod.Horizontal;
        healthFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        healthFill.color = new Color(0.86f, 0.22f, 0.2f, 0.95f);
        healthFill.raycastTarget = false;

        var hpGo = CreateUiObject("Value", barBgRt);
        Stretch(hpGo.GetComponent<RectTransform>());
        healthText = hpGo.AddComponent<Text>();
        healthText.text = "100 / 100";
        healthText.alignment = TextAnchor.MiddleCenter;
        healthText.color = Color.white;
        healthText.fontSize = 18;
        healthText.raycastTarget = false;
        healthText.font = ResolveUiFont(18);

        var staminaBgGo = CreateUiObject("RunStaminaBar", infoRt);
        var staminaBgRt = staminaBgGo.GetComponent<RectTransform>();
        staminaBgRt.anchorMin = new Vector2(0f, 0f);
        staminaBgRt.anchorMax = new Vector2(1f, 0f);
        staminaBgRt.pivot = new Vector2(0f, 0f);
        staminaBgRt.sizeDelta = new Vector2(0f, 12f);
        staminaBgRt.anchoredPosition = new Vector2(0f, -8f);
        var staminaBg = staminaBgGo.AddComponent<Image>();
        staminaBg.sprite = LoadWhiteSprite();
        staminaBg.color = new Color(0.18f, 0.18f, 0.18f, 0.72f);
        staminaBg.raycastTarget = false;

        var staminaFillGo = CreateUiObject("Fill", staminaBgRt);
        Stretch(staminaFillGo.GetComponent<RectTransform>());
        staminaFillGo.GetComponent<RectTransform>().offsetMin = new Vector2(2f, 2f);
        staminaFillGo.GetComponent<RectTransform>().offsetMax = new Vector2(-2f, -2f);
        runStaminaFill = staminaFillGo.AddComponent<Image>();
        runStaminaFill.sprite = LoadWhiteSprite();
        runStaminaFill.type = Image.Type.Filled;
        runStaminaFill.fillMethod = Image.FillMethod.Horizontal;
        runStaminaFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        runStaminaFill.color = new Color(0.72f, 0.72f, 0.72f, 0.95f);
        runStaminaFill.raycastTarget = false;
        runStaminaFill.fillAmount = 1f;
    }

    void EnsureRunStaminaBar()
    {
        if (runStaminaFill != null)
            return;

        Transform info = transform.Find("HeroStatus/Info");
        if (info == null)
            return;

        var infoRt = info.GetComponent<RectTransform>();
        infoRt.sizeDelta = new Vector2(400f, 108f);

        var staminaBgGo = CreateUiObject("RunStaminaBar", infoRt);
        var staminaBgRt = staminaBgGo.GetComponent<RectTransform>();
        staminaBgRt.anchorMin = new Vector2(0f, 0f);
        staminaBgRt.anchorMax = new Vector2(1f, 0f);
        staminaBgRt.pivot = new Vector2(0f, 0f);
        staminaBgRt.sizeDelta = new Vector2(0f, 12f);
        staminaBgRt.anchoredPosition = new Vector2(0f, -8f);
        var staminaBg = staminaBgGo.AddComponent<Image>();
        staminaBg.sprite = LoadWhiteSprite();
        staminaBg.color = new Color(0.18f, 0.18f, 0.18f, 0.72f);
        staminaBg.raycastTarget = false;

        var staminaFillGo = CreateUiObject("Fill", staminaBgRt);
        Stretch(staminaFillGo.GetComponent<RectTransform>());
        staminaFillGo.GetComponent<RectTransform>().offsetMin = new Vector2(2f, 2f);
        staminaFillGo.GetComponent<RectTransform>().offsetMax = new Vector2(-2f, -2f);
        runStaminaFill = staminaFillGo.AddComponent<Image>();
        runStaminaFill.sprite = LoadWhiteSprite();
        runStaminaFill.type = Image.Type.Filled;
        runStaminaFill.fillMethod = Image.FillMethod.Horizontal;
        runStaminaFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        runStaminaFill.color = new Color(0.72f, 0.72f, 0.72f, 0.95f);
        runStaminaFill.raycastTarget = false;
        runStaminaFill.fillAmount = 1f;
    }

    void EnsureProgressHud()
    {
        if (goldText != null && eliteText != null && reviveStaminaText != null)
            return;

        RectTransform rootRt = null;
        if (goldText != null)
            rootRt = goldText.transform.parent as RectTransform;
        if (rootRt == null)
        {
            var root = CreateUiObject("ProgressStatus", transform);
            rootRt = root.GetComponent<RectTransform>();
            rootRt.anchorMin = new Vector2(1f, 1f);
            rootRt.anchorMax = new Vector2(1f, 1f);
            rootRt.pivot = new Vector2(1f, 1f);
            rootRt.anchoredPosition = new Vector2(-36f, -28f);
        }

        rootRt.sizeDelta = new Vector2(280f, 168f);
        if (eliteText == null)
            CreateProgressRow(rootRt, "EliteKills", new Vector2(0f, -8f), UiSprites.EliteSkull(), out eliteIcon, out eliteText);
        if (goldText == null)
            CreateProgressRow(rootRt, "Gold", new Vector2(0f, -60f), UiSprites.Coin(), out goldIcon, out goldText);
        if (reviveStaminaText == null)
            CreateProgressRow(rootRt, "ReviveStamina", new Vector2(0f, -112f), UiSprites.Heart(), out reviveStaminaIcon, out reviveStaminaText);
        EnsureAddStaminaButton();
    }

    void EnsureAddStaminaButton()
    {
        if (addStaminaButton != null)
        {
            addStaminaButton.interactable = true;
            addStaminaButton.transform.SetAsLastSibling();
            return;
        }

        var go = CreateUiObject("AddStaminaButton", transform);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(160f, 64f);
        rt.anchoredPosition = new Vector2(-36f, -208f);
        var image = go.AddComponent<Image>();
        image.sprite = LoadWhiteSprite();
        image.color = new Color(0.22f, 0.56f, 0.34f, 0.96f);
        image.raycastTarget = true;
        addStaminaButton = go.AddComponent<Button>();
        addStaminaButton.targetGraphic = image;
        addStaminaButton.interactable = true;
        addStaminaButton.onClick.AddListener(OnAddStamina);

        var labelGo = CreateUiObject("Label", rt);
        Stretch(labelGo.GetComponent<RectTransform>());
        var label = labelGo.AddComponent<Text>();
        label.text = "+体力";
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = 28;
        label.raycastTarget = false;
        label.font = ResolveUiFont(28);
        addStaminaButton.transform.SetAsLastSibling();
    }

    void OnAddStamina()
    {
        PlayerProgress.Instance.AddStamina(1);
    }

    void LateUpdate()
    {
        if (addStaminaButton != null)
            addStaminaButton.transform.SetAsLastSibling();
    }

    void CreateProgressRow(
        RectTransform parent,
        string objectName,
        Vector2 anchored,
        Sprite iconSprite,
        out Image icon,
        out Text valueText)
    {
        var row = CreateUiObject(objectName, parent);
        var rowRt = row.GetComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0f, 1f);
        rowRt.anchorMax = new Vector2(1f, 1f);
        rowRt.pivot = new Vector2(1f, 1f);
        rowRt.sizeDelta = new Vector2(0f, 44f);
        rowRt.anchoredPosition = anchored;

        var iconGo = CreateUiObject("Icon", rowRt);
        var iconRt = iconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(1f, 0.5f);
        iconRt.anchorMax = new Vector2(1f, 0.5f);
        iconRt.pivot = new Vector2(1f, 0.5f);
        iconRt.sizeDelta = new Vector2(36f, 36f);
        iconRt.anchoredPosition = new Vector2(0f, 0f);
        icon = iconGo.AddComponent<Image>();
        icon.sprite = iconSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;

        var valueGo = CreateUiObject("Value", rowRt);
        var valueRt = valueGo.GetComponent<RectTransform>();
        valueRt.anchorMin = new Vector2(0f, 0f);
        valueRt.anchorMax = new Vector2(1f, 1f);
        valueRt.offsetMin = Vector2.zero;
        valueRt.offsetMax = new Vector2(-44f, 0f);
        valueText = valueGo.AddComponent<Text>();
        valueText.text = "0";
        valueText.alignment = TextAnchor.MiddleRight;
        valueText.color = Color.white;
        valueText.fontSize = 30;
        valueText.raycastTarget = false;
        valueText.font = ResolveUiFont(30);
    }

    Text CreateProgressLabel(RectTransform parent, string objectName, Vector2 anchored, string value)
    {
        var go = CreateUiObject(objectName, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(0f, 42f);
        rt.anchoredPosition = anchored;
        var text = go.AddComponent<Text>();
        text.text = value;
        text.alignment = TextAnchor.MiddleRight;
        text.color = Color.white;
        text.fontSize = 30;
        text.raycastTarget = false;
        text.font = ResolveUiFont(30);
        return text;
    }

    void RefreshProgress()
    {
        EnsureProgressHud();
        var progress = PlayerProgress.Instance;
        if (eliteText != null)
            eliteText.text = progress.EliteKills.ToString();
        if (goldText != null)
            goldText.text = progress.Gold.ToString();
        if (reviveStaminaText != null)
            reviveStaminaText.text = progress.Stamina.ToString();
        RefreshReviveMessage();
    }

    void OnHeroDied()
    {
        ShowReviveDialog();
    }

    void EnsureReviveDialog()
    {
        if (revivePanel != null)
            return;

        var overlay = CreateUiObject("ReviveOverlay", transform);
        var overlayRt = overlay.GetComponent<RectTransform>();
        Stretch(overlayRt);
        var overlayImage = overlay.AddComponent<Image>();
        overlayImage.sprite = LoadWhiteSprite();
        overlayImage.color = new Color(0f, 0f, 0f, 0.55f);
        overlayImage.raycastTarget = true;

        var box = CreateUiObject("Box", overlayRt);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(720f, 320f);
        boxRt.anchoredPosition = Vector2.zero;
        var boxImage = box.AddComponent<Image>();
        boxImage.sprite = LoadWhiteSprite();
        boxImage.color = new Color(0.12f, 0.12f, 0.14f, 0.96f);
        boxImage.raycastTarget = true;

        reviveMessage = CreateProgressLabel(boxRt, "Message", new Vector2(0f, -36f), "");
        reviveMessage.alignment = TextAnchor.MiddleCenter;
        var messageRt = reviveMessage.GetComponent<RectTransform>();
        messageRt.anchorMin = new Vector2(0.08f, 0.46f);
        messageRt.anchorMax = new Vector2(0.92f, 0.88f);
        messageRt.offsetMin = Vector2.zero;
        messageRt.offsetMax = Vector2.zero;
        messageRt.anchoredPosition = Vector2.zero;
        messageRt.sizeDelta = Vector2.zero;
        reviveMessage.fontSize = 32;

        reviveConfirm = CreateDialogButton(boxRt, "Confirm", "确定", new Vector2(-140f, 48f), new Color(0.22f, 0.62f, 0.36f, 1f));
        reviveCancel = CreateDialogButton(boxRt, "Cancel", "取消", new Vector2(140f, 48f), new Color(0.42f, 0.42f, 0.44f, 1f));
        reviveConfirm.onClick.AddListener(OnReviveConfirm);
        reviveCancel.onClick.AddListener(HideReviveDialog);
        revivePanel = overlay;
        HideReviveDialog();
    }

    Button CreateDialogButton(RectTransform parent, string objectName, string label, Vector2 anchored, Color color)
    {
        var go = CreateUiObject(objectName, parent);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0f);
        rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 72f);
        rt.anchoredPosition = anchored;
        var image = go.AddComponent<Image>();
        image.sprite = LoadWhiteSprite();
        image.color = color;
        image.raycastTarget = true;
        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        var textGo = CreateUiObject("Label", rt);
        Stretch(textGo.GetComponent<RectTransform>());
        var text = textGo.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontSize = 30;
        text.raycastTarget = false;
        text.font = ResolveUiFont(30);
        return button;
    }

    void ShowReviveDialog()
    {
        EnsureReviveDialog();
        RefreshReviveMessage();
        if (revivePanel != null)
            revivePanel.SetActive(true);
    }

    void HideReviveDialog()
    {
        if (revivePanel != null)
            revivePanel.SetActive(false);
    }

    void RefreshReviveMessage()
    {
        if (reviveMessage == null)
            return;

        int stamina = PlayerProgress.Instance.Stamina;
        reviveMessage.text = stamina > 0
            ? "是否消耗 1 点体力复活？\n当前体力 " + stamina
            : "体力不足，无法复活。\n当前体力 0";
        if (reviveConfirm != null)
            reviveConfirm.interactable = stamina > 0;
    }

    void OnReviveConfirm()
    {
        if (_hero == null || !_hero.IsDead)
        {
            HideReviveDialog();
            return;
        }

        if (!PlayerProgress.Instance.TryConsumeStamina(1))
        {
            RefreshReviveMessage();
            return;
        }

        _hero.Revive();
        HideReviveDialog();
    }

    void OnHeroHealth(int current, int max)
    {
        SetHealth(current, max);
    }

    void OnHeroRunStamina(float amount)
    {
        SetRunStamina(amount);
    }

    void SetRunStamina(float amount)
    {
        if (runStaminaFill != null)
            runStaminaFill.fillAmount = Mathf.Clamp01(amount);
    }

    void SetHealth(int current, int max)
    {
        max = Mathf.Max(1, max);
        current = Mathf.Clamp(current, 0, max);
        if (healthFill != null)
            healthFill.fillAmount = current / (float)max;
        if (healthText != null)
            healthText.text = $"{current} / {max}";
    }

    static Sprite LoadHeroPortrait()
    {
        var sprite = Resources.Load<Sprite>(HeroPortraitResource);
        if (sprite != null)
            return sprite;

        var texture = Resources.Load<Texture2D>(HeroPortraitResource);
        if (texture == null)
            return null;

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    static Sprite LoadCircleSprite()
    {
        var sprite = Resources.Load<Sprite>(CircleSpriteResource);
        return sprite != null ? sprite : UiSprites.Circle(256);
    }

    static Sprite LoadWhiteSprite()
    {
        var sprite = Resources.Load<Sprite>(WhiteSpriteResource);
        return sprite != null ? sprite : UiSprites.White();
    }

    VirtualJoystick BuildJoystick(Sprite circle)
    {
        var zoneGo = CreateUiObject("JoystickZone", transform);
        var zoneRt = zoneGo.GetComponent<RectTransform>();
        zoneRt.anchorMin = Vector2.zero;
        zoneRt.anchorMax = new Vector2(0.48f, 1f);
        zoneRt.pivot = Vector2.zero;
        zoneRt.offsetMin = Vector2.zero;
        zoneRt.offsetMax = Vector2.zero;

        var zoneImage = zoneGo.AddComponent<Image>();
        zoneImage.color = new Color(1f, 1f, 1f, 0f);
        zoneImage.raycastTarget = true;

        var joystick = zoneGo.AddComponent<VirtualJoystick>();

        var handleGo = CreateUiObject("Handle", zoneRt);
        var handleRt = handleGo.GetComponent<RectTransform>();
        handleRt.anchorMin = Vector2.zero;
        handleRt.anchorMax = Vector2.zero;
        handleRt.pivot = new Vector2(0.5f, 0.5f);
        handleRt.sizeDelta = new Vector2(260f, 260f);
        handleRt.anchoredPosition = new Vector2(220f, 230f);

        var baseGo = CreateUiObject("Base", handleRt);
        Stretch(baseGo.GetComponent<RectTransform>());
        var baseImage = baseGo.AddComponent<Image>();
        baseImage.sprite = circle;
        baseImage.color = new Color(0f, 0f, 0f, 0.38f);
        baseImage.raycastTarget = false;

        var ringGo = CreateUiObject("Ring", handleRt);
        var ringRt = ringGo.GetComponent<RectTransform>();
        ringRt.anchorMin = new Vector2(0.5f, 0.5f);
        ringRt.anchorMax = new Vector2(0.5f, 0.5f);
        ringRt.pivot = new Vector2(0.5f, 0.5f);
        ringRt.sizeDelta = new Vector2(248f, 248f);
        ringRt.anchoredPosition = Vector2.zero;
        var ringImage = ringGo.AddComponent<Image>();
        ringImage.sprite = circle;
        ringImage.color = new Color(1f, 1f, 1f, 0.16f);
        ringImage.raycastTarget = false;

        var knobGo = CreateUiObject("Knob", handleRt);
        var knobRt = knobGo.GetComponent<RectTransform>();
        knobRt.anchorMin = new Vector2(0.5f, 0.5f);
        knobRt.anchorMax = new Vector2(0.5f, 0.5f);
        knobRt.pivot = new Vector2(0.5f, 0.5f);
        knobRt.sizeDelta = new Vector2(108f, 108f);
        knobRt.anchoredPosition = Vector2.zero;
        var knobImage = knobGo.AddComponent<Image>();
        knobImage.sprite = circle;
        knobImage.color = new Color(1f, 1f, 1f, 0.82f);
        knobImage.raycastTarget = false;

        joystick.Configure(handleRt, knobRt, 86f);
        return joystick;
    }

    MobileActionButton BuildActionButton(string objectName, string label, Vector2 anchoredPos, float size, Color color, Sprite circle)
    {
        var go = CreateUiObject(objectName, transform);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = anchoredPos;

        var image = go.AddComponent<Image>();
        image.sprite = circle;
        image.color = color;
        image.raycastTarget = true;

        var button = go.AddComponent<MobileActionButton>();
        button.Configure(image);

        var textGo = CreateUiObject("Label", rt);
        Stretch(textGo.GetComponent<RectTransform>());
        var text = textGo.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.fontSize = Mathf.RoundToInt(size * 0.32f);
        text.raycastTarget = false;
        text.font = ResolveUiFont(text.fontSize);
        return button;
    }

    static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() != null)
            return;

        var eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var module = eventGo.GetComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    static Font ResolveUiFont(int size)
    {
        Font osFont = Font.CreateDynamicFontFromOSFont(
            new[] { "PingFang SC", "Heiti SC", "Arial Unicode MS", "Arial" },
            size);
        if (osFont != null)
            return osFont;

        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    static GameObject CreateUiObject(string objectName, Transform parent)
    {
        var go = new GameObject(objectName, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }
}

static class UiSprites
{
    public static Sprite Circle(int size)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        float center = (size - 1) * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                float alpha = Mathf.Clamp01(radius - distance + 0.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }

    public static Sprite White()
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
                texture.SetPixel(x, y, Color.white);
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0f, 0f, 4, 4), new Vector2(0.5f, 0.5f), 4f);
    }

    public static Sprite Coin()
    {
        return RasterIcon(64, (x, y, center, radius) =>
        {
            float dist = Vector2.Distance(new Vector2(x, y), center);
            if (dist > radius)
                return Color.clear;
            Color edge = new Color(0.86f, 0.62f, 0.12f, 1f);
            Color core = new Color(1f, 0.86f, 0.28f, 1f);
            return Color.Lerp(core, edge, Mathf.Clamp01((dist - radius * 0.55f) / (radius * 0.45f)));
        });
    }

    public static Sprite EliteSkull()
    {
        return RasterIcon(64, (x, y, center, radius) =>
        {
            Vector2 p = new Vector2(x, y) - center;
            float head = Vector2.Distance(p, new Vector2(0f, 4f)) / (radius * 0.72f);
            float jaw = Vector2.Distance(p, new Vector2(0f, -10f)) / (radius * 0.42f);
            bool shape = head <= 1f || jaw <= 1f;
            if (!shape)
                return Color.clear;
            bool eye = Vector2.Distance(p, new Vector2(-8f, 6f)) < 4.5f ||
                       Vector2.Distance(p, new Vector2(8f, 6f)) < 4.5f;
            return eye ? new Color(0.12f, 0.12f, 0.12f, 1f) : new Color(0.92f, 0.78f, 0.2f, 1f);
        });
    }

    public static Sprite Heart()
    {
        return RasterIcon(64, (x, y, center, radius) =>
        {
            Vector2 p = (new Vector2(x, y) - center) / radius;
            float a = p.x * p.x + (p.y - 0.18f) * (p.y - 0.18f) - 0.22f;
            float b = p.x * p.x + (p.y + 0.28f) * (p.y + 0.28f) - 0.55f;
            bool shape = a * a * a - p.x * p.x * (p.y - 0.18f) * (p.y - 0.18f) * (p.y - 0.18f) <= 0f ||
                         b <= 0f;
            return shape ? new Color(0.92f, 0.24f, 0.28f, 1f) : Color.clear;
        });
    }

    static Sprite RasterIcon(int size, System.Func<int, int, Vector2, float, Color> sample)
    {
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float radius = center.x - 2f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, sample(x, y, center, radius));
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
    }
}
