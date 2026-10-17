using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Ajusta la importacion de las texturas que usa el juego para movil: compresion ASTC en Android (en vez de RGBA32 sin
/// comprimir), sin mipmaps ni lectura/escritura, y un tamano maximo acorde a lo que se ve en pantalla. Solo toca la
/// plataforma Android y los metas de las texturas del juego; el resto de plataformas queda como estaba.
/// Menu: Tools &gt; Dharitz &gt; Optimizar &gt; Texturas (Android).
/// </summary>
public static class DharitzTextureOptimizer
{
    [MenuItem("Tools/Dharitz/Optimizar/Texturas (Android)")]
    public static void Run() => Debug.Log(Apply());

    public static string Apply()
    {
        HashSet<string> reachable = DharitzPurgeAudit.ReachableAssets();
        int changed = 0, skipped = 0;
        long before = 0, after = 0;

        foreach (string path in reachable.Where(p => p.StartsWith("Assets/") && !p.StartsWith("Assets/TextMesh Pro/")).OrderBy(p => p))
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || importer.textureType == TextureImporterType.Cursor) { skipped++; continue; }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) { skipped++; continue; }
            int largest = Mathf.Max(texture.width, texture.height);

            before += (long)texture.width * texture.height * 4;

            // Tamano maximo segun el uso en pantalla
            int maxSize = largest;
            if (path.StartsWith("Assets/Dice/")) maxSize = 256;                     // dados: ~150 px en pantalla
            else if (path.Contains("/III. Wooden Character/")) maxSize = 256;       // avatares
            else if (largest > 1024) maxSize = 1024;

            // Formato: ASTC 4x4 para sprites pequenos o con bordes 9-slice; 6x6 para lo grande (fondos, personajes)
            bool nineSlice = importer.spriteBorder != Vector4.zero;
            var format = (nineSlice || Mathf.Min(texture.width, texture.height) <= 256 || maxSize <= 256) ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;

            bool dirty = false;
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            if (importer.isReadable) { importer.isReadable = false; dirty = true; }
            if (importer.crunchedCompression) { importer.crunchedCompression = false; dirty = true; }

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.format != format || android.maxTextureSize != maxSize)
            {
                android.name = "Android";
                android.overridden = true;
                android.maxTextureSize = Mathf.Min(maxSize, 2048);
                android.format = format;
                android.textureCompression = TextureImporterCompression.Compressed;
                android.compressionQuality = 50;
                importer.SetPlatformTextureSettings(android);
                dirty = true;
            }

            int effective = Mathf.Min(largest, maxSize);
            float ratio = (float)effective / largest;
            after += (long)(texture.width * ratio * texture.height * ratio * (format == TextureImporterFormat.ASTC_4x4 ? 1f : 0.445f));

            if (dirty)
            {
                importer.SaveAndReimport();
                changed++;
            }
        }

        return $"texturas ajustadas: {changed} (omitidas {skipped}); memoria estimada en Android: {before / 1048576f:F1} MB (RGBA32) -> {after / 1048576f:F1} MB (ASTC)";
    }
}
