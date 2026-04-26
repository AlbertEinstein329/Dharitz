using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LapKan
{
    public class BoardBase : MonoBehaviour
    {
        public UnityEvent CloseBoardEvent;

        public void CloseBoard()
        {
            CloseBoardEvent?.Invoke();
        }

        private void OnDestroy()
        {
            CloseBoardEvent.RemoveAllListeners();
        }
    }
}