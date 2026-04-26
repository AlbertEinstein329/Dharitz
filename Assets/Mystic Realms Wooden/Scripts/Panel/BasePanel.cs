using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using UnityEngine.Events;

namespace LapKan
{
    public abstract class BasePanel : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] protected GameObject backDrop;
        [SerializeField] protected GameObject panelRoot;
        [SerializeField] protected CanvasGroup canvasGroup;
        [SerializeField] protected RectTransform panelTransform;

        [Header("Animation Settings")]
        [SerializeField] protected float fadeDuration = 0.3f;
        [SerializeField] protected float scaleDuration = 0.3f;
        [SerializeField] protected Vector3 startScale = new Vector3(0.7f, 0.7f, 0.7f);
        [SerializeField] protected Vector3 hideScale = new Vector3(0.7f, 0.7f, 0.7f);

        protected Coroutine currentAnim;

        protected virtual void Awake()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
                backDrop.SetActive(false);
            }
        }

        public virtual void Show()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                backDrop.SetActive(true);
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            if (panelTransform != null)
                panelTransform.localScale = startScale;

            if (currentAnim != null) StopCoroutine(currentAnim);
            currentAnim = StartCoroutine(AnimateShow());
        }

        public virtual void Hide()
        {
            if (currentAnim != null) StopCoroutine(currentAnim);
            currentAnim = StartCoroutine(AnimateHide());
            if (backDrop != null) backDrop.SetActive(false);
        }

        public virtual void Dismiss()
        {
            if (currentAnim != null) StopCoroutine(currentAnim);
            currentAnim = StartCoroutine(AnimateHide());
        }

        protected IEnumerator AnimateShow()
        {
            float timer = 0f;
            while (timer < fadeDuration || timer < scaleDuration)
            {
                timer += Time.deltaTime;
                float tFade = Mathf.Clamp01(timer / fadeDuration);
                float tScale = Mathf.Clamp01(timer / scaleDuration);

                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(0f, 1f, tFade);

                if (panelTransform != null)
                    panelTransform.localScale = Vector3.Lerp(startScale, Vector3.one, tScale);

                yield return null;
            }
            if (canvasGroup != null)
            {
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
            }
        }

        protected IEnumerator AnimateHide()
        {
            float timer = 0f;
            while (timer < fadeDuration || timer < scaleDuration)
            {
                timer += Time.deltaTime;
                float tFade = Mathf.Clamp01(timer / fadeDuration);
                float tScale = Mathf.Clamp01(timer / scaleDuration);

                if (canvasGroup != null)
                    canvasGroup.alpha = Mathf.Lerp(1f, 0f, tFade);

                if (panelTransform != null)
                    panelTransform.localScale = Vector3.Lerp(Vector3.one, hideScale, tScale);

                yield return null;
            }
            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public UnityEvent ClosePanelEvent;

        public void CloseBoard()
        {
            ClosePanelEvent?.Invoke();
        }

        private void OnDestroy()
        {
            ClosePanelEvent.RemoveAllListeners();
        }
    }
}