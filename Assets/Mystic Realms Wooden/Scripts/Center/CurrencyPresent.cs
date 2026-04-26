using UnityEngine;
using TMPro;

namespace LapKan
{
    public class CurrencyPresent : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text gemsText;

        private void Start()
        {
            // Khởi tạo UI lúc start
            UpdateUI("Coins", CurrencyCenter.Instance.GetCoins());
            UpdateUI("Gems", CurrencyCenter.Instance.GetGems());

            // Đăng ký lắng nghe sự kiện thay đổi currency
            CurrencyCenter.Instance.OnCurrencyChanged += UpdateUI;
        }

        private void OnDestroy()
        {
            // Hủy đăng ký khi object bị destroy để tránh memory leak
            if (CurrencyCenter.Instance != null)
                CurrencyCenter.Instance.OnCurrencyChanged -= UpdateUI;
        }

        /// <summary>
        /// Cập nhật UI khi currency thay đổi
        /// </summary>
        private void UpdateUI(string currency, int newValue)
        {
            switch (currency)
            {
                case "Coins":
                    if (coinsText != null) coinsText.text = newValue.ToString();
                    break;
                case "Gems":
                    if (gemsText != null) gemsText.text = newValue.ToString();
                    break;
            }
        }

        /// <summary>
        /// Cho phép gán TMP text runtime (nếu không drag từ inspector)
        /// </summary>
        public void SetUIRefs(TMP_Text coins, TMP_Text gems)
        {
            coinsText = coins;
            gemsText = gems;

            UpdateUI("Coins", CurrencyCenter.Instance.GetCoins());
            UpdateUI("Gems", CurrencyCenter.Instance.GetGems());
        }
    }
}