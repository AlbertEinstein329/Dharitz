using Unity.MLAgents;
using UnityEngine;

public class MLAgentsMetricsController : MonoBehaviour
{
    private StatsRecorder _statsRecorder;

    private void Awake()
    {
        // En Unity 6 / ML-Agents 3.0+, el Academy es estricto en su inicialización.
        _statsRecorder = Academy.Instance.StatsRecorder;
    }

    /// <summary>
    /// Invocar estrictamente en la misma capa donde se llama a EndEpisode() o EndMatch().
    /// </summary>
    public void RecordEpisodeTerminationMetrics(int remainingDicesInBag, int finalScore, GameEndReason endReason)
    {
        if (_statsRecorder == null) return;

        // Inyección de métricas a TensorBoard.
        _statsRecorder.Add("GameStats/DicesLeftInBag", remainingDicesInBag);
        _statsRecorder.Add("GameStats/FinalScore", finalScore);

        // Mapeo numérico del motivo de fin de partida. No registrar strings puros.
        int reasonCode = (int)endReason;
        _statsRecorder.Add("GameStats/TerminationReasonCode", reasonCode);
    }
}

public enum GameEndReason
{
    BagDepleted = 0,
    BoardLocked = 1, // El agente se quedó sin espacio físico/legal
    IllegalMovePenalty = 2,
    MaxStepsReached = 3
}