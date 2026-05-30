using System.Collections;
using UnityEngine;

public class PlacementOrchestrator
{
    private GameManager gm;
    private Coroutine confirmationRoutine;
    public bool isMoveModeActive = false;

    public PlacementOrchestrator(GameManager gm)
    {
        this.gm = gm;
    }

    public void BeginPlacement(int row, int col)
    {
        ConfirmAndProcessScore(row, col);
        UIManager.Instance.ClearDieUI();
    }



    public void ConfirmAndProcessScore(int r, int c)
    {
        UIManager.Instance.SetDrawInputLock(true);

        // ==========================================
        // ESCUDO ANTI-EXPLOIT: Bloqueo de Transición Inmediato
        // ==========================================
        if (CommandManager.Instance != null) CommandManager.Instance.isTransitioning = true;
        if (GridInteractionManager.Instance != null) GridInteractionManager.Instance.LockUIForTransition();

        if (gm.reDrawButton != null) gm.reDrawButton.interactable = false;

        PlayerData p = gm.turnManager.GetCurrentPlayer();
        GroupData group = p.activeGroups[gm.turnManager.CurrentDrawnColor];

        // Creamos el UndoCommand con el estado actual ANTES de alterar las lógicas y puntajes
        UndoCommand undoCmd = new UndoCommand(gm, gm.turnManager.CurrentPlayerIndex, r, c, gm.turnManager.CurrentDrawnColor, group.id, group.targetSize);
        if (CommandManager.Instance != null) CommandManager.Instance.RegisterCommand(undoCmd);

        gm.gridManager.CommitDieToLogic(gm.turnManager.CurrentPlayerIndex, r, c, gm.turnManager.CurrentDrawnColor, group.id, group.targetSize);

        group.occupiedCells.Add(new Vector2Int(r, c));
        p.placedDice++;
        
        gm.gridManager.ClearHighlights(gm.turnManager.CurrentPlayerIndex);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX("Placed");
        }

        Vector3 posMundo = gm.gridManager.GetWorldPosition(gm.turnManager.CurrentPlayerIndex, r, c);

        PatternData currentPattern = gm.currentSession.selectedVariant.GetPattern(group.targetSize);

        int orthContacts = gm.gridManager.GetOrthogonalConnections(gm.turnManager.CurrentPlayerIndex, r, c, gm.turnManager.CurrentDrawnColor, group.id);
        int diagContacts = gm.gridManager.GetDiagonalConnections(gm.turnManager.CurrentPlayerIndex, r, c, gm.turnManager.CurrentDrawnColor, group.id);

        SpecialRule reglaActiva = (SpecialRule)(int)currentPattern.specialRule;

        bool isFirstDie = (p.placedDice == 1);

        RuleEvaluationResult result = SpecialRuleEvaluator.EvaluatePlacement(reglaActiva, orthContacts, diagContacts, isFirstDie);

        if (result.ScoreDelta < 0)
        {
            p.score += result.ScoreDelta;
            PopUpManager.Instance.ShowPopUp(posMundo, $"{result.ScoreDelta}", Color.red);
        }
        else
        {
            int puntosGanados = 50 + result.ScoreDelta;
            p.score += puntosGanados;
            PopUpManager.Instance.ShowPopUp(posMundo, $"+{puntosGanados}", Color.white);
        }

        if (currentPattern != null && reglaActiva == SpecialRule.ExtraDiagonalContact)
        {
            // EL ÚNICO CAMBIO: Usamos el nuevo puente GetDiagonalConnections
            int conexionesNuevas = gm.gridManager.GetDiagonalConnections(gm.turnManager.CurrentPlayerIndex, r, c, gm.turnManager.CurrentDrawnColor, group.id);

            if (conexionesNuevas > 0)
            {
                int bono = conexionesNuevas * 200;
                p.score += bono;
                // Esto asume que tienes posMundo definido antes en tu código
                PopUpManager.Instance.ShowPopUp(posMundo + Vector3.up * 0.5f, $"+{bono}", Color.magenta);
            }
        }

        int puntosCombo = gm.gridManager.EvaluateAndApplyCombos(gm.turnManager.CurrentPlayerIndex);
        if (puntosCombo > 0)
        {
            p.score += puntosCombo;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Nice");
            PopUpManager.Instance.ShowPopUp(posMundo + Vector3.up * 1f, $"COMBO! +{puntosCombo}", Color.yellow);
        }

        if (group.isClosed)
        {
            if (result.IsPatternValid && PatternValidator.CheckPattern(group.occupiedCells, currentPattern))
            {
                p.patternCounts[group.targetSize]++;
                int bonoPatron = ScoreManager.Instance.GetPatternBonus(group.targetSize);
                p.score += bonoPatron;
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Nice");
                PopUpManager.Instance.ShowPopUp(posMundo + Vector3.down * 1f, $"PERFECT! +{bonoPatron}", Color.cyan);
                
                gm.gridManager.MarkGroupAsCompleted(gm.turnManager.CurrentPlayerIndex, group.occupiedCells);
            }
        }

