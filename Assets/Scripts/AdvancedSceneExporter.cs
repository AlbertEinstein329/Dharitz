using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Newtonsoft.Json;

public class AdvancedSceneExporter : EditorWindow
{
    [MenuItem("Tools/Exportar Escena Completa a JSON")]
    public static void ExportSceneToText()
    {
        // 1. Obtener la ruta donde guardar el archivo de texto
        string path = EditorUtility.SaveFilePanel("Guardar Configuración de Escena", Application.dataPath, "Escena_Config", "json");

        if (string.IsNullOrEmpty(path)) return;

        // 2. Obtener los objetos raíz de la escena activa
        GameObject[] rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
        List<Dictionary<string, object>> sceneData = new List<Dictionary<string, object>>();

        foreach (GameObject rootObj in rootObjects)
        {
            sceneData.Add(SerializeGameObject(rootObj));
        }

        // 3. Configurar la serialización para que sea legible y evite bucles infinitos
        JsonSerializerSettings settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        string jsonOutput = JsonConvert.SerializeObject(sceneData, settings);

        // 4. Escribir el archivo de texto
        File.WriteAllText(path, jsonOutput);
        Debug.Log($"[Éxito] Escena exportada correctamente en: {path}");
        AssetDatabase.Refresh();
    }

    private static Dictionary<string, object> SerializeGameObject(GameObject obj)
    {
        Dictionary<string, object> objData = new Dictionary<string, object>();
        objData["Nombre"] = obj.name;
        objData["Activo"] = obj.activeSelf;
        objData["Tag"] = obj.tag;
        objData["Layer"] = LayerMask.LayerToName(obj.layer);

        // Serializar Componentes y sus configuraciones
        List<Dictionary<string, object>> componentsData = new List<Dictionary<string, object>>();
        Component[] components = obj.GetComponents<Component>();

        foreach (Component comp in components)
        {
            if (comp == null) continue; // Ignorar componentes rotos o vacíos

            Dictionary<string, object> compData = new Dictionary<string, object>();
            string compName = comp.GetType().Name;
            compData["TipoComponente"] = compName;

            // Intentar convertir las variables del componente a texto plano JSON
            try
            {
                // Se usa JsonUtility nativo para capturar fielmente las variables serializadas por Unity (p.ej. campos públicos de scripts)
                string compJson = JsonUtility.ToJson(comp);

                if (!string.IsNullOrEmpty(compJson) && compJson != "{}")
                {
                    compData["Configuracion"] = JsonConvert.DeserializeObject<Dictionary<string, object>>(compJson);
                }
                else
                {
                    // Si JsonUtility no lo lee (como en Transform), guardamos datos básicos legibles
                    if (comp is Transform t)
                    {
                        compData["Configuracion"] = new Dictionary<string, float[]> {
                            { "Posicion", new float[] { t.localPosition.x, t.localPosition.y, t.localPosition.z } },
                            { "Rotacion", new float[] { t.localEulerAngles.x, t.localEulerAngles.y, t.localEulerAngles.z } },
                            { "Escala", new float[] { t.localScale.x, t.localScale.y, t.localScale.z } }
                        };
                    }
                    else
                    {
                        compData["Configuracion"] = "Componente sin propiedades públicas serializables";
                    }
                }
            }
            catch (Exception)
            {
                compData["Configuracion"] = "No se pudo serializar este componente";
            }

            componentsData.Add(compData);
        }
        objData["Componentes"] = componentsData;

        // Procesar Hijos (Jerarquía recursiva)
        List<Dictionary<string, object>> childrenData = new List<Dictionary<string, object>>();
        foreach (Transform child in obj.transform)
        {
            childrenData.Add(SerializeGameObject(child.gameObject));
        }
        objData["Hijos"] = childrenData;

        return objData;
    }
}
