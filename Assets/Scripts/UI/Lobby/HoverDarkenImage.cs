using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Enemy;

namespace Week14.UI
{
    public sealed class HoverDarkenImage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private SpriteRenderer targetSpriteRenderer;
        [SerializeField] private Transform scaleTarget;
        [SerializeField, Min(1f)] private float hoverScale = 1.02f;
        [SerializeField, Min(1f)] private float popScale = 1.03f;
        [SerializeField, Min(0f)] private float popSeconds = 0.06f;
        [SerializeField, Min(0f)] private float settleSeconds = 0.1f;
        [SerializeField, Min(0f)] private float returnSeconds = 0.1f;

        [Tooltip("이 오브젝트에 호버 시 재생할 SFX의 SoundLibrary ID입니다. 비워두면 재생하지 않습니다.")]
        [BossGraphSfxId]
        [SerializeField] private string hoverSfxId;
        [Tooltip("이 시간(초) 안에 다시 호버해도 재생을 막습니다. 레이캐스트 잔떨림 등으로 PointerEnter가 짧은 간격에 반복될 때 중복 재생을 막는 용도입니다.")]
        [SerializeField, Min(0f)] private float hoverSfxDebounceSeconds = 0.1f;

        private float lastSfxPlayTime = float.NegativeInfinity;

        private Vector3 baseScale;
        private Vector3 scaleFrom;
        private Vector3 scaleTo;
        private float scaleElapsed;
        private float scaleDuration;
        private bool hasBaseScale;
        private bool isHovering;
        private bool scaleEffectSuppressed;
        private Vector2 lastPointerPosition;
        private ScalePhase scalePhase;
        private PointerEventData hoverPointerEventData;
        private readonly List<RaycastResult> raycastResults = new List<RaycastResult>();

        private bool HasTarget => targetGraphic != null || targetSpriteRenderer != null;

        private enum ScalePhase
        {
            Idle,
            Pop,
            Settle,
            Return
        }

