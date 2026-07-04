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

    [AddComponentMenu("")]
    internal sealed class BossSpiralProjectileMotion : MonoBehaviour
    {
        private EnemyProjectile projectile;
        private Rigidbody2D body;
        private Vector2 origin;
        private float startAngleDegrees;
        private float radialSpeed;
        private float angularSpeedDegrees;
        private float startRadius;
        private float durationSeconds;
        private float elapsed;
        private bool destroyOnComplete;

        public void Initialize(
            Vector2 nextOrigin,
            float nextStartAngleDegrees,
            float nextRadialSpeed,
            float nextAngularSpeedDegrees,
            float nextStartRadius,
            float nextDurationSeconds,
            bool nextDestroyOnComplete)
        {
            projectile = GetComponent<EnemyProjectile>();
            body = GetComponent<Rigidbody2D>();
            origin = nextOrigin;
            startAngleDegrees = nextStartAngleDegrees;
            radialSpeed = Mathf.Max(0f, nextRadialSpeed);
            angularSpeedDegrees = nextAngularSpeedDegrees;
            startRadius = Mathf.Max(0f, nextStartRadius);
            durationSeconds = Mathf.Max(0f, nextDurationSeconds);
            destroyOnComplete = nextDestroyOnComplete;
            MoveTo(origin, 0f);
        }

        private void LateUpdate()
        {
            if (projectile == null || PlayerCombatController.IsExecutionCinematicActive)
            {
                StopBody();
                return;
            }

            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            float angle = startAngleDegrees + angularSpeedDegrees * elapsed;
            float radius = startRadius + radialSpeed * elapsed;
            Vector2 nextPosition = origin + BossActionContext.AngleToDirection(angle) * radius;
            MoveTo(nextPosition, deltaTime);

            if (destroyOnComplete && durationSeconds > 0f && elapsed >= durationSeconds)
            {
                projectile.DestroyFromOwner();
            }
        }

        private void MoveTo(Vector2 position, float deltaTime)
        {
            Vector2 previous = body != null ? body.position : (Vector2)transform.position;
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = deltaTime > 0f ? (position - previous) / deltaTime : Vector2.zero;
            }

            Vector3 worldPosition = transform.position;
            worldPosition.x = position.x;
            worldPosition.y = position.y;
            transform.position = worldPosition;
        }

        private void StopBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }
    }

    [AddComponentMenu("")]
    internal sealed class BossAttachedProjectileMotion : MonoBehaviour
    {
        private EnemyProjectile projectile;
        private Rigidbody2D body;
        private Transform anchor;
        private float radius;
        private float angleDegrees;
        private float rotateDegreesPerSecond;
        private float durationSeconds;
        private float elapsed;
        private bool destroyOnComplete;

        public void Initialize(
            Transform nextAnchor,
            float nextRadius,
            float nextAngleDegrees,
            float nextRotateDegreesPerSecond,
            float nextDurationSeconds,
            bool nextDestroyOnComplete)
        {
            projectile = GetComponent<EnemyProjectile>();
            body = GetComponent<Rigidbody2D>();
            anchor = nextAnchor;
            radius = Mathf.Max(0f, nextRadius);
            angleDegrees = nextAngleDegrees;
            rotateDegreesPerSecond = nextRotateDegreesPerSecond;
            durationSeconds = Mathf.Max(0f, nextDurationSeconds);
            destroyOnComplete = nextDestroyOnComplete;
            Tick(0f);
        }

        private void LateUpdate()
        {
            if (projectile == null || anchor == null || PlayerCombatController.IsExecutionCinematicActive)
            {
                StopBody();
                return;
            }

            Tick(Time.deltaTime);
        }

        private void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            float currentAngle = angleDegrees + rotateDegreesPerSecond * elapsed;
            Vector2 nextPosition = (Vector2)anchor.position + BossActionContext.AngleToDirection(currentAngle) * radius;
            MoveTo(nextPosition, deltaTime);

            if (destroyOnComplete && durationSeconds > 0f && elapsed >= durationSeconds)
            {
                projectile.DestroyFromOwner();
            }
        }

        private void MoveTo(Vector2 position, float deltaTime)
        {
            Vector2 previous = body != null ? body.position : (Vector2)transform.position;
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = deltaTime > 0f ? (position - previous) / deltaTime : Vector2.zero;
            }

            Vector3 worldPosition = transform.position;
            worldPosition.x = position.x;
            worldPosition.y = position.y;
            transform.position = worldPosition;
        }

        private void StopBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }
    }

    [AddComponentMenu("")]
    internal sealed class BossPointOrbitProjectileMotion : MonoBehaviour
    {
        private EnemyProjectile projectile;
        private Rigidbody2D body;
        private Vector2 center;
        private float radius;
        private float angleDegrees;
        private float angularSpeedDegrees;
        private float durationSeconds;
        private float elapsed;
        private bool destroyOnComplete;

        public void Initialize(
            Vector2 nextCenter,
            float nextRadius,
            float nextAngleDegrees,
            float nextAngularSpeedDegrees,
            float nextDurationSeconds,
            bool nextDestroyOnComplete)
        {
            projectile = GetComponent<EnemyProjectile>();
            body = GetComponent<Rigidbody2D>();
            center = nextCenter;
            radius = Mathf.Max(0f, nextRadius);
            angleDegrees = nextAngleDegrees;
            angularSpeedDegrees = nextAngularSpeedDegrees;
            durationSeconds = Mathf.Max(0f, nextDurationSeconds);
            destroyOnComplete = nextDestroyOnComplete;
            Tick(0f);
        }

        private void LateUpdate()
        {
            if (projectile == null || PlayerCombatController.IsExecutionCinematicActive)
            {
                StopBody();
                return;
            }

            Tick(Time.deltaTime);
        }

        private void Tick(float deltaTime)
        {
            elapsed += deltaTime;
            angleDegrees += angularSpeedDegrees * deltaTime;
            Vector2 nextPosition = center + BossActionContext.AngleToDirection(angleDegrees) * radius;
            MoveTo(nextPosition, deltaTime);

            if (destroyOnComplete && durationSeconds > 0f && elapsed >= durationSeconds)
            {
                projectile.DestroyFromOwner();
            }
        }

        private void MoveTo(Vector2 position, float deltaTime)
        {
            Vector2 previous = body != null ? body.position : (Vector2)transform.position;
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = deltaTime > 0f ? (position - previous) / deltaTime : Vector2.zero;
            }

            Vector3 worldPosition = transform.position;
            worldPosition.x = position.x;
            worldPosition.y = position.y;
            transform.position = worldPosition;
        }

        private void StopBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }
    }

    [AddComponentMenu("")]
    internal sealed class BossDelayedPathProjectileMotion : MonoBehaviour
    {
        private EnemyProjectile projectile;
        private Rigidbody2D body;
        private Vector2 startPosition;
        private Vector2 holdPosition;
        private Vector2[] points;
        private float[] cumulativeLengths;
        private float totalLength;
        private float lineupDurationSeconds;
        private float sweepStartSeconds;
        private float durationSeconds;
        private float elapsed;
        private bool destroyOnComplete;

        public void Initialize(
            Vector2 nextStartPosition,
            Vector2 nextHoldPosition,
            Vector2[] nextPoints,
            float nextLineupDurationSeconds,
            float nextSweepStartSeconds,
            float nextDurationSeconds,
            bool nextDestroyOnComplete)
        {
            projectile = GetComponent<EnemyProjectile>();
            body = GetComponent<Rigidbody2D>();
            startPosition = nextStartPosition;
            holdPosition = nextHoldPosition;
            points = nextPoints;
            lineupDurationSeconds = Mathf.Max(0f, nextLineupDurationSeconds);
            sweepStartSeconds = Mathf.Max(lineupDurationSeconds, nextSweepStartSeconds);
            durationSeconds = Mathf.Max(0.01f, nextDurationSeconds);
            destroyOnComplete = nextDestroyOnComplete;
            BuildLengths();
            MoveTo(startPosition, 0f);
        }

        private void LateUpdate()
        {
            if (projectile == null || points == null || points.Length == 0 || PlayerCombatController.IsExecutionCinematicActive)
            {
                StopBody();
                return;
            }

            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            if (elapsed < lineupDurationSeconds)
            {
                float lineupT = lineupDurationSeconds > 0.0001f ? elapsed / lineupDurationSeconds : 1f;
                MoveTo(Vector2.Lerp(startPosition, holdPosition, lineupT), deltaTime);
                return;
            }

            if (elapsed < sweepStartSeconds)
            {
                MoveTo(holdPosition, deltaTime);
                return;
            }

            float t = Mathf.Clamp01((elapsed - sweepStartSeconds) / durationSeconds);
            MoveTo(Evaluate(t), deltaTime);

            if (destroyOnComplete && t >= 1f)
            {
                projectile.DestroyFromOwner();
            }
        }

        private void BuildLengths()
        {
            if (points == null || points.Length < 2)
            {
                cumulativeLengths = new[] { 0f };
                totalLength = 0f;
                return;
            }

            cumulativeLengths = new float[points.Length];
            totalLength = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                totalLength += Vector2.Distance(points[i - 1], points[i]);
                cumulativeLengths[i] = totalLength;
            }
        }

        private Vector2 Evaluate(float t)
        {
            if (points.Length == 1 || totalLength <= 0.0001f)
            {
                return points[0];
            }

            float targetLength = totalLength * t;
            for (int i = 1; i < points.Length; i++)
            {
                if (targetLength > cumulativeLengths[i])
                {
                    continue;
                }

                float segmentLength = cumulativeLengths[i] - cumulativeLengths[i - 1];
                float segmentT = segmentLength > 0.0001f
                    ? (targetLength - cumulativeLengths[i - 1]) / segmentLength
                    : 1f;
                return Vector2.Lerp(points[i - 1], points[i], segmentT);
            }

            return points[^1];
        }

        private void MoveTo(Vector2 position, float deltaTime)
        {
            Vector2 previous = body != null ? body.position : (Vector2)transform.position;
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = deltaTime > 0f ? (position - previous) / deltaTime : Vector2.zero;
            }

            Vector3 worldPosition = transform.position;
            worldPosition.x = position.x;
            worldPosition.y = position.y;
            transform.position = worldPosition;
        }

        private void StopBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }
    }

    [AddComponentMenu("")]
    internal sealed class BossPathProjectileMotion : MonoBehaviour
    {
        private EnemyProjectile projectile;
        private Rigidbody2D body;
        private Vector2[] points;
        private float[] cumulativeLengths;
        private float totalLength;
        private float durationSeconds;
        private float elapsed;
        private bool destroyOnComplete;

        public void Initialize(Vector2[] nextPoints, float nextDurationSeconds, bool nextDestroyOnComplete)
        {
            projectile = GetComponent<EnemyProjectile>();
            body = GetComponent<Rigidbody2D>();
            points = nextPoints;
            durationSeconds = Mathf.Max(0.01f, nextDurationSeconds);
            destroyOnComplete = nextDestroyOnComplete;
            BuildLengths();
            if (points != null && points.Length > 0)
            {
                MoveTo(points[0], 0f);
            }
        }

        private void LateUpdate()
        {
            if (projectile == null || points == null || points.Length == 0 || PlayerCombatController.IsExecutionCinematicActive)
            {
                StopBody();
                return;
            }

            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            float t = Mathf.Clamp01(elapsed / durationSeconds);
            MoveTo(Evaluate(t), deltaTime);

            if (destroyOnComplete && elapsed >= durationSeconds)
            {
                projectile.DestroyFromOwner();
            }
        }

        private void BuildLengths()
        {
            if (points == null || points.Length < 2)
            {
                cumulativeLengths = new[] { 0f };
                totalLength = 0f;
                return;
            }

            cumulativeLengths = new float[points.Length];
            totalLength = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                totalLength += Vector2.Distance(points[i - 1], points[i]);
                cumulativeLengths[i] = totalLength;
            }
        }

        private Vector2 Evaluate(float t)
        {
            if (points.Length == 1 || totalLength <= 0.0001f)
            {
                return points[0];
            }

            float targetLength = totalLength * t;
            for (int i = 1; i < points.Length; i++)
            {
                if (targetLength > cumulativeLengths[i])
                {
                    continue;
                }

                float segmentLength = cumulativeLengths[i] - cumulativeLengths[i - 1];
                float segmentT = segmentLength > 0.0001f
                    ? (targetLength - cumulativeLengths[i - 1]) / segmentLength
                    : 1f;
                return Vector2.Lerp(points[i - 1], points[i], segmentT);
            }

            return points[^1];
        }

        private void MoveTo(Vector2 position, float deltaTime)
        {
            Vector2 previous = body != null ? body.position : (Vector2)transform.position;
            if (body != null)
            {
                body.position = position;
                body.linearVelocity = deltaTime > 0f ? (position - previous) / deltaTime : Vector2.zero;
            }

            Vector3 worldPosition = transform.position;
            worldPosition.x = position.x;
            worldPosition.y = position.y;
            transform.position = worldPosition;
        }

        private void StopBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }
    }
}
