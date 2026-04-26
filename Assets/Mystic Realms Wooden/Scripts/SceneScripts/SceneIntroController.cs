using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace LapKan
{
    public class SceneIntroController : SceneBase
    {
        private float currentTime;
        public float coolDownTime = 5;
        public float percent;
        public TextMeshProUGUI txtLoading;
        public Image loadingBar;

        void Start()
        {
            currentTime = coolDownTime;
        }

        void Update()
        {
            currentTime -= Time.deltaTime;
            UpdateText();
            if (currentTime <= 0)
            {
                currentTime = 0;
                UpdateText();
                GoToMainMenu();
            }
        }

        public void UpdateText()
        {
            percent = currentTime / coolDownTime * 100;
            txtLoading.text = "LOADING PROCESS: " + (100 - percent).ToString("#") + " %";
            loadingBar.fillAmount = (100 - percent) / 100;
        }

        public void GoToMainMenu()
        {
            SceneManager.LoadScene("Scene 1 Menu");
        }
    }
}