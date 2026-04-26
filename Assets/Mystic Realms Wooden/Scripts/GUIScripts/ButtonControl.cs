using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LapKan
{
    public class ButtonControl : MonoBehaviour
    {
        public Image image;
        public Image imageButton;

        public Sprite spriteOn;
        public Sprite spriteOff;
        public Sprite spiteButtonOn;
        public Sprite spiteButtonOff;

        public bool isOn = true;

        public void ChangeState()
        {
            isOn = !isOn;
            UpdateDisplay();
        }

        public void UpdateDisplay()
        {
            if (isOn)
            {
                image.sprite = spriteOn;
                imageButton.sprite = spiteButtonOn;
            }
            else
            {
                image.sprite = spriteOff;
                imageButton.sprite = spiteButtonOff;
            }
        }
    }
}