using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LapKan
{
    public class BoardPauseController : BoardBase
    {
        public void OpenBoardOptions()
        {
            FindAnyObjectByType<SceneBase>().OpenBoardOptions();
        }

        public void OnpenBoardConfirm()
        {
            FindAnyObjectByType<SceneBase>().OpenBoardConfirm();
        }

        public void SaveComplete()
        {
            FindAnyObjectByType<SceneBase>().OpenBoardNotice();
        }
    }
}