using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BoardPlayerDisplay : MonoBehaviour
{
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Image avatarImage;
    [SerializeField] private Sprite[] allAvatars; // Asigna aquí la misma lista que usas en el Menú

    public void Setup(string playerName, int avatarId)
    {
        nameText.text = playerName;
        if (avatarId >= 0 && avatarId < allAvatars.Length)
        {
            avatarImage.sprite = allAvatars[avatarId];
        }
    }
}