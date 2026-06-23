using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Week14.UI
{
    public sealed class BossDetailPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text crimeText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Image iconImage;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.2f;

        private Coroutine fadeRoutine;

        private void Awake()
        {
            HideImmediate();
        }

        public void Show(string bossName, string crime, string description, Sprite icon)
        {
            if (nameText != null)
            {
                nameText.text = bossName;
            }

            if (crimeText != null)
            {
                crimeText.text = crime;
            }

            if (descriptionText != null)
            {
                descriptionText.text = description;
            }

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            PlayFade(1f);
        }

        public void Hide()
        {
            PlayFade(0f);
        }

        public void HideImmediate()
        {
            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
                fadeRoutine = null;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void PlayFade(float targetAlpha)
        {
            if (canvasGroup == null)
            {
                return;
            }

            bool show = targetAlpha > 0f;
            canvasGroup.interactable = show;
            canvasGroup.blocksRaycasts = show;

            if (fadeRoutine != null)
            {
                StopCoroutine(fadeRoutine);
            }

            fadeRoutine = StartCoroutine(FadeRoutine(targetAlpha));
        }

        private IEnumerator FadeRoutine(float targetAlpha)
        {
            float startAlpha = canvasGroup.alpha;
            float t = 0f;
            while (fadeSeconds > 0f && t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t / fadeSeconds);
                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
            fadeRoutine = null;
        }
    }
}
