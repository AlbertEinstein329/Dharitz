using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LapKan
{
    public class BoardNoticeController : BoardBase
    {
        public void Continues()
        {
            FindAnyObjectByType<SceneBase>().ClosePanel();
        }
    }
}