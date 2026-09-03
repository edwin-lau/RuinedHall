using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

public static class PlaceGameHudInScene
{
    const string FlagPath = "Temp/PlaceGameHud.flag";
    const string ScenePath = "Assets/Scenes/HelpOthers.unity";
    const string PrefabPath = "Assets/Prefabs/GameHud.prefab";

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

    [MenuItem("Game/Place Game HUD In Scene")]
    public static void Place()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

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
