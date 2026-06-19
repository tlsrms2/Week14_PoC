using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Week14.Combat
{
    public sealed class ExecutionImageEffect : MonoBehaviour
    {
        [SerializeField] private Image image;
        [Tooltip("처형 이미지의 FillAmount가 0에서 1까지 차오르는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float growSeconds = 0.1f;
        [Tooltip("처형 순간보다 몇 초 전에 처형 이미지를 꺼질지 결정합니다.")]
        [SerializeField, Min(0f)] private float hideLeadSeconds = 0.1f;
        [Tooltip("사라질 때 FillAmount가 1에서 0까지 줄어드는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float disappearSeconds = 0.08f;

        private Coroutine routine;

        private void Awake()
        {
            if (image != null)
            {
                image.fillOrigin = (int)Image.OriginHorizontal.Right;
                image.fillAmount = 0f;
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
                image.fillOrigin = (int)Image.OriginHorizontal.Right;
                image.fillAmount = 0f;
                image.gameObject.SetActive(false);
            }
        }

        private IEnumerator PlayRoutine(float secondsUntilKillMoment)
        {
            float hideAt = Mathf.Max(0f, secondsUntilKillMoment - hideLeadSeconds);

            image.fillOrigin = (int)Image.OriginHorizontal.Right;
            image.fillAmount = 0f;
            image.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < hideAt)
            {
                image.fillAmount = Mathf.Clamp01(elapsed / growSeconds);
                elapsed += Time.deltaTime;
                yield return null;
            }

            image.fillOrigin = (int)Image.OriginHorizontal.Left;
            elapsed = 0f;
            while (elapsed < disappearSeconds)
            {
                image.fillAmount = Mathf.Lerp(1f, 0f, elapsed / disappearSeconds);
                elapsed += Time.deltaTime;
                yield return null;
            }

            image.fillAmount = 0f;
            image.gameObject.SetActive(false);
            routine = null;
        }
    }
}
