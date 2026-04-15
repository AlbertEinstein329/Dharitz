using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Constantes de Puntaje Base")]
    public const int POINTS_PER_DIE = 50;

    [Header("Bonos de Estructura")]
    public const int ROW_COMPLETE_BONUS = 250;
    public const int COL_COMPLETE_BONUS = 350;
    public const int INTERSECTION_BONUS = 200;

    [Header("Multiplicadores")]
    // Multiplicadores por filas o columnas consecutivas (índice 0 = 1 línea, índice 1 = 2 líneas consecutivas, etc.)
    // Los multiplicadores de filas son: x1.5, x2.0, x2.5, x3.0, x3.5, x5.0
    private readonly float[] rowMultipliers = { 1.5f, 2.0f, 2.5f, 3.0f, 3.5f, 5.0f };
    // Los multiplicadores de columnas son: x2.0, x2.5, x3.0, x3.5, x5.0 
    private readonly float[] colMultipliers = { 2.0f, 2.5f, 3.0f, 3.5f, 5.0f };

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

    /// <summary>
    /// Retorna los bonos por patrón según el tamaño del patrón armado.
    /// Patrón 1-2: +50 / Patrón 3: +100 / Patrón 4: +150 / Patrón 5: +200 / Patrón 6: +250
    /// </summary>
    public int GetPatternBonus(int targetSize)
    {
        switch (targetSize)
        {
            case 1:
            case 2: return 50;
            case 3: return 100;
            case 4: return 150;
            case 5: return 200;
            case 6: return 250;
            default: return 0;
        }
    }

    /// <summary>
    /// Obtiene el multiplicador por completar filas sucesivas (sin huecos entre ellas).
    /// linesConsecutive debe ser >= 1 (1 indica que completaste una sola fila recientemente evaluada).
    /// </summary>
    public float GetConsecutiveRowMultiplier(int linesConsecutive)
    {
        if (linesConsecutive <= 0) return 1f;
        int index = Mathf.Min(linesConsecutive - 1, rowMultipliers.Length - 1);
        return rowMultipliers[index];
    }

    /// <summary>
    /// Obtiene el multiplicador por completar columnas sucesivas.
    /// linesConsecutive debe ser >= 1.
    /// </summary>
    public float GetConsecutiveColMultiplier(int linesConsecutive)
    {
        if (linesConsecutive <= 0) return 1f;
        int index = Mathf.Min(linesConsecutive - 1, colMultipliers.Length - 1);
        return colMultipliers[index];
    }

    /// <summary>
    /// Calcula el puntaje total que aporta un cruce (intersección) de fila y columna, sumando 
    /// los puntajes base multiplicados de la fila y la columna involucradas más el bono de intersección.
    /// </summary>
    /// <param name="baseRowScore">El puntaje base de la fila.</param>
    /// <param name="baseColScore">El puntaje base de la columna.</param>
    /// <param name="consecutiveRows">Número de filas consecutivas en el combo.</param>
    /// <param name="consecutiveCols">Número de columnas consecutivas en el combo.</param>
    public int GetIntersectionScore(int baseRowScore, int baseColScore, int consecutiveRows, int consecutiveCols)
    {
        float rowMult = GetConsecutiveRowMultiplier(consecutiveRows);
        float colMult = GetConsecutiveColMultiplier(consecutiveCols);

        int totalRowScore = Mathf.FloorToInt(baseRowScore * rowMult);
        int totalColScore = Mathf.FloorToInt(baseColScore * colMult);

        return INTERSECTION_BONUS + totalRowScore + totalColScore;
    }

    /// <summary>
    /// Calcula el valor positivo de penalización por una agrupación de huecos encerrados.
    /// Este valor debe restarse del puntaje final.
    /// </summary>
    public int GetHolePenalty(int holeSize)
    {
        if (holeSize <= 0) return 0;
        if (holeSize == 1) return 250;
        if (holeSize == 2) return 350;
        
        // 3 o más casillas: 500 por CADA espacio
        return 500 * holeSize;
    }

    /// <summary>
    /// Calcula el puntaje final restando las penalizaciones.
    /// </summary>
    public int CalcularPuntajeFinal(PlayerData player, int totalPenalizaciones)
    {
        // El puntaje de 'player' ya debe incluir sumas de dados, multiplicadores y bonos.
        int total = player.score - totalPenalizaciones;
        return Mathf.Max(0, total); // Se asegura de que no baje de 0
    }
}