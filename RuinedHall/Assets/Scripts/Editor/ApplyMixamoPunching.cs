using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// Copies the Mixamo Punching clip (as previewed on the Mixamo FBX) into Punching.anim
/// and points Hero.controller at it. Idle/Walk/Run/Jump/Death use a bind-pose Idle clip
/// that matches the Mixamo skeleton.
/// </summary>
public static class ApplyMixamoPunching
{
    const string FlagPath = "Temp/ApplyMixamoPunching.flag";
    const string MixamoFbx = "Assets/Resources/characters/hero/Soldier_Female_MeshOnly@Punching.fbx";
    const string PunchingAnim = "Assets/Resources/characters/hero/Punching.anim";
    const string IdleMixamoAnim = "Assets/Resources/characters/hero/Idle_Mixamo.anim";
    const string ControllerPath = "Assets/Resources/characters/hero/Hero.controller";

    [InitializeOnLoadMethod]
    static void Register()
    {
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

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

    [MenuItem("Build/Apply Mixamo Punching")]
    public static void Apply()
    {
        var importer = AssetImporter.GetAtPath(MixamoFbx) as ModelImporter;
        if (importer == null)
        {
            Debug.LogError("Missing " + MixamoFbx);
            return;
        }

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

        // Overwrite Punching.anim with the exact Mixamo clip (same as Inspector preview).
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

        // Bind-pose idle for Mixamo skeleton (empty clip + Write Defaults).
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
