using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LapKan
{
    public class SceneBoardController : SceneBase
    {
        public void RestartScene()
        {
            GoToScene("Scene 5 Play");
            //SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}