using System;
using UnityEngine;

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

            public void Tick(
                bool threatened,
                float moveScale,
                AnimationCurve moveCurve,
                Color idleColor,
                Color threatenedColor,
                float moveSpeed,
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
                float targetProgress = threatened ? 1f : 0f;
                moveProgress = Mathf.MoveTowards(moveProgress, targetProgress, Mathf.Max(0f, moveSpeed) * deltaTime);
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
        [SerializeField] private Color idleColor = Color.white;
        [SerializeField] private Color threatenedColor = new(1f, 0.45f, 0.05f, 1f);
        [SerializeField, Min(0f)] private float moveScale = 1f;
        [SerializeField] private AnimationCurve moveCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        [SerializeField, Min(0f)] private float moveSpeed = 5f;
        [SerializeField, Min(0f)] private float colorSpeed = 8f;
        [SerializeField] private bool useUnscaledTime;

        private bool threatened;
        private bool visible = true;

        public void SetThreatened(bool value)
        {
            threatened = value;
        }

        public void SetVisible(bool value)
        {
            visible = value;
            for (int i = 0; i < pieces.Length; i++)
            {
                pieces[i]?.SetVisible(value);
            }

            for (int i = 0; i < colorOnlyRenderers.Length; i++)
            {
                SetRendererVisible(colorOnlyRenderers[i], value);
            }
        }

        private void Awake()
        {
            CacheBaseLocalPositions();
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
                pieces[i]?.Tick(threatened, moveScale, moveCurve, idleColor, threatenedColor, moveSpeed, colorSpeed, deltaTime);
            }

            Color targetColor = threatened ? threatenedColor : idleColor;
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
