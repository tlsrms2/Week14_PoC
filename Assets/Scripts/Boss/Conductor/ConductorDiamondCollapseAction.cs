using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class ConductorDiamondCollapseAction : BossAction,
        IBossProjectileEmissionAction,
        IConductorCueOverlayExplicitStartSource
    {
        private const int DroneCount = 4;
        private const float LaneShrinkPushPadding = 0.02f;

        [Serializable]
        public sealed class VolleyStage
        {
            [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
            [SerializeField, Min(0f)] private float fireSeconds = 1f;
            [SerializeField, Min(0f)] private float fireInterval = 0.12f;
            [SerializeField] private bool shrinkAfter;
            [SerializeField, Min(0.01f)] private float nextRadius = 1.5f;
            [SerializeField, Min(0f)] private float firePauseBeforeShrinkSeconds = 0.25f;
            [SerializeField, Min(0.01f)] private float shrinkMoveSeconds = 0.15f;

            public VolleyStage()
            {
            }

            public VolleyStage(bool shrinkAfter)
            {
                this.shrinkAfter = shrinkAfter;
            }

            public string ProjectileName => projectileName?.Trim();
            public float FireSeconds => Mathf.Max(0f, fireSeconds);
            public float FireInterval => Mathf.Max(0f, fireInterval);
            public bool ShrinkAfter => shrinkAfter;
            public float NextRadius => Mathf.Max(0.01f, nextRadius);
            public float FirePauseBeforeShrinkSeconds => Mathf.Max(0f, firePauseBeforeShrinkSeconds);
            public float ShrinkMoveSeconds => Mathf.Max(0.01f, shrinkMoveSeconds);
        }

        [Header("Formation")]
        [SerializeField] private Vector2 centerPosition;
        [SerializeField, Min(0.01f)] private float initialRadius = 3f;
        [SerializeField, Min(0.01f)] private float initialAlignSeconds = 0.5f;
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField] private BossGraphEffectSettings effects = new();

        [Header("Prelude Wander")]
        [SerializeField, Min(0f)] private float preludeWanderSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float preludeWanderRadius = 2.8f;
        [SerializeField, Min(0.1f)] private float preludeWanderRetargetSeconds = 1.5f;

        [Header("Score Lane Preview")]
        [SerializeField, Min(0.1f)] private float previewLaneDistance = 3f;
        [SerializeField, Min(0f)] private float previewLaneSpacing = 0.7f;
        [SerializeField, Min(0f)] private float previewLaneLength = 8f;
        [SerializeField, Min(1f)] private float previewInitialLaneDistanceMultiplier = 1.5f;
        [SerializeField, Min(0f)] private float previewInitialLaneHoldSeconds = 0.2f;
        [SerializeField, Min(0f)] private float previewLaneShrinkSeconds = 0.6f;
        [SerializeField, Min(0f)] private float previewLaneFadeSeconds = 0.14f;
        [SerializeField] private Color previewLaneColor = new(0.62f, 0.92f, 1f, 0.66f);
        [SerializeField, Min(0.001f)] private float previewLaneWidth = 0.035f;
        [SerializeField] private int previewLaneSortingOrder = 66;
        [SerializeField, Min(1)] private int previewLaneLineCount = 4;

        [Header("Diamond Volleys")]
        [SerializeField] private List<VolleyStage> volleyStages = new()
        {
            new VolleyStage(true),
            new VolleyStage()
        };

        [Header("Final Center Shot")]
        [SerializeField, BossGraphProjectileName] private string finalProjectileName = "Default";
        [SerializeField, Min(0f)] private float finalShotDelaySeconds = 0.15f;

        [Header("Boss Reposition")]
        [SerializeField] private bool repositionBossAtPatternStart;
        [SerializeField] private Vector2 bossTargetPosition;
        [SerializeField, Min(0.01f)] private float bossMoveSpeedMultiplier = 1f;
        [SerializeField, Min(0.001f)] private float bossArriveDistance = 0.04f;
        [SerializeField, Min(0.01f)] private float bossMoveTimeoutSeconds = 5f;

        private bool finalCenterShotTriggered;
        private bool executionFinished;

        public bool ShouldStartConductorCueOverlay => finalCenterShotTriggered;
        public bool IsConductorCueOverlaySourceFinished => executionFinished;

        public void ClearConductorCueOverlayStart()
        {
            finalCenterShotTriggered = false;
            executionFinished = false;
        }

        public override IEnumerator Execute(BossActionContext context)
        {
            finalCenterShotTriggered = false;
            executionFinished = false;
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || volleyStages == null
                || volleyStages.Count == 0)
            {
                executionFinished = true;
                yield break;
            }

            yield return host.EnsureMinionCount(DroneCount);
            List<Minion> drones = GetDrones(host.GetControlledMinionsForGraph());
            if (drones.Count != DroneCount)
            {
                executionFinished = true;
                yield break;
            }

            Coroutine bossMoveRoutine = repositionBossAtPatternStart && context?.Boss != null
                ? context.Boss.StartCoroutine(MoveBossToTargetPosition(context))
                : null;
            try
            {
                CommandPreludeWander(drones, windupSeconds);
                yield return MinionGraphCommandRunner.WaitWindupIfNeeded(context, windupSeconds);

                float currentRadius = Mathf.Max(0.01f, initialRadius);
                CommandDronesToCardinalSlots(drones, currentRadius, initialAlignSeconds);
                yield return ShowScoreLanePreview(context);
                yield return context.WaitSeconds(Mathf.Max(
                    0f,
                    initialAlignSeconds - GetScoreLanePreviewSeconds()));

                for (int i = 0; i < volleyStages.Count; i++)
                {
                    VolleyStage stage = volleyStages[i];
                    if (stage == null)
                    {
                        continue;
                    }

                    yield return FireStage(context, host, drones, stage);
                    if (!stage.ShrinkAfter || stage.NextRadius >= currentRadius)
                    {
                        continue;
                    }

                    yield return context.WaitSeconds(stage.FirePauseBeforeShrinkSeconds);
                    currentRadius = stage.NextRadius;
                    CommandDronesToCardinalSlots(drones, currentRadius, stage.ShrinkMoveSeconds);
                    yield return context.WaitSeconds(stage.ShrinkMoveSeconds);
                }

                yield return context.WaitSeconds(finalShotDelaySeconds);
                finalCenterShotTriggered = true;
                FireCenterVolley(context, host, drones);
                if (bossMoveRoutine != null)
                {
                    yield return bossMoveRoutine;
                }
            }
            finally
            {
                if (bossMoveRoutine != null && context?.Boss != null)
                {
                    context.Boss.StopCoroutine(bossMoveRoutine);
                }

                executionFinished = true;
            }
        }

        private float GetScoreLanePreviewSeconds()
        {
            return Mathf.Max(0f, previewInitialLaneHoldSeconds)
                + (previewInitialLaneDistanceMultiplier > 1f
                    ? Mathf.Max(0f, previewLaneShrinkSeconds)
                    : 0f)
                + Mathf.Max(0f, previewLaneFadeSeconds);
        }

        private void CommandPreludeWander(IReadOnlyList<Minion> drones, float duration)
        {
            duration = Mathf.Max(0f, duration);
            if (drones == null || duration <= 0f)
            {
                return;
            }

            for (int i = 0; i < drones.Count; i++)
            {
                drones[i]?.CommandWander(
                    duration,
                    preludeWanderSpeed,
                    preludeWanderRadius,
                    preludeWanderRetargetSeconds);
            }
        }

        private IEnumerator ShowScoreLanePreview(BossActionContext context)
        {
            float targetDistance = Mathf.Max(0.1f, previewLaneDistance);
            float initialDistance = targetDistance * Mathf.Max(1f, previewInitialLaneDistanceMultiplier);
            ConductorScoreLaneRushIndicatorVisual visual = CreateScoreLanePreview(initialDistance);
            try
            {
                yield return context.WaitSeconds(previewInitialLaneHoldSeconds);
                yield return ShrinkScoreLanePreview(context, visual, initialDistance, targetDistance);
                yield return FadeScoreLanePreview(context, visual);
            }
            finally
            {
                visual?.ClearAndDestroy();
            }
        }

        private ConductorScoreLaneRushIndicatorVisual CreateScoreLanePreview(float distance)
        {
            GameObject indicatorObject = new("ConductorDiamondCollapseScoreLanePreview");
            ConductorScoreLaneRushIndicatorVisual visual = indicatorObject.AddComponent<ConductorScoreLaneRushIndicatorVisual>();
            visual.Configure(previewLaneColor, previewLaneWidth, previewLaneSortingOrder);
            visual.ConfigurePlayerBlocking(true);
            visual.ConfigureClearOnExecutionCinematic(true);
            int visualLineIndex = 0;
            int lineCount = Mathf.Max(1, previewLaneLineCount);
            for (int sideIndex = 0; sideIndex < DroneCount; sideIndex++)
            {
                for (int laneIndex = 0; laneIndex < lineCount; laneIndex++)
                {
                    BuildScoreLanePreviewLine(
                        sideIndex,
                        laneIndex,
                        lineCount,
                        distance,
                        out Vector2 start,
                        out Vector2 end);
                    visual.SetLane(visualLineIndex, start, end);
                    visual.SetProgress(visualLineIndex, 1f);
                    visualLineIndex++;
                }
            }
            return visual;
        }

        private void BuildScoreLanePreviewLine(
            int sideIndex,
            int laneIndex,
            int lineCount,
            float distance,
            out Vector2 start,
            out Vector2 end)
        {
            Vector2 sideOffset = GetScoreLaneSideOffset(sideIndex);
            bool isHorizontal = sideIndex == 0 || sideIndex == 2;
            Vector2 lineAxis = isHorizontal ? Vector2.up : Vector2.right;
            Vector2 travelDirection = isHorizontal ? Vector2.right : Vector2.up;
            float centeredOffset = (Mathf.Max(1, lineCount) - 1) * 0.5f;
            Vector2 lineCenter = centerPosition
                + sideOffset * Mathf.Max(0.1f, distance)
                + lineAxis * ((laneIndex - centeredOffset) * Mathf.Max(0f, previewLaneSpacing));
            float halfLength = Mathf.Max(0f, previewLaneLength) * 0.5f;
            start = lineCenter - travelDirection * halfLength;
            end = lineCenter + travelDirection * halfLength;
        }

        private void UpdateScoreLanePreview(ConductorScoreLaneRushIndicatorVisual visual, float distance)
        {
            if (visual == null)
            {
                return;
            }

            int visualLineIndex = 0;
            int lineCount = Mathf.Max(1, previewLaneLineCount);
            for (int sideIndex = 0; sideIndex < DroneCount; sideIndex++)
            {
                for (int laneIndex = 0; laneIndex < lineCount; laneIndex++)
                {
                    BuildScoreLanePreviewLine(
                        sideIndex,
                        laneIndex,
                        lineCount,
                        distance,
                        out Vector2 start,
                        out Vector2 end);
                    visual.SetLane(visualLineIndex, start, end);
                    visual.SetProgress(visualLineIndex, 1f);
                    visualLineIndex++;
                }
            }
        }

        private IEnumerator ShrinkScoreLanePreview(
            BossActionContext context,
            ConductorScoreLaneRushIndicatorVisual visual,
            float initialDistance,
            float targetDistance)
        {
            if (visual == null || initialDistance <= targetDistance)
            {
                yield break;
            }

            if (previewLaneShrinkSeconds <= 0f)
            {
                UpdateScoreLanePreview(visual, targetDistance);
                PushPlayerWithShrinkingScoreLanes(context, initialDistance, targetDistance);
                yield break;
            }

            float elapsed = 0f;
            float previousDistance = initialDistance;
            while (elapsed < previewLaneShrinkSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                float progress = Mathf.Clamp01(elapsed / previewLaneShrinkSeconds);
                float currentDistance = Mathf.Lerp(initialDistance, targetDistance, progress);
                UpdateScoreLanePreview(visual, currentDistance);
                PushPlayerWithShrinkingScoreLanes(context, previousDistance, currentDistance);
                previousDistance = currentDistance;
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            UpdateScoreLanePreview(visual, targetDistance);
            PushPlayerWithShrinkingScoreLanes(context, previousDistance, targetDistance);
        }

        private IEnumerator FadeScoreLanePreview(
            BossActionContext context,
            ConductorScoreLaneRushIndicatorVisual visual)
        {
            if (visual == null)
            {
                yield break;
            }

            if (previewLaneFadeSeconds <= 0f)
            {
                visual.SetAlpha(0f);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < previewLaneFadeSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                visual.SetAlpha(1f - Mathf.Clamp01(elapsed / previewLaneFadeSeconds));
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            visual.SetAlpha(0f);
        }

        private static Vector2 GetScoreLaneSideOffset(int sideIndex)
        {
            return sideIndex switch
            {
                0 => Vector2.up,
                1 => Vector2.right,
                2 => Vector2.down,
                _ => Vector2.left
            };
        }

        private void PushPlayerWithShrinkingScoreLanes(
            BossActionContext context,
            float previousDistance,
            float currentDistance)
        {
            if (currentDistance >= previousDistance
                || context?.Boss?.Player == null
                || !TryGetPlayerBodyAndColliders(context.Boss.Player, out Rigidbody2D body, out Collider2D[] colliders))
            {
                return;
            }

            int lineCount = Mathf.Max(1, previewLaneLineCount);
            for (int sideIndex = 0; sideIndex < DroneCount; sideIndex++)
            {
                for (int laneIndex = 0; laneIndex < lineCount; laneIndex++)
                {
                    if (!TryGetPlayerBounds(colliders, out Bounds playerBounds))
                    {
                        return;
                    }

                    BuildScoreLanePreviewLine(
                        sideIndex,
                        laneIndex,
                        lineCount,
                        previousDistance,
                        out Vector2 previousStart,
                        out Vector2 previousEnd);
                    BuildScoreLanePreviewLine(
                        sideIndex,
                        laneIndex,
                        lineCount,
                        currentDistance,
                        out Vector2 currentStart,
                        out Vector2 currentEnd);
                    if (!TryGetScoreLaneShrinkPushDisplacement(
                            sideIndex,
                            playerBounds,
                            previousStart,
                            previousEnd,
                            currentStart,
                            currentEnd,
                            out Vector2 displacement))
                    {
                        continue;
                    }

                    Vector2 currentPosition = body.position;
                    body.position = GroundMovementConstraint.ClampStep(
                        currentPosition,
                        currentPosition + displacement,
                        colliders);
                    RemovePlayerOutwardVelocity(body, GetScoreLaneSideOffset(sideIndex));
                }
            }
        }

        private static bool TryGetScoreLaneShrinkPushDisplacement(
            int sideIndex,
            Bounds playerBounds,
            Vector2 previousStart,
            Vector2 previousEnd,
            Vector2 currentStart,
            Vector2 currentEnd,
            out Vector2 displacement)
        {
            displacement = Vector2.zero;
            bool isHorizontal = sideIndex == 0 || sideIndex == 2;
            if (isHorizontal)
            {
                float segmentMinX = Mathf.Min(currentStart.x, currentEnd.x);
                float segmentMaxX = Mathf.Max(currentStart.x, currentEnd.x);
                if (playerBounds.max.x < segmentMinX || playerBounds.min.x > segmentMaxX)
                {
                    return false;
                }

                float previousY = previousStart.y;
                float currentY = currentStart.y;
                if (sideIndex == 0)
                {
                    if (playerBounds.max.y <= currentY - LaneShrinkPushPadding
                        || playerBounds.min.y > previousY + LaneShrinkPushPadding)
                    {
                        return false;
                    }

                    displacement.y = currentY - LaneShrinkPushPadding - playerBounds.max.y;
                    return displacement.y < 0f;
                }

                if (playerBounds.min.y >= currentY + LaneShrinkPushPadding
                    || playerBounds.max.y < previousY - LaneShrinkPushPadding)
                {
                    return false;
                }

                displacement.y = currentY + LaneShrinkPushPadding - playerBounds.min.y;
                return displacement.y > 0f;
            }

            float segmentMinY = Mathf.Min(currentStart.y, currentEnd.y);
            float segmentMaxY = Mathf.Max(currentStart.y, currentEnd.y);
            if (playerBounds.max.y < segmentMinY || playerBounds.min.y > segmentMaxY)
            {
                return false;
            }

            float previousX = previousStart.x;
            float currentX = currentStart.x;
            if (sideIndex == 3)
            {
                if (playerBounds.min.x >= currentX + LaneShrinkPushPadding
                    || playerBounds.max.x < previousX - LaneShrinkPushPadding)
                {
                    return false;
                }

                displacement.x = currentX + LaneShrinkPushPadding - playerBounds.min.x;
                return displacement.x > 0f;
            }

            if (playerBounds.max.x <= currentX - LaneShrinkPushPadding
                || playerBounds.min.x > previousX + LaneShrinkPushPadding)
            {
                return false;
            }

            displacement.x = currentX - LaneShrinkPushPadding - playerBounds.max.x;
            return displacement.x < 0f;
        }

        private static bool TryGetPlayerBodyAndColliders(
            Transform player,
            out Rigidbody2D body,
            out Collider2D[] colliders)
        {
            body = player != null ? player.GetComponentInParent<Rigidbody2D>() : null;
            if (body == null && player != null)
            {
                body = player.GetComponentInChildren<Rigidbody2D>();
            }

            colliders = body != null ? body.GetComponentsInChildren<Collider2D>() : null;
            return body != null && colliders != null && colliders.Length > 0;
        }

        private static bool TryGetPlayerBounds(Collider2D[] colliders, out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            if (colliders == null)
            {
                return false;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }

            return hasBounds;
        }

        private static void RemovePlayerOutwardVelocity(Rigidbody2D body, Vector2 outwardDirection)
        {
            if (body == null || outwardDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 velocity = body.linearVelocity;
            float outwardSpeed = Vector2.Dot(velocity, outwardDirection);
            if (outwardSpeed > 0f)
            {
                body.linearVelocity = velocity - outwardDirection * outwardSpeed;
            }
        }

        private IEnumerator MoveBossToTargetPosition(BossActionContext context)
        {
            if (!repositionBossAtPatternStart || context?.Boss == null || context.Boss.Body == null)
            {
                yield break;
            }

            float arriveDistance = Mathf.Max(0.001f, bossArriveDistance);
            float arriveDistanceSqr = arriveDistance * arriveDistance;
            float elapsed = 0f;
            while (((Vector2)context.Boss.Body.position - bossTargetPosition).sqrMagnitude > arriveDistanceSqr
                && elapsed < Mathf.Max(0.01f, bossMoveTimeoutSeconds))
            {
                if (context.IsExecutionPaused)
                {
                    context.Boss.Stop();
                    yield return null;
                    continue;
                }

                Vector2 toTarget = bossTargetPosition - context.Boss.Body.position;
                float speed = context.Boss.MoveSpeed * Mathf.Max(0.01f, bossMoveSpeedMultiplier);
                context.Boss.SetMovementVelocity(toTarget.normalized * speed);
                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            context.Boss.Stop();
        }

        private IEnumerator FireStage(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> drones,
            VolleyStage stage)
        {
            BossProjectileSettings projectile = host.ResolveMinionProjectileSettings(stage.ProjectileName);
            if (projectile == null)
            {
                yield break;
            }

            if (stage.FireSeconds <= 0f || stage.FireInterval <= 0f)
            {
                FireDiamondVolley(context, host, drones, projectile);
                yield break;
            }

            float elapsed = 0f;
            float nextFireSeconds = 0f;
            while (elapsed < stage.FireSeconds)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                while (elapsed >= nextFireSeconds && nextFireSeconds < stage.FireSeconds)
                {
                    FireDiamondVolley(context, host, drones, projectile);
                    nextFireSeconds += stage.FireInterval;
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }
        }

        private void FireDiamondVolley(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> drones,
            BossProjectileSettings projectile)
        {
            for (int i = 0; i < DroneCount; i++)
            {
                Minion source = drones[i];
                Minion target = drones[(i + 1) % DroneCount];
                if (source == null || target == null)
                {
                    continue;
                }

                Vector3 origin = source.GetGraphProjectileOrigin();
                Vector2 direction = (Vector2)target.GetGraphProjectileOrigin() - (Vector2)origin;
                FireProjectile(context, host, source, projectile, origin, direction);
            }
        }

        private void FireCenterVolley(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> drones)
        {
            BossProjectileSettings projectile = host.ResolveMinionProjectileSettings(finalProjectileName);
            if (projectile == null)
            {
                return;
            }

            for (int i = 0; i < DroneCount; i++)
            {
                Minion source = drones[i];
                if (source == null)
                {
                    continue;
                }

                Vector3 origin = source.GetGraphProjectileOrigin();
                Vector2 direction = centerPosition - (Vector2)origin;
                FireProjectile(context, host, source, projectile, origin, direction);
            }
        }

        private void CommandDronesToCardinalSlots(
            IReadOnlyList<Minion> drones,
            float radius,
            float moveSeconds)
        {
            for (int i = 0; i < DroneCount; i++)
            {
                Minion minion = drones[i];
                if (minion == null)
                {
                    continue;
                }

                minion.CommandConductorFanBlade(
                    centerPosition,
                    90f * i,
                    radius,
                    moveSeconds,
                    0f,
                    0f);
            }
        }

        private void FireProjectile(
            BossActionContext context,
            IMinionPatternHost host,
            Minion source,
            BossProjectileSettings projectile,
            Vector3 origin,
            Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            EnemyProjectile firedProjectile = host.FireMinionProjectile(
                source,
                projectile,
                origin,
                direction.normalized,
                true);
            if (firedProjectile == null)
            {
                return;
            }

            firedProjectile.ConfigurePathIndicatorSuppressed(true);
            context.PlayOriginBurst(effects, origin);
            context.PlayMuzzleFlashIfEnabled(effects, firedProjectile, direction);
            context.PlayCameraShakeIfEnabled(effects, direction);
        }

        private static List<Minion> GetDrones(IReadOnlyList<Minion> source)
        {
            List<Minion> drones = new(DroneCount);
            if (source == null)
            {
                return drones;
            }

            for (int i = 0; i < source.Count; i++)
            {
                Minion minion = source[i];
                if (minion != null && minion.Health != null && !minion.Health.IsDead)
                {
                    drones.Add(minion);
                }
            }

            drones.Sort((left, right) => GetSlotNumber(left).CompareTo(GetSlotNumber(right)));
            if (drones.Count > DroneCount)
            {
                drones.RemoveRange(DroneCount, drones.Count - DroneCount);
            }

            return drones;
        }

        private static int GetSlotNumber(Minion minion)
        {
            return minion != null && minion.HasOwnerSlotNumber ? minion.OwnerSlotNumber : int.MaxValue;
        }
    }
}
