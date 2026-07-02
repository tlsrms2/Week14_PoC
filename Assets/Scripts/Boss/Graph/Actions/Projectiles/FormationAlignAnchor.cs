using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    // Moving charge-anchor for FireDashFormationAction: EnemyProjectile snaps to this
    // transform every frame while charging, so animating this position tweens the bullet
    // from its spawn point to its formation slot without touching EnemyProjectile itself.
    public sealed class FormationAlignAnchor : MonoBehaviour
    {
        private Vector3 startPosition;
        private Vector3 targetPosition;
        private float duration;
        private AnimationCurve ease;
        private float startedAt;
        private float pausedSince = -1f;

        public static Transform Create(
            Vector3 startPosition,
            Vector3 targetPosition,
            float duration,
            AnimationCurve ease,
            float lifetimeSeconds)
        {
            GameObject anchorObject = new("FormationAlignAnchor");
            FormationAlignAnchor anchor = anchorObject.AddComponent<FormationAlignAnchor>();
            anchor.Initialize(startPosition, targetPosition, duration, ease);
            Destroy(anchorObject, Mathf.Max(0.05f, lifetimeSeconds));
            return anchorObject.transform;
        }

        private void Initialize(Vector3 nextStartPosition, Vector3 nextTargetPosition, float nextDuration, AnimationCurve nextEase)
        {
            startPosition = nextStartPosition;
            targetPosition = nextTargetPosition;
            duration = Mathf.Max(0f, nextDuration);
            ease = nextEase != null && nextEase.length > 0 ? nextEase : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            startedAt = Time.time;
            transform.position = startPosition;
        }

        private void Update()
        {
            if (PlayerCombatController.IsExecutionCinematicActive)
            {
                if (pausedSince < 0f)
                {
                    pausedSince = Time.time;
                }

                return;
            }

            if (pausedSince >= 0f)
            {
                startedAt += Time.time - pausedSince;
                pausedSince = -1f;
            }

            if (duration <= 0f)
            {
                transform.position = targetPosition;
                return;
            }

            float t = Mathf.Clamp01((Time.time - startedAt) / duration);
            transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, ease.Evaluate(t));
        }
    }
}
