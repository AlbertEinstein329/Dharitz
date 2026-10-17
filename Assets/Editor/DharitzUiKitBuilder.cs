using System.IO;
using SpankyBoy.JuiceUI.Free;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using UImage = UnityEngine.UI.Image;

/// <summary>
/// Construye el kit de interfaz online: fuentes dinamicas con tildes y enes (las SDF de Mystic Realms solo traen ASCII),
/// variantes de los botones de Mystic Realms Wooden con Juice UI, panel, slider, toggle, campo de texto y el panel de
/// ajustes. Todo queda en Assets/Prefabs/OnlineUI y en Resources/OnlineUiKit.asset. Es idempotente: reconstruye los prefabs.
/// </summary>
public static class DharitzUiKitBuilder
{
    private const string Root = "Assets/Prefabs/OnlineUI";
    private const string FontDir = Root + "/Fonts";
    private const string MysticRoot = "Assets/Mystic Realms Wooden";
    private const string KitPath = "Assets/Resources/OnlineUiKit.asset";

    private static readonly Color Cream = new Color(0.882f, 0.796f, 0.549f, 1f);
    private static readonly Color DarkWood = new Color(0.20f, 0.12f, 0.06f, 1f);

    [MenuItem("Tools/Dharitz/UI/Construir kit de UI online")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(FontDir);
        Directory.CreateDirectory("Assets/Resources");

        var fonts = BuildFonts();
        TMP_FontAsset luckiest = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MysticRoot + "/Resources/FontsGame/Luckiest Guy SDF.asset");
        TMP_FontAsset passion = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(MysticRoot + "/Resources/FontsGame/PassionOne-Bold SDF.asset");

        // ---- Botones
        Button primary = BuildRectButton("ButtonPrimary", "ButtonRect Green", null);
        Button secondary = BuildRectButton("ButtonSecondary", "ButtonRect Blue", null);
        Button neutral = BuildRectButton("ButtonNeutral", "ButtonRect Cream", null);
        Button danger = BuildRectButton("ButtonDanger", "ButtonRect Yellow", Sprite("SpritesWooden/I. Wooden Buttons/01. Rect Button/RectButton_Red.png"));
        Button close = BuildCloseButton();

        // ---- Controles
        TMP_Text label = BuildLabel(passion);
        TMP_InputField input = BuildInputField(passion);
        Slider slider = BuildSlider();
        Toggle toggle = BuildToggle();
        RectTransform panel = BuildPanel(luckiest);

        // ---- Panel de ajustes (usa los prefabs anteriores)
        SettingsPanel settings = BuildSettingsPanel(panel, label, slider, toggle, secondary, close);

