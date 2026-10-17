using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Kit de interfaz con el aspecto de Mystic Realms Wooden y la vida de Juice UI. Agrupa los prefabs (botones, paneles,
/// campo de texto, slider, toggle, panel de ajustes) para que los controladores creados por codigo —menu online, HUD online—
/// los instancien en lugar de dibujar rectangulos planos. Lo genera Tools > Dharitz > UI > Construir kit de UI online
/// y vive en Resources/OnlineUiKit.asset.
/// </summary>
[CreateAssetMenu(fileName = "OnlineUiKit", menuName = "Dharitz/Online UI Kit")]
public class OnlineUiKit : ScriptableObject
{
    public enum ButtonStyle
    {
        Primary,    // accion principal (verde)
        Secondary,  // accion secundaria (azul)
        Neutral,    // acciones menores (crema)
        Danger,     // salir / cancelar (rojo)
        Close       // cuadrado con X
    }

    private const string ResourceName = "OnlineUiKit";

    [Header("Botones (variantes de los ButtonRect de Mystic Realms + ButtonAnimator_Free)")]
    public Button buttonPrimary;
    public Button buttonSecondary;
    public Button buttonNeutral;
    public Button buttonDanger;
    public Button buttonClose;

    [Header("Paneles y controles")]
    public RectTransform panel;
    public TMP_Text label;
    public TMP_InputField inputField;
    public Slider slider;
    public Toggle toggle;
    public SettingsPanel settingsPanel;

    [Header("Fuentes")]
    public TMP_FontAsset bodyFont;
    public TMP_FontAsset buttonFont;

    private static OnlineUiKit instance;

    public static OnlineUiKit Instance
    {
        get
        {
            if (instance == null) instance = Resources.Load<OnlineUiKit>(ResourceName);
            if (instance == null) Debug.LogError("[OnlineUiKit] Falta Resources/OnlineUiKit.asset. Ejecuta Tools > Dharitz > UI > Construir kit de UI online.");
            return instance;
        }
    }

    // ------------------------------------------------------------------ Fabricas

    public Button CreateButton(Transform parent, ButtonStyle style, string text, Vector2 anchor, Vector2 size, UnityAction onClick)
    {
        Button prefab = style switch
        {
            ButtonStyle.Secondary => buttonSecondary,
            ButtonStyle.Neutral => buttonNeutral,
            ButtonStyle.Danger => buttonDanger,
            ButtonStyle.Close => buttonClose,
            _ => buttonPrimary
        };

        Button button = Instantiate(prefab, parent, false);
        button.name = style + "Button";
        Place((RectTransform)button.transform, anchor, size);

        TMP_Text caption = button.GetComponentInChildren<TMP_Text>(true);
        if (caption != null)
        {
            caption.text = text;
            var captionRect = caption.rectTransform;   // el texto ocupa todo el boton para que el autoajuste no lo encoja de mas
            captionRect.anchorMin = Vector2.zero;
            captionRect.anchorMax = Vector2.one;
            captionRect.offsetMin = new Vector2(24f, 8f);
            captionRect.offsetMax = new Vector2(-24f, -8f);
            caption.enableAutoSizing = true;   // los textos largos se encogen en lugar de salirse del boton
            caption.fontSizeMin = 18f;
            caption.fontSizeMax = 40f;
            caption.textWrappingMode = TextWrappingModes.NoWrap;
        }
        if (onClick != null) button.onClick.AddListener(onClick);
        return button;
    }

    public TMP_Text CreateLabel(Transform parent, string text, Vector2 anchor, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        TMP_Text instanceLabel = Instantiate(label, parent, false);
        instanceLabel.name = "Label";
        Place((RectTransform)instanceLabel.transform, anchor, size);
        instanceLabel.text = text;
        instanceLabel.fontSize = fontSize;
        instanceLabel.alignment = alignment;
        return instanceLabel;
    }

    public RectTransform CreatePanel(Transform parent, Vector2 size, string title)
    {
        RectTransform instancePanel = Instantiate(panel, parent, false);
        instancePanel.name = "Panel";
        Place(instancePanel, new Vector2(0.5f, 0.5f), size);
        SetPanelTitle(instancePanel, title);
        return instancePanel;
    }

    public TMP_InputField CreateInputField(Transform parent, string text, string placeholder, Vector2 anchor, Vector2 size)
    {
        TMP_InputField field = Instantiate(inputField, parent, false);
        field.name = "InputField";
        Place((RectTransform)field.transform, anchor, size);
        field.text = text;
        if (field.placeholder is TMP_Text placeholderText) placeholderText.text = placeholder;
        return field;
    }

    public static void SetPanelTitle(RectTransform panelRoot, string title)
    {
        Transform banner = panelRoot.Find("Title");
        TMP_Text text = banner != null ? banner.GetComponentInChildren<TMP_Text>(true) : null;
        if (text != null) text.text = title;
    }

    private static void Place(RectTransform rect, Vector2 anchor, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }
}