        UIManager.Instance.UpdateProgressText(group.color, group.targetSize, group.occupiedCells.Count, group.targetSize);

        if (gm.AreAllPlayersFinished())
        {
            gm.StartCoroutine(gm.EndGameSequence());
        }
        else
        {
            UIManager.Instance.UpdateScore(p.score);
        }

        gm.turnManager.HasDrawn = false;

        if (gm.diceManager.GetTotalDiceLeft() == 0 && !gm.turnManager.HasDrawn)
        {
            gm.EndMatch();
            return;
        }
        else
        {
            gm.StartCoroutine(gm.turnManager.TurnTransitionPause());
        }
    }

    /// <summary>
    /// Recalcula e ilumina las celdas válidas para el dado que el jugador tiene actualmente en la mano.
    /// Invocado después de usar un comodín (Move/Undo) para restaurar el estado visual.
    /// </summary>
    public void RefreshPlacementHighlights()
    {
        // 1. Verificación de estado: Si no hay dado en la mano, no hay nada que iluminar.
        if (!gm.turnManager.HasDrawn)
        {
            gm.gridManager.ClearHighlights(gm.turnManager.CurrentPlayerIndex);
            return;
        }

        // 2. Limpieza de seguridad antes de recalcular
        int pIndex = gm.turnManager.CurrentPlayerIndex;
        gm.gridManager.ClearHighlights(pIndex);

        // 3. Recopilación de datos inyectables
        PlayerData p = gm.turnManager.GetCurrentPlayer();
        DieColor drawnColor = gm.turnManager.CurrentDrawnColor;

        // Prevención de NullReference si el grupo aún no existe
        if (!p.activeGroups.ContainsKey(drawnColor)) return;

        GroupData group = p.activeGroups[drawnColor];
        VariantData variant = gm.currentSession.selectedVariant;

        // Obtenemos la matriz lógica actual
        var logic = gm.gridManager.GetBoardLogic(pIndex);

        bool foundValidSpot = false;

        // 4. Escaneo del tablero (8x10)
        for (int r = 0; r < 10; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                // Inyectamos la información al Validador
                bool isValid = PlacementValidator.IsValidPlacement(
                    logic,
                    10, 8,
                    r, c,
                    drawnColor,
                    group.id,
                    group.targetSize,
                    p,
                    variant
                );

                if (isValid)
                {
                    
                    CellComponent cell = gm.gridManager.allCellsVisual[pIndex][r, c];
                    if (cell != null)
                    {
                        cell.SetHighlight(true);
                        foundValidSpot = true;
                    }
                }
            }
        }

        if (!foundValidSpot)
        {
            Debug.LogWarning("[UX] Softlock topológico detectado. Evaluando supervivencia...");

            UIManager.Instance.ClearDieUI();
            UIManager.Instance.SetDrawInputLock(false);
            gm.turnManager.HasDrawn = false;


            if (p.reDraws > 0)
            {
                // SE SALVA (AÚN TIENE REDRAWS)
                if (gm.reDrawButton != null) gm.reDrawButton.interactable = true;
                if (PopUpManager.Instance != null)
                {
                    PopUpManager.Instance.ShowPopUp(Vector3.up * 2f, "DADO INJUGABLE\n¡Usa un Re-Draw!", Color.yellow);
                }
            }
            else
            {
                // MUERTE SÚBITA (0 REDRAWS)
                p.isEliminated = true;

                // Calculamos cuántas rondas le quedaban (Ej: 52 dados máximos por tablero)
                int maxDadosPorJugador = 52;
                int rondasFaltantes = maxDadosPorJugador - p.placedDice;

                // Contamos cuántos jugadores NO están eliminados
                int jugadoresVivos = 0;
                // NOTA: Ajusta esto a cómo se llame tu lista de jugadores en GameManager o TurnManager
                // Asumo que tienes algo como gm.turnManager.GetAllPlayers() o puedes consultarlo
                foreach (var player in gm.players) // Reemplaza 'playerList' por tu lista real
                {
                    if (!player.isEliminated) jugadoresVivos++;
                }

                if (jugadoresVivos == 0)
                {
                    // SINGLE PLAYER O ÚLTIMO JUGADOR VIVO: Se acaba la partida de inmediato
                    if (PopUpManager.Instance != null)
                        PopUpManager.Instance.ShowPopUp(Vector3.up * 2f, "TABLERO MUERTO", Color.red);

                    gm.EndMatch();
                }
                else
                {
                    // MULTIJUGADOR: Quedan otros. Quemamos sus dados y pasamos turno
                    if (rondasFaltantes > 0)
                    {
                        gm.diceManager.BurnRandomDice(rondasFaltantes);
                    }

                    if (PopUpManager.Instance != null)
                        PopUpManager.Instance.ShowPopUp(Vector3.up * 2f, "¡JUGADOR ELIMINADO!", Color.red);

                    // Pasamos turno forzosamente
                    gm.turnManager.EndTurn();
                }
            }
            return;
        }

    }

}
