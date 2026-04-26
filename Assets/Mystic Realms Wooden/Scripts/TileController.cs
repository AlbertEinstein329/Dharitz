using UnityEngine;

namespace LapKan
{
    public class TileController : MonoBehaviour
    {
        [Header("Renderers")]
        public SpriteRenderer tileBack;
        public SpriteRenderer tileSymbol;

        [Header("Data")]
        public TileSet tileSet;

        void Start()
        {
            ApplyRandomSymbol();
        }

        public void ApplyRandomSymbol()
        {
            if (tileSet == null)
            {
                Debug.LogWarning($"TileController on {name} has no TileSet assigned!");
                return;
            }

            Sprite randomSymbol = tileSet.GetRandomSymbol();

            if (randomSymbol != null)
            {
                tileSymbol.sprite = randomSymbol;
            }
        }
    }
}