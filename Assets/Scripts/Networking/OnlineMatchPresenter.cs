using System.Collections.Generic;
using UnityEngine;
using MyGame.Core;

namespace MyGame.Networking
{
    /// <summary>
    /// Capa 1 del modo online: traduce el snapshot autoritativo a la escena (tableros, HUD, mano).
    /// No decide reglas; solo refleja lo que el servidor ya resolvió.
    /// </summary>
    public static class OnlineMatchPresenter
    {
        private static bool hasAppliedOnce;

        public static void ResetPresentation() => hasAppliedOnce = false;

        // La escena debe tener tableros y jugadores creados (GridManager.Start) antes de aplicar
        public static bool IsSceneReady(int playerCount)
        {
            GameManager gm = GameManager.Instance;
            return gm != null && gm.gridManager != null
                && gm.gridManager.allBoardsLogic != null
                && gm.gridManager.allBoardsLogic.Count == playerCount
                && gm.players.Count == playerCount
                && UIManager.Instance != null;
        }

        public static void Apply(NetworkMatchSnapshot snap, int localPlayerIndex, bool isServer)
        {
            GameManager gm = GameManager.Instance;

            // En el host, ServerState ES la autoridad: nunca se sobrescribe con su propio espejo
            if (!isServer) gm.AdoptServerState(snap.ToMatchState());

            int previousPlayer = gm.turnManager.CurrentPlayerIndex;
            bool hadHandInMyTurn = gm.turnManager.HasDrawn && previousPlayer == localPlayerIndex;

            for (int i = 0; i < snap.Players.Length; i++)
            {
                ApplyPlayer(gm.players[i], snap.Players[i]);
                ApplyBoard(gm.gridManager, i, snap.Players[i].Board);
                ApplyCompletionOverlays(gm.gridManager, i, snap.Players[i]);
            }

            DieColor drawnColor = snap.DrawnColor >= 0 ? (DieColor)snap.DrawnColor : DieColor.Red;
            gm.turnManager.SyncFromServer(snap.CurrentPlayerIndex, snap.HasDrawn, drawnColor, snap.DrawnValue);

            if (!hasAppliedOnce || previousPlayer != snap.CurrentPlayerIndex)
            {
                gm.gridManager.SwitchViewTo(snap.CurrentPlayerIndex);
            }

            PlayActionFeedback(gm, snap);
            ApplyHud(gm, snap, localPlayerIndex, hadHandInMyTurn);

            hasAppliedOnce = true;
        }

        private static void ApplyPlayer(PlayerData player, NetworkPlayerSnapshot ps)
        {
            player.name = ps.Name;
            player.score = ps.Score;
            player.placedDice = ps.PlacedDice;
            player.reDraws = ps.ReDraws;
            player.currentUndoUses = ps.UndoUses;
            player.currentMoveUses = ps.MoveUses;
            player.isEliminated = ps.IsEliminated;
            player.isBot = false;
            player.patternCounts = (int[])ps.PatternCounts.Clone();

            // PlacementValidator del cliente usa estos grupos para iluminar casillas válidas
            player.activeGroups.Clear();
            int cols = ps.Board.Cols;
            foreach (NetworkGroupSnapshot g in ps.Groups)
            {
                var group = new GroupData
                {
                    id = g.Id,
                    color = (DieColor)g.Color,
                    targetSize = g.TargetSize,
                    occupiedCells = new List<Vector2Int>(g.Cells.Length)
                };
                foreach (short cell in g.Cells) group.occupiedCells.Add(new Vector2Int(cell / cols, cell % cols));
                player.activeGroups[group.color] = group;
            }
        }

        // El overlay de «patron completado» se deriva siempre del estado del servidor: asi se repinta bien al reconectar
        // y se retira solo al deshacer o mover un dado que rompe la forma. La celebracion (animacion) es aparte.
        private static void ApplyCompletionOverlays(GridManager grid, int playerIndex, NetworkPlayerSnapshot ps)
        {
            int cols = ps.Board.Cols;
            var cells = new HashSet<Vector2Int>();
            if (ps.MatchedCells != null)
            {
                foreach (short index in ps.MatchedCells) cells.Add(new Vector2Int(index / cols, index % cols));
            }
            grid.SetCompletionOverlays(playerIndex, cells);
        }

