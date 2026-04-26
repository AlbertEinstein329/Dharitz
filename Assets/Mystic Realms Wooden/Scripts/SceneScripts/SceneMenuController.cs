using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace LapKan
{
    public class SceneMenuController : SceneBase
    {
        public void GoToPlayScene()
        {
            GoToScene("Scene 5 Play");
        }

        public void GoToOptions()
        {
            GoToScene("Scene 9 Options");
        }

        public void QuitGame()
        {
            Application.Quit();
        }
    }
}