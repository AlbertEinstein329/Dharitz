using UnityEngine;

namespace LapKan
{
    public class GameManager : MonoBehaviour
    {
        public GameObject coinPrefab;
        public float timeSpawn = 2f;

        public Vector2 spawnAreaMin = new Vector2(-5f, 3f);
        public Vector2 spawnAreaMax = new Vector2(5f, 5f);

        private float timer;

        void Update()
        {
            timer += Time.deltaTime;
            if (timer >= timeSpawn)
            {
                timer = 0f;
                SpawnCoin();
            }
        }

        void SpawnCoin()
        {
            Vector2 spawnPos = new Vector2(
                Random.Range(spawnAreaMin.x, spawnAreaMax.x),
                Random.Range(spawnAreaMin.y, spawnAreaMax.y)
            );

            Instantiate(coinPrefab, spawnPos, Quaternion.identity);
        }
    }
}