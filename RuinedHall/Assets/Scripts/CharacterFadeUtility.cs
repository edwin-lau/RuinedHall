using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 把角色材质改成透明后按时间淡出，最后销毁物体（尸体消散）。
/// </summary>
public static class CharacterFadeUtility
{
    /// <summary>淡出期间每帧改 alpha，结束销毁根物体。</summary>
    public static IEnumerator FadeAndDestroy(Transform root, float duration)
    {
        List<FadeMaterial> materials = PrepareMaterials(root);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            foreach (FadeMaterial material in materials)
                material.SetAlpha(alpha);
            yield return null;
        }

        if (root != null)
            Object.Destroy(root.gameObject);
    }

    /// <summary>复制实例材质、关掉阴影，并切到透明渲染。</summary>
    static List<FadeMaterial> PrepareMaterials(Transform root)
    {
        var result = new List<FadeMaterial>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            foreach (Material material in renderer.materials)
            {
                if (material == null)
                    continue;

                MakeTransparent(material);
                result.Add(new FadeMaterial(material));
            }
        }
        return result;
    }

    /// <summary>URP Lit 常见透明参数：Surface=Transparent，标准 Alpha 混合。</summary>
    static void MakeTransparent(Material material)
    {
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);

        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }

    /// <summary>记住原始颜色，只改 alpha 做淡出。</summary>
    readonly struct FadeMaterial
    {
        readonly Material _material;
        readonly Color _baseColor;
        readonly Color _legacyColor;
        readonly bool _hasBaseColor;
        readonly bool _hasLegacyColor;

        /// <summary>记下材质和原始颜色，后面只改 alpha。</summary>
        public FadeMaterial(Material material)
        {
            _material = material;
            _hasBaseColor = material.HasProperty("_BaseColor");
            _hasLegacyColor = material.HasProperty("_Color");
            _baseColor = _hasBaseColor ? material.GetColor("_BaseColor") : Color.white;
            _legacyColor = _hasLegacyColor ? material.GetColor("_Color") : Color.white;
        }

        /// <summary>按比例乘在原始 alpha 上。</summary>
        public void SetAlpha(float alpha)
        {
            if (_hasBaseColor)
            {
                Color color = _baseColor;
                color.a *= alpha;
                _material.SetColor("_BaseColor", color);
            }
            if (_hasLegacyColor)
            {
                Color color = _legacyColor;
                color.a *= alpha;
                _material.SetColor("_Color", color);
            }
        }
    }
}
