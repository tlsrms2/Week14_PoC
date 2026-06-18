using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Week14.Combat
{
    public sealed class ExecutionImageEffect : MonoBehaviour
    {
        [SerializeField] private Image image;
        [Tooltip("처형 이미지가 빠르게 커질 때 도달하는 목표 RectTransform 높이입니다.")]
        [SerializeField, Min(0f)] private float targetHeight = 400f;
        [Tooltip("처형 이미지가 0에서 목표 높이까지 커지는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float growSeconds = 0.1f;
        [Tooltip("이미지가 다 차오른 뒤 꺼지기 전까지 유지하는 시간입니다.")]
        [SerializeField, Min(0f)] private float holdSeconds = 0.1f;

        private void Awake()
        {
            if (image != null)
            {
                image.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        public IEnumerator PlayAndWait()
        {
            if (image == null)
            {
                yield break;
            }

            RectTransform rect = image.rectTransform;
            Vector2 size = rect.sizeDelta;
            size.y = 0f;
            rect.sizeDelta = size;
            image.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < growSeconds)
            {
                size.y = Mathf.Lerp(0f, targetHeight, Mathf.Clamp01(elapsed / growSeconds));
                rect.sizeDelta = size;
                elapsed += Time.deltaTime;
                yield return null;
            }

            size.y = targetHeight;
            rect.sizeDelta = size;

            if (holdSeconds > 0f)
            {
                yield return new WaitForSeconds(holdSeconds);
            }

            image.gameObject.SetActive(false);
        }

        public void Stop()
        {
            if (image != null)
            {
                image.gameObject.SetActive(false);
            }
        }
    }
}
