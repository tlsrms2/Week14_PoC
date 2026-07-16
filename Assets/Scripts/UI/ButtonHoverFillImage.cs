using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Week14.UI
{
    public sealed class ButtonHoverFillImage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Tooltip("버튼 안쪽을 채울 Fill 이미지입니다. 비워두면 자식 오브젝트 중 이름이 Fill인 Image를 찾습니다.")]
        [SerializeField] private Image fillImage;
        [Tooltip("호버 시 Fill 이미지가 차오르는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float fillDuration = 0.15f;
        [Tooltip("Fill 이미지가 비워지는 데 걸리는 시간입니다.")]
        [SerializeField, Min(0.01f)] private float emptyDuration = 0.1f;
        [Tooltip("Fill 진행도를 조절하는 커브입니다.")]
        [SerializeField] private AnimationCurve fillCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("켜져 있으면 Fill 이미지를 Horizontal Filled 이미지로 설정해 왼쪽부터 차오르게 합니다.")]
        [SerializeField] private bool configureAsHorizontalFill = false;

        private Coroutine fillRoutine;
        private float currentFill;

        private void OnValidate()
        {
            ResolveFillImage();
            if (configureAsHorizontalFill)
            {
                ConfigureFillImage();
            }
        }

        private void OnEnable()
        {
            ResolveFillImage();
            SetFill(0f);
        }

        private void OnDisable()
        {
            StopFillRoutine();
            SetFill(0f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!configureAsHorizontalFill)
            {
                SetFillImageVisible(true);
                return;
            }

            PlayFill(1f, fillDuration);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!configureAsHorizontalFill)
            {
                SetFillImageVisible(false);
                return;
            }

            PlayFill(0f, emptyDuration);
        }

        private void ResolveFillImage()
        {
            if (fillImage != null)
            {
                return;
            }

            Transform fillTransform = transform.Find("Fill");
            if (fillTransform != null)
            {
                fillImage = fillTransform.GetComponent<Image>();
            }
        }

        private void PlayFill(float targetFill, float duration)
        {
            ResolveFillImage();
            if (fillImage == null)
            {
                return;
            }

            if (!configureAsHorizontalFill)
            {
                SetFillImageVisible(targetFill > 0f);
                return;
            }

            fillImage.gameObject.SetActive(true);
            StopFillRoutine();
            fillRoutine = StartCoroutine(AnimateFill(Mathf.Clamp01(targetFill), duration));
        }

        private IEnumerator AnimateFill(float targetFill, float duration)
        {
            float startFill = currentFill;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / duration);
                float curveProgress = fillCurve != null ? fillCurve.Evaluate(normalizedTime) : normalizedTime;
                SetFill(Mathf.LerpUnclamped(startFill, targetFill, curveProgress));
                yield return null;
            }

            SetFill(targetFill);
            fillRoutine = null;
        }

        private void SetFill(float fill)
        {
            ResolveFillImage();
            currentFill = Mathf.Clamp01(fill);

            if (fillImage == null)
            {
                return;
            }

            if (!configureAsHorizontalFill)
            {
                SetFillImageVisible(currentFill > 0f);
                return;
            }

            ConfigureFillImage();
            fillImage.fillAmount = currentFill;
            fillImage.gameObject.SetActive(currentFill > 0f);
        }

        private void ConfigureFillImage()
        {
            if (!configureAsHorizontalFill || fillImage == null)
            {
                return;
            }

            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        private void SetFillImageVisible(bool visible)
        {
            ResolveFillImage();
            StopFillRoutine();
            currentFill = visible ? 1f : 0f;

            if (fillImage != null)
            {
                fillImage.gameObject.SetActive(visible);
            }
        }

        private void StopFillRoutine()
        {
            if (fillRoutine == null)
            {
                return;
            }

            StopCoroutine(fillRoutine);
            fillRoutine = null;
        }
    }
}
