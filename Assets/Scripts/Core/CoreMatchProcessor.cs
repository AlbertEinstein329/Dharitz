using System;
using System.Collections.Generic;
using System.Linq;

namespace MyGame.Core
{
    public static class CoreMatchProcessor
    {
        // ÚNICO PUNTO DE ENTRADA: El cliente pide colocar un dado, el servidor decide si es legal.
        public static bool ProcessPlacementIntent(MatchStateDTO state, PlaceDieCommand command, PatternDefDTO variantPattern, PatternDefDTO newPattern, int rowBonus, int colBonus, int intersectionBonus, float[] rowMults, float[] colMults)
        {
            // 1. Verificaciones de Autoridad (Anti-Cheat)
            if (state.CurrentPhase != MatchPhase.PlayerTurn) return false;
            if (state.CurrentPlayerIndex != command.PlayerId) return false;

            if (!state.PlayerBoards.ContainsKey(command.PlayerId) || !state.PlayerProfiles.ContainsKey(command.PlayerId))
                return false;

            BoardStateDTO board = state.PlayerBoards[command.PlayerId];
            PlayerDataDTO player = state.PlayerProfiles[command.PlayerId];

            int targetIndex = board.GetIndex(command.TargetCell.X, command.TargetCell.Y);
            if (targetIndex < 0 || targetIndex >= board.Cells.Length) return false; // Fuera de límites
            if (board.Cells[targetIndex].IsOccupied) return false; // Celda ocupada

            // 2. Motor Topológico Matemático
            bool isSurvivalValid = CoreTopologyCalculator.ValidateSurvival(
                board,
                command.TargetCell.X,
                command.TargetCell.Y,
                command.Color,
                command.GroupId,
                command.Number,
                player,
                variantPattern,
                newPattern
            );

            if (!isSurvivalValid) return false;

            // ==========================================
            // MUTACIÓN DE ESTADO AUTORITATIVO
            // ==========================================

            // 3. Comprometer el dado en el tablero 1D
            board.Cells[targetIndex] = new CellStateDTO
            {
                IsOccupied = true,
                Color = command.Color,
                GroupId = command.GroupId,
                Value = command.Number
            };

            player.PlacedDice++;

            // 4. Actualizar Grupos Activos
            if (!player.ActiveGroups.ContainsKey(command.Color))
            {
                player.ActiveGroups[command.Color] = new GroupDataDTO(command.GroupId, command.Color, command.Number);
            }
            player.ActiveGroups[command.Color].OccupiedCells.Add(command.TargetCell);

            // 5. Motor de Puntaje y Combos
            int basePoints = 50;
            int comboPoints = CoreScoreCalculator.EvaluateAndApplyCombos(
                board.Cells, board.Rows, board.Cols, player,
                rowBonus, colBonus, intersectionBonus, rowMults, colMults
            );

            player.Score += (basePoints + comboPoints);

            // 6. Validación de Cierre de Patrón
            GroupDataDTO currentGroup = player.ActiveGroups[command.Color];
            if (currentGroup.IsClosed)
            {
                PatternDefDTO targetPatternData = variantPattern ?? newPattern;

                // 2. APLICAMOS EL PUENTE DE TIPO .ToList() AL HASHSET
                if (targetPatternData != null && CorePatternValidator.CheckPattern(currentGroup.OccupiedCells.ToList(), targetPatternData))
                {
                    if (command.Number >= 1 && command.Number <= 6)
                    {
                        player.PatternCounts[command.Number]++;
                    }

                    int patternBonus = CalculatePatternBonus(command.Number);
                    player.Score += patternBonus;
                }
            }

            // El comando fue procesado con éxito
            return true;
        }

        private static int CalculatePatternBonus(int targetSize)
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
    }
}