        // ---- Asset del kit
        var kit = AssetDatabase.LoadAssetAtPath<OnlineUiKit>(KitPath);
        if (kit == null)
        {
            kit = ScriptableObject.CreateInstance<OnlineUiKit>();
            AssetDatabase.CreateAsset(kit, KitPath);
        }
        kit.buttonPrimary = primary;
        kit.buttonSecondary = secondary;
        kit.buttonNeutral = neutral;
        kit.buttonDanger = danger;
        kit.buttonClose = close;
        kit.panel = panel;
        kit.label = label;
        kit.inputField = input;
        kit.slider = slider;
        kit.toggle = toggle;
        kit.settingsPanel = settings;
        kit.bodyFont = passion;
        kit.buttonFont = luckiest;
        EditorUtility.SetDirty(kit);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[DharitzUiKitBuilder] Kit construido en {Root} ({fonts} fuente(s) dinamica(s) de respaldo).");
    }

    // ------------------------------------------------------------------ Fuentes

    private static int BuildFonts()
    {
        string[] sources = { "PassionOne-Bold", "Luckiest Guy" }; // Passion primero: tiene minusculas, Luckiest solo mayusculas
        var created = new System.Collections.Generic.List<TMP_FontAsset>();

        foreach (string name in sources)
        {
            string assetPath = $"{FontDir}/{name} Dynamic SDF.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath);
            if (existing != null) { created.Add(existing); continue; }

            var ttf = AssetDatabase.LoadAssetAtPath<Font>($"{MysticRoot}/Resources/FontsGame/{name}.ttf");
            if (ttf == null) { Debug.LogError($"[DharitzUiKitBuilder] Falta {name}.ttf"); continue; }

            TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(ttf, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            fa.name = $"{name} Dynamic SDF";
            AssetDatabase.CreateAsset(fa, assetPath);
            fa.material.name = fa.name + " Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);
            foreach (Texture2D atlas in fa.atlasTextures)
            {
                atlas.name = fa.name + " Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fa);
            }
            // Precarga lo que usa el idioma espanol para que no aparezcan huecos
            fa.TryAddCharacters("áéíóúüñÁÉÍÓÚÜÑ¿¡ºª", out _);
            EditorUtility.SetDirty(fa);
            created.Add(fa);
        }

        // Respaldo global: cualquier TMP que pida un glifo ausente lo busca aqui
        var settings = TMP_Settings.instance;
        if (settings != null)
        {
            var so = new SerializedObject(settings);
            SerializedProperty list = so.FindProperty("m_fallbackFontAssets");
            foreach (TMP_FontAsset fa in created)
            {
                bool present = false;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == fa) present = true;
                if (present) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = fa;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
        AssetDatabase.SaveAssets();
        return created.Count;
    }

    // ------------------------------------------------------------------ Botones

    private static Button BuildRectButton(string name, string sourceName, Sprite overrideSprite)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{MysticRoot}/Prefabs/01. Buttons/ButtonRect/{sourceName}.prefab");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = name;
        if (overrideSprite != null) instance.GetComponent<UImage>().sprite = overrideSprite;
        AddJuice(instance, ButtonAnimator_Free.HoverEffect.BounceScale, ButtonAnimator_Free.ClickEffect.Squeeze);
        UiButtonSfx.Attach(instance);
        return SaveButton(instance, name);
    }

    private static Button BuildCloseButton()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>($"{MysticRoot}/Prefabs/01. Buttons/ButtonSquare/ButtonSquareSmall.prefab");
        if (source == null) source = FindPrefab("ButtonSquareSmall");
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = "ButtonClose";
        var image = instance.GetComponent<UImage>();
        image.sprite = Sprite("SpritesWooden/I. Wooden Buttons/03. Square Button/ButtonSquare_Cancel.png");
        image.type = UImage.Type.Simple;
        image.preserveAspect = true;
        var button = instance.GetComponent<Button>() ?? instance.AddComponent<Button>();
        button.targetGraphic = image;
        ((RectTransform)instance.transform).sizeDelta = new Vector2(96f, 96f);
        AddJuice(instance, ButtonAnimator_Free.HoverEffect.Scale, ButtonAnimator_Free.ClickEffect.Squeeze);
        UiButtonSfx.Attach(instance);
        return SaveButton(instance, "ButtonClose");
    }

    private static void AddJuice(GameObject go, ButtonAnimator_Free.HoverEffect hover, ButtonAnimator_Free.ClickEffect click)
    {
        var animator = go.GetComponent<ButtonAnimator_Free>() ?? go.AddComponent<ButtonAnimator_Free>();
        animator.hoverEffect = hover;
        animator.clickEffect = click;
        animator.hoverScale = 1.06f;
        animator.squeezeAmount = 0.9f;
    }

    private static Button SaveButton(GameObject instance, string name)
    {
        GameObject prefab = Save(instance, name);
        return prefab.GetComponent<Button>();
    }

    // ------------------------------------------------------------------ Controles

    private static TMP_Text BuildLabel(TMP_FontAsset font)
    {
        var go = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        var text = go.GetComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = 40;
        text.color = Cream;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        ((RectTransform)go.transform).sizeDelta = new Vector2(600f, 80f);
        return Save(go, "OnlineLabel").GetComponent<TMP_Text>();
    }

    private static TMP_InputField BuildInputField(TMP_FontAsset font)
    {
        var resources = new TMP_DefaultControls.Resources();
        GameObject go = TMP_DefaultControls.CreateInputField(resources);
        go.name = "OnlineInputField";
        var image = go.GetComponent<UImage>();
        image.sprite = Sprite("SpritesWooden/II. Wooden UI/TextBox.png");
        image.type = UImage.Type.Sliced;
        image.color = Color.white;
        var field = go.GetComponent<TMP_InputField>();
        field.pointSize = 40;
        field.fontAsset = font;
        field.textComponent.color = DarkWood;
        field.textComponent.fontSize = 40;
        field.textComponent.alignment = TextAlignmentOptions.MidlineLeft;
        field.textComponent.font = font;
        if (field.placeholder is TMP_Text placeholder)
        {
            placeholder.font = font;
            placeholder.fontSize = 40;
            placeholder.alignment = TextAlignmentOptions.MidlineLeft;
            placeholder.color = new Color(DarkWood.r, DarkWood.g, DarkWood.b, 0.5f);
        }
        ((RectTransform)go.transform).sizeDelta = new Vector2(600f, 100f);
        UiButtonSfx.Attach(go);
        return Save(go, "OnlineInputField").GetComponent<TMP_InputField>();
    }

    private static Slider BuildSlider()
    {
        var root = new GameObject("OnlineSlider", typeof(RectTransform), typeof(Slider));
        var rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(520f, 56f);

        var back = CreateChild(root, "Background", Stretch);
        var backImage = back.AddComponent<UImage>();
        backImage.sprite = Sprite("SpritesWooden/II. Wooden UI/ProcessingBar/BarLongBack.png");
        backImage.type = UImage.Type.Sliced;
        backImage.pixelsPerUnitMultiplier = 3f;

        var fillArea = CreateChild(root, "Fill Area", Stretch);
        ((RectTransform)fillArea.transform).offsetMin = new Vector2(10f, 8f);
        ((RectTransform)fillArea.transform).offsetMax = new Vector2(-10f, -8f);
        var fill = CreateChild(fillArea, "Fill", rect => { rect.anchorMin = Vector2.zero; rect.anchorMax = new Vector2(0f, 1f); rect.sizeDelta = new Vector2(10f, 0f); });
        var fillImage = fill.AddComponent<UImage>();
        fillImage.sprite = Sprite("SpritesWooden/II. Wooden UI/ProcessingBar/BarLongFill.png");
        fillImage.type = UImage.Type.Sliced;
        fillImage.pixelsPerUnitMultiplier = 3f;

        var handleArea = CreateChild(root, "Handle Slide Area", Stretch);
        ((RectTransform)handleArea.transform).offsetMin = new Vector2(30f, 0f);
        ((RectTransform)handleArea.transform).offsetMax = new Vector2(-30f, 0f);
        var handle = CreateChild(handleArea, "Handle", rect => { rect.anchorMin = new Vector2(0f, 0.5f); rect.anchorMax = new Vector2(0f, 0.5f); rect.sizeDelta = new Vector2(76f, 76f); });
        var handleImage = handle.AddComponent<UImage>();
        handleImage.sprite = Sprite("SpritesWooden/I. Wooden Buttons/02. Button Hex/ButtonHex_Wooden.png");
        handleImage.preserveAspect = true;

        var slider = root.GetComponent<Slider>();
        slider.fillRect = (RectTransform)fill.transform;
        slider.handleRect = (RectTransform)handle.transform;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.75f;
        return Save(root, "OnlineSlider").GetComponent<Slider>();
    }

    private static Toggle BuildToggle()
    {
        var root = new GameObject("OnlineToggle", typeof(RectTransform), typeof(Toggle));
        ((RectTransform)root.transform).sizeDelta = new Vector2(88f, 88f);

        var bg = CreateChild(root, "Background", Stretch);
        var bgImage = bg.AddComponent<UImage>();
        bgImage.sprite = Sprite("SpritesGeneric/III. UI Elements/CheckMate_OFF.png");
        bgImage.preserveAspect = true;

        var check = CreateChild(bg, "Checkmark", Stretch);
        var checkImage = check.AddComponent<UImage>();
        checkImage.sprite = Sprite("SpritesGeneric/III. UI Elements/CheckMate_ON.png");
        checkImage.preserveAspect = true;

        var toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = bgImage;
        toggle.graphic = checkImage;
        toggle.isOn = true;
        AddJuiceToToggle(root);
        UiButtonSfx.Attach(root);
        return Save(root, "OnlineToggle").GetComponent<Toggle>();
    }

    private static void AddJuiceToToggle(GameObject root)
    {
        // ButtonAnimator_Free exige un Button; el toggle se anima con un pulso suave al activarse
        root.AddComponent<Animate_Pulse_Free>().playOnEnable = false;
    }

    private static RectTransform BuildPanel(TMP_FontAsset titleFont)
    {
        var root = new GameObject("OnlinePanel", typeof(RectTransform), typeof(CanvasGroup), typeof(UImage));
        ((RectTransform)root.transform).sizeDelta = new Vector2(900f, 1000f);
        var image = root.GetComponent<UImage>();
        image.sprite = Sprite("SpritesWooden/IV. Wooden Panels/BoardPanelA.png");
        image.type = UImage.Type.Sliced;

        var title = CreateChild(root, "Title", rect =>
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -10f);
            rect.sizeDelta = new Vector2(620f, 130f);
        });
        var banner = title.AddComponent<UImage>();
        banner.sprite = Sprite("SpritesGeneric/I. Banner/BannerRed.png");
        banner.type = UImage.Type.Sliced;
        banner.raycastTarget = false;

        var textObject = CreateChild(title, "Text", Stretch);
        var text = textObject.AddComponent<TextMeshProUGUI>();
        text.font = titleFont;
        text.fontSize = 56;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        text.text = "TITULO";

        var animator = root.AddComponent<PanelAnimator_Free>();
        animator.inAnimation = PanelAnimator_Free.AnimationType.Pop;
        animator.inDuration = 0.35f;
        animator.playInOnEnable = true;
        animator.disableOnOutComplete = false;

        return (RectTransform)Save(root, "OnlinePanel").transform;
    }

    // ------------------------------------------------------------------ Panel de ajustes

    private static SettingsPanel BuildSettingsPanel(RectTransform panelPrefab, TMP_Text labelPrefab, Slider sliderPrefab, Toggle togglePrefab, Button languageButtonPrefab, Button closePrefab)
    {
        var root = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab.gameObject);
        root.name = "SettingsPanel";
        var rootRect = (RectTransform)root.transform;
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(900f, 950f);
        OnlineUiKit.SetPanelTitle(rootRect, "AJUSTES");

        Slider music, sfx; Toggle musicToggle, sfxToggle; Button es, en;

        MakeRow(root, labelPrefab, "Música", 260f, sliderPrefab, togglePrefab, out music, out musicToggle);
        MakeRow(root, labelPrefab, "Efectos", 80f, sliderPrefab, togglePrefab, out sfx, out sfxToggle);

        MakeLabel(root, labelPrefab, "Idioma / Language", new Vector2(0f, -110f));
        es = MakeButton(root, languageButtonPrefab, "ESPAÑOL", new Vector2(-190f, -220f), new Vector2(340f, 130f));
        en = MakeButton(root, languageButtonPrefab, "ENGLISH", new Vector2(190f, -220f), new Vector2(340f, 130f));

        var close = (Button)PrefabUtility.InstantiatePrefab(closePrefab, root.transform);
        var closeRect = (RectTransform)close.transform;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-40f, -40f);
        closeRect.sizeDelta = new Vector2(100f, 100f);
        close.name = "CloseButton";

        var settings = root.AddComponent<SettingsPanel>();
        var so = new SerializedObject(settings);
        so.FindProperty("musicSlider").objectReferenceValue = music;
        so.FindProperty("musicToggle").objectReferenceValue = musicToggle;
        so.FindProperty("sfxSlider").objectReferenceValue = sfx;
        so.FindProperty("sfxToggle").objectReferenceValue = sfxToggle;
        so.FindProperty("languageSpanish").objectReferenceValue = es;
        so.FindProperty("languageEnglish").objectReferenceValue = en;
        so.FindProperty("closeButton").objectReferenceValue = close;
        so.ApplyModifiedPropertiesWithoutUndo();

        return Save(root, "SettingsPanel").GetComponent<SettingsPanel>();
    }

    // y se mide desde el centro del panel (positivo arriba)
    private static void MakeRow(GameObject panel, TMP_Text labelPrefab, string caption, float y, Slider sliderPrefab, Toggle togglePrefab, out Slider slider, out Toggle toggle)
    {
        MakeLabel(panel, labelPrefab, caption, new Vector2(0f, y + 80f));
        slider = (Slider)PrefabUtility.InstantiatePrefab(sliderPrefab, panel.transform);
        PlaceCenter((RectTransform)slider.transform, new Vector2(-70f, y), new Vector2(520f, 56f));
        slider.name = caption + "Slider";
        toggle = (Toggle)PrefabUtility.InstantiatePrefab(togglePrefab, panel.transform);
        PlaceCenter((RectTransform)toggle.transform, new Vector2(300f, y), new Vector2(88f, 88f));
        toggle.name = caption + "Toggle";
    }

    private static TMP_Text MakeLabel(GameObject parent, TMP_Text prefab, string text, Vector2 position, float size = 44f, Vector2? dimension = null)
    {
        var label = (TMP_Text)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        PlaceCenter((RectTransform)label.transform, position, dimension ?? new Vector2(700f, 80f));
        label.text = text;
        label.fontSize = size;
        return label;
    }

    private static Button MakeButton(GameObject parent, Button prefab, string text, Vector2 position, Vector2 size)
    {
        var button = (Button)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
        PlaceCenter((RectTransform)button.transform, position, size);
        button.GetComponentInChildren<TMP_Text>(true).text = text;
        button.name = text + "Button";
        return button;
    }

    // ------------------------------------------------------------------ Utilidades

    private static void PlaceCenter(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static GameObject CreateChild(GameObject parent, string name, System.Action<RectTransform> setup)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        setup((RectTransform)go.transform);
        return go;
    }

    private static Sprite Sprite(string relativePath)
    {
        string path = $"{MysticRoot}/{relativePath}";
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null) Debug.LogError($"[DharitzUiKitBuilder] Falta el sprite {path}");
        return sprite;
    }

    private static GameObject FindPrefab(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets($"{name} t:Prefab", new[] { MysticRoot }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name) return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        return null;
    }

    // Guarda la instancia como prefab (conectado si viene de otro prefab, o sea, variante) y la destruye de la escena
    private static GameObject Save(GameObject instance, string name)
    {
        string path = $"{Root}/{name}.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return prefab;
    }
}
