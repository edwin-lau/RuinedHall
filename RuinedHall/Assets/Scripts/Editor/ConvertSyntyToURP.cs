using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 把 Synty Sidekick 缺失的 Shader Graph 材质改成 URP Lit。
/// </summary>
public static class ConvertSyntyToURP
{
    const string FlagPath = "Temp/ConvertSyntyToURP.flag";
    const string Root = "Assets/Synty";

    /// <summary>编辑器启动后监听转换 flag。</summary>
    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    /// <summary>发现 flag 就执行一次转换。</summary>
    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!File.Exists(FlagPath))
            return;

        File.Delete(FlagPath);
        Convert();
    }

    /// <summary>遍历 Assets/Synty 下材质，拷贝贴图/颜色后换成 URP Lit。</summary>
    [MenuItem("Build/Convert Synty Materials to URP")]
    public static void Convert()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("ConvertSyntyToURP: URP Lit shader not found.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { Root });
        int count = 0;
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
                continue;

            // 先从旧 shader 属性里抠出贴图和颜色，再换成 URP Lit。
            Texture albedo = ReadSavedTexture(mat, "_ColorMap") ?? ReadSavedTexture(mat, "_MainTex");
            Texture metallic = ReadSavedTexture(mat, "_MetallicMap") ?? ReadSavedTexture(mat, "_MetallicGlossMap");
            Color color = ReadSavedColor(mat, "_Color", Color.white);

            mat.shader = lit;
            if (albedo != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetTexture("_MainTex", albedo);
            }

            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Surface", 0f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_AlphaClip", 0f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.28f);
            mat.SetColor("_EmissionColor", Color.black);
            mat.DisableKeyword("_EMISSION");
            mat.SetOverrideTag("RenderType", "Opaque");
            mat.renderQueue = -1;

            // 有金属贴图才开对应 keyword。
            if (metallic != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            EditorUtility.SetDirty(mat);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("ConvertSyntyToURP: converted " + count + " materials under " + Root + " to URP Lit.");
    }

    /// <summary>先读当前 shader 属性，没有再从序列化的 SavedProperties 里找贴图。</summary>
    static Texture ReadSavedTexture(Material mat, string property)
    {
        if (mat.HasProperty(property))
        {
            Texture live = mat.GetTexture(property);
            if (live != null)
                return live;
        }

        SerializedObject so = new SerializedObject(mat);
        SerializedProperty texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
        if (texEnvs == null)
            return null;

        for (int i = 0; i < texEnvs.arraySize; i++)
        {
            SerializedProperty entry = texEnvs.GetArrayElementAtIndex(i);
            SerializedProperty first = entry.FindPropertyRelative("first");
            if (first == null || first.stringValue != property)
                continue;

            SerializedProperty texture = entry.FindPropertyRelative("second.m_Texture");
            return texture != null ? texture.objectReferenceValue as Texture : null;
        }

        return null;
    }

    /// <summary>先读当前颜色，没有再从 SavedProperties 里找。</summary>
    static Color ReadSavedColor(Material mat, string property, Color fallback)
    {
        if (mat.HasProperty(property))
            return mat.GetColor(property);

        SerializedObject so = new SerializedObject(mat);
        SerializedProperty colors = so.FindProperty("m_SavedProperties.m_Colors");
        if (colors == null)
            return fallback;

        for (int i = 0; i < colors.arraySize; i++)
        {
            SerializedProperty entry = colors.GetArrayElementAtIndex(i);
            SerializedProperty first = entry.FindPropertyRelative("first");
            if (first == null || first.stringValue != property)
                continue;

            SerializedProperty value = entry.FindPropertyRelative("second");
            return value != null ? value.colorValue : fallback;
        }

        return fallback;
    }
}
