namespace MyGame.Networking
{
    /// <summary>Recorta los identificadores de jugador antes de escribirlos en el log (no hace falta el id completo para depurar).</summary>
    public static class LogMask
    {
        public static string Id(string id)
        {
            if (string.IsNullOrEmpty(id)) return "(vacio)";
            return id.Length <= 6 ? "***" : id.Substring(0, 4) + "...";
        }
    }
}
