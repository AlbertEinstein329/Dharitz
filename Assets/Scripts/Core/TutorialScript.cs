using System.Collections.Generic;

namespace MyGame.Core
{
    /// <summary>Un dado del guion del tutorial: color y numero de dados del grupo (solo vale si el grupo es nuevo).</summary>
    public struct ScriptedDraw
    {
        public DieColor Color;
        public int Value;

        public ScriptedDraw(DieColor color, int value)
        {
            Color = color;
            Value = value;
        }
    }

    /// <summary>
    /// Dados fijos del tutorial. El orden de robo no depende del RNG: asi cada paso (colocar, cerrar un patron, deshacer,
    /// re-draw) se explica con el dado exacto que toca. Cuando la lista se agota, la partida sigue con la bolsa normal.
    /// </summary>
    public static class TutorialScript
    {
        public static List<ScriptedDraw> CreateDraws()
        {
            return new List<ScriptedDraw>
            {
                new ScriptedDraw(DieColor.Red, 2),    // 1: primer dado de un grupo de 2
                new ScriptedDraw(DieColor.Red, 2),    // 2: el mismo grupo (el valor se ignora: el grupo ya existe)
                new ScriptedDraw(DieColor.Blue, 1),   // 3: grupo de 1 dado: se cierra solo; sirve para enseñar Undo
                new ScriptedDraw(DieColor.White, 3),  // 4: dado que se devuelve con Re-draw
                new ScriptedDraw(DieColor.Black, 1),  // 5: el dado que llega tras el Re-draw
            };
        }
    }
}
