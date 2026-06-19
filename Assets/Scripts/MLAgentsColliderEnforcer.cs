using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class MLAgentsColliderEnforcer : MonoBehaviour
{
    private void Awake()
    {
        // 1. Configuración del colisionador (Esto sí es estático y va en Awake)
        BoxCollider col = GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1f, 1f, 0.5f);

        // 2. Aislamiento de Capa Inmediato
        int mlLayer = LayerMask.NameToLayer("MLGrid");
        if (mlLayer != -1)
        {
            gameObject.layer = mlLayer;
        }
    }

    /// <summary>
    /// DEBE ser invocado por el PlacementOrchestrator o GridManager
    /// inmediatamente después de instanciar el dado en la celda.
    /// </summary>
    public void InjectSensorData(DieColor assignedColor)
    {
        gameObject.tag = GetTagForColor(assignedColor);
    }

    private string GetTagForColor(DieColor color)
    {
        return color switch
        {
            DieColor.Red => "DieRed",
            DieColor.Blue => "DieBlue",
            DieColor.White => "DieWhite",
            DieColor.Black => "DieBlack",
            _ => "Untagged"
        };
    }
}