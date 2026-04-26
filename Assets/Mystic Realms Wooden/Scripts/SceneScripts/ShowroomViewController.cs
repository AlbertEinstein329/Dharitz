using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace LapKan
{
    public class ShowroomViewController : MonoBehaviour
    {
        [Header("Page Settings")]
        public int numbPage = 0;        // Trang hiện tại
        public int maxPage = 4;         // Trang tối đa

        [Header("UI Buttons")]
        public Button PrevSceneButton;
        public Button NextSceneButton;
        public Button BackToMenuButton;

        private void Start()
        {
            // Lấy numbPage từ PlayerPrefs nếu có
            if (PlayerPrefs.HasKey("ShowroomPage"))
                numbPage = PlayerPrefs.GetInt("ShowroomPage");

            UpdateButtonVisibility();

            // Gán sự kiện nút
            PrevSceneButton.onClick.AddListener(OnPrevPage);
            NextSceneButton.onClick.AddListener(OnNextPage);
            BackToMenuButton.onClick.AddListener(OnBackToMenu);
        }

        private void UpdateButtonVisibility()
        {
            if (numbPage < 0) numbPage = 0;

            if (numbPage == 0)
            {
                PrevSceneButton.gameObject.SetActive(false);
                NextSceneButton.gameObject.SetActive(true);
            }
            else if (numbPage == maxPage)
            {
                PrevSceneButton.gameObject.SetActive(true);
                NextSceneButton.gameObject.SetActive(false);
            }
            else if (numbPage > maxPage)
            {
                PrevSceneButton.gameObject.SetActive(false);
                NextSceneButton.gameObject.SetActive(false);
            }
            else
            {
                PrevSceneButton.gameObject.SetActive(true);
                NextSceneButton.gameObject.SetActive(true);
            }
        }

        private void OnPrevPage()
        {
            numbPage--;
            PlayerPrefs.SetInt("ShowroomPage", numbPage);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void OnNextPage()
        {
            numbPage++;
            PlayerPrefs.SetInt("ShowroomPage", numbPage);
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private void OnBackToMenu()
        {
            SceneManager.LoadScene("SceneCentral");
        }
    }
}