using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public static class RealismAssetSetup
{
    [MenuItem("Tools/老板来了/⑤ 配置可选下载人物",false,50)]
    public static void ConfigureOptionalCharacter()
    {
        ConfigureImports();
        ConfigureBlackBack();
    }

    static string[] FindAssets(string filter,string folder)
    {
        return AssetDatabase.IsValidFolder(folder) ? AssetDatabase.FindAssets(filter,new[]{folder}) : new string[0];
    }

    // Separate the vest back's existing UV island, preserving the original texture and skinning.
    public static void ConfigureBlackBack()
    {
        const string folder = "Assets/Resources/Realism/Renderpeople/";
        var source = AssetDatabase.LoadAllAssetsAtPath(folder + "Eric.fbx").OfType<Mesh>().FirstOrDefault();
        if(source==null)
        {
            Debug.Log("Optional Eric.fbx is not installed. The original built-in boss is ready to play.");
            return;
        }
        var mesh = Object.Instantiate(source);
        mesh.name = "EricBlackBack";
        var uv = mesh.uv;
        var original = mesh.triangles;
        var body = new List<int>();
        var back = new List<int>();
        for (int i = 0; i < original.Length; i += 3)
        {
            Vector2 center = (uv[original[i]] + uv[original[i + 1]] + uv[original[i + 2]]) / 3f;
            bool vestBack = center.x > .70f && center.x < .91f && center.y > .65f && center.y < .98f;
            var target = vestBack ? back : body;
            target.Add(original[i]); target.Add(original[i + 1]); target.Add(original[i + 2]);
        }
        if (back.Count == 0) throw new System.InvalidOperationException("Eric vest back UV island was not found.");
        mesh.subMeshCount = 2;
        mesh.SetTriangles(body, 0); mesh.SetTriangles(back, 1);
        string path = folder + "EricBlackBack.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); }
        else AssetDatabase.CreateAsset(mesh, path);
        AssetDatabase.SaveAssets();
        Debug.Log("Eric black vest back: " + back.Count / 3 + " triangles; original UVs, normals and skin weights preserved.");
    }

    public static void ConfigureImports()
    {
        foreach (string guid in FindAssets("t:Texture2D", "Assets/Resources/Realism/Textures"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            bool normal = path.Contains("_Normal");
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normal;
            importer.mipmapEnabled = true; importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        foreach(string guid in FindAssets("t:Texture2D","Assets/Resources/Realism/Renderpeople"))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            bool normal=path.Contains("_norm.");
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!normal;importer.maxTextureSize=normal?2048:4096;
            importer.mipmapEnabled=true;importer.anisoLevel=8;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        foreach (string guid in FindAssets("t:Model", "Assets/Resources/Realism"))
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
            if (importer == null) continue;
            importer.importAnimation = false;
            importer.importBlendShapes = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.SaveAndReimport();
        }
    }
}
