using System.Text;

namespace MyGame.Core
{
    /// <summary>
    /// Codigo de sala que se comparte entre jugadores. Es el codigo de la sesion de Unity (lo genera el servicio) mas el
    /// numero de jugadores al final, por ejemplo <c>K7M2QX-2</c>. Asi quien se une sabe de antemano si la sala es de 2 o de 4
    /// (el tablero se construye antes de entrar) y no hay que ensenar ni pedir ninguna IP.
    /// </summary>
    public static class RoomCodes
    {
        private const int MinSessionCodeLength = 4;
        private const int MaxSessionCodeLength = 10;

        /// <summary>Codigo para mostrar: el de la sesion en mayusculas, un guion y el numero de jugadores.</summary>
        public static string Format(string sessionCode, int players)
        {
            return (sessionCode ?? string.Empty).Trim().ToUpperInvariant() + "-" + players;
        }

        /// <summary>
        /// Lee lo que escribe el jugador: ignora mayusculas, espacios, guiones y cualquier simbolo. El ultimo caracter debe ser
        /// 2 o 4 (jugadores) y lo anterior el codigo de la sesion.
        /// </summary>
        public static bool TryParse(string input, out string sessionCode, out int players)
        {
            sessionCode = null;
            players = 0;
            if (string.IsNullOrWhiteSpace(input)) return false;

            var clean = new StringBuilder(input.Length);
            foreach (char c in input)
            {
                if (char.IsLetterOrDigit(c) && c < 128) clean.Append(char.ToUpperInvariant(c));
            }

            if (clean.Length < MinSessionCodeLength + 1 || clean.Length > MaxSessionCodeLength + 1) return false;

            char last = clean[clean.Length - 1];
            if (last != '2' && last != '4') return false;

            sessionCode = clean.ToString(0, clean.Length - 1);
            players = last - '0';
            return true;
        }
    }
}
