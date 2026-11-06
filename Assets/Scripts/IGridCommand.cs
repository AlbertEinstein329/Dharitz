public interface IGridCommand
{
    void Execute();
    /// <summary>Devuelve false si la Capa 0 rechazo la reversion (el comando debe conservarse).</summary>
    bool Undo();
}