        // Diff contra la matriz lógica local: solo se tocan las casillas que cambiaron
        private static void ApplyBoard(GridManager grid, int playerIndex, NetworkBoardState board)
        {
            GridManager.DieData[,] logic = grid.GetBoardLogic(playerIndex);
            int rows = logic.GetLength(0);
            int cols = logic.GetLength(1);

            if (rows != board.Rows || cols != board.Cols)
            {
                Debug.LogError($"[OnlineMatchPresenter] Tablero {playerIndex}: el servidor envía {board.Rows}x{board.Cols} y la escena tiene {rows}x{cols}.");
                return;
            }

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int index = r * cols + c;
                    byte raw = board.CompressedCells[index];
                    GridManager.DieData existing = logic[r, c];

                    if (raw == 0)
                    {
                        if (existing != null) grid.RemoveDie(playerIndex, r, c);
                        continue;
                    }

                    DieColor color = (DieColor)((raw >> 4) - 1);
                    int value = raw & 0x0F;
                    int groupId = board.GroupIds[index];

                    if (existing != null && existing.color == color && existing.value == value && existing.groupId == groupId) continue;

                    if (existing != null) grid.RemoveDie(playerIndex, r, c);
                    grid.CommitDieToLogic(playerIndex, r, c, color, groupId, value);
                }
            }
        }

        private static void PlayActionFeedback(GameManager gm, NetworkMatchSnapshot snap)
        {
            NetworkActionSnapshot action = snap.LastAction;
            if (action.PlayerIndex < 0 || action.PlayerIndex >= gm.players.Count) return;

            if (action.Type == NetworkActionType.Undone)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Undo");
                return;
            }

            if (action.Type == NetworkActionType.Moved)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX(action.PatternCompleted ? "Nice" : "Placed");
                if (action.PatternCompleted)
                {
                    if (PopUpManager.Instance != null)
                    {
                        Vector3 movePos = gm.gridManager.GetWorldPosition(action.PlayerIndex, action.Row, action.Col);
                        PopUpManager.Instance.ShowPopUp(movePos, $"PERFECT! +{action.ScoreDelta}", Color.cyan);
                    }
                    CelebrateCompletedPattern(gm, snap, action);
                }
                return;
            }

            if (action.Type != NetworkActionType.Placed) return;

            if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Placed");

            if (PopUpManager.Instance != null)
            {
                Vector3 worldPos = gm.gridManager.GetWorldPosition(action.PlayerIndex, action.Row, action.Col);
                PopUpManager.Instance.ShowPopUp(worldPos, $"+{action.ScoreDelta}", Color.white);
            }

            // Grupo cerrado con forma distinta al patron: sin «Nice» ni highlight
            if (action.PatternCompleted)
            {
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Nice");

                CelebrateCompletedPattern(gm, snap, action);
            }
        }

        // Anima el patron que acaba de completarse: las celdas del grupo (mismo GroupId) que el servidor marco como patron
        private static void CelebrateCompletedPattern(GameManager gm, NetworkMatchSnapshot snap, NetworkActionSnapshot action)
        {
            NetworkPlayerSnapshot ps = snap.Players[action.PlayerIndex];
            int cols = ps.Board.Cols;
            int actionIndex = action.Row * cols + action.Col;
            if (actionIndex < 0 || actionIndex >= ps.Board.GroupIds.Length) return;

            int groupId = ps.Board.GroupIds[actionIndex];
            var matched = new HashSet<int>();
            if (ps.MatchedCells != null) foreach (short index in ps.MatchedCells) matched.Add(index);
            if (!matched.Contains(actionIndex)) return;

            var cells = new List<Vector2Int>();
            for (int i = 0; i < ps.Board.GroupIds.Length; i++)
            {
                if (ps.Board.CompressedCells[i] != 0 && ps.Board.GroupIds[i] == groupId && matched.Contains(i))
                    cells.Add(new Vector2Int(i / cols, i % cols));
            }

            if (cells.Count > 0) gm.gridManager.MarkGroupAsCompleted(action.PlayerIndex, cells);
        }

        // Cada cliente guarda su propio progreso: el servidor dedicado no tiene perfil ni Cloud Save
        private static void RewardLocalPlayer(PlayerData me)
        {
            if (me == null) return;

            int coins = Mathf.Max(0, me.score / 10);
            if (SaveManager.Instance != null) SaveManager.Instance.AddCoins(coins);
            if (CloudSaveManager.Instance != null) _ = CloudSaveManager.Instance.SaveMetaProgress();

            Debug.Log($"[OnlineMatchPresenter] Partida online terminada. Puntaje {me.score}, +{coins} monedas.");
        }

        private static void ApplyHud(GameManager gm, NetworkMatchSnapshot snap, int localPlayerIndex, bool hadHandInMyTurn)
        {
            bool isGameOver = snap.Phase == (byte)MatchPhase.GameOver;
            bool isMyTurn = !isGameOver && localPlayerIndex >= 0 && snap.CurrentPlayerIndex == localPlayerIndex;
            bool hasHandNow = isMyTurn && snap.HasDrawn;
            PlayerData me = localPlayerIndex >= 0 && localPlayerIndex < gm.players.Count ? gm.players[localPlayerIndex] : null;

            int[] bag = snap.BagCountsByColor;
            UIManager.Instance.UpdateDiceCounters(bag[0], bag[1], bag[2], bag[3]);
            if (me != null) UIManager.Instance.UpdateScore(me.score);

            // Solo se puede robar en el turno propio, con la mano vacía y sin haber colocado ya (hay que confirmar)
            bool canDraw = isMyTurn && !snap.HasDrawn && !snap.TurnPlaced;
            UIManager.Instance.SetDrawInputLock(!canDraw);
            if (gm.drawButton != null) gm.drawButton.interactable = canDraw && snap.BagTotal > 0;
            if (gm.reDrawButton != null) gm.reDrawButton.interactable = hasHandNow && !snap.TurnPlaced && me != null && me.reDraws > 0;
            if (gm.reDrawText != null && me != null) gm.reDrawText.text = $"{me.reDraws}";

            // Undo y Move pasan por el servidor y solo valen mientras el turno propio siga abierto
            UIManager.Instance.UpdateUndoUI(me != null ? me.currentUndoUses : 0, isMyTurn && snap.CheckpointCount > 0);
            if (GridInteractionManager.Instance != null)
                GridInteractionManager.Instance.SetOnlineMoveAvailable(isMyTurn && me != null && me.currentMoveUses > 0);

            // Un Re-Draw mantiene la mano llena pero cambia el dado, y un Undo de colocación la devuelve: se repinta la mano
            bool handReplaced = snap.LastAction.Type == NetworkActionType.Drew || snap.LastAction.Type == NetworkActionType.Undone;
            if (hasHandNow && (!hadHandInMyTurn || handReplaced))
            {
                gm.gridManager.ClearHighlights(localPlayerIndex);
                DieColor color = gm.turnManager.CurrentDrawnColor;
                if (me != null && me.activeGroups.TryGetValue(color, out GroupData group))
                {
                    int pIndex = localPlayerIndex;
                    UIManager.Instance.UpdateHandUI(color, group.targetSize, group.occupiedCells.Count, group.targetSize, () =>
                    {
                        gm.gridManager.ShowValidMoves(pIndex, color, group.id, group.targetSize);
                    });
                }
            }
            else if (hasHandNow && snap.LastAction.Type == NetworkActionType.Moved
                && me != null && me.activeGroups.TryGetValue(gm.turnManager.CurrentDrawnColor, out GroupData handGroup))
            {
                // El tablero cambió con un Move: se recalculan las casillas válidas del dado en mano
                gm.gridManager.ClearHighlights(localPlayerIndex);
                gm.gridManager.ShowValidMoves(localPlayerIndex, gm.turnManager.CurrentDrawnColor, handGroup.id, handGroup.targetSize);
            }
            else if (hadHandInMyTurn && !hasHandNow)
            {
                UIManager.Instance.ClearDieUI();
                gm.gridManager.ClearHighlights(localPlayerIndex);
            }

            if (isGameOver && !gm.isGameOver)
            {
                gm.isGameOver = true;
                if (gm.drawButton != null) gm.drawButton.interactable = false;
                if (AudioManager.Instance != null) AudioManager.Instance.PlaySFX("Victory");
                UIManager.Instance.ShowFinalResults(Mathf.Max(0, localPlayerIndex));
                RewardLocalPlayer(me);
            }
        }
    }
}
