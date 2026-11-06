using UnityEngine;

/// <summary>
/// En el juego publicado solo se escriben avisos y errores en el log del dispositivo (logcat): los mensajes informativos
/// llevan identificadores de jugador, codigos de sala y detalles de la red que no deben quedar a la vista.
/// En el Editor y en builds de desarrollo no cambia nada.
/// </summary>
public static class ReleaseLogging
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Configure()
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        Debug.unityLogger.filterLogType = LogType.Warning;
#endif
    }
}
