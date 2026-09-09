using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 导出 HelpOthers 的 iOS Xcode 工程，并写入签名、横屏、图标。
/// 也可以靠 Temp/BuildIOSNow.flag 在编辑器里自动触发。
/// </summary>
public static class BuildIOS
{
    const string FlagPath = "Temp/BuildIOSNow.flag";
    const string OutputPath = "Builds/iOS";
    const string IconPath = "Assets/Resources/characters/hero/imges/college.png";

    /// <summary>编辑器启动后监听 flag 文件。</summary>
    [InitializeOnLoadMethod]
    static void RegisterBuildHook()
    {
        EditorApplication.update -= TryBuildIfFlagged;
        EditorApplication.update += TryBuildIfFlagged;
    }

    /// <summary>发现 flag 就删掉并立刻打包（给外部脚本用）。</summary>
    static void TryBuildIfFlagged()
    {
        if (!File.Exists(FlagPath))
            return;

        EditorApplication.update -= TryBuildIfFlagged;
        File.Delete(FlagPath);
        Build();
    }

    /// <summary>菜单：Build/iOS Device。</summary>
    [MenuItem("Build/iOS Device")]
    public static void BuildFromMenu()
    {
        var code = Build();
        if (code != 0)
        {
            throw new BuildFailedException("iOS build failed.");
        }
    }

    /// <summary>真正调用 BuildPipeline，只打 HelpOthers 场景。</summary>
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

    /// <summary>包名、自动签名、仅横屏、应用图标。</summary>
    static void ApplyPlayerSettings()
    {
        PlayerSettings.companyName = "Liu Hongbin";
        PlayerSettings.productName = "RuinedHall";
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.liuhongbin.RuinedHall");
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.appleDeveloperTeamID = "L7S49HJY3G";
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.iOS.buildNumber = "5";

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

    /// <summary>某类 iOS 图标槽全部填同一张图。</summary>
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
