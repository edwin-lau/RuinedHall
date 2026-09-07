using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Converts Vefects / Vexa Built-in and Amplify materials to URP Lit.
/// </summary>
public static class ConvertVefectsToURP
{
    const string FlagPath = "Temp/ConvertVefectsToURP.flag";
    const string Root = "Assets/Vefects";

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!File.Exists(FlagPath))
            return;

        File.Delete(FlagPath);
        Convert();
    }

    [MenuItem("Build/Convert Vefects Materials to URP")]
    public static void Convert()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            Debug.LogError("ConvertVefectsToURP: URP Lit shader not found.");
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

            Texture albedo = FirstTexture(mat, "_BaseColorTexture", "_MultTexture", "_MainTex", "_BaseMap");
            Texture normal = FirstTexture(mat, "_NormalTexture", "_BumpMap");
            Texture metallic = FirstTexture(mat, "_MetallicTexture", "_MetallicGlossMap");
            Texture occlusion = FirstTexture(mat, "_AmbientOcclusionTexture", "_OcclusionMap");
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

            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }

            if (metallic != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            if (occlusion != null)
            {
                mat.SetTexture("_OcclusionMap", occlusion);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }

            EditorUtility.SetDirty(mat);
            count++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log("ConvertVefectsToURP: converted " + count + " materials under " + Root + " to URP Lit.");
    }

    static Texture FirstTexture(Material mat, params string[] names)
    {
        foreach (string name in names)
        {
            Texture texture = ReadSavedTexture(mat, name);
            if (texture != null)
                return texture;
        }

        return null;
    }

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