        private void Awake()
        {
            if (HasTarget)
            {
                if (scaleTarget == null)
                {
                    scaleTarget = transform;
                }

                return;
            }

            targetGraphic = GetComponent<Graphic>();
            if (targetGraphic == null)
            {
                targetSpriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (scaleTarget == null)
            {
                scaleTarget = transform;
            }
        }

        private void OnEnable()
        {
            CacheBaseScale();
            ApplyScale(baseScale);
            isHovering = false;
            scalePhase = ScalePhase.Idle;
        }

        private void OnDisable()
        {
            isHovering = false;
            scalePhase = ScalePhase.Idle;

            if (hasBaseScale)
            {
                ApplyScale(baseScale);
            }
        }

        public void SetScaleEffectSuppressed(bool suppressed)
        {
            if (scaleEffectSuppressed == suppressed)
            {
                return;
            }

            scaleEffectSuppressed = suppressed;

            if (suppressed)
            {
                isHovering = false;
                scalePhase = ScalePhase.Idle;

                // 애니메이션 도중(커진 상태)에 억제가 걸리면 그 크기로 멈춰버리므로,
                // 항상 진짜 베이스 크기로 즉시 되돌려 다음 캐싱이 오염되지 않게 한다.
                CacheBaseScale();
                ApplyScale(baseScale);
            }
            else
            {
                // 억제가 풀리는 시점엔 scaleTarget이 이미 외부에서 다른 크기로 바뀌어 있을 수 있으니,
                // 다음 호버 때 그 크기를 새 기준(baseScale)으로 다시 캐싱하게 한다.
                hasBaseScale = false;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            lastPointerPosition = eventData.position;
            StartHover();
            PlayHoverSfx();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            lastPointerPosition = eventData.position;
            EndHover();
        }

        public void OnPointerMove(PointerEventData eventData)
        {
            lastPointerPosition = eventData.position;
        }

        private void Update()
        {
            if (isHovering && !IsPointerStillOverThis())
            {
                EndHover();
            }

            UpdateScale();
        }

        private void UpdateScale()
        {
            if (scaleEffectSuppressed || scaleTarget == null || scalePhase == ScalePhase.Idle)
            {
                return;
            }

            if (scaleDuration <= 0f)
            {
                ApplyScale(scaleTo);
                FinishScalePhase();
                return;
            }

            scaleElapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(scaleElapsed / scaleDuration);
            t = Mathf.SmoothStep(0f, 1f, t);
            ApplyScale(Vector3.LerpUnclamped(scaleFrom, scaleTo, t));

            if (scaleElapsed >= scaleDuration)
            {
                FinishScalePhase();
            }
        }

        private void StartHover()
        {
            if (isHovering || scaleEffectSuppressed)
            {
                return;
            }

            isHovering = true;
            StartScalePhase(ScalePhase.Pop, GetScaledBase(Mathf.Max(popScale, hoverScale)), popSeconds);
        }

        private void EndHover()
        {
            if (!isHovering)
            {
                return;
            }

            isHovering = false;
            StartScalePhase(ScalePhase.Return, baseScale, returnSeconds);
        }

        private void PlayHoverSfx()
        {
            if (string.IsNullOrEmpty(hoverSfxId))
            {
                return;
            }

            float now = Time.unscaledTime;
            if (now - lastSfxPlayTime < hoverSfxDebounceSeconds)
            {
                return;
            }

            lastSfxPlayTime = now;
            SoundManager.PlaySfx(hoverSfxId);
        }

        private void CacheBaseScale()
        {
            if (hasBaseScale || scaleTarget == null)
            {
                return;
            }

            baseScale = scaleTarget.localScale;
            hasBaseScale = true;
        }

        private Vector3 GetScaledBase(float multiplier)
        {
            CacheBaseScale();
            return baseScale * multiplier;
        }

        private void StartScalePhase(ScalePhase phase, Vector3 targetScale, float duration)
        {
            if (scaleTarget == null)
            {
                return;
            }

            CacheBaseScale();
            scalePhase = phase;
            scaleFrom = scaleTarget.localScale;
            scaleTo = targetScale;
            scaleElapsed = 0f;
            scaleDuration = duration;

            if (scaleDuration <= 0f)
            {
                ApplyScale(scaleTo);
                FinishScalePhase();
            }
        }

        private void FinishScalePhase()
        {
            ApplyScale(scaleTo);

            if (scalePhase == ScalePhase.Pop)
            {
                if (isHovering)
                {
                    StartScalePhase(ScalePhase.Settle, GetScaledBase(hoverScale), settleSeconds);
                }
                else
                {
                    StartScalePhase(ScalePhase.Return, baseScale, returnSeconds);
                }

                return;
            }

            scalePhase = ScalePhase.Idle;
        }

        private void ApplyScale(Vector3 scale)
        {
            if (scaleTarget == null)
            {
                return;
            }

            scaleTarget.localScale = scale;

            if (targetSpriteRenderer != null)
            {
                // 프로젝트 Physics2D 설정의 autoSyncTransforms가 꺼져 있어, 콜라이더와 같은 트랜스폼을
                // 매 프레임 스크립트로 스케일 변경하면 다음 FixedUpdate 전까지 레이캐스트가 갱신 전 콜라이더를
                // 봐서 호버가 깜빡이고 Pop 애니메이션이 계속 재시작되는 것처럼 보인다. 직접 동기화해서 막는다.
                Physics2D.SyncTransforms();
            }
        }

        private bool IsPointerStillOverThis()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return true;
            }

            if (hoverPointerEventData == null)
            {
                hoverPointerEventData = new PointerEventData(eventSystem);
            }

            hoverPointerEventData.position = lastPointerPosition;
            raycastResults.Clear();
            eventSystem.RaycastAll(hoverPointerEventData, raycastResults);

            if (raycastResults.Count == 0)
            {
                return false;
            }

            return IsOwnTransform(raycastResults[0].gameObject.transform);
        }

        private bool IsOwnTransform(Transform hitTransform)
        {
            if (hitTransform == null)
            {
                return false;
            }

            if (hitTransform == transform || hitTransform.IsChildOf(transform))
            {
                return true;
            }

            if (targetGraphic != null && (hitTransform == targetGraphic.transform || hitTransform.IsChildOf(targetGraphic.transform)))
            {
                return true;
            }

            return targetSpriteRenderer != null
                   && (hitTransform == targetSpriteRenderer.transform || hitTransform.IsChildOf(targetSpriteRenderer.transform));
        }
    }
}
