using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 把 Mixamo Punching 片段（与 FBX Inspector 预览一致）拷进 Punching.anim，
/// 并接到 Hero.controller。Idle/Walk/Run/Jump/Death 改用匹配 Mixamo 骨骼的绑定姿势 Idle。
/// </summary>
public static class ApplyMixamoPunching
{
    const string FlagPath = "Temp/ApplyMixamoPunching.flag";
    const string MixamoFbx = "Assets/Resources/characters/hero/Soldier_Female_MeshOnly@Punching.fbx";
    const string PunchingAnim = "Assets/Resources/characters/hero/Punching.anim";
    const string IdleMixamoAnim = "Assets/Resources/characters/hero/Idle_Mixamo.anim";
    const string ControllerPath = "Assets/Resources/characters/hero/Hero.controller";

    /// <summary>编辑器加载时挂上检测标记文件的回调。</summary>
    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    /// <summary>看到标记文件后执行一次 Apply，然后卸掉回调。</summary>
    static void Tick()
    {
        if (!File.Exists(FlagPath))
            return;
        EditorApplication.update -= Tick;
        File.Delete(FlagPath);
        try
        {
            Apply();
        }
        catch (System.Exception ex)
        {
            Debug.LogError("ApplyMixamoPunching failed: " + ex);
        }
    }

    /// <summary>从 Mixamo FBX 抽出 Punching，并改写 Hero.controller 的 Punch/Idle。</summary>
    [MenuItem("Build/Apply Mixamo Punching")]
    public static void Apply()
    {
        var importer = AssetImporter.GetAtPath(MixamoFbx) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("Missing " + MixamoFbx);
            return;
        }

        // Generic 导入，保证片段和 Inspector 预览一致。
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.SaveAndReimport();

        AnimationClip mixamoPunch = AssetDatabase.LoadAllAssetsAtPath(MixamoFbx)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c != null && !c.name.StartsWith("__preview__", System.StringComparison.Ordinal));
        if (mixamoPunch == null)
        {
            Debug.LogError("No AnimationClip in Mixamo FBX");
            return;
        }

        // 用 Mixamo 原片覆盖 Punching.anim，效果与 Inspector 预览一致。
        var punching = AssetDatabase.LoadAssetAtPath<AnimationClip>(PunchingAnim);
        if (punching == null)
        {
            punching = Object.Instantiate(mixamoPunch);
            punching.name = "Punching";
            AssetDatabase.CreateAsset(punching, PunchingAnim);
        }
        else
        {
            EditorUtility.CopySerialized(mixamoPunch, punching);
            punching.name = "Punching";
            EditorUtility.SetDirty(punching);
        }

        // Mixamo 骨骼用绑定姿势 Idle（空片段 + Write Defaults）。
        var idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleMixamoAnim);
        if (idle == null)
        {
            idle = new AnimationClip { name = "Idle_Mixamo", frameRate = 30f };
            AssetDatabase.CreateAsset(idle, IdleMixamoAnim);
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError("Missing Hero.controller");
            return;
        }

        // Punch 用拳击片段，其它状态改成 Mixamo 绑定姿势 Idle。
        foreach (var layer in controller.layers)
        {
            foreach (ChildAnimatorState child in layer.stateMachine.states)
            {
                AnimatorState state = child.state;
                if (state.name == "Punch")
                    state.motion = punching;
                else
                    state.motion = idle;
                EditorUtility.SetDirty(controller);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("ApplyMixamoPunching OK. Punching length=" + punching.length +
                  "s. Use Mixamo model in play mode via GameplayBootstrap.");
    }
}
