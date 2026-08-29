using System.IO;
using UnityEditor;
using UnityEngine;

public class EnvironomentPlantAxisFix : AssetPostprocessor
{
    static readonly string[] PlantNames =
    {
        "Grass",
        "Plant",
        "Daisy",
        "Bell_flower",
        "Anthurium",
        "Rose"
    };

    void OnPreprocessModel()
    {
        if (!IsTargetPlant(assetPath))
            return;

        var importer = (ModelImporter)assetImporter;
        importer.isReadable = true;
        importer.meshCompression = ModelImporterMeshCompression.Off;
    }

    void OnPostprocessModel(GameObject gameObject)
    {
        if (!IsTargetPlant(assetPath))
            return;

        Quaternion rot = Quaternion.Euler(-90f, 0f, 0f);
        foreach (MeshFilter filter in gameObject.GetComponentsInChildren<MeshFilter>(true))
            BakeRotation(filter.sharedMesh, rot);
        foreach (SkinnedMeshRenderer renderer in gameObject.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            BakeRotation(renderer.sharedMesh, rot);
    }

    static bool IsTargetPlant(string path)
    {
        string normalized = path.Replace('\\', '/');
        if (!normalized.Contains("/Environoment/"))
            return false;

        string name = Path.GetFileNameWithoutExtension(normalized);
        for (int i = 0; i < PlantNames.Length; i++)
        {
            if (name == PlantNames[i])
                return true;
        }

        return false;
    }

    static void BakeRotation(Mesh mesh, Quaternion rotation)
    {
        if (mesh == null)
            return;

        Vector3[] vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = rotation * vertices[i];
        mesh.vertices = vertices;

        Vector3[] normals = mesh.normals;
        if (normals != null && normals.Length == vertices.Length)
        {
            for (int i = 0; i < normals.Length; i++)
                normals[i] = rotation * normals[i];
            mesh.normals = normals;
        }

        Vector4[] tangents = mesh.tangents;
        if (tangents != null && tangents.Length == vertices.Length)
        {
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 tangent = rotation * new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w);
            }
            mesh.tangents = tangents;
        }

        mesh.RecalculateBounds();
    }
}
