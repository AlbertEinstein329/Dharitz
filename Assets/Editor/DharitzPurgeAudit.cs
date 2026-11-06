using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Auditoria de la purga: calcula que assets alcanzan las escenas del build, Resources, Prefabs, Settings y los ajustes del
/// proyecto, y cuanto pesa lo que no usa nadie por carpeta. Solo lee: no borra nada. El informe se escribe en un fichero.
/// </summary>
public static class DharitzPurgeAudit
{
    private static readonly string[] ExtraRootFolders = { "Assets/Resources", "Assets/Prefabs", "Assets/Audio", "Assets/Settings", "Assets/Scenes", "Assets/ML-Config", "Assets/TextMesh Pro/Resources" };

    public static HashSet<string> ReachableAssets()
    {
        var roots = new List<string>();
        roots.AddRange(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path));
        foreach (string guid in AssetDatabase.FindAssets("", ExtraRootFolders.Where(Directory.Exists).ToArray()))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!AssetDatabase.IsValidFolder(path)) roots.Add(path);
        }
        foreach (string path in new[] { "Assets/UniversalRenderPipelineGlobalSettings.asset" }) if (File.Exists(path)) roots.Add(path);
        // Assets precargados y de graficos: cuelgan de ProjectSettings
        foreach (Object preloaded in PlayerSettings.GetPreloadedAssets()) if (preloaded != null) roots.Add(AssetDatabase.GetAssetPath(preloaded));
        var rp = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
        if (rp != null) roots.Add(AssetDatabase.GetAssetPath(rp));

        var reachable = new HashSet<string>(AssetDatabase.GetDependencies(roots.Where(p => !string.IsNullOrEmpty(p)).ToArray(), true));
        return reachable;
    }

    /// <summary>Borra los assets de una carpeta que no alcanza ningun root (no toca codigo ni lo que si se usa). Devuelve un resumen.</summary>
    public static string DeleteUnused(string folder)
    {
        HashSet<string> reachable = ReachableAssets();
        int deleted = 0, kept = 0;
        long freed = 0;
        foreach (string guid in AssetDatabase.FindAssets("", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path) || !File.Exists(path)) continue;
            if (path.EndsWith(".cs") || path.EndsWith(".asmdef") || path.EndsWith(".dll")) { kept++; continue; }
            if (reachable.Contains(path)) { kept++; continue; }
            long size = new FileInfo(path).Length;
            if (AssetDatabase.DeleteAsset(path)) { deleted++; freed += size; }
        }

        // Carpetas que quedaron vacias
        foreach (string dir in Directory.GetDirectories(folder, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(dir).Any(e => !e.EndsWith(".meta"))) AssetDatabase.DeleteAsset(dir.Replace('\\', '/'));
        }
        return $"{folder}: {deleted} borrados ({freed / 1048576f:F1} MB), {kept} conservados";
    }

    public static string Report(string reportPath)
    {
        HashSet<string> reachable = ReachableAssets();
        var byFolder = new Dictionary<string, (long total, long unused, int unusedCount)>();
        var unusedByDir = new Dictionary<string, long>();

        foreach (string guid in AssetDatabase.FindAssets("", new[] { "Assets" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path) || !File.Exists(path)) continue;
            if (path.EndsWith(".cs") || path.EndsWith(".asmdef") || path.StartsWith("Assets/Editor")) continue; // el codigo no se purga aqui
            long size = new FileInfo(path).Length;
            string top = path.Split('/').Length > 2 ? string.Join("/", path.Split('/').Take(2)) : path;
            byFolder.TryGetValue(top, out var entry);
            bool used = reachable.Contains(path);
            entry.total += size;
            if (!used) { entry.unused += size; entry.unusedCount++; }
            byFolder[top] = entry;

            if (!used)
            {
                string dir = string.Join("/", path.Split('/').Take(4).Take(path.Split('/').Length - 1));
                unusedByDir.TryGetValue(dir, out long acc);
                unusedByDir[dir] = acc + size;
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("CARPETA | total MB | sin usar MB | ficheros sin usar");
        foreach (var kv in byFolder.OrderByDescending(k => k.Value.total))
            sb.AppendLine($"{kv.Key} | {kv.Value.total / 1048576f:F1} | {kv.Value.unused / 1048576f:F1} | {kv.Value.unusedCount}");
        sb.AppendLine();
        sb.AppendLine("SUBCARPETAS CON MAS PESO SIN USAR");
        foreach (var kv in unusedByDir.OrderByDescending(k => k.Value).Take(60))
            sb.AppendLine($"{kv.Value / 1048576f:F1} MB  {kv.Key}");
        File.WriteAllText(reportPath, sb.ToString());
        return $"{byFolder.Count} carpetas; {reachable.Count} assets alcanzables. Informe en {reportPath}";
    }
}
