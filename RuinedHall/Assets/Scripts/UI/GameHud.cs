using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class GameHud : MonoBehaviour
{
    public VirtualJoystick Joystick { get; private set; }
    public MobileActionButton PunchButton { get; private set; }
    public MobileActionButton JumpButton { get; private set; }

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

    void Build()
    {
        Sprite circle = UiSprites.Circle(256);

        Joystick = BuildJoystick(circle);
        PunchButton = BuildActionButton(
            "PunchButton",
            "拳",
            new Vector2(-210f, 210f),
            200f,
            new Color(0.92f, 0.38f, 0.22f, 0.92f),
            circle);
        JumpButton = BuildActionButton(
            "JumpButton",
            "跳",
            new Vector2(-430f, 320f),
            150f,
            new Color(0.22f, 0.72f, 0.86f, 0.92f),
            circle);
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
        if (EventSystem.current != null)
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
}
