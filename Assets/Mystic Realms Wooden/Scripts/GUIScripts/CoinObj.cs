using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LapKan
{
    [RequireComponent(typeof(Collider2D), typeof(AudioSource), typeof(SpriteRenderer))]
    [RequireComponent(typeof(Rigidbody2D))]

    public class CoinObj : MonoBehaviour
    {
        public float fadeDuration = 1f;
        public float moveDistance = 1f;

        private bool isClicked = false;
        private float timer = 0f;

        private SpriteRenderer spriteRenderer;
        private AudioSource audioSource;
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private Rigidbody2D rb;

        void Start()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            audioSource = GetComponent<AudioSource>();
            rb = GetComponent<Rigidbody2D>();
        }

        void OnMouseDown()
        {
            if (isClicked) return;

            isClicked = true;
            startPosition = transform.position;
            targetPosition = startPosition + Vector3.up * moveDistance;

            rb.simulated = false; // ngừng vật lý rơi

            if (audioSource != null)
            {
                audioSource.Play();
            }
        }

        void Update()
        {
            if (!isClicked) return;

            timer += Time.deltaTime;

            // Di chuyển và fade
            transform.position = Vector3.Lerp(startPosition, targetPosition, timer / fadeDuration);
            Color color = spriteRenderer.color;
            color.a = Mathf.Lerp(1f, 0f, timer / fadeDuration);
            spriteRenderer.color = color;

            if (timer >= fadeDuration)
            {
                Destroy(gameObject);
            }
        }
    }
}