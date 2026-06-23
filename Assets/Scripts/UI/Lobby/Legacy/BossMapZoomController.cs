using System.Collections;
using UnityEngine;

namespace Week14.UI
{
    // 보스 선택 카메라 줌 연출이 호버형 디테일 패널 + 클릭 강조 방식으로 대체되어 더 이상 사용하지 않음. 참고용으로 보관.
    public sealed class BossMapZoomController : MonoBehaviour
    {
        [SerializeField] private Camera targetCamera;
        [SerializeField, Min(0.01f)] private float zoomedOrthographicSize = 2.5f;
        [SerializeField, Range(0f, 1f)] private float focusScreenXFraction = 0.25f;
        [SerializeField, Min(0f)] private float zoomSeconds = 0.45f;
        [SerializeField] private AnimationCurve zoomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private float defaultOrthographicSize;
        private Vector3 defaultPosition;
        private bool hasDefaultState;
        private Coroutine zoomRoutine;

        private void Awake()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            CacheDefaultState();
        }

        public void ZoomTo(Vector3 worldPosition)
        {
            if (targetCamera == null)
            {
                return;
            }

            CacheDefaultState();

            float startSize = targetCamera.orthographicSize;
            Vector3 startPosition = targetCamera.transform.position;
            Vector3 endPosition = ComputeFocusedCameraPosition(worldPosition);
            PlayZoom(startSize, startPosition, zoomedOrthographicSize, endPosition);
        }

        public void ZoomOut()
        {
            if (targetCamera == null || !hasDefaultState)
            {
                return;
            }

            float startSize = targetCamera.orthographicSize;
            Vector3 startPosition = targetCamera.transform.position;
            PlayZoom(startSize, startPosition, defaultOrthographicSize, defaultPosition);
        }

        public void ResetImmediate()
        {
            if (targetCamera == null)
            {
                return;
            }

            CacheDefaultState();

            if (zoomRoutine != null)
            {
                StopCoroutine(zoomRoutine);
                zoomRoutine = null;
            }

            targetCamera.orthographicSize = defaultOrthographicSize;
            targetCamera.transform.position = defaultPosition;
        }

        private void CacheDefaultState()
        {
            if (hasDefaultState || targetCamera == null)
            {
                return;
            }

            defaultOrthographicSize = targetCamera.orthographicSize;
            defaultPosition = targetCamera.transform.position;
            hasDefaultState = true;
        }

        private Vector3 ComputeFocusedCameraPosition(Vector3 worldPosition)
        {
            float viewWidth = zoomedOrthographicSize * 2f * targetCamera.aspect;
            float offsetX = (0.5f - focusScreenXFraction) * viewWidth;
            float cameraZ = targetCamera.transform.position.z;
            return new Vector3(worldPosition.x + offsetX, worldPosition.y, cameraZ);
        }

        private void PlayZoom(float startSize, Vector3 startPosition, float endSize, Vector3 endPosition)
        {
            if (zoomRoutine != null)
            {
                StopCoroutine(zoomRoutine);
            }

            zoomRoutine = StartCoroutine(PlayZoomRoutine(startSize, startPosition, endSize, endPosition));
        }

        private IEnumerator PlayZoomRoutine(float startSize, Vector3 startPosition, float endSize, Vector3 endPosition)
        {
            float t = 0f;
            while (zoomSeconds > 0f && t < zoomSeconds)
            {
                t += Time.unscaledDeltaTime;
                float eased = zoomCurve.Evaluate(Mathf.Clamp01(t / zoomSeconds));
                targetCamera.orthographicSize = Mathf.Lerp(startSize, endSize, eased);
                targetCamera.transform.position = Vector3.Lerp(startPosition, endPosition, eased);
                yield return null;
            }

            targetCamera.orthographicSize = endSize;
            targetCamera.transform.position = endPosition;
            zoomRoutine = null;
        }
    }
}
