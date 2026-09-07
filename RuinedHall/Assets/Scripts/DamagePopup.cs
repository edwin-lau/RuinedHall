using UnityEngine;
using UnityEngine.UI;

public sealed class DamagePopup : MonoBehaviour
{
    const float Lifetime = 0.7f;
    const float Rise = 0.85f;

    Text _text;
    float _born;
    Vector3 _origin;

    public static void Spawn(Vector3 worldPoint, int amount)
    {
        if (amount <= 0)
            return;

        var go = new GameObject("DamagePopup", typeof(RectTransform), typeof(Canvas), typeof(DamagePopup));
        var popup = go.GetComponent<DamagePopup>();
        popup.Build(worldPoint, amount);
    }

    void Build(Vector3 worldPoint, int amount)
    {
        _origin = worldPoint + Vector3.up * 0.15f;
        _born = Time.time;

        var canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvas.sortingOrder = 220;
        canvas.overrideSorting = true;

        var rt = transform as RectTransform;
        rt.sizeDelta = new Vector2(160f, 48f);
        rt.localScale = Vector3.one * 0.012f;

        var textGo = new GameObject("Value", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        _text = textGo.GetComponent<Text>();
        _text.alignment = TextAnchor.MiddleCenter;
        _text.fontSize = 36;
        _text.fontStyle = FontStyle.Bold;
        _text.color = new Color(1f, 0.86f, 0.28f, 1f);
        _text.raycastTarget = false;
        _text.text = "-" + amount;
        _text.font = Font.CreateDynamicFontFromOSFont(
            new[] { "PingFang SC", "Heiti SC", "Arial Unicode MS", "Arial" },
            36);
        if (_text.font == null)
            _text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        transform.position = _origin;
    }

    void LateUpdate()
    {
        float t = (Time.time - _born) / Lifetime;
        if (t >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        transform.position = _origin + Vector3.up * (Rise * t);
        if (_text != null)
        {
            Color color = _text.color;
            color.a = 1f - t;
            _text.color = color;
        }

        if (Camera.main != null)
            transform.rotation = Camera.main.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
    }
}
