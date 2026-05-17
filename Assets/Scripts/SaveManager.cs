using UnityEngine;
using System.IO;

public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    public PlayerProfile CurrentProfile { get; private set; }

    private string SaveFilePath => Path.Combine(Application.persistentDataPath, "dharitz_save.json");

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadLocal();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadLocal()
    {
        if (File.Exists(SaveFilePath))
        {
            try
            {
                string json = File.ReadAllText(SaveFilePath);
                CurrentProfile = JsonUtility.FromJson<PlayerProfile>(json);
                Debug.Log("💾 Datos locales cargados exitosamente.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error al leer archivo de guardado local: {e.Message}");
                CurrentProfile = new PlayerProfile(); // Fallback si el archivo está corrupto
            }
        }
        else
        {
            Debug.Log("💾 Archivo local no encontrado. Creando nuevo perfil...");
            CurrentProfile = new PlayerProfile();
            SaveLocal();
        }
    }

    public void SaveLocal()
    {
        if (CurrentProfile == null) return;

        try
        {
            string json = JsonUtility.ToJson(CurrentProfile, true);
            File.WriteAllText(SaveFilePath, json);
            Debug.Log("💾 Progreso guardado localmente (JSON).");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al escribir guardado local: {e.Message}");
        }
    }

    // --- ACCIONES PRINCIPALES ---
    public void AddCoins(int amount)
    {
        CurrentProfile.totalCoins += amount;
        SaveLocal();
    }

    public void UpdateLevelProgress(int levelId, int score, int stars)
    {
        var level = CurrentProfile.GetLevel(levelId);
        
        if (level == null)
        {
            level = new LevelProgress(levelId, true);
            CurrentProfile.levels.Add(level);
        }

        if (score > level.highScore) level.highScore = score;
        if (stars > level.stars) level.stars = stars;

        // Desbloquear siguiente nivel si ganaste al menos 1 estrella
        if (stars >= 1)
        {
            CurrentProfile.UnlockNextLevel(levelId);
        }

        SaveLocal();
    }

    // Para sincronizar cuando descargamos de la nube
    public void ResolveCloudConflict(int cloudCoins, int cloudMaxLevel)
    {
        bool changed = false;

        if (cloudCoins > CurrentProfile.totalCoins)
        {
            CurrentProfile.totalCoins = cloudCoins;
            changed = true;
        }

        if (cloudMaxLevel > CurrentProfile.maxLevelReached)
        {
            CurrentProfile.maxLevelReached = cloudMaxLevel;
            changed = true;
        }

        if (changed) SaveLocal();
    }
}
