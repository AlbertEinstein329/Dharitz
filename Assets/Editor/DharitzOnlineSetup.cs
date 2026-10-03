using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using MyGame.Networking;

/// <summary>
/// Monta en la escena Tablero los objetos que necesita el modo online y alterna el modo de prueba
/// en el SessionConfig. Idempotente: si los objetos ya existen, solo completa los componentes que falten.
/// </summary>
public static class DharitzOnlineSetup
{
    private const string BoardScenePath = "Assets/Scenes/Tablero.unity";
    private const string SessionAssetPath = "Assets/Resources/DatosDeSesionActual.asset";

    [MenuItem("Tools/Dharitz/Online/1. Montar objetos de red en Tablero")]
    public static void SetupBoardScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != BoardScenePath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            scene = EditorSceneManager.OpenScene(BoardScenePath, OpenSceneMode.Single);
        }

        // 1. NetworkManager + transporte UTP
        GameObject managerGo = FindOrCreate("NetworkManager");
        NetworkManager networkManager = GetOrAdd<NetworkManager>(managerGo);
        UnityTransport transport = GetOrAdd<UnityTransport>(managerGo);
        if (networkManager.NetworkConfig == null) networkManager.NetworkConfig = new NetworkConfig();
        networkManager.NetworkConfig.NetworkTransport = transport;
        transport.SetConnectionData("127.0.0.1", 7777);
        EditorUtility.SetDirty(networkManager);
        EditorUtility.SetDirty(transport);

        // 2. Objeto de red de la partida (NetworkObject colocado en escena)
        GameObject gameGo = FindOrCreate("NetworkGame");
        GetOrAdd<NetworkObject>(gameGo);
        GetOrAdd<NetworkGameManager>(gameGo);
        GetOrAdd<NetworkTurnManager>(gameGo);
        GetOrAdd<NetworkDebugLauncher>(gameGo);
        EditorUtility.SetDirty(gameGo);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Selection.activeGameObject = gameGo;
        Debug.Log("[DharitzOnlineSetup] Tablero listo: NetworkManager (UTP 127.0.0.1:7777) y NetworkGame (NetworkObject + NetworkGameManager + NetworkTurnManager + NetworkDebugLauncher). Escena guardada.");
    }

    [MenuItem("Tools/Dharitz/Online/2. Activar modo prueba online (2 jugadores)")]
    public static void EnableOnlineTestMode() => SetOnlineMode(true, 2);

    [MenuItem("Tools/Dharitz/Online/2. Activar modo prueba online (4 jugadores)")]
    public static void EnableOnlineTestMode4() => SetOnlineMode(true, 4);

    [MenuItem("Tools/Dharitz/Online/3. Volver a modo local")]
    public static void DisableOnlineTestMode() => SetOnlineMode(false, 1);

    private static void SetOnlineMode(bool online, int playerCount)
    {
        SessionConfig session = AssetDatabase.LoadAssetAtPath<SessionConfig>(SessionAssetPath);
        if (session == null)
        {
            Debug.LogError($"[DharitzOnlineSetup] No se encontró SessionConfig en {SessionAssetPath}.");
            return;
        }

        Undo.RecordObject(session, "Dharitz online mode");
        session.isOnlineMatch = online;
        session.playerCount = playerCount;
        EditorUtility.SetDirty(session);
        AssetDatabase.SaveAssets();

        Debug.Log(online
            ? $"[DharitzOnlineSetup] Modo online ACTIVO ({playerCount} jugadores). Recuerda volver a modo local para jugar en hotseat."
            : "[DharitzOnlineSetup] Modo local restaurado (isOnlineMatch = false).");
    }

    private static GameObject FindOrCreate(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (root.name == name) return root;
        }

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        return component != null ? component : Undo.AddComponent<T>(go);
    }
}
