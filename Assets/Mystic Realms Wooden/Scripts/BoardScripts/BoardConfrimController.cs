using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LapKan
{
    public class BoardConfrimController : BoardBase
    {
        public void CancelQuit()
        {
            FindAnyObjectByType<SceneBase>().ClosePanel();
        }

        public void QuitGamePlay()
        {
            FindAnyObjectByType<SceneBase>().GoToMenu();
        }
    }
}
