using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LapKan
{
    public class PlayerHUDController : MonoBehaviour
    {
        public SceneBase boardController;

        void Start()
        {
            boardController = FindAnyObjectByType<SceneBase>();
        }

        public void OpenBoardOptions()
        {
            boardController.OpenBoardOptions();
        }

        public void OpenBoardPause()
        {
            boardController.OpenBoardPause();
        }

        public void BackScene()
        {
            boardController.BackScene();
        }

        public void GoToCredit()
        {
            boardController.GoToCredit();
        }

        public void GoToShopCoin()
        {
            boardController.GoToScene("Scene 14 Shop Golden Coin");
        }

        public void GoToShopGem()
        {
            boardController.GoToScene("Scene 15 Shop Bloody Gem");
        }
    }
}