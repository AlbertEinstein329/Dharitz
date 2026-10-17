using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Builds del servidor dedicado (Linux para Multiplay, Windows para pruebas locales) y simulacion del servidor
/// dedicado dentro del Editor.
///
/// IMPORTANTE: compilar para Linux/Windows desde un proyecto con Android como plataforma activa obliga a cambiar de
/// plataforma y reimportar los assets (varios minutos). Por eso los builds piden confirmacion y hay que volver a
/// Android a mano cuando se termine (File > Build Profiles).
/// </summary>
public static class DharitzServerBuild
{
    private const string LinuxOutput = "Builds/LinuxServer/DharitzServer.x86_64";
    private const string WindowsOutput = "Builds/WindowsServer/DharitzServer.exe";
    private const string SimulationMenu = "Tools/Dharitz/Server/Simular servidor dedicado en Play (Editor)";

    // Clave compartida con DedicatedServerLauncher.IsDedicatedServer (solo Editor)
    public const string SimulationPrefKey = "Dharitz.DedicatedServerSimulation";

    // -----------------------------------------------------------------
    // Simulacion en el Editor: Play arranca como servidor dedicado (puerto 7777, 2 jugadores)
    // -----------------------------------------------------------------

    [MenuItem(SimulationMenu)]
    private static void ToggleSimulation()
    {
        bool enabled = !EditorPrefs.GetBool(SimulationPrefKey, false);
        EditorPrefs.SetBool(SimulationPrefKey, enabled);
        Debug.Log(enabled
            ? "[DharitzServerBuild] Simulacion de servidor dedicado ACTIVADA: al pulsar Play el Editor arranca como servidor (puerto 7777, 2 jugadores). Recuerda desactivarla para jugar normal."
            : "[DharitzServerBuild] Simulacion de servidor dedicado desactivada.");
    }

    [MenuItem(SimulationMenu, true)]
    private static bool ToggleSimulationValidate()
    {
        Menu.SetChecked(SimulationMenu, EditorPrefs.GetBool(SimulationPrefKey, false));
        return true;
    }

    // -----------------------------------------------------------------
    // Builds
    // -----------------------------------------------------------------

    [MenuItem("Tools/Dharitz/Server/Build Linux Dedicated Server (Multiplay)")]
    public static void BuildLinuxFromMenu()
    {
        if (!ConfirmPlatformSwitch("Linux")) return;
        Build(BuildTarget.StandaloneLinux64, LinuxOutput);
    }

    [MenuItem("Tools/Dharitz/Server/Build Windows Dedicated Server (pruebas locales)")]
    public static void BuildWindowsFromMenu()
    {
        if (!ConfirmPlatformSwitch("Windows")) return;
        Build(BuildTarget.StandaloneWindows64, WindowsOutput);
    }

    /// <summary>Para CI: Unity -batchmode -quit -executeMethod DharitzServerBuild.BuildLinuxServerCli</summary>
    public static void BuildLinuxServerCli()
    {
        EditorApplication.Exit(Build(BuildTarget.StandaloneLinux64, LinuxOutput) ? 0 : 1);
    }

    /// <summary>Para CI: Unity -batchmode -quit -executeMethod DharitzServerBuild.BuildWindowsServerCli</summary>
    public static void BuildWindowsServerCli()
    {
        EditorApplication.Exit(Build(BuildTarget.StandaloneWindows64, WindowsOutput) ? 0 : 1);
    }

    private static bool ConfirmPlatformSwitch(string platform)
    {
        return EditorUtility.DisplayDialog(
            $"Build de servidor dedicado ({platform})",
            $"Se compilara el servidor para {platform}. Si la plataforma activa es otra (p. ej. Android) Unity cambiara de plataforma y reimportara los assets, " +
            "lo que puede tardar varios minutos. Despues tendras que volver a Android en File > Build Profiles.\n\n¿Continuar?",
            "Compilar", "Cancelar");
    }

    private static bool Build(BuildTarget target, string relativeOutput)
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        if (scenes.Length == 0)
        {
            Debug.LogError("[DharitzServerBuild] No hay escenas habilitadas en Build Settings.");
            return false;
        }

        // Mono evita exigir la cadena de IL2CPP en el servidor; es suficiente para la logica de turnos
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Server, ScriptingImplementation.Mono2x);

        string output = Path.GetFullPath(relativeOutput);
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = target,
            subtarget = (int)StandaloneBuildSubtarget.Server,
            options = BuildOptions.None
        };

        Debug.Log($"[DharitzServerBuild] Compilando servidor {target} -> {output} ({scenes.Length} escenas)...");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[DharitzServerBuild] OK: {summary.totalSize / (1024 * 1024)} MB en {summary.totalTime.TotalSeconds:F0} s. " +
                      $"Ejecutar: {Path.GetFileName(output)} -batchmode -nographics -port 7777 -queryport 7778 -maxPlayers 2");
            return true;
        }

        Debug.LogError($"[DharitzServerBuild] Build fallido: {summary.result} ({summary.totalErrors} errores).");
        return false;
    }
}
