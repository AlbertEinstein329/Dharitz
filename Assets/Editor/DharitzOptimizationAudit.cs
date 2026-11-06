using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Informe de optimizacion para movil: ajustes de render, calidad, texturas, audio y build de Android. Solo lee.
/// Se limita a los assets que alcanzan el juego (los mismos que usa la purga).
/// </summary>
public static class DharitzOptimizationAudit
{
    public static string Run(string reportPath)
    {
        var reachable = DharitzPurgeAudit.ReachableAssets();
        var sb = new StringBuilder();

        // ---- Render
        var rp = GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        sb.AppendLine("== RENDER (URP) ==");
        if (rp != null)
        {
            sb.AppendLine($"asset={rp.name} HDR={rp.supportsHDR} MSAA={rp.msaaSampleCount} renderScale={rp.renderScale} depthTex={rp.supportsCameraDepthTexture} opaqueTex={rp.supportsCameraOpaqueTexture} shadows(maxDist)={rp.shadowDistance} additionalLights={rp.additionalLightsRenderingMode} SRPBatcher={rp.useSRPBatcher}");
        }
        sb.AppendLine($"quality levels={QualitySettings.names.Length} current={QualitySettings.names[QualitySettings.GetQualityLevel()]} vSync={QualitySettings.vSyncCount} targetFPS={Application.targetFrameRate}");

        // ---- Texturas
        sb.AppendLine();
        sb.AppendLine("== TEXTURAS alcanzables por el juego (ruta | WxH | mips | read/write | maxSize | compresion Android | tipo) ==");
        long totalBytes = 0;
        int count = 0, withMips = 0, readable = 0, noAndroidOverride = 0;
        foreach (string path in reachable.Where(p => p.StartsWith("Assets/")).OrderBy(p => p))
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            count++;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            long size = File.Exists(path) ? new FileInfo(path).Length : 0;
            totalBytes += size;
            var android = importer.GetPlatformTextureSettings("Android");
            if (importer.mipmapEnabled) withMips++;
            if (importer.isReadable) readable++;
            if (!android.overridden) noAndroidOverride++;
            sb.AppendLine($"{path} | {(tex ? tex.width + "x" + tex.height : "?")} | {(importer.mipmapEnabled ? "mips" : "-")} | {(importer.isReadable ? "RW" : "-")} | {importer.maxTextureSize} | {(android.overridden ? android.format.ToString() : "auto")} | {importer.textureType} | {size / 1024} KB");
        }
        sb.AppendLine($"TOTAL {count} texturas, {totalBytes / 1048576f:F1} MB en disco; con mipmaps={withMips}; legibles(R/W)={readable}; sin ajuste Android={noAndroidOverride}");

        // ---- Audio
        sb.AppendLine();
        sb.AppendLine("== AUDIO alcanzable (ruta | segundos | canales | carga | compresion | calidad | mono forzado) ==");
        long audioBytes = 0;
        foreach (string path in reachable.Where(p => p.StartsWith("Assets/")).OrderBy(p => p))
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            var settings = importer.defaultSampleSettings;
            var android = importer.GetOverrideSampleSettings("Android");
            long size = new FileInfo(path).Length;
            audioBytes += size;
            sb.AppendLine($"{path} | {(clip ? clip.length.ToString("F1") : "?")}s | {(clip ? clip.channels : 0)}ch | {settings.loadType} | {settings.compressionFormat} | q={settings.quality:F2} | forceMono={importer.forceToMono} | androidOverride={importer.ContainsSampleSettingsOverride("Android")} | {size / 1024} KB");
        }
        sb.AppendLine($"TOTAL audio {audioBytes / 1048576f:F1} MB");

        // ---- Build Android
        var android2 = UnityEditor.Build.NamedBuildTarget.Android;
        sb.AppendLine();
        sb.AppendLine("== BUILD ANDROID ==");
        sb.AppendLine($"backend={PlayerSettings.GetScriptingBackend(android2)} arch={PlayerSettings.Android.targetArchitectures} stripping={PlayerSettings.GetManagedStrippingLevel(android2)} minSdk={PlayerSettings.Android.minSdkVersion} targetSdk={PlayerSettings.Android.targetSdkVersion} aab={EditorUserBuildSettings.buildAppBundle}");
        sb.AppendLine($"graphicsAPIs={string.Join("/", PlayerSettings.GetGraphicsAPIs(BuildTarget.Android))} auto={PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.Android)} gcIncremental={PlayerSettings.gcIncremental} stripEngineCode={PlayerSettings.stripEngineCode} compression={EditorUserBuildSettings.androidBuildSubtarget} textureCompression={EditorUserBuildSettings.androidBuildSubtarget}");
        sb.AppendLine($"defaultOrientation={PlayerSettings.defaultInterfaceOrientation} accelerometerFreq={PlayerSettings.accelerometerFrequency} splash={PlayerSettings.SplashScreen.show} muteOtherAudio={PlayerSettings.muteOtherAudioSources}");

        File.WriteAllText(reportPath, sb.ToString());
        return $"informe en {reportPath}: {count} texturas ({totalBytes / 1048576f:F1} MB), audio {audioBytes / 1048576f:F1} MB";
    }
}
