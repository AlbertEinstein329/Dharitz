using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace LapKan
{
    public class MissionProgressPresent : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Slider progressSlider;
        [SerializeField] private TMP_Text progressText;
        [SerializeField] private Image progressFill;

        [SerializeField] private TMP_Text currentScore;

        [Header("Animation Settings")]
        [SerializeField] private float barLerpSpeed = 5f; // tốc độ mượt cho progress bar
        [SerializeField] private float textPopScale = 1.2f; // scale khi pop
        [SerializeField] private float textPopDuration = 0.2f;

        private int TargetScore => MissionCenter.Instance != null ? MissionCenter.Instance.TargetScore : 0;
        private int CurrentScore => ScoreCenter.Instance != null ? ScoreCenter.Instance.CurrentScore : 0;

        private float targetFill;   // tỉ lệ thực (CurrentScore / TargetScore)
        private Vector3 originalTextScale;
        private float popTimer;

        private void Start()
        {
            if (progressSlider != null) progressSlider.value = 0;
            if (progressFill != null) progressFill.fillAmount = 0;
            if (progressText != null) originalTextScale = progressText.transform.localScale;

            UpdateProgressImmediate();
        }

        private void Update()
        {
            UpdateProgressSmooth();
            AnimateTextPop();
        }

        private void UpdateProgressImmediate()
        {
            if (TargetScore <= 0) return;
            targetFill = Mathf.Clamp01((float)CurrentScore / TargetScore);
            if (progressSlider != null) progressSlider.value = targetFill;
            if (progressFill != null) progressFill.fillAmount = targetFill;

            if (progressText != null)
                progressText.text = $"{CurrentScore} / {TargetScore}";

            if (currentScore != null)
                currentScore.text = $"{CurrentScore}";
        }

        private void UpdateProgressSmooth()
        {
            if (TargetScore <= 0) return;

            // Tính targetFill từ điểm số
            float newTargetFill = Mathf.Clamp01((float)CurrentScore / TargetScore);
            if (Mathf.Abs(newTargetFill - targetFill) > 0.001f)
            {
                // Có thay đổi score → trigger pop text
                TriggerTextPop();
            }
            targetFill = newTargetFill;

            // Lerp thanh progress cho mượt
            if (progressSlider != null)
            {
                progressSlider.value = Mathf.Lerp(progressSlider.value, targetFill, Time.deltaTime * barLerpSpeed);
            }

            if (progressFill != null)
            {
                progressFill.fillAmount = Mathf.Lerp(progressFill.fillAmount, targetFill, Time.deltaTime * barLerpSpeed);
            }

            // Update text hiển thị
            if (progressText != null)
                progressText.text = $"{CurrentScore} / {TargetScore}";

            if (currentScore != null)
                currentScore.text = $"{CurrentScore}";
        }

        private void TriggerTextPop()
        {
            popTimer = textPopDuration;
            if (progressText != null)
                progressText.transform.localScale = originalTextScale * textPopScale;
        }

        private void AnimateTextPop()
        {
            if (progressText == null) return;

            if (popTimer > 0)
            {
                popTimer -= Time.deltaTime;
                if (popTimer <= 0)
                {
                    // Trở về scale gốc
                    progressText.transform.localScale = originalTextScale;
                }
                else
                {
                    // Lerp về scale gốc
                    progressText.transform.localScale = Vector3.Lerp(
                        progressText.transform.localScale,
                        originalTextScale,
                        Time.deltaTime * (1f / textPopDuration) * 10f
                    );
                }
            }
        }
    }
}