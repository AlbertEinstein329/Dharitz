using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LapKan
{
    public class ScenePlayController : SceneBase
    {
        public void DisplaySuccessBoard()
        {
            GoToScene("Scene 3 Success");
        }

        public void DisplayFailBoard()
        {
            GoToScene("Scene 8 Fail");
        }
    }
}