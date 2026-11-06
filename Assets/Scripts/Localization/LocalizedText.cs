using TMPro;
using UnityEngine;

/// <summary>
/// Mantiene un TMP_Text traducido: pone el texto de su clave y lo actualiza al cambiar el idioma.
/// <see cref="AttachToAll"/> lo asocia solo a los textos de escena cuyo contenido original coincide con un alias de la tabla,
/// de modo que las escenas no hay que editarlas.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] private string key;

    private TMP_Text text;

    public string Key => key;

    private void Awake() => text = GetComponent<TMP_Text>();

    private void OnEnable()
    {
        Loc.LanguageChanged += Refresh;
        Refresh();
    }

    private void OnDisable() => Loc.LanguageChanged -= Refresh;

    public void Refresh()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (!string.IsNullOrEmpty(key)) text.text = Loc.T(key);
    }

    public void SetKey(string newKey)
    {
        key = newKey;
        if (isActiveAndEnabled) Refresh();
    }

    /// <summary>Asocia una clave a un texto (lo crea si hace falta) y lo traduce ya.</summary>
    public static LocalizedText Bind(TMP_Text target, string key)
    {
        var localized = target.GetComponent<LocalizedText>();
        if (localized == null) localized = target.gameObject.AddComponent<LocalizedText>();
        localized.SetKey(key);
        return localized;
    }

    /// <summary>Recorre todos los TMP_Text cargados (incluidos los inactivos) y traduce los que coinciden con un alias.</summary>
    public static void AttachToAll()
    {
        foreach (TMP_Text target in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (target.GetComponent<LocalizedText>() != null) continue;

            string aliasKey = Loc.FindKeyByAlias(target.text);
            if (aliasKey != null) Bind(target, aliasKey);
        }
    }
}
