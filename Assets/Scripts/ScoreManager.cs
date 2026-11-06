using UnityEngine;

public partial class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Constantes de Puntaje Base")]
    public static readonly int POINTS_PER_DIE = MyGame.Core.ScoringConfigDTO.Shared.PointsPerDie;

    [Header("Bonos de Estructura")]
    public static readonly int ROW_COMPLETE_BONUS = MyGame.Core.ScoringConfigDTO.Shared.RowCompleteBonus;
    public static readonly int COL_COMPLETE_BONUS = MyGame.Core.ScoringConfigDTO.Shared.ColCompleteBonus;
    public static readonly int INTERSECTION_BONUS = MyGame.Core.ScoringConfigDTO.Shared.IntersectionBonus;

    // Multiplicadores por líneas sucesivas 
    public static readonly float[] RowMultipliers = MyGame.Core.ScoringConfigDTO.Shared.RowMultipliers;
    private readonly float[] rowMultipliers = MyGame.Core.ScoringConfigDTO.Shared.RowMultipliers;
    public static readonly float[] ColMultipliers = MyGame.Core.ScoringConfigDTO.Shared.ColMultipliers;
    private readonly float[] colMultipliers = MyGame.Core.ScoringConfigDTO.Shared.ColMultipliers;

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    public int GetPatternBonus(int targetSize)
    {
        return MyGame.Core.ScoringConfigDTO.Shared.GetPatternBonus(targetSize);
    }

    public float GetConsecutiveRowMultiplier(int linesConsecutive)
    {
        if (linesConsecutive <= 0) return 1f;
        int index = Mathf.Min(linesConsecutive - 1, rowMultipliers.Length - 1);
        return rowMultipliers[index];
    }

    public float GetConsecutiveColMultiplier(int linesConsecutive)
    {
        if (linesConsecutive <= 0) return 1f;
        int index = Mathf.Min(linesConsecutive - 1, colMultipliers.Length - 1);
        return colMultipliers[index];
    }
}