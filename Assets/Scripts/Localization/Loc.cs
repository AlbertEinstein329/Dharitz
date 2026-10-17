using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Localizacion minima (espanol / ingles) sobre Resources/Localization/Strings.csv con columnas key, es, en, aliases.
/// El idioma sale de <see cref="SettingsService.LanguageIndex"/> (0 = es, 1 = en; en la primera ejecucion, el del dispositivo),
/// asi que cambiarlo desde cualquier pantalla actualiza todo. Los textos de las escenas no se tocan: al cargar cada escena
/// <see cref="LocalizedText.AttachToAll"/> reconoce los textos originales por sus «aliases» y les asocia su clave.
/// </summary>
public static class Loc
{
    public static readonly string[] Codes = { "es", "en" };

    private const string TableResource = "Localization/Strings";

    private static Dictionary<string, string[]> table;          // clave -> textos por idioma
    private static Dictionary<string, string> keyByAlias;       // texto original (en minusculas) -> clave
    private static bool subscribed;

    /// <summary>Se dispara cuando cambia el idioma.</summary>
    public static event Action LanguageChanged;

    private static int lastLanguage = -1;

    public static int CurrentLanguage => Mathf.Clamp(SettingsService.LanguageIndex, 0, Codes.Length - 1);

    /// <summary>Texto de la clave en el idioma actual; si falta, el de otro idioma o la propia clave. Admite string.Format.</summary>
    public static string T(string key, params object[] args)
    {
        EnsureLoaded();

        string text = key;
        if (table.TryGetValue(key, out string[] values))
        {
            text = values[CurrentLanguage];
            if (string.IsNullOrEmpty(text)) text = values[0];
            if (string.IsNullOrEmpty(text)) text = key;
        }
        else
        {
            Debug.LogWarning($"[Loc] Falta la clave '{key}' en {TableResource}.csv");
        }

        if (args != null && args.Length > 0)
        {
            try { text = string.Format(text, args); }
            catch (FormatException) { Debug.LogWarning($"[Loc] Formato invalido en '{key}': {text}"); }
        }
        return text;
    }

    /// <summary>Clave asociada a un texto original de escena (sin distinguir mayusculas), o null.</summary>
    public static string FindKeyByAlias(string originalText)
    {
        EnsureLoaded();
        if (string.IsNullOrWhiteSpace(originalText)) return null;
        return keyByAlias.TryGetValue(originalText.Trim().ToLowerInvariant(), out string key) ? key : null;
    }

    private static void EnsureLoaded()
    {
        if (table != null) return;

        table = new Dictionary<string, string[]>();
        keyByAlias = new Dictionary<string, string>();

        var asset = Resources.Load<TextAsset>(TableResource);
        if (asset == null)
        {
            Debug.LogError($"[Loc] No existe Resources/{TableResource}.csv");
            return;
        }

        List<List<string>> rows = ParseCsv(asset.text);
        for (int r = 1; r < rows.Count; r++) // la fila 0 es la cabecera
        {
            List<string> row = rows[r];
            if (row.Count < 3 || string.IsNullOrWhiteSpace(row[0]) || row[0].StartsWith("#")) continue;

            string key = row[0].Trim();
            table[key] = new[] { Unescape(row[1]), Unescape(row[2]) };

            if (row.Count > 3 && !string.IsNullOrWhiteSpace(row[3]))
            {
                foreach (string alias in row[3].Split('|'))
                {
                    string normalized = alias.Trim().ToLowerInvariant();
                    if (normalized.Length > 0) keyByAlias[normalized] = key;
                }
            }
        }
    }

    private static string Unescape(string value) => value.Replace("\\n", "\n");

    // CSV con campos entre comillas (y "" como comilla escapada)
    private static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = false;
                }
                else field.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',') { row.Add(field.ToString()); field.Length = 0; }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(field.ToString());
                field.Length = 0;
                if (row.Count > 1 || row[0].Length > 0) rows.Add(row);
                row = new List<string>();
            }
            else field.Append(c);
        }

        if (field.Length > 0 || row.Count > 0)
        {
            row.Add(field.ToString());
            rows.Add(row);
        }
        return rows;
    }

    // ------------------------------------------------------------------ Arranque

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        table = null; // recarga limpia al entrar en Play sin recargar el dominio
        keyByAlias = null;
        lastLanguage = -1;

        if (subscribed) return;
        subscribed = true;

        var runner = new GameObject("[Loc]", typeof(LocRunner));
        UnityEngine.Object.DontDestroyOnLoad(runner);
    }

    private class LocRunner : MonoBehaviour
    {
        private void OnEnable()
        {
            SettingsService.Changed += OnSettingsChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SettingsService.Changed -= OnSettingsChanged;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void Start() => StartCoroutine(AttachNextFrame());

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartCoroutine(AttachNextFrame());

        // Un fotograma despues, para incluir tambien los textos que las escenas crean en su Start
        private IEnumerator AttachNextFrame()
        {
            yield return new WaitForEndOfFrame();
            lastLanguage = CurrentLanguage;
            LocalizedText.AttachToAll();
        }

        private void OnSettingsChanged()
        {
            if (lastLanguage == CurrentLanguage) return;
            lastLanguage = CurrentLanguage;
            LanguageChanged?.Invoke();
        }
    }
}
