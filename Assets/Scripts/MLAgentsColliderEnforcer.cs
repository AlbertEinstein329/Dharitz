using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class MLAgentsColliderEnforcer : MonoBehaviour
{
    private void Awake()
    {
        BoxCollider col = GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1f, 1f, 0.5f);

        int mlLayer = LayerMask.NameToLayer("MLGrid"); // Asegúrate de que esta capa exista en tu proyecto
        if (mlLayer != -1)
        {
            gameObject.layer = mlLayer;
        }
    }

    // 🔥 AHORA RECIBE EL COLOR Y EL NÚMERO
    public void InjectSensorData(DieColor assignedColor, int dieValue)
    {
        gameObject.tag = GetTagForDie(assignedColor, dieValue);
    }

    // Constructor automático del Tag
    private string GetTagForDie(DieColor color, int value)
    {
        return $"Die{color}{value}";
    }
}