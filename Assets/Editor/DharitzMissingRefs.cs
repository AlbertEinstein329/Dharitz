using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Busca referencias rotas (un campo que apunta a un asset que ya no existe) en las escenas del build y en los prefabs de
/// Prefabs y Resources. Sirve de red de seguridad tras borrar assets.
/// </summary>
public static class DharitzMissingRefs
{
    public static string Run()
    {
        var found = new List<string>();

        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled) continue;
            EditorSceneManager.OpenScene(scene.path);
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Scan(go, scene.path, found);
        }

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs", "Assets/Resources" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (Transform t in prefab.GetComponentsInChildren<Transform>(true)) Scan(t.gameObject, path, found);
        }

        var sb = new StringBuilder();
        sb.AppendLine($"referencias rotas: {found.Count}");
        for (int i = 0; i < Mathf.Min(found.Count, 40); i++) sb.AppendLine(found[i]);
        return sb.ToString();
    }

    private static void Scan(GameObject go, string container, List<string> found)
    {
        foreach (Component component in go.GetComponents<Component>())
        {
            if (component == null) continue;
            var so = new SerializedObject(component);
            SerializedProperty it = so.GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (it.objectReferenceValue == null && it.objectReferenceInstanceIDValue != 0)
                    found.Add($"{container} :: {go.name} / {component.GetType().Name}.{it.propertyPath}");
            }
        }
    }
}
