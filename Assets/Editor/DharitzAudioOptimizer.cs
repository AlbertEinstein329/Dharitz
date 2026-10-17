using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ajusta la importacion de los clips que usa el juego para Android: la musica de fondo se reproduce en streaming (no se
/// descomprime entera en RAM) y los efectos son mono, Vorbis a calidad media; los cortos se descomprimen al cargar y los
/// largos se quedan comprimidos en memoria. Solo se toca la plataforma Android y el flag de mono.
/// Menu: Tools &gt; Dharitz &gt; Optimizar &gt; Audio (Android).
/// </summary>
public static class DharitzAudioOptimizer
{
    private const float MusicMinSeconds = 20f;     // a partir de aqui es musica / ambiente
    private const float ShortSfxMaxSeconds = 1.5f; // hasta aqui se descomprime al cargar

    [MenuItem("Tools/Dharitz/Optimizar/Audio (Android)")]
    public static void Run() => Debug.Log(Apply());

    public static string Apply()
    {
        var reachable = DharitzPurgeAudit.ReachableAssets();
        int music = 0, sfx = 0;
        long ramBefore = 0, ramAfter = 0;

        foreach (string path in reachable.Where(p => p.StartsWith("Assets/")).OrderBy(p => p))
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (importer == null || clip == null) continue;

            bool isMusic = clip.length >= MusicMinSeconds;
            var settings = new AudioImporterSampleSettings
            {
                compressionFormat = AudioCompressionFormat.Vorbis,
                quality = 0.5f,
                sampleRateSetting = AudioSampleRateSetting.OptimizeSampleRate,
                preloadAudioData = !isMusic, // la musica no se precarga: el streaming la lee al empezar
                loadType = isMusic ? AudioClipLoadType.Streaming
                         : clip.length <= ShortSfxMaxSeconds ? AudioClipLoadType.DecompressOnLoad
                         : AudioClipLoadType.CompressedInMemory
            };

            // RAM estimada si todo se descomprimiera al cargar (antes) frente a lo que queda cargado (despues)
            long pcmBytes = (long)(clip.length * clip.frequency * clip.channels * 2);
            ramBefore += pcmBytes;
            ramAfter += isMusic ? 200 * 1024 : settings.loadType == AudioClipLoadType.DecompressOnLoad ? pcmBytes / clip.channels : pcmBytes / 10;

            importer.forceToMono = !isMusic;                 // los efectos de UI y juego suenan igual en mono
            importer.SetOverrideSampleSettings("Android", settings);
            importer.SaveAndReimport();

            if (isMusic) music++; else sfx++;
        }

        return $"audio ajustado: {music} musica en streaming, {sfx} efectos en mono; RAM estimada {ramBefore / 1048576f:F1} MB -> {ramAfter / 1048576f:F1} MB";
    }
}
