using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class SidekickLooks
{
    const string HumanMatPath =
        "Assets/Synty/SidekickCharacters/Characters/HumanSpecies/HumanSpecies_01/Materials/HumanSpecies_01.mat";
    const string KnightMatPath =
        "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_01/Materials/Starter_01.mat";
    const string ScifiMatPath =
        "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_02/Materials/Starter_02.mat";
    const string HorrorMatPath =
        "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_04/Materials/Starter_04.mat";

    const string KnightPrefabPath =
        "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_01/Starter_01.prefab";
    const string ScifiPrefabPath =
        "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_02/Starter_02.prefab";
    const string MixPrefabPath =
        "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_03/Starter_03.prefab";
    const string HorrorPrefabPath =
        "Assets/Synty/SidekickCharacters/Characters/Starter/Starter_04/Starter_04.prefab";

    public static Material Human { get; private set; }
    public static Material Knight { get; private set; }
    public static Material Scifi { get; private set; }
    public static Material Horror { get; private set; }
    public static GameObject KnightExample { get; private set; }
    public static GameObject ScifiExample { get; private set; }
    public static GameObject MixExample { get; private set; }
    public static GameObject HorrorExample { get; private set; }

    public static void Ensure()
    {
        Human = LoadMat(HumanMatPath);
        Knight = LoadMat(KnightMatPath);
        Scifi = LoadMat(ScifiMatPath);
        Horror = LoadMat(HorrorMatPath);
        KnightExample = LoadPrefab(KnightPrefabPath);
        ScifiExample = LoadPrefab(ScifiPrefabPath);
        MixExample = LoadPrefab(MixPrefabPath);
        HorrorExample = LoadPrefab(HorrorPrefabPath);
    }

    static Material LoadMat(string path)
    {
#if UNITY_EDITOR
        Material editorMat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (editorMat != null)
            return editorMat;
#endif
        return Resources.Load<Material>("Sidekick/Looks/" + System.IO.Path.GetFileNameWithoutExtension(path));
    }

    static GameObject LoadPrefab(string path)
    {
#if UNITY_EDITOR
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab != null)
            return prefab;
#endif
        return Resources.Load<GameObject>("Sidekick/Looks/" + System.IO.Path.GetFileNameWithoutExtension(path));
    }
}
