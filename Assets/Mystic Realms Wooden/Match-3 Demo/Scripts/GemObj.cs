using UnityEngine;

namespace LapKan
{
    public class GemObj : MonoBehaviour
    {
        public enum GemType { Red, Yellow, Purple, Blue, Pink, Green }

        [Header("Sprites Config (6 cái, đúng thứ tự type)")]
        public Sprite[] ListGemSprite;

        public GemType gemType;
        public int gx;
        public int gy;

        public SpriteRenderer tileSymbol;
        public SpriteRenderer tileBack;

        void Awake()
        {
            //tileSymbol = GetComponent<SpriteRenderer>();
        }

        public void InitRandomGem()
        {
            if (ListGemSprite == null || ListGemSprite.Length == 0)
            {
                Debug.LogError("ListGemSprite is empty!");
                return;
            }
            int index = Random.Range(0, Mathf.Min(6, ListGemSprite.Length));
            tileSymbol.sprite = ListGemSprite[index];
            gemType = (GemType)index;
        }

        public void SetTypeByIndex(int index)
        {
            index = Mathf.Clamp(index, 0, ListGemSprite.Length - 1);
            tileSymbol.sprite = ListGemSprite[index];
            gemType = (GemType)index;
        }

        private void OnMouseDown()
        {
            if (GridManager2D.Instance == null) return;
            if (GridManager2D.Instance.IsBusy) return; // đang anim
            GridManager2D.Instance.SelectGem(this);
        }

        public void SetHighlight(bool on)
        {
            if (!tileSymbol) tileSymbol = GetComponent<SpriteRenderer>();
            tileSymbol.color = on ? new Color(1f, 0.9f, 0.4f, 1f) : Color.white;
        }
    }
}