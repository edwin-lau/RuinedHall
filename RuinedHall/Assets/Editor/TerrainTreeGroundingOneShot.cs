using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class TerrainTreeGroundingOneShot
{
    const string TreeFolder = "Assets/Resources/Terrain/tree";
    const string SessionKey = "RuinedHall.TerrainTreeGroundingOneShot.v5";

    static TerrainTreeGroundingOneShot()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/Terrain/Fix Tree Grounding")]
    static void Run()
    {
        try
        {
            var fixedTrees = new GameObject[4];
            for (var i = 0; i < fixedTrees.Length; i++)
            {
                var number = i + 1;
                var fixedPath = $"{TreeFolder}/Tree{number}_Brush.fbx";
                fixedTrees[i] = AssetDatabase.LoadAssetAtPath<GameObject>(fixedPath);
                if (fixedTrees[i] == null)
                {
                    Debug.LogWarning($"[TerrainTreeGrounding] Waiting for import: {fixedPath}");
                    EditorApplication.delayCall += Run;
                    return;
                }
            }

            var updated = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:TerrainData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(path);
                if (terrainData == null)
                    continue;

                var prototypes = terrainData.treePrototypes;
                var changed = false;
                for (var i = 0; i < prototypes.Length; i++)
                {
                    var prototype = prototypes[i];
                    if (prototype.prefab == null)
                        continue;

                    var prototypePath = AssetDatabase.GetAssetPath(prototype.prefab);
                    for (var treeIndex = 0; treeIndex < fixedTrees.Length; treeIndex++)
                    {
                        var number = treeIndex + 1;
                        var treePrefix = $"{TreeFolder}/Tree{number}";
                        var brushPath = $"{treePrefix}_Brush.fbx";
                        if (!prototypePath.StartsWith(treePrefix, StringComparison.OrdinalIgnoreCase) ||
                            prototypePath.Equals(brushPath, StringComparison.OrdinalIgnoreCase))
                            continue;

                        prototype.prefab = fixedTrees[treeIndex];
                        prototypes[i] = prototype;
                        changed = true;
                        updated++;
                        break;
                    }
                }

                if (!changed)
                    continue;

                terrainData.treePrototypes = prototypes;
                EditorUtility.SetDirty(terrainData);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[TerrainTreeGrounding] COMPLETE bakedBrushFBX=4 updated={updated}");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            Debug.Log("[TerrainTreeGrounding] FAILED");
        }
    }
}
