using UnityEditor;
using UnityEngine;
using System.IO;

public static class FindRenamedScripts
{
    [MenuItem("Tools/Find Renamed Scripts in Project")]
    public static void FindRenamedScriptsInProject()
    {
        string[] guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" });
        int issues = 0;

        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var mono = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            if (mono == null) continue;

            var cls = mono.GetClass();
            string fileName = Path.GetFileNameWithoutExtension(path);

            if (cls == null)
            {
                Debug.LogWarning($"MonoScript sin clase asociada o compilación pendiente: {path}", mono);
                issues++;
            }
            else if (cls.Name != fileName)
            {
                Debug.LogWarning($"Posible script renombrado: archivo '{fileName}.cs' pero la clase es '{cls.Name}' -> {path}", mono);
                issues++;
            }
        }

        EditorUtility.DisplayDialog("Find Renamed Scripts", $"Escaneo completado. Issues encontrados: {issues}", "OK");
    }
}