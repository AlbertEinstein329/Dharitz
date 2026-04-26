using UnityEngine;

namespace LapKan
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class BackgroundScaler : MonoBehaviour
    {
        void Start()
        {
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr == null) return;

            // Lấy sprite gốc
            Sprite sprite = sr.sprite;
            float pixelsPerUnit = sprite.pixelsPerUnit;

            // Kích thước sprite tính theo world unit
            float spriteWidth = sprite.rect.width / pixelsPerUnit;
            float spriteHeight = sprite.rect.height / pixelsPerUnit;

            // Lấy camera chính
            Camera cam = Camera.main;
            float worldHeight = cam.orthographicSize * 2f;
            float worldWidth = worldHeight * cam.aspect;

            // Scale cho vừa màn hình
            transform.localScale = new Vector3(
                worldWidth / spriteWidth,
                worldHeight / spriteHeight,
                1
            );
        }
    }
} 