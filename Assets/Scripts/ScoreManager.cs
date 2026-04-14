using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    void Awake()
    {
        // Configuración del Singleton
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

    // Retorna los bonos definidos en tu GDD [cite: 42-46]
    public int GetPatternBonus(int targetSize)
    {
        switch (targetSize)
        {
            case 2: return 1;
            case 3: return 2;
            case 4: return 3; // Bono de +3 para el patrón de 4 [cite: 44]
            case 5: return 4;
            case 6: return 5;
            default: return 0;
        }
    }

    public int CalcularPuntajeFinal(PlayerData player, int penalizaciones)
    {
        // El puntaje ya tiene los +1 por dado y bonos por patrón
        int total = player.score - penalizaciones;
        return Mathf.Max(0, total); // Que no baje de 0 para no ser crueles
    }


}