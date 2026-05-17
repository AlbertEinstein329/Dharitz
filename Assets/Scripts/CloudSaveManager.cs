using UnityEngine;
using Unity.Services.CloudSave;
using System.Collections.Generic;
using System.Threading.Tasks;

public class CloudSaveManager : MonoBehaviour
{
    public static CloudSaveManager Instance { get; private set; }

    public const string CAMPAIGN_LEVEL_KEY = "CAMPAIGN_LEVEL";
    public const string TOTAL_COINS_KEY = "TOTAL_COINS";

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Guarda el progreso de la campaña y las estadísticas globales en la nube leyendo del SaveManager local.
    /// </summary>
    public async Task SaveMetaProgress()
    {
        if (SaveManager.Instance == null || SaveManager.Instance.CurrentProfile == null) return;

        try
        {
            var datos = new Dictionary<string, object>
            {
                { CAMPAIGN_LEVEL_KEY, SaveManager.Instance.CurrentProfile.maxLevelReached },
                { TOTAL_COINS_KEY, SaveManager.Instance.CurrentProfile.totalCoins }
            };

            // Lo enviamos a la nube de UGS
            await CloudSaveService.Instance.Data.Player.SaveAsync(datos);
            Debug.Log("☁️ Progreso guardado en la nube exitosamente.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al guardar en la nube: {e.Message}");
            // Aquí en el futuro puedes implementar un guardado local de respaldo (PlayerPrefs)
        }
    }

    /// <summary>
    /// Descarga el progreso de la nube al iniciar el juego o al vincular una cuenta.
    /// </summary>
    public async Task LoadMetaProgress()
    {
        try
        {
            // Pedimos a la nube específicamente estas dos "llaves"
            var query = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { CAMPAIGN_LEVEL_KEY, TOTAL_COINS_KEY });

            int nivelActual = 1; // Valor por defecto si es un jugador nuevo
            int monedas = 0;

            if (query.TryGetValue(CAMPAIGN_LEVEL_KEY, out var nivelItem))
            {
                nivelActual = nivelItem.Value.GetAs<int>();
            }

            if (query.TryGetValue(TOTAL_COINS_KEY, out var monedasItem))
            {
                monedas = monedasItem.Value.GetAs<int>();
            }

            Debug.Log($"☁️ Progreso descargado: Nivel {nivelActual} | Monedas: {monedas}");

            // Resolvemos conflictos y sincronizamos localmente
            if (SaveManager.Instance != null)
            {
                SaveManager.Instance.ResolveCloudConflict(monedas, nivelActual);
            }

            // TODO: Si estás en el MainMenu, aquí llamarías a MenuManager.Instance.UpdateCoinsUI()
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al cargar de la nube: {e.Message}");
        }
    }
}