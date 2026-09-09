using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// 往 HelpOthers 场景放 GameHud，并另存预制体。可用 flag 自动触发。
/// </summary>
public static class PlaceGameHudInScene
{
    const string FlagPath = "Temp/PlaceGameHud.flag";
    const string ScenePath = "Assets/Scenes/HelpOthers.unity";
    const string PrefabPath = "Assets/Prefabs/GameHud.prefab";

    /// <summary>编辑器启动后若存在 flag 则延迟执行放置。</summary>
    [InitializeOnLoadMethod]
    static void PlaceIfFlagged()
    {
        if (!File.Exists(FlagPath))
            return;

        EditorApplication.delayCall += () =>
        {
            if (!File.Exists(FlagPath))
                return;

            File.Delete(FlagPath);
            Place();
        };
    }

    /// <summary>清旧 HUD、保证只有一个 EventSystem，创建 HUD 并保存场景/预制体。</summary>
    [MenuItem("Game/Place Game HUD In Scene")]
    public static void Place()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        // 清掉旧 HUD，只留一个 EventSystem。
        foreach (var existing in Object.FindObjectsByType<GameHud>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            Object.DestroyImmediate(existing.gameObject);

        var eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 1; i < eventSystems.Length; i++)
            Object.DestroyImmediate(eventSystems[i].gameObject);

        AssetDatabase.ImportAsset("Assets/Resources/ui/circle.png", ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset("Assets/Resources/ui/white.png", ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.ImportAsset("Assets/Resources/characters/hero/imges/college.png", ImportAssetOptions.ForceSynchronousImport);

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventGo.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        // 创建 HUD，连到预制体并保存场景。
        var hud = GameHud.Create();
        hud.gameObject.hideFlags = HideFlags.None;

        Directory.CreateDirectory("Assets/Prefabs");
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(
            hud.gameObject,
            PrefabPath,
            InteractionMode.AutomatedAction);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Game HUD placed in {ScenePath} and saved as prefab {PrefabPath} ({prefab}).");
        Selection.activeGameObject = hud.gameObject;
    }
}
