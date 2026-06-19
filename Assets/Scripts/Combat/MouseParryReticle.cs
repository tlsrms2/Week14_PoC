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
                float moveSpeed,
                bool oscillateWhileThreatened,
                float oscillationSpeed,
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
                Vector3 targetPosition = baseLocalPosition + localOffset * moveScale * curvedProgress;
                Color targetColor = threatened ? threatenedColor : idleColor;

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

        public void SetThreatened(bool value)
        {
            threatened = value;
        }

        public void SetForceOscillationWhileThreatened(bool value)
        {
            forceOscillationWhileThreatened = value;
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
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i]?.Tick(
                    threatened,
                    moveScale,
                    moveCurve,
                    pieceIdleColor,
                    pieceThreatenedColor,
                    moveSpeed,
                    oscillatePiecesWhileThreatened || forceOscillationWhileThreatened,
                    pieceOscillationSpeed,
                    colorSpeed,
                    deltaTime);
            }

            Color targetColor = threatened ? colorOnlyThreatenedColor : colorOnlyIdleColor;
            for (int i = 0; i < colorOnlyRenderers.Length; i++)
            {
                SpriteRenderer renderer = colorOnlyRenderers[i];
                if (renderer != null)
                {
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
