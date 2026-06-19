using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Week14.Combat
{
    public sealed class ExecutionImageEffect : MonoBehaviour
    {
        [SerializeField] private Image image;
        [Tooltip("처형 이미지의 RGB와 알파가 0에서 원래 값까지 올라가는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float growSeconds = 0.1f;
        [Tooltip("처형 순간보다 몇 초 전에 처형 이미지를 꺼질지 결정합니다.")]
        [SerializeField, Min(0f)] private float hideLeadSeconds = 0.1f;
        [Tooltip("사라질 때 RGB와 알파가 원래 값에서 0까지 줄어드는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float disappearSeconds = 0.08f;

        private Coroutine routine;
        private Color baseColor;
        private bool baseColorCaptured;

        private void Awake()
        {
            CaptureBaseColor();

            if (image != null)
            {
                image.color = Color.clear;
                image.gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            Stop();
        }

        public void Play(float secondsUntilKillMoment)
        {
            if (image == null)
            {
                return;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(PlayRoutine(secondsUntilKillMoment));
        }

        public void Stop()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            if (image != null)
            {
                image.color = Color.clear;
                image.gameObject.SetActive(false);
            }
        }

        private IEnumerator PlayRoutine(float secondsUntilKillMoment)
        {
            CaptureBaseColor();
            float hideAt = Mathf.Max(0f, secondsUntilKillMoment - hideLeadSeconds);

            image.color = Color.clear;
            image.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < hideAt)
            {
                image.color = Color.Lerp(Color.clear, baseColor, Mathf.Clamp01(elapsed / growSeconds));
                elapsed += Time.deltaTime;
                yield return null;
            }

            elapsed = 0f;
            while (elapsed < disappearSeconds)
            {
                image.color = Color.Lerp(baseColor, Color.clear, elapsed / disappearSeconds);
                elapsed += Time.deltaTime;
                yield return null;
            }

            image.color = Color.clear;
            image.gameObject.SetActive(false);
            routine = null;
        }

        private void CaptureBaseColor()
        {
            if (baseColorCaptured || image == null)
            {
                return;
            }

            baseColor = image.color;
            baseColorCaptured = true;
        }
    }
}
