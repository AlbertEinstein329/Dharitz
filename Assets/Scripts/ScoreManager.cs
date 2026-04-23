using UnityEngine;

public partial class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance;

    [Header("Constantes de Puntaje Base")]
    public const int POINTS_PER_DIE = 50; // Aumento de 1 a 50 

    [Header("Bonos de Estructura")]
    public const int ROW_COMPLETE_BONUS = 120;
    public const int COL_COMPLETE_BONUS = 170;
    public const int INTERSECTION_BONUS = 100;

    // Multiplicadores por líneas sucesivas 
    private readonly float[] rowMultipliers = { 1.0f, 1.5f, 2.0f, 2.5f, 3.0f, 4.0f };
    private readonly float[] colMultipliers = { 1.0f, 2.0f, 3.0f, 3.5f, 5.0f, 6.0f };

    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); }
    }

    public int GetPatternBonus(int targetSize)
    {
        switch (targetSize)
        {
            case 1:
            case 2: return 100;
            case 3: return 200; 
            case 4: return 300;
            case 5: return 400;
            case 6: return 500;
            default: return 0;
        }
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

    public int GetHolePenalty(int holeSize)
    {
        if (holeSize <= 0) return 0;
        if (holeSize == 1) return 500; // 
        if (holeSize == 2) return 700; // 
        return 1000 * holeSize;        // 3 o más: 1000 por cada uno 
    }




}