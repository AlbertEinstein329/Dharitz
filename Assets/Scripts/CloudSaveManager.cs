using UnityEngine;
using Unity.Services.CloudSave;
using System.Collections.Generic;
using System.Threading.Tasks;

public class CloudSaveManager : MonoBehaviour
{
    public static CloudSaveManager Instance { get; private set; }

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
    /// Guarda el progreso de la campaña y las estadísticas globales en la nube.
    /// </summary>
    public async Task GuardarProgresoMeta(int nivelCampana, int monedasTotales)
    {
        try
        {
            // Creamos un "paquete" con los datos que queremos subir
            var datos = new Dictionary<string, object>
            {
                { "NivelCampana", nivelCampana },
                { "MonedasTotales", monedasTotales }
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
    public async Task CargarProgresoMeta()
    {
        try
        {
            // Pedimos a la nube específicamente estas dos "llaves"
            var query = await CloudSaveService.Instance.Data.Player.LoadAsync(new HashSet<string> { "NivelCampana", "MonedasTotales" });

            int nivelActual = 1; // Valor por defecto si es un jugador nuevo
            int monedas = 0;

            if (query.TryGetValue("NivelCampana", out var nivelItem))
            {
                nivelActual = nivelItem.Value.GetAs<int>();
            }

            if (query.TryGetValue("MonedasTotales", out var monedasItem))
            {
                monedas = monedasItem.Value.GetAs<int>();
            }

            Debug.Log($"☁️ Progreso descargado: Nivel {nivelActual} | Monedas: {monedas}");

            // TODO: Aquí debes asignar estos valores a tu script real de Meta-Progresión
            // CONECTA LOS DATOS AQUÍ:
            // GlobalData.NivelActual = nivelActual;
            // GlobalData.MonedasTotales = monedas;
            // MenuManager.Instance.ActualizarTextosDeMonedas(); // Que la UI se actualice

        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error al cargar de la nube: {e.Message}");
        }
    }
}