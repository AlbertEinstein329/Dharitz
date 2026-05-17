using System.Collections;
using UnityEngine;

public class PlacementOrchestrator
{
    private GameManager gm;
    private Coroutine confirmationRoutine;
    public bool isMoveModeActive = false;
    private Vector2Int? moveOrigin = null;

    public PlacementOrchestrator(GameManager gm)
    {
        this.gm = gm;
    }

    public void ToggleMoveMode()
    {
        isMoveModeActive = !isMoveModeActive;
        moveOrigin = null;
        Debug.Log("Modo Mover: " + (isMoveModeActive ? "ACTIVADO. Haz clic en un dado para moverlo." : "DESACTIVADO."));
    }

    public void HandleMoveClick(int r, int c)
    {
        if (moveOrigin == null)
        {
            // Seleccionando Origen (debe tener un dado)
            var logic = gm.gridManager.GetBoardLogic(gm.turnManager.CurrentPlayerIndex);
            if (logic[r, c] != null)
            {
                moveOrigin = new Vector2Int(r, c);
                Debug.Log($"Origen seleccionado ({r}, {c}). Ahora haz clic en una celda vacía.");
                // Feedback visual: gm.gridManager.allCellsVisual[...].SetHighlight(true) (opcional)
            }
        }
        else
        {
            // Seleccionando Destino (debe estar vacío)
            var logic = gm.gridManager.GetBoardLogic(gm.turnManager.CurrentPlayerIndex);
            if (logic[r, c] == null)
            {
                MoveCommand moveCmd = new MoveCommand(gm, gm.turnManager.CurrentPlayerIndex, moveOrigin.Value.x, moveOrigin.Value.y, r, c);
                CommandManager.Instance.ExecuteCommand(moveCmd);
            }
            
            // Salir del modo mover sin importar si fue válido o no, para resetear el estado
            isMoveModeActive = false;
            moveOrigin = null;
            Debug.Log("Modo Mover: DESACTIVADO.");
        }
    }

    public void BeginPlacement(int row, int col)
    {
        ConfirmAndProcessScore(row, col);
        UIManager.Instance.ClearDieUI();
    }



    public void ConfirmAndProcessScore(int r, int c)
    {
        UIManager.Instance.SetDrawInputLock(true);

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

        Vector3 posMundo = gm.gridManager.GetWorldPosition(gm.turnManager.CurrentPlayerIndex, r, c);

        PatternData currentPattern = gm.currentSession.selectedVariant.GetPattern(group.targetSize);

        int contactosDiagonales = 0;
        int contactosTotales = gm.gridManager.Count3x3Contacts(gm.turnManager.CurrentPlayerIndex, r, c, group.targetSize, out contactosDiagonales);

        SpecialRule reglaActiva = (SpecialRule)(int)currentPattern.specialRule;

        bool isFirstDie = (p.placedDice == 1);

        RuleEvaluationResult result = SpecialRuleEvaluator.EvaluatePlacement(reglaActiva, contactosTotales, contactosDiagonales, isFirstDie);

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
            int conexionesNuevas = gm.gridManager.ScanNewDiagonalConnections(gm.turnManager.CurrentPlayerIndex, r, c, gm.turnManager.CurrentDrawnColor, group.id);
            if (conexionesNuevas > 0)
            {
                int bono = conexionesNuevas * 200;
                p.score += bono;
                PopUpManager.Instance.ShowPopUp(posMundo + Vector3.up * 0.5f, $"+{bono}", Color.magenta);
            }
        }

        int puntosCombo = gm.gridManager.EvaluateAndApplyCombos(gm.turnManager.CurrentPlayerIndex);
        if (puntosCombo > 0)
        {
            p.score += puntosCombo;
            PopUpManager.Instance.ShowPopUp(posMundo + Vector3.up * 1f, $"COMBO! +{puntosCombo}", Color.yellow);
        }

        if (group.isClosed)
        {
            if (result.IsPatternValid && PatternValidator.CheckPattern(group.occupiedCells, currentPattern))
            {
                p.patternCounts[group.targetSize]++;
                int bonoPatron = ScoreManager.Instance.GetPatternBonus(group.targetSize);
                p.score += bonoPatron;
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

        if (gm.diceManager.diceBag.Count == 0 && !gm.turnManager.HasDrawn)
        {
            gm.EndMatch();
        }
        else
        {
            gm.StartCoroutine(gm.turnManager.TurnTransitionPause());
        }
    }
}
