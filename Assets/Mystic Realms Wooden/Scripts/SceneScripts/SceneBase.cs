using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.UI;
using UnityEngine.Events;

namespace LapKan
{
    public class SceneBase : MonoBehaviour
    {
        public const string strBackScene = "BackScene";

        [Header("Base Scene")]
        private string backSceneName = "Scene 1 Menu";
        public string menuSceneName = "Scene 1 Menu";
        public string creditSceneName = "Scene 10 Credit";

        private void Start()
        {
            backSceneName = PlayerPrefs.GetString(strBackScene, "Scene 1 Menu");
        }

        public void GoToScene(string sceneName)
        {
            PlayerPrefs.SetString(strBackScene, SceneManager.GetActiveScene().name);
            Debug.Log("Back Scene is: " + SceneManager.GetActiveScene().name);
            SceneManager.LoadScene(sceneName);
        }

        public void BackScene()
        {
            GoToScene(backSceneName);
        }

        public void GoToMenu()
        {
            GoToScene(menuSceneName);
        }

        public void GoToCredit()
        {
            GoToScene(creditSceneName);
        }

        [Header("Board Control")]
        public GameObject canvas;
        public GameObject blackDrop;

        public GameObject boardOptions;
        public GameObject boardSucess;
        public GameObject boardFail;
        public GameObject boardPause;
        public GameObject boardConfirm;
        public GameObject boardNotice;

        private GameObject boardCurrent;
        private GameObject blackDropCurrent;

        public UnityEvent CloseBoardEvent;

        public GameObject boardCanvas;

        public void DimissBoard()
        {
            if (boardCurrent != null)
            {
                DestroyImmediate(boardCurrent);
            }

            if (blackDropCurrent != null)
            {
                DestroyImmediate(blackDropCurrent);
            }
        }

        public void OpenBoard(GameObject boardObj)
        {
            GameObject blackDropN = Instantiate(blackDrop, transform.position, Quaternion.identity, canvas.transform);
            if (boardCanvas != null)
                blackDropN.transform.SetParent(boardCanvas.transform);
            blackDropCurrent = blackDropN;
            blackDropN.transform.position = Vector3.zero;

            GameObject board = Instantiate(boardObj, transform.position, Quaternion.identity, canvas.transform);
            if (boardCanvas != null)
                board.transform.SetParent(boardCanvas.transform);
            boardCurrent = board;
            board.transform.position = Vector3.zero;

            if (boardCurrent.GetComponent<BoardBase>())
                boardCurrent.GetComponent<BoardBase>().CloseBoardEvent.AddListener(ClosePanel);

            if (boardCurrent.GetComponent<BasePanel>())
                boardCurrent.GetComponent<BasePanel>().ClosePanelEvent.AddListener(ClosePanel);
        }

        public void ClosePanel()
        {
            if (boardCurrent != null)
            {
                DestroyImmediate(boardCurrent);
            }

            if (blackDropCurrent != null)
            {
                DestroyImmediate(blackDropCurrent);
            }
        }

        public void OpenBoardOptions()
        {
            DimissBoard();
            OpenBoard(boardOptions);
        }

        public void OpenBoardNotice()
        {
            DimissBoard();
            OpenBoard(boardNotice);
        }

        public void OpenBoardConfirm()
        {
            DimissBoard();
            OpenBoard(boardConfirm);
        }

        public void OpenBoardSuccess()
        {
            DimissBoard();
            OpenBoard(boardSucess);
        }

        public void OpenBoardFail()
        {
            DimissBoard();
            OpenBoard(boardFail);
        }

        public void OpenBoardPause()
        {
            DimissBoard();
            OpenBoard(boardPause);
        }
    }
}