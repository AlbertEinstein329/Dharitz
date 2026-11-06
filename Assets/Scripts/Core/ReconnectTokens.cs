using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace MyGame.Core
{
    /// <summary>
    /// Secreto de reconexion de cada jugador de una sala. El servidor lo genera al registrar al jugador y se lo entrega solo a
    /// el; para recuperar su asiento con otro ClientId hay que presentar el mismo token. Sin esto, cualquiera que conozca el
    /// PlayerId de otro miembro de la sala podria reclamar su asiento (el PlayerId lo declara el cliente, no es un secreto).
    /// </summary>
    public sealed class ReconnectTokens
    {
        private const int TokenBytes = 16;
        private readonly Dictionary<string, string> byPlayer = new Dictionary<string, string>();

        /// <summary>Crea (o renueva) el token de un jugador y lo devuelve en hexadecimal.</summary>
        public string Issue(string playerId)
        {
            var bytes = new byte[TokenBytes];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            var sb = new StringBuilder(TokenBytes * 2);
            foreach (byte b in bytes) sb.Append(b.ToString("x2"));

            string token = sb.ToString();
            byPlayer[playerId] = token;
            return token;
        }

        /// <summary>true si <paramref name="presented"/> es el token vigente de ese jugador.</summary>
        public bool Verify(string playerId, string presented)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(presented)) return false;
            if (!byPlayer.TryGetValue(playerId, out string expected)) return false;
            return ConstantTimeEquals(expected, presented);
        }

        public bool Has(string playerId) => playerId != null && byPlayer.ContainsKey(playerId);

        public void Clear() => byPlayer.Clear();

        // Comparacion que tarda lo mismo acierte o falle (no revela cuantos caracteres coinciden)
        private static bool ConstantTimeEquals(string a, string b)
        {
            if (a.Length != b.Length) return false;

            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
