using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    internal sealed class HackerMeleeParryWindow : MonoBehaviour
    {
        private const int CircleSegments = 32;
        private static readonly Color DefaultIndicatorColor = new(0.2f, 0.65f, 1f, 0.9f);
        private static readonly Color HologramIndicatorColor = new(0.35f, 0.9f, 1f, 0.9f);
        private static readonly List<HackerMeleeParryWindow> ActiveWindows = new();

        private Transform followTarget;
        private Vector3 followWorldOffset;
        private bool useFollowWorldOffset;
        private float endsAt;
        private Action parried;
        private LineRenderer indicator;
        private Color indicatorColor = DefaultIndicatorColor;

        internal void SetHologramStyle(bool isHologram)
        {
            indicatorColor = isHologram ? HologramIndicatorColor : DefaultIndicatorColor;
            ApplyIndicatorColor();
        }

        internal void Initialize(Transform nextFollowTarget, float radius, float durationSeconds, Action onParried)
        {
            followTarget = nextFollowTarget;
            useFollowWorldOffset = false;
            transform.position = GetFollowPosition();
            endsAt = Time.time + Mathf.Max(0f, durationSeconds);
            parried = onParried;
            CreateIndicator(Mathf.Max(0.05f, radius));
            ActiveWindows.Add(this);
        }

        internal void Initialize(
            Transform nextFollowTarget,
            Vector3 worldOffset,
            float radius,
            float durationSeconds,
            Action onParried)
        {
            followTarget = nextFollowTarget;
            followWorldOffset = worldOffset;
            useFollowWorldOffset = true;
            transform.position = GetFollowPosition();
            endsAt = Time.time + Mathf.Max(0f, durationSeconds);
            parried = onParried;
            CreateIndicator(Mathf.Max(0.05f, radius));
            ActiveWindows.Add(this);
        }

        internal static HackerMeleeParryWindow FindClosest(Predicate<Vector2> isInParryRange, Vector2 cursorPosition)
        {
            HackerMeleeParryWindow best = null;
            float bestDistance = float.PositiveInfinity;
            for (int i = ActiveWindows.Count - 1; i >= 0; i--)
            {
                HackerMeleeParryWindow candidate = ActiveWindows[i];
                if (candidate == null || !candidate.IsAvailable)
                {
                    ActiveWindows.RemoveAt(i);
                    continue;
                }

                if (isInParryRange == null || !isInParryRange(candidate.transform.position))
                {
                    continue;
                }

                float distance = Vector2.Distance(cursorPosition, candidate.transform.position);
                if (distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        internal bool TryParry()
        {
            if (!IsAvailable)
            {
                return false;
            }

            parried?.Invoke();
            Destroy(gameObject);
            return true;
        }

        private bool IsAvailable => Time.time <= endsAt;

        private void Update()
        {
            if (followTarget != null)
            {
                transform.position = GetFollowPosition();
            }

            if (!IsAvailable)
            {
                Destroy(gameObject);
                return;
            }

            if (indicator != null)
            {
                ApplyIndicatorColor();
            }
        }

        private Vector3 GetFollowPosition()
        {
            if (followTarget == null)
            {
                return transform.position;
            }

            return useFollowWorldOffset
                ? followTarget.position + followWorldOffset
                : followTarget.position;
        }

        private void OnDestroy()
        {
            ActiveWindows.Remove(this);
        }

        private void CreateIndicator(float radius)
        {
            GameObject lineObject = new("HackerMeleeParryIndicator");
            lineObject.transform.SetParent(transform, false);
            indicator = lineObject.AddComponent<LineRenderer>();
            indicator.useWorldSpace = false;
            indicator.loop = true;
            indicator.positionCount = CircleSegments;
            indicator.startWidth = 0.045f;
            indicator.endWidth = 0.045f;
            indicator.sortingOrder = 20;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                indicator.material = new Material(shader);
            }

            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / CircleSegments;
                indicator.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
            }

            ApplyIndicatorColor();
        }

        private void ApplyIndicatorColor()
        {
            if (indicator == null)
            {
                return;
            }

            indicator.startColor = indicatorColor;
            indicator.endColor = indicatorColor;
        }
    }
}
