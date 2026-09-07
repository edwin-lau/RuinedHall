using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildIOS
{
    const string FlagPath = "Temp/BuildIOSNow.flag";
    const string OutputPath = "Builds/iOS";
    const string IconPath = "Assets/Resources/characters/hero/imges/college.png";

    [InitializeOnLoadMethod]
    static void RegisterBuildHook()
    {
        EditorApplication.update -= TryBuildIfFlagged;
        EditorApplication.update += TryBuildIfFlagged;
    }

    static void TryBuildIfFlagged()
    {
        if (!File.Exists(FlagPath))
            return;

        EditorApplication.update -= TryBuildIfFlagged;
        File.Delete(FlagPath);
        Build();
    }

    [MenuItem("Build/iOS Device")]
    public static void BuildFromMenu()
    {
        var code = Build();
        if (code != 0)
        {
            throw new BuildFailedException("iOS build failed.");
        }
    }

    public static int Build()
    {
        ApplyPlayerSettings();

        Directory.CreateDirectory(OutputPath);

        var report = UnityEditor.BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/HelpOthers.unity" },
            locationPathName = OutputPath,
            target = BuildTarget.iOS,
            options = BuildOptions.CompressWithLz4
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"iOS build failed: {report.summary.result}");
            return 1;
        }

        Debug.Log($"iOS build succeeded: {OutputPath}");
        return 0;
    }

    static void ApplyPlayerSettings()
    {
        PlayerSettings.companyName = "Liu Hongbin";
        PlayerSettings.productName = "RuinedHall";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.liuhongbin.RuinedHall");
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.appleDeveloperTeamID = "L7S49HJY3G";
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.buildNumber = "4";

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null)
        {
            Debug.LogError($"Missing app icon at {IconPath}");
            return;
        }

        AssignIosIcons(icon, UnityEditor.iOS.iOSPlatformIconKind.Application);
        AssignIosIcons(icon, UnityEditor.iOS.iOSPlatformIconKind.Spotlight);
        AssignIosIcons(icon, UnityEditor.iOS.iOSPlatformIconKind.Settings);
        AssignIosIcons(icon, UnityEditor.iOS.iOSPlatformIconKind.Notification);
        AssignIosIcons(icon, UnityEditor.iOS.iOSPlatformIconKind.Marketing);
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Application);
    }

    static void AssignIosIcons(Texture2D icon, PlatformIconKind kind)
    {
        var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.iOS, kind);
        for (var i = 0; i < icons.Length; i++)
        {
            icons[i].SetTexture(icon);
        }

        PlayerSettings.SetPlatformIcons(NamedBuildTarget.iOS, kind, icons);
    }
}
