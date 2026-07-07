using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionConductorPlayerPathSideFireAction : BossAction
    {
        [Header("Player Path")]
        [SerializeField] private MinionGraphPlayerPathMode mode;
        [SerializeField, Min(0.1f)] private float distanceFromPlayer = 2.8f;
        [SerializeField, Min(0f)] private float moveToStartSeconds = 0.6f;
        [SerializeField, Min(0.05f)] private float moveSeconds = 2f;
        [SerializeField] private bool waitForPlayerPathDuration = true;

        [Header("Side Fire")]
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField] private MinionGraphProjectileOriginSpec minionOrigin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, Min(0.05f)] private float fireSeconds = 1f;
        [SerializeField, Min(0.01f)] private float fireInterval = 0.18f;
        [SerializeField, Range(1f, 179f)] private float sideFireAngleDegrees = 90f;
        [SerializeField] private MinionGraphSideFireOriginMode originMode = MinionGraphSideFireOriginMode.BodySides;
        [SerializeField, Min(0f)] private float bodySideSpacing = 0.35f;
        [SerializeField] private bool waitForSideFireDuration = true;

        [Header("Precast Grid Indicator")]
        [SerializeField] private bool drawPrecastGridIndicator = true;
        [SerializeField] private Color gridIndicatorColor = new(0.62f, 0.92f, 1f, 0.66f);
        [SerializeField, Min(0.001f)] private float gridIndicatorWidth = 0.035f;
        [SerializeField] private int gridIndicatorSortingOrder = 66;
        [SerializeField, Min(1)] private int indicatorPathCount = 4;
        [SerializeField, Min(1)] private int maxIndicatorShotsPerPath = 64;
        [SerializeField, Min(0f)] private float indicatorRetainSeconds = 0.2f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryResolveProjectile(
                    context,
                    projectileName,
                    out IMinionPatternHost host,
                    out BossProjectileSettings projectile))
            {
                yield break;
            }

            Vector2 pathCenter = ResolvePathCenter(context);
            ConductorScoreLaneRushIndicatorVisual gridIndicator =
                CreatePrecastGridIndicator(context, host, projectile, pathCenter);

            MinionGraphCommandRequest pathRequest = MinionGraphCommandRequest.PlayerPath(
                mode,
                distanceFromPlayer,
                moveToStartSeconds,
                moveSeconds);
            float pathDuration = host.CommandMinions(pathRequest);
            yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(
                context,
                pathDuration,
                waitForPlayerPathDuration);

            MinionGraphProjectileFireSpec fireSpec = new MinionGraphProjectileFireSpec(minionOrigin, aim, effects, context)
                .WithProjectilePathIndicatorSuppressed();
            yield return MinionGraphCommandRunner.WaitWindupIfNeeded(context, windupSeconds);
            MinionGraphCommandRequest fireRequest = MinionGraphCommandRequest.SideFire(
                projectile,
                fireSeconds,
                fireInterval,
                fireSpec,
                sideFireAngleDegrees,
                originMode,
                bodySideSpacing);
            float fireDuration = host.CommandMinions(fireRequest);

            if (waitForSideFireDuration)
            {
                yield return MinionGraphCommandRunner.WaitForDurationIfNeeded(context, fireDuration, true);
                gridIndicator?.ClearAndDestroy();
            }
            else if (gridIndicator != null)
            {
                UnityEngine.Object.Destroy(
                    gridIndicator.gameObject,
                    Mathf.Max(0.01f, fireDuration + indicatorRetainSeconds));
            }
        }

        private ConductorScoreLaneRushIndicatorVisual CreatePrecastGridIndicator(
            BossActionContext context,
            IMinionPatternHost host,
            BossProjectileSettings projectile,
            Vector2 pathCenter)
        {
            if (!drawPrecastGridIndicator || projectile == null)
            {
                return null;
            }

            GameObject indicatorObject = new("ConductorPlayerPathSideFireGridIndicator");
            ConductorScoreLaneRushIndicatorVisual visual =
                indicatorObject.AddComponent<ConductorScoreLaneRushIndicatorVisual>();
            visual.Configure(gridIndicatorColor, gridIndicatorWidth, gridIndicatorSortingOrder);

            int pathCount = ResolveIndicatorPathCount(host);
            int shotCount = GetIndicatorShotCount();
            float indicatorLength = Mathf.Max(0.1f, projectile.Speed * projectile.Lifetime);
            float safeSideAngle = Mathf.Clamp(sideFireAngleDegrees, 1f, 179f);
            int lineIndex = 0;

            for (int pathIndex = 0; pathIndex < pathCount; pathIndex++)
            {
                MinionGraphPlayerPathType pathType = GetPlayerPathType(mode, pathIndex);
                GetPlayerPathOffsets(pathType, distanceFromPlayer, out Vector2 startOffset, out Vector2 endOffset);
                Vector2 pathDirection = (endOffset - startOffset).sqrMagnitude > 0.0001f
                    ? (endOffset - startOffset).normalized
                    : Vector2.right;

                for (int shotIndex = 0; shotIndex < shotCount; shotIndex++)
                {
                    float shotSeconds = Mathf.Max(0.01f, fireInterval) * shotIndex;
                    float pathElapsed = waitForPlayerPathDuration
                        ? moveToStartSeconds + moveSeconds
                        : windupSeconds + shotSeconds;
                    Vector2 origin = pathCenter + GetPathOffsetAtTime(startOffset, endOffset, pathElapsed);
                    Vector2 aimDirection = ResolveAimDirection(context, origin);
                    Vector2 forward = originMode == MinionGraphSideFireOriginMode.BodySides
                        ? pathDirection
                        : aimDirection;

                    GetSideFireOrigins(
                        origin,
                        forward,
                        out Vector2 firstOrigin,
                        out Vector2 secondOrigin);
                    AddIndicatorLine(
                        visual,
                        lineIndex++,
                        firstOrigin,
                        RotateDirection(forward, safeSideAngle),
                        indicatorLength);
                    AddIndicatorLine(
                        visual,
                        lineIndex++,
                        secondOrigin,
                        RotateDirection(forward, -safeSideAngle),
                        indicatorLength);
                }
            }

            return visual;
        }

        private int ResolveIndicatorPathCount(IMinionPatternHost host)
        {
            IReadOnlyList<Minion> minions = host?.GetControlledMinionsForGraph();
            int activeCount = 0;
            if (minions != null)
            {
                for (int i = 0; i < minions.Count; i++)
                {
                    if (minions[i] != null && minions[i].Health != null && !minions[i].Health.IsDead)
                    {
                        activeCount++;
                    }
                }
            }

            return Mathf.Max(1, Mathf.Min(Mathf.Max(1, indicatorPathCount), activeCount > 0 ? activeCount : 4));
        }

        private int GetIndicatorShotCount()
        {
            float duration = Mathf.Max(0.05f, fireSeconds);
            float interval = Mathf.Max(0.01f, fireInterval);
            int count = 0;
            float nextFireAt = 0f;
            while (nextFireAt < duration && count < maxIndicatorShotsPerPath)
            {
                count++;
                nextFireAt += interval;
            }

            return Mathf.Max(1, count);
        }

        private Vector2 ResolvePathCenter(BossActionContext context)
        {
            if (context != null && context.Boss != null && context.Boss.Player != null)
            {
                return context.Boss.Player.position;
            }

            return context != null && context.Boss != null
                ? context.Boss.transform.position
                : Vector2.zero;
        }

        private Vector2 ResolveAimDirection(BossActionContext context, Vector2 origin)
        {
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            Vector2 direction = aimSpec.GetDirection(context, origin);
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
        }

        private Vector2 GetPathOffsetAtTime(Vector2 startOffset, Vector2 endOffset, float pathElapsed)
        {
            float safeMoveSeconds = Mathf.Max(0.05f, moveSeconds);
            if (pathElapsed <= moveToStartSeconds)
            {
                return startOffset;
            }

            float t = Mathf.Clamp01((pathElapsed - moveToStartSeconds) / safeMoveSeconds);
            return Vector2.Lerp(startOffset, endOffset, t);
        }

        private void GetSideFireOrigins(
            Vector2 origin,
            Vector2 forward,
            out Vector2 firstOrigin,
            out Vector2 secondOrigin)
        {
            firstOrigin = origin;
            secondOrigin = origin;
            if (originMode != MinionGraphSideFireOriginMode.BodySides || bodySideSpacing <= 0f)
            {
                return;
            }

            Vector2 safeForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector2.right;
            Vector2 side = new(-safeForward.y, safeForward.x);
            Vector2 offset = side * bodySideSpacing;
            firstOrigin += offset;
            secondOrigin -= offset;
        }

        private static void AddIndicatorLine(
            ConductorScoreLaneRushIndicatorVisual visual,
            int index,
            Vector2 origin,
            Vector2 direction,
            float length)
        {
            if (visual == null)
            {
                return;
            }

            Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            visual.SetLane(index, origin, origin + safeDirection * Mathf.Max(0.1f, length));
            visual.SetProgress(index, 1f);
        }

        private static Vector2 RotateDirection(Vector2 direction, float degrees)
        {
            Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(
                safeDirection.x * cos - safeDirection.y * sin,
                safeDirection.x * sin + safeDirection.y * cos);
        }

        private static MinionGraphPlayerPathType GetPlayerPathType(MinionGraphPlayerPathMode pathMode, int index)
        {
            int normalizedIndex = Mathf.Abs(index) % 4;
            if (pathMode == MinionGraphPlayerPathMode.Diagonal)
            {
                return normalizedIndex switch
                {
                    1 => MinionGraphPlayerPathType.DiagonalRightTopToLeftBottom,
                    2 => MinionGraphPlayerPathType.DiagonalRightBottomToLeftTop,
                    3 => MinionGraphPlayerPathType.DiagonalLeftBottomToRightTop,
                    _ => MinionGraphPlayerPathType.DiagonalLeftTopToRightBottom
                };
            }

            return normalizedIndex switch
            {
                1 => MinionGraphPlayerPathType.VerticalTopToBottom,
                2 => MinionGraphPlayerPathType.HorizontalRightToLeft,
                3 => MinionGraphPlayerPathType.VerticalBottomToTop,
                _ => MinionGraphPlayerPathType.HorizontalLeftToRight
            };
        }

        private static void GetPlayerPathOffsets(
            MinionGraphPlayerPathType pathType,
            float distanceFromPlayer,
            out Vector2 startOffset,
            out Vector2 endOffset)
        {
            float distance = Mathf.Max(0.1f, distanceFromPlayer);
            switch (pathType)
            {
                case MinionGraphPlayerPathType.VerticalTopToBottom:
                    startOffset = Vector2.up * distance;
                    endOffset = Vector2.down * distance;
                    break;
                case MinionGraphPlayerPathType.HorizontalRightToLeft:
                    startOffset = Vector2.right * distance;
                    endOffset = Vector2.left * distance;
                    break;
                case MinionGraphPlayerPathType.VerticalBottomToTop:
                    startOffset = Vector2.down * distance;
                    endOffset = Vector2.up * distance;
                    break;
                case MinionGraphPlayerPathType.DiagonalLeftTopToRightBottom:
                    startOffset = new Vector2(-distance, distance);
                    endOffset = new Vector2(distance, -distance);
                    break;
                case MinionGraphPlayerPathType.DiagonalRightTopToLeftBottom:
                    startOffset = new Vector2(distance, distance);
                    endOffset = new Vector2(-distance, -distance);
                    break;
                case MinionGraphPlayerPathType.DiagonalRightBottomToLeftTop:
                    startOffset = new Vector2(distance, -distance);
                    endOffset = new Vector2(-distance, distance);
                    break;
                case MinionGraphPlayerPathType.DiagonalLeftBottomToRightTop:
                    startOffset = new Vector2(-distance, -distance);
                    endOffset = new Vector2(distance, distance);
                    break;
                default:
                    startOffset = Vector2.left * distance;
                    endOffset = Vector2.right * distance;
                    break;
            }
        }
    }
}
