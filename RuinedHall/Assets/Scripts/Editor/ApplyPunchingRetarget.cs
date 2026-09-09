using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// 从 Soldier_Female@Punching.fbx 抽出拳击片段，覆盖 Punching.anim 并接到 Hero.controller 的 Punch 状态。
/// </summary>
public static class ApplyPunchingRetarget
{
    const string FlagPath = "Temp/ApplyPunchingRetarget.flag";
    const string FbxPath = "Assets/Resources/characters/hero/Soldier_Female@Punching.fbx";
    const string PunchingAnimPath = "Assets/Resources/characters/hero/Punching.anim";
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
            Debug.LogError("ApplyPunchingRetarget failed: " + ex);
        }
    }

    /// <summary>强制重导 FBX，把片段拷进 Punching.anim 并接到 Punch 状态。</summary>
    [MenuItem("Build/Apply Punching Retarget")]
    public static void Apply()
    {
        AssetDatabase.ImportAsset(FbxPath, ImportAssetOptions.ForceUpdate);

        // Generic 导入并关掉循环，保证拳击是一次性片段
        var importer = AssetImporter.GetAtPath(FbxPath) as ModelImporter;
        if (importer != null)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            var clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].name = "Punching";
                    clips[i].loopTime = false;
                }
                importer.clipAnimations = clips;
            }
            importer.SaveAndReimport();
        }

        AnimationClip source = AssetDatabase.LoadAllAssetsAtPath(FbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c != null && !c.name.StartsWith("__preview__", System.StringComparison.Ordinal));
        if (source == null)
        {
            Debug.LogError("ApplyPunchingRetarget: no AnimationClip in " + FbxPath);
            return;
        }

        // 覆盖或新建 Punching.anim，再接到控制器 Punch 状态
        var dest = AssetDatabase.LoadAssetAtPath<AnimationClip>(PunchingAnimPath);
        if (dest == null)
        {
            dest = Object.Instantiate(source);
            dest.name = "Punching";
            AssetDatabase.CreateAsset(dest, PunchingAnimPath);
        }
        else
        {
            EditorUtility.CopySerialized(source, dest);
            dest.name = "Punching";
            EditorUtility.SetDirty(dest);
        }

        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller != null)
        {
            foreach (var layer in controller.layers)
            {
                foreach (var state in layer.stateMachine.states)
                {
                    if (state.state.name == "Punch")
                    {
                        state.state.motion = dest;
                        EditorUtility.SetDirty(controller);
                    }
                }
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("ApplyPunchingRetarget: Punching.anim updated from " + FbxPath +
                  " length=" + dest.length + "s bindings=" + AnimationUtility.GetCurveBindings(dest).Length);
    }
}
