using UnityEngine;
using System.Collections.Generic;

namespace LapKan
{
    [CreateAssetMenu(fileName = "NewTileSet", menuName = "Game/Tile Set")]
    public class TileSet : ScriptableObject
    {
        public string setName;
        public List<Sprite> symbols = new List<Sprite>();

        public Sprite GetRandomSymbol()
        {
            if (symbols == null || symbols.Count == 0)
            {
                return null;
            }

            return symbols[Random.Range(0, symbols.Count)];
        }
    }
}