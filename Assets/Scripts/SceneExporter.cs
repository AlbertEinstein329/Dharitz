using System.IO;
using UnityEngine;
using UnityEditor;

public class SceneExporter : MonoBehaviour
{
    [MenuItem("Tools/Export Hierarchy to Text")]
    public static void ExportHierarchyToText()
    {
        string path = Application.dataPath + "/SceneHierarchy.txt";
        using (StreamWriter writer = new StreamWriter(path))
        {
            // Obtiene todos los objetos en la escena actual
            GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();

            foreach (GameObject obj in rootObjects)
            {
                WriteObjectDetails(obj, writer, "");
            }
        }

        Debug.Log("¡Escena exportada a: " + path);
        AssetDatabase.Refresh();
    }

    private static void WriteObjectDetails(GameObject obj, StreamWriter writer, string indent)
    {
        // Escribe el nombre del objeto y su posición en el texto
        writer.WriteLine(indent + "Objeto: " + obj.name + " | Posición: " + obj.transform.position);

        // Escribe los componentes adjuntos
        Component[] components = obj.GetComponents<Component>();
        foreach (Component comp in components)
        {
            if (comp != null)
                writer.WriteLine(indent + "  - Componente: " + comp.GetType().Name);
        }

        // Llama a la función recursivamente para hijos
        foreach (Transform child in obj.transform)
        {
            WriteObjectDetails(child.gameObject, writer, indent + "  ");
        }
    }
}
