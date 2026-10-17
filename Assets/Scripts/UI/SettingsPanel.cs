using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Panel de ajustes unico (menu principal y pausa). Lee y escribe solo en <see cref="SettingsService"/>:
/// volumen y silencio de musica y efectos, idioma y modo zurdo. Se abre con <see cref="Open"/> sobre su propio canvas,
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

    [Header("Accesibilidad")]
    [SerializeField] private Toggle leftHandedToggle;

    [Header("Cierre")]
    [SerializeField] private Button closeButton;

    private static SettingsPanel current;
    private Action onClosed;
    private bool refreshing;

    /// <summary>Abre el panel de ajustes. Si ya hay uno abierto no crea otro.</summary>
    public static SettingsPanel Open(Action onClosed = null)
    {
        if (current != null) return current;

        OnlineUiKit kit = OnlineUiKit.Instance;
        if (kit == null || kit.settingsPanel == null) return null;

        var canvasObject = new GameObject("[SettingsCanvas]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.matchWidthOrHeight = 0.5f;

        // Fondo que oscurece y bloquea lo que hay detras
        var backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        backdrop.transform.SetParent(canvasObject.transform, false);
        var backdropRect = (RectTransform)backdrop.transform;
        backdropRect.anchorMin = Vector2.zero;
        backdropRect.anchorMax = Vector2.one;
        backdropRect.offsetMin = backdropRect.offsetMax = Vector2.zero;
        backdrop.GetComponent<UnityEngine.UI.Image>().color = new Color(0f, 0f, 0f, 0.6f);

        SettingsPanel panel = Instantiate(kit.settingsPanel, canvasObject.transform, false);
        panel.onClosed = onClosed;
        current = panel;

        if (UnityEngine.EventSystems.EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        UiButtonSfx.AttachToAll();
        return panel;
    }

    private void Awake()
    {
        musicSlider.onValueChanged.AddListener(v => { if (!refreshing) SettingsService.SetMusicVolume(v); });
        sfxSlider.onValueChanged.AddListener(v => { if (!refreshing) SettingsService.SetSfxVolume(v); });
        musicToggle.onValueChanged.AddListener(on => { if (!refreshing) SettingsService.SetMusicMuted(!on); });
        sfxToggle.onValueChanged.AddListener(on => { if (!refreshing) SettingsService.SetSfxMuted(!on); });
        leftHandedToggle.onValueChanged.AddListener(OnLeftHandedChanged);
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

        // Destruye el canvas completo (fondo incluido)
        Transform root = transform.parent != null ? transform.parent : transform;
        Destroy(root.gameObject);
        callback?.Invoke();
    }

    private void Refresh()
    {
        refreshing = true;
        musicSlider.SetValueWithoutNotify(SettingsService.MusicVolume);
        sfxSlider.SetValueWithoutNotify(SettingsService.SfxVolume);
        musicToggle.SetIsOnWithoutNotify(!SettingsService.MusicMuted);
        sfxToggle.SetIsOnWithoutNotify(!SettingsService.SfxMuted);
        leftHandedToggle.SetIsOnWithoutNotify(SettingsService.LeftHanded);

        // El idioma elegido queda resaltado (el otro, atenuado)
        languageSpanish.interactable = SettingsService.LanguageIndex != 0;
        languageEnglish.interactable = SettingsService.LanguageIndex != 1;
        refreshing = false;
    }

    private static void SetLanguage(int index)
    {
        SettingsService.SetLanguageIndex(index);
    }

    private void OnLeftHandedChanged(bool on)
    {
        if (refreshing) return;
        SettingsService.SetLeftHanded(on);

        UIManager ui = FindFirstObjectByType<UIManager>();
        if (ui != null) ui.ApplyHandednessLayout();
    }
}
