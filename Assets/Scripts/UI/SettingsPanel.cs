using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de ajustes unico (menu principal y pausa). Lee y escribe solo en <see cref="SettingsService"/>:
/// volumen y silencio de musica y efectos, idioma. Se abre con <see cref="Open"/> sobre su propio canvas,
/// asi funciona igual desde cualquier escena. El prefab lo arma Tools > Dharitz > UI > Construir kit de UI online.
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("Sonido")]
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Toggle musicToggle;   // ON = suena
    [SerializeField] private Slider sfxSlider;
    [SerializeField] private Toggle sfxToggle;     // ON = suena

    [Header("Idioma")]
    [SerializeField] private Button languageSpanish;
    [SerializeField] private Button languageEnglish;

    [Header("Cierre")]
    [SerializeField] private Button closeButton;

    private static SettingsPanel current;
    private Action onClosed;
    private GameObject ownedCanvas; // canvas de respaldo (solo si la escena no tiene UiRoot)
    private bool refreshing;

    public static bool IsOpen => current != null;

    /// <summary>Cierra el panel si esta abierto (boton Atras de Android).</summary>
    public static void CloseCurrent()
    {
        if (current != null) current.Close();
    }

    /// <summary>Abre el panel de ajustes. Si ya hay uno abierto no crea otro.</summary>
    public static SettingsPanel Open(Action onClosed = null)
    {
        if (current != null) return current;

        OnlineUiKit kit = OnlineUiKit.Instance;
        if (kit == null || kit.settingsPanel == null) return null;

        // Cuelga del canvas unico de la escena (capa Overlays); sin UiRoot crea un canvas propio
        RectTransform overlayParent = UiRoot.GetOverlayParent("[SettingsCanvas]", 500);

        var container = new GameObject("SettingsOverlay", typeof(RectTransform));
        container.transform.SetParent(overlayParent, false);
        var containerRect = (RectTransform)container.transform;
        containerRect.anchorMin = Vector2.zero;
        containerRect.anchorMax = Vector2.one;
        containerRect.offsetMin = containerRect.offsetMax = Vector2.zero;

        // Fondo que oscurece y bloquea lo que hay detras
        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        backdrop.transform.SetParent(container.transform, false);
        var backdropRect = (RectTransform)backdrop.transform;
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;
        backdrop.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.6f);

        SettingsPanel panel = Instantiate(kit.settingsPanel, container.transform, false);
        panel.onClosed = onClosed;
        panel.ownedCanvas = UiRoot.Instance == null ? overlayParent.gameObject : null;
        current = panel;

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        UiButtonSfx.AttachToAll();
        LocalizedText.AttachToAll(); // etiquetas del prefab segun el idioma actual
        return panel;
    }

    private void Awake()
    {
        musicSlider.onValueChanged.AddListener(v => { if (!refreshing) SettingsService.SetMusicVolume(v); });
        sfxSlider.onValueChanged.AddListener(v => { if (!refreshing) SettingsService.SetSfxVolume(v); });
        musicToggle.onValueChanged.AddListener(on => { if (!refreshing) SettingsService.SetMusicMuted(!on); });
        sfxToggle.onValueChanged.AddListener(on => { if (!refreshing) SettingsService.SetSfxMuted(!on); });
        languageSpanish.onClick.AddListener(() => SetLanguage(0));
        languageEnglish.onClick.AddListener(() => SetLanguage(1));
        closeButton.onClick.AddListener(Close);
    }

    private void OnEnable()
    {
        SettingsService.Changed += Refresh;
        Refresh();
    }

    private void OnDisable() => SettingsService.Changed -= Refresh;

    private void OnDestroy()
    {
        if (current == this) current = null;
    }

    public void Close()
    {
        SettingsService.Save();
        Action callback = onClosed;
        onClosed = null;
        if (current == this) current = null; // Destroy es diferido: se libera ya para que IsOpen sea cierto de inmediato

        // Destruye el contenedor completo (fondo incluido) y el canvas de respaldo si lo hubo
        Transform root = transform.parent != null ? transform.parent : transform;
        Destroy(root.gameObject);
        if (ownedCanvas != null) Destroy(ownedCanvas);
        callback?.Invoke();
    }

    private void Refresh()
    {
        refreshing = true;
        musicSlider.SetValueWithoutNotify(SettingsService.MusicVolume);
        sfxSlider.SetValueWithoutNotify(SettingsService.SfxVolume);
        musicToggle.SetIsOnWithoutNotify(!SettingsService.MusicMuted);
        sfxToggle.SetIsOnWithoutNotify(!SettingsService.SfxMuted);

        // El idioma elegido queda resaltado (el otro, atenuado)
        languageSpanish.interactable = SettingsService.LanguageIndex != 0;
        languageEnglish.interactable = SettingsService.LanguageIndex != 1;
        refreshing = false;
    }

    private static void SetLanguage(int index)
    {
        SettingsService.SetLanguageIndex(index);
    }
}
