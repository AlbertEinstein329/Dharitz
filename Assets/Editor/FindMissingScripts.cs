using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class FindMissingScripts
{
    [MenuItem("Tools/Find Missing Scripts in Scene")]
    public static void FindInScene()
    {
        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        int total = 0;
        foreach (var root in roots) total += CheckGameObjectRecursive(root);
        EditorUtility.DisplayDialog("Find Missing Scripts", $"Escaneo de escena completado. Objetos con scripts faltantes: {total}", "OK");
    }

    [MenuItem("Tools/Find Missing Scripts in Project Prefabs")]
    public static void FindInProjectPrefabs()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab");
        int total = 0;
        foreach (var g in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(g);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            var comps = prefab.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < comps.Length; i++)
            {
                if (comps[i] == null)
                {
                    Debug.LogWarning($"Prefab con componente faltante: {path}", prefab);
                    total++;
                    break;
                }
            }
        }
        EditorUtility.DisplayDialog("Find Missing Scripts", $"Escaneo de prefabs completado. Prefabs con scripts faltantes: {total}", "OK");
    }

    static int CheckGameObjectRecursive(GameObject go)
    {
        int found = 0;
        var comps = go.GetComponents<Component>();
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null)
            {
                Debug.LogWarning($"GameObject con componente faltante: {GetGameObjectPath(go)}", go);
                found++;
            }
        }
        foreach (Transform child in go.transform)
        {
            found += CheckGameObjectRecursive(child.gameObject);
        }
        return found;
    }

    static string GetGameObjectPath(GameObject go)
    {
        string path = go.name;
        Transform t = go.transform.parent;
        while (t != null)
        {
            path = t.name + "/" + path;
            t = t.parent;
        }
        return path;
    }
}