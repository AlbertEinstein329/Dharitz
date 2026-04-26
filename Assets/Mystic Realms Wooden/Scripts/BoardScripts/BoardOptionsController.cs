using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LapKan
{
    public class BoardOptionsController : BoardBase
    {
        public void SaveComplete()
        {
            // Save before Go
            FindAnyObjectByType<SceneBase>().OpenBoardNotice();
        }
    }
}