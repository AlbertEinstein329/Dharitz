using UnityEngine;
using System;

namespace LapKan
{
    public class CurrencyCenter : MonoBehaviour
    {
        public static CurrencyCenter Instance { get; private set; }

        private const string CoinsKey = "Currency_Coins";
        private const string GemsKey = "Currency_Gems";

        public event Action<string, int> OnCurrencyChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // init nếu chưa có
            if (!PlayerPrefs.HasKey(CoinsKey)) PlayerPrefs.SetInt(CoinsKey, 0);
            if (!PlayerPrefs.HasKey(GemsKey)) PlayerPrefs.SetInt(GemsKey, 0);
        }

        #region Public API

        public int GetCoins() => PlayerPrefs.GetInt(CoinsKey, 0);
        public int GetGems() => PlayerPrefs.GetInt(GemsKey, 0);

        public void AddCoins(int amount) => SetCoins(GetCoins() + amount);
        public void AddGems(int amount) => SetGems(GetGems() + amount);

        public bool SpendCoins(int amount)
        {
            if (GetCoins() < amount) return false;
            SetCoins(GetCoins() - amount);
            return true;
        }

        public bool SpendGems(int amount)
        {
            if (GetGems() < amount) return false;
            SetGems(GetGems() - amount);
            return true;
        }

        public void SetCoins(int value)
        {
            PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
            OnCurrencyChanged?.Invoke("Coins", GetCoins());
        }

        public void SetGems(int value)
        {
            PlayerPrefs.SetInt(GemsKey, Mathf.Max(0, value));
            PlayerPrefs.Save();
            OnCurrencyChanged?.Invoke("Gems", GetGems());
        }

        #endregion
    }
}