using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LapKan
{
    public class ShopPanel : BasePanel
    {
        public void Purchased(string contentStr)
        {
            ConfirmPanel.Instance.Show(contentStr, () =>
            {
            // Purchase code here
            Hide();
            });
        }
    }
}