using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.Combat
{
    public sealed class MouseParryReticle : MonoBehaviour
    {
        [Serializable]
        private sealed class ReticlePiece
        {
            [SerializeField] private SpriteRenderer renderer;
            [SerializeField] private Vector2 moveDirection = Vector2.up;

            private Vector3 baseLocalPosition;
            private bool hasBaseLocalPosition;
            private float moveProgress;
            private float oscillationTime;

            public SpriteRenderer Renderer => renderer;

            public void CacheBaseLocalPosition()
            {
                if (renderer == null)
                {
                    return;
                }

                baseLocalPosition = renderer.transform.localPosition;
                hasBaseLocalPosition = true;
            }

            public void SetVisible(bool visible)
            {
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }

            public void ResetMotion()
            {
                if (renderer == null)
                {
                    return;
                }

                if (!hasBaseLocalPosition)
                {
                    CacheBaseLocalPosition();
                }

                moveProgress = 0f;
                oscillationTime = 0f;
                renderer.transform.localPosition = baseLocalPosition;
            }

            public void Tick(
                bool threatened,
                float moveScale,
                AnimationCurve moveCurve,
                Color idleColor,
                Color threatenedColor,
                Color feedbackColor,
                bool useFeedbackColor,
                float moveSpeed,
                bool oscillateWhileThreatened,
                float oscillationSpeed,
                Vector3 shakeOffset,
                float colorSpeed,
                float deltaTime)
            {
                if (renderer == null)
                {
                    return;
                }

                if (!hasBaseLocalPosition)
                {
                    CacheBaseLocalPosition();
                }

                Vector3 localOffset = moveDirection.sqrMagnitude > 0.0001f
                    ? renderer.transform.localRotation * moveDirection
                    : Vector3.zero;
                if (threatened && oscillateWhileThreatened)
                {
                    oscillationTime += Mathf.Max(0f, oscillationSpeed) * deltaTime;
                    moveProgress = Mathf.PingPong(oscillationTime, 1f);
                }
                else
                {
                    oscillationTime = 0f;
                    float targetProgress = threatened ? 1f : 0f;
                    moveProgress = Mathf.MoveTowards(moveProgress, targetProgress, Mathf.Max(0f, moveSpeed) * deltaTime);
                }

                float curvedProgress = moveCurve != null ? Mathf.Clamp01(moveCurve.Evaluate(moveProgress)) : moveProgress;
                Vector3 targetPosition = baseLocalPosition + localOffset * moveScale * curvedProgress + shakeOffset;
                Color targetColor = useFeedbackColor ? feedbackColor : (threatened ? threatenedColor : idleColor);

                renderer.transform.localPosition = targetPosition;
                renderer.color = MoveColor(renderer.color, targetColor, Mathf.Max(0f, colorSpeed) * deltaTime);
            }
        }

        [SerializeField] private ReticlePiece[] pieces =
        {
            new ReticlePiece(),
            new ReticlePiece(),
            new ReticlePiece(),
            new ReticlePiece()
        };
        [SerializeField] private SpriteRenderer[] colorOnlyRenderers = Array.Empty<SpriteRenderer>();
        [SerializeField, FormerlySerializedAs("idleColor")] private Color pieceIdleColor = Color.white;
        [SerializeField, FormerlySerializedAs("threatenedColor")] private Color pieceThreatenedColor = new(1f, 0.45f, 0.05f, 1f);
        [SerializeField] private Color colorOnlyIdleColor = Color.white;
        [SerializeField] private Color colorOnlyThreatenedColor = new(1f, 0.45f, 0.05f, 1f);
        [SerializeField] private Color missPieceColor = new(1f, 0.12f, 0.08f, 1f);
        [SerializeField] private Color missColorOnlyColor = new(1f, 0.12f, 0.08f, 1f);
        [SerializeField, Min(0f)] private float moveScale = 1f;
        [SerializeField] private AnimationCurve moveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField] private bool oscillatePiecesWhileThreatened;
        [SerializeField, Min(0f)] private float pieceOscillationSpeed = 2f;
        [SerializeField, Min(0f)] private float colorSpeed = 8f;
        [SerializeField] private bool useUnscaledTime;
        [SerializeField, HideInInspector] private bool colorOnlyColorsInitialized;

        private bool threatened;
        private bool visible = true;
        private bool forceOscillationWhileThreatened;
        private Vector3[] colorOnlyBaseLocalPositions = Array.Empty<Vector3>();
        private bool[] colorOnlyHasBaseLocalPositions = Array.Empty<bool>();
        private float missFeedbackEndsAt;
        private float missShakeStartedAt;
        private float missShakeEndsAt;
        private float missShakeDuration;
        private float missShakeMagnitude;
        private float missShakeFrequency;
        private Vector2 missShakeDirection = Vector2.right;

        public void SetThreatened(bool value)
        {
            threatened = value;
        }

        public void SetForceOscillationWhileThreatened(bool value)
        {
            forceOscillationWhileThreatened = value;
        }

        public void PlayMissFeedback(float colorSeconds, float shakeSeconds, float shakeAmplitude, float shakeFrequency)
        {
            float now = CurrentTime;
            missFeedbackEndsAt = now + Mathf.Max(0f, colorSeconds);
            missShakeStartedAt = now;
            missShakeDuration = Mathf.Max(0f, shakeSeconds);
            missShakeMagnitude = Mathf.Max(0f, shakeAmplitude);
            missShakeFrequency = Mathf.Max(0f, shakeFrequency);
            missShakeEndsAt = now + missShakeDuration;
            Vector2 randomDirection = UnityEngine.Random.insideUnitCircle;
            missShakeDirection = randomDirection.sqrMagnitude > 0.0001f ? randomDirection.normalized : Vector2.right;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i]?.SetVisible(value);
                if (!value)
                {
                    pieces[i]?.ResetMotion();
                }
            }

            for (int i = 0; i < colorOnlyRenderers.Length; i++)
            {
                SetRendererVisible(colorOnlyRenderers[i], value);
            }

            if (!value)
            {
                ResetColorOnlyMotion();
            }
        }

        private void Awake()
        {
            InitializeColorOnlyColors();
            CacheBaseLocalPositions();
        }

        private void OnValidate()
        {
            InitializeColorOnlyColors();
        }

        private void OnEnable()
        {
            CacheBaseLocalPositions();
            SetVisible(visible);
        }

        private void LateUpdate()
        {
            if (!visible)
            {
                return;
            }

            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float now = CurrentTime;
            bool useFeedbackColor = now < missFeedbackEndsAt;
            Vector3 shakeOffset = GetMissShakeOffset(now);
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i]?.Tick(
                    threatened,
                    moveScale,
                    moveCurve,
                    pieceIdleColor,
                    pieceThreatenedColor,
                    missPieceColor,
                    useFeedbackColor,
                    moveSpeed,
                    oscillatePiecesWhileThreatened || forceOscillationWhileThreatened,
                    pieceOscillationSpeed,
                    shakeOffset,
                    colorSpeed,
                    deltaTime);
            }

            Color targetColor = useFeedbackColor
                ? missColorOnlyColor
                : (threatened ? colorOnlyThreatenedColor : colorOnlyIdleColor);
            for (int i = 0; i < colorOnlyRenderers.Length; i++)
            {
                SpriteRenderer renderer = colorOnlyRenderers[i];
                if (renderer != null)
                {
                    ApplyColorOnlyShake(i, renderer, shakeOffset);
                    renderer.color = MoveColor(renderer.color, targetColor, Mathf.Max(0f, colorSpeed) * deltaTime);
                }
            }
        }

        private void CacheBaseLocalPositions()
        {
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i]?.CacheBaseLocalPosition();
            }

            if (colorOnlyBaseLocalPositions.Length != colorOnlyRenderers.Length)
            {
                colorOnlyBaseLocalPositions = new Vector3[colorOnlyRenderers.Length];
                colorOnlyHasBaseLocalPositions = new bool[colorOnlyRenderers.Length];
            }

            for (int i = 0; i < colorOnlyRenderers.Length; i++)
            {
                SpriteRenderer renderer = colorOnlyRenderers[i];
                if (renderer == null || renderer.transform == transform)
                {
                    continue;
                }

                if (!colorOnlyHasBaseLocalPositions[i])
                {
                    colorOnlyBaseLocalPositions[i] = renderer.transform.localPosition;
                    colorOnlyHasBaseLocalPositions[i] = true;
                }
            }
        }

        private void InitializeColorOnlyColors()
        {
            if (colorOnlyColorsInitialized)
            {
                return;
            }

            colorOnlyIdleColor = pieceIdleColor;
            colorOnlyThreatenedColor = pieceThreatenedColor;
            colorOnlyColorsInitialized = true;
        }

        private Vector3 GetMissShakeOffset(float now)
        {
            if (now >= missShakeEndsAt || missShakeDuration <= 0f || missShakeMagnitude <= 0f)
            {
                return Vector3.zero;
            }

            float elapsed = Mathf.Max(0f, now - missShakeStartedAt);
            float normalized = Mathf.Clamp01(elapsed / missShakeDuration);
            float damping = 1f - normalized;
            float wave = Mathf.Sin(elapsed * missShakeFrequency);
            return (Vector3)(missShakeDirection * (missShakeMagnitude * damping * wave));
        }

        private void ApplyColorOnlyShake(int index, SpriteRenderer renderer, Vector3 shakeOffset)
        {
            if (renderer.transform == transform)
            {
                return;
            }

            if (index < 0 || index >= colorOnlyBaseLocalPositions.Length || !colorOnlyHasBaseLocalPositions[index])
            {
                CacheBaseLocalPositions();
                if (index < 0 || index >= colorOnlyBaseLocalPositions.Length || !colorOnlyHasBaseLocalPositions[index])
                {
                    return;
                }
            }

            renderer.transform.localPosition = colorOnlyBaseLocalPositions[index] + shakeOffset;
        }

        private void ResetColorOnlyMotion()
        {
            CacheBaseLocalPositions();
            for (int i = 0; i < colorOnlyRenderers.Length; i++)
            {
                SpriteRenderer renderer = colorOnlyRenderers[i];
                if (renderer != null
                    && renderer.transform != transform
                    && i < colorOnlyBaseLocalPositions.Length
                    && colorOnlyHasBaseLocalPositions[i])
                {
                    renderer.transform.localPosition = colorOnlyBaseLocalPositions[i];
                }
            }
        }

        private float CurrentTime => useUnscaledTime ? Time.unscaledTime : Time.time;

        private static void SetRendererVisible(SpriteRenderer renderer, bool value)
        {
            if (renderer != null)
            {
                renderer.enabled = value;
            }
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
