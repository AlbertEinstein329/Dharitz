using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Prepara el audio del proyecto: crea el AudioMixer MainMixer (grupos Music y SFX con parametros expuestos),
/// convierte el AudioManager de MenuPrincipal en el prefab Resources/AudioManager (que AudioManager instancia solo)
/// y retira las copias que quedaban en las escenas. Los pasos son idempotentes.
/// </summary>
public static class DharitzAudioSetup
{
    public const string MixerPath = "Assets/Audio/MainMixer.mixer";
    public const string PrefabPath = "Assets/Resources/AudioManager.prefab";
    private const string MenuScene = "Assets/Scenes/MenuPrincipal.unity";
    private static readonly string[] CopyScenes = { "Assets/Scenes/MenuPrincipal.unity", "Assets/Scenes/Tablero.unity" };

    [MenuItem("Tools/Dharitz/Audio/Preparar todo (mixer + prefab + limpiar escenas)")]
    public static void RunAll()
    {
        CreateMixer();
        CreatePrefab();
        RemoveSceneCopies();
    }

    // ------------------------------------------------------------------ Mixer

    [MenuItem("Tools/Dharitz/Audio/1. Crear MainMixer")]
    public static AudioMixer CreateMixer()
    {
        var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (existing != null && existing.FindMatchingGroups("Music").Length > 0 && existing.FindMatchingGroups("SFX").Length > 0)
        {
            Debug.Log("[DharitzAudioSetup] MainMixer ya existe.");
            return existing;
        }

        Assembly editorAsm = typeof(EditorWindow).Assembly;
        Type controllerType = editorAsm.GetType("UnityEditor.Audio.AudioMixerController");
        Type pathType = editorAsm.GetType("UnityEditor.Audio.AudioParameterPath");
        const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

        if (controllerType == null || pathType == null)
        {
            Debug.LogError("[DharitzAudioSetup] Esta version de Unity no expone el API interno del AudioMixer. Crea MainMixer a mano (grupos Music y SFX, parametros MusicVolume y SFXVolume).");
            return null;
        }

        if (existing != null) AssetDatabase.DeleteAsset(MixerPath);

        object controller = controllerType.GetMethod("CreateMixerControllerAtPath", All).Invoke(null, new object[] { MixerPath });
        object master = controllerType.GetProperty("masterGroup", All).GetValue(controller);

        foreach (var (groupName, parameterName) in new[] { ("Music", "MusicVolume"), ("SFX", "SFXVolume") })
        {
            object group = controllerType.GetMethod("CreateNewGroup", All).Invoke(controller, new object[] { groupName, true });
            controllerType.GetMethod("AddChildToParent", All).Invoke(controller, new object[] { group, master });

            // Expone el volumen del grupo con un nombre estable para scripts
            object volumeGuid = group.GetType().GetMethod("GetGUIDForVolume", All).Invoke(group, null);
            // AudioParameterPath es abstracta: la implementacion concreta para un grupo recibe (grupo, GUID del parametro)
            Type concretePath = editorAsm.GetTypes().FirstOrDefault(t => pathType.IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructors(All).Any(c => c.GetParameters().Length == 2));
            if (concretePath == null)
            {
                Debug.LogError("[DharitzAudioSetup] No se encontro la implementacion concreta de AudioParameterPath en esta version de Unity.");
                return null;
            }
            ConstructorInfo pathCtor = concretePath.GetConstructors(All).First(c => c.GetParameters().Length == 2);
            object parameterPath = pathCtor.Invoke(new object[] { group, volumeGuid });
            controllerType.GetMethod("AddExposedParameter", All).Invoke(controller, new object[] { parameterPath });

            PropertyInfo exposedProp = controllerType.GetProperty("exposedParameters", All);
            Array exposed = (Array)exposedProp.GetValue(controller);
            for (int i = 0; i < exposed.Length; i++)
            {
                object element = exposed.GetValue(i);
                FieldInfo guidField = element.GetType().GetField("guid", All);
                if (!guidField.GetValue(element).Equals(volumeGuid)) continue;

                element.GetType().GetField("name", All).SetValue(element, parameterName);
                exposed.SetValue(element, i);
            }
            exposedProp.SetValue(controller, exposed);
            controllerType.GetMethod("OnChangedExposedParameter", All)?.Invoke(controller, null);
        }

        EditorUtility.SetDirty((UnityEngine.Object)controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(MixerPath, ImportAssetOptions.ForceUpdate);

        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        Debug.Log($"[DharitzAudioSetup] MainMixer creado: grupos {string.Join(", ", mixer.FindMatchingGroups("").Select(g => g.name))}.");
        return mixer;
    }

    // ------------------------------------------------------------------ Prefab

    [MenuItem("Tools/Dharitz/Audio/2. Crear prefab AudioManager desde MenuPrincipal")]
    public static void CreatePrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
        {
            Debug.Log("[DharitzAudioSetup] El prefab AudioManager ya existe.");
            AssignMixer();
            return;
        }

        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        EditorSceneManager.OpenScene(MenuScene);

        var source = UnityEngine.Object.FindFirstObjectByType<AudioManager>();
        if (source == null)
        {
            Debug.LogError("[DharitzAudioSetup] No hay AudioManager en MenuPrincipal para convertir en prefab.");
            return;
        }

        System.IO.Directory.CreateDirectory("Assets/Resources");
        PrefabUtility.SaveAsPrefabAsset(source.gameObject, PrefabPath);
        AssignMixer();
        Debug.Log("[DharitzAudioSetup] Prefab Resources/AudioManager creado con la biblioteca y la musica de MenuPrincipal.");
    }

    // Enlaza el mixer en el prefab y enruta cada AudioSource a su grupo
    private static void AssignMixer()
    {
        var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MixerPath);
        if (mixer == null) return;

        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var manager = root.GetComponent<AudioManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("mixer").objectReferenceValue = mixer;
            so.ApplyModifiedPropertiesWithoutUndo();

            AudioMixerGroup music = mixer.FindMatchingGroups("Music").FirstOrDefault();
            AudioMixerGroup sfx = mixer.FindMatchingGroups("SFX").FirstOrDefault();
            var musicSource = so.FindProperty("musicSource").objectReferenceValue as AudioSource;
            var sfxSource = so.FindProperty("sfxSource").objectReferenceValue as AudioSource;
            if (musicSource != null) musicSource.outputAudioMixerGroup = music;
            if (sfxSource != null) sfxSource.outputAudioMixerGroup = sfx;

            // Las fuentes no deben sonar solas al instanciar el prefab
            if (musicSource != null) musicSource.playOnAwake = false;
            if (sfxSource != null) sfxSource.playOnAwake = false;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // ------------------------------------------------------------------ Escenas

    [MenuItem("Tools/Dharitz/Audio/3. Quitar AudioManager de las escenas")]
    public static void RemoveSceneCopies()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            Debug.LogError("[DharitzAudioSetup] Crea primero el prefab AudioManager; no se quita nada.");
            return;
        }

        EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        foreach (string scenePath in CopyScenes)
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            var copies = UnityEngine.Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (AudioManager copy in copies) UnityEngine.Object.DestroyImmediate(copy.gameObject);

            if (copies.Length > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[DharitzAudioSetup] {scenePath}: {copies.Length} copia(s) de AudioManager retiradas.");
        }
    }
}
