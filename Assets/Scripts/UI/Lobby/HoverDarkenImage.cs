using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Week14.UI
{
    public sealed class HoverDarkenImage : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private Graphic targetGraphic;
        [SerializeField] private SpriteRenderer targetSpriteRenderer;
        [SerializeField, Range(0f, 1f)] private float darkenAmount = 0.25f;
        [SerializeField, Min(0f)] private float transitionSeconds = 0.08f;
        [SerializeField] private Transform scaleTarget;
        [SerializeField, Min(1f)] private float hoverScale = 1.02f;
        [SerializeField, Min(1f)] private float popScale = 1.03f;
        [SerializeField, Min(0f)] private float popSeconds = 0.06f;
        [SerializeField, Min(0f)] private float settleSeconds = 0.1f;
        [SerializeField, Min(0f)] private float returnSeconds = 0.1f;

        private Color baseColor;
        private Color targetColor;
        private bool hasBaseColor;
        private Vector3 baseScale;
        private Vector3 scaleFrom;
        private Vector3 scaleTo;
        private float scaleElapsed;
        private float scaleDuration;
        private bool hasBaseScale;
        private bool isHovering;
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
            CacheBaseColor();
            targetColor = baseColor;
            ApplyColor(baseColor);
            CacheBaseScale();
            ApplyScale(baseScale);
            isHovering = false;
            scalePhase = ScalePhase.Idle;
        }

        private void OnDisable()
        {
            isHovering = false;
            targetColor = baseColor;
            scalePhase = ScalePhase.Idle;

            if (hasBaseColor)
            {
                ApplyColor(baseColor);
            }

            if (hasBaseScale)
            {
                ApplyScale(baseScale);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            lastPointerPosition = eventData.position;
            StartHover();
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

            UpdateColor();
            UpdateScale();
        }

        private void UpdateColor()
        {
            if (!HasTarget)
            {
                return;
            }

            if (transitionSeconds <= 0f)
            {
                ApplyColor(targetColor);
                return;
            }

            float maxDelta = Time.unscaledDeltaTime / transitionSeconds;
            ApplyColor(MoveColor(GetColor(), targetColor, maxDelta));
        }

        private void UpdateScale()
        {
            if (scaleTarget == null || scalePhase == ScalePhase.Idle)
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
            if (isHovering)
            {
                return;
            }

            isHovering = true;
            targetColor = GetDarkenedColor();
            StartScalePhase(ScalePhase.Pop, GetScaledBase(Mathf.Max(popScale, hoverScale)), popSeconds);
        }

        private void EndHover()
        {
            if (!isHovering)
            {
                return;
            }

            isHovering = false;
            targetColor = baseColor;
            StartScalePhase(ScalePhase.Return, baseScale, returnSeconds);
        }

        private void CacheBaseColor()
        {
            if (hasBaseColor || !HasTarget)
            {
                return;
            }

            baseColor = GetColor();
            hasBaseColor = true;
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

        private Color GetDarkenedColor()
        {
            Color darkened = baseColor * (1f - darkenAmount);
            darkened.a = baseColor.a;
            return darkened;
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

        private Color GetColor()
        {
            return targetGraphic != null ? targetGraphic.color : targetSpriteRenderer.color;
        }

        private void ApplyColor(Color color)
        {
            if (targetGraphic != null)
            {
                targetGraphic.color = color;
            }
            else if (targetSpriteRenderer != null)
            {
                targetSpriteRenderer.color = color;
            }
        }

        private void ApplyScale(Vector3 scale)
        {
            if (scaleTarget != null)
            {
                scaleTarget.localScale = scale;
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

        private static Color MoveColor(Color current, Color target, float maxDelta)
        {
            return new Color(
                Mathf.MoveTowards(current.r, target.r, maxDelta),
                Mathf.MoveTowards(current.g, target.g, maxDelta),
                Mathf.MoveTowards(current.b, target.b, maxDelta),
                Mathf.MoveTowards(current.a, target.a, maxDelta));
        }
    }
}
