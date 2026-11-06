using System;
using System.Collections;
using System.Threading.Tasks;
using MyGame.Core;
using UnityEngine;

/// <summary>
/// Juega el turno de un bot en partida local: roba, decide con <see cref="BotBrain"/> (en otro hilo si hace falta pensar mucho),
/// valida la jugada con las reglas visuales y la coloca por el mismo camino que un jugador humano.
/// El nivel sale de la variante elegida (Easy = Greedy, Normal = GreedyPenalized, Hard = MonteCarlo).
/// </summary>
public class BotRunner : MonoBehaviour
{
    private const float ThinkPause = 0.8f;    // para que se vea que el bot "piensa"
    private const float HandAnimPause = 0.7f; // deja terminar la animacion del dado en la mano
    private const int MonteCarloBudgetMs = BotBrain.DefaultBudgetMs;

    private static BotRunner instance;
    private GameManager gm;

    public static void RunTurn(GameManager gameManager)
    {
        if (instance == null)
        {
            instance = new GameObject("BotRunner").AddComponent<BotRunner>();
            instance.gameObject.AddComponent<BotPerfOverlay>();
        }

        instance.gm = gameManager;
        instance.StopAllCoroutines();
        instance.StartCoroutine(instance.PlayTurn());
    }

    private bool TurnStillValid(int playerIndex) =>
        gm != null && !gm.isGameOver && gm.turnManager.CurrentPlayerIndex == playerIndex && !gm.players[playerIndex].isEliminated;

    private BotLevel CurrentLevel()
    {
        VariantData variant = gm.currentVariant;
        return variant != null ? variant.botLevel : BotLevel.Greedy;
    }

    private IEnumerator PlayTurn()
    {
        int pi = gm.turnManager.CurrentPlayerIndex;
        PlayerData bot = gm.players[pi];

        yield return new WaitForSeconds(ThinkPause);

        for (int attempt = 0; attempt < 6; attempt++)
        {
            if (!TurnStillValid(pi)) yield break;

            if (!gm.turnManager.HasDrawn)
            {
                gm.diceManager.DrawDie();
                if (!gm.turnManager.HasDrawn) yield break; // bolsa vacia: el flujo normal cierra la partida
                yield return new WaitForSeconds(HandAnimPause);
                if (!TurnStillValid(pi)) yield break;
            }

            DieColor color = gm.turnManager.CurrentDrawnColor;
            if (!bot.activeGroups.TryGetValue(color, out GroupData group) || group == null) yield break;

            BotLevel level = CurrentLevel();
            BotDecision decision = null;
            BotSnapshot snapshot = BotSnapshot.Capture(gm.ServerState, pi, gm.maxDicePerPlayer);

            if (snapshot != null)
            {
                int seed = Environment.TickCount ^ (pi * 7919);
                float slowestFrame = 0f;

                if (level == BotLevel.MonteCarlo)
                {
                    // El calculo pesado va en otro hilo: la partida sigue fluida mientras el bot piensa
                    Task<BotDecision> task = Task.Run(() => BotBrain.Choose(snapshot, level, seed, MonteCarloBudgetMs));
                    while (!task.IsCompleted)
                    {
                        slowestFrame = Mathf.Max(slowestFrame, Time.unscaledDeltaTime);
                        yield return null;
                    }

                    if (task.IsFaulted)
                    {
                        Debug.LogError("[Bot] Monte Carlo fallo, se usa Greedy penalizado: " + task.Exception?.GetBaseException());
                        decision = BotBrain.Choose(snapshot, BotLevel.GreedyPenalized, seed);
                    }
                    else
                    {
                        decision = task.Result;
                    }
                }
                else
                {
                    decision = BotBrain.Choose(snapshot, level, seed);
                }

                BotPerf.Record(decision, slowestFrame * 1000f);
            }

            if (!TurnStillValid(pi)) yield break;

            if (decision != null && decision.HasMove)
            {
                int r = decision.Cell.X, c = decision.Cell.Y;

                // Red de seguridad: la capa visual manda. Si discrepa del nucleo se usa la primera casilla que acepte
                if (!gm.gridManager.IsValidPlacement(pi, r, c, color, group.id, group.targetSize) && !TryFindVisualMove(pi, color, group, out r, out c))
                {
                    Debug.LogWarning($"[Bot] El nucleo propuso ({decision.Cell.X},{decision.Cell.Y}) pero el tablero visual no la acepta y no hay otra.");
                    decision = null;
                }
                else
                {
                    gm.placementOrchestrator.BeginPlacement(r, c);
                    yield break;
                }
            }

            // Sin jugada: gasta un Re-Draw; sin Re-Draws entra la muerte subita (la gestiona el orquestador)
            if (bot.reDraws > 0)
            {
                gm.diceManager.UseReDraw(true);
                yield return new WaitForSeconds(HandAnimPause);
                continue;
            }

            gm.placementOrchestrator.RefreshPlacementHighlights();
            yield break;
        }
    }

    private bool TryFindVisualMove(int pi, DieColor color, GroupData group, out int row, out int col)
    {
        for (int r = 0; r < gm.gridManager.rows; r++)
        {
            for (int c = 0; c < gm.gridManager.cols; c++)
            {
                if (gm.gridManager.IsValidPlacement(pi, r, c, color, group.id, group.targetSize))
                {
                    row = r;
                    col = c;
                    return true;
                }
            }
        }

        row = col = -1;
        return false;
    }
}
