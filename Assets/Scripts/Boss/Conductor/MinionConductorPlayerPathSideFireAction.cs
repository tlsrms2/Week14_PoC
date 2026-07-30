using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum ConductorPlayerPathSideFireMode
    {
        HorizontalVertical,
        Diagonal,
        HorizontalVerticalThenDiagonal
    }

    [Serializable]
    public sealed class MinionConductorPlayerPathSideFireAction : BossAction,
        IBossActionContextDurationProvider,
        IBossProjectileEmissionAction
    {
        [Header("Player Path")]
        [SerializeField] private ConductorPlayerPathSideFireMode mode;
        [SerializeField, Min(0.1f)] private float distanceFromPlayer = 2.8f;
        [SerializeField, Min(0f)] private float minPathCenterOffset = 1f;
        [SerializeField, Min(0f)] private float maxPathCenterOffset = 2f;
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
        [SerializeField, Min(0.01f)] private float gridIndicatorDashLength = 0.22f;
        [SerializeField, Min(0.001f)] private float gridIndicatorDashGap = 0.16f;
        [SerializeField] private int gridIndicatorSortingOrder = 66;
        [SerializeField, Min(1)] private int indicatorPathCount = 4;
        [SerializeField, Min(1)] private int maxIndicatorShotsPerPath = 64;
        [SerializeField, Min(0f)] private float indicatorRetainSeconds = 0.2f;

        public bool TryGetDurationSeconds(BossActionContext context, out float seconds)
        {
            seconds = 0f;
            if (!MinionGraphActionHost.TryResolveProjectile(
                    context,
                    projectileName,
                    out _,
                    out _))
            {
                return false;
            }

            float finalStepStartSeconds = mode == ConductorPlayerPathSideFireMode.HorizontalVerticalThenDiagonal
                ? GetForcedStepCompletionSeconds()
                : 0f;
            seconds = finalStepStartSeconds
                + GetFireStartDelaySeconds()
                + GetLastProjectileFireOffsetSeconds();
            return seconds > 0f;
        }

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

            Vector2 pathCenterOffset = ResolveRandomPathCenterOffset();
            yield return ExecutePathSideFireStep(
                context,
                host,
                projectile,
                ResolveFirstPathMode(mode),
                mode == ConductorPlayerPathSideFireMode.HorizontalVerticalThenDiagonal,
                pathCenterOffset);

            if (mode == ConductorPlayerPathSideFireMode.HorizontalVerticalThenDiagonal)
            {
                yield return ExecutePathSideFireStep(
                    context,
                    host,
                    projectile,
                    MinionGraphPlayerPathMode.Diagonal,
                    false,
                    pathCenterOffset);
            }
        }

        private IEnumerator ExecutePathSideFireStep(
            BossActionContext context,
            IMinionPatternHost host,
            BossProjectileSettings projectile,
            MinionGraphPlayerPathMode pathMode,
            bool forceWaitForStepCompletion,
            Vector2 pathCenterOffset)
        {
            Vector2 pathCenter = ResolvePathCenter(context) + pathCenterOffset;
            List<PrecastIndicatorTiming> indicatorTimings = new();
            ConductorScoreLaneRushIndicatorVisual gridIndicator =
                CreatePrecastGridIndicator(context, host, projectile, pathCenter, pathMode, indicatorTimings);

            MinionGraphCommandRequest pathRequest = MinionGraphCommandRequest.PlayerPath(
                pathMode,
                distanceFromPlayer,
                moveToStartSeconds,
                moveSeconds,
                pathCenterOffset);
            float pathDuration = host.CommandMinions(pathRequest);
            float fireStartDelay = GetFireStartDelaySeconds();
            yield return WaitSecondsIfNeeded(context, fireStartDelay);

            MinionGraphProjectileFireSpec fireSpec = new MinionGraphProjectileFireSpec(minionOrigin, aim, effects, context)
                .WithProjectilePathIndicatorSuppressed();
            fireSpec = fireSpec.WithOnFired(CreateShotSfxHandler(context, GetSideFireShotCount()));

            MinionGraphCommandRequest fireRequest = MinionGraphCommandRequest.SideFire(
                projectile,
                fireSeconds,
                fireInterval,
                fireSpec,
                sideFireAngleDegrees,
                originMode,
                bodySideSpacing);
            float fireDuration = host.CommandMinions(fireRequest);

            float indicatorDuration = GetIndicatorDurationAfterFire(indicatorTimings);
            if (gridIndicator != null && indicatorTimings.Count > 0)
            {
                gridIndicator.StartCoroutine(AdvancePrecastIndicators(gridIndicator, indicatorTimings));
            }

            bool shouldWaitForPath = waitForPlayerPathDuration || forceWaitForStepCompletion;
            bool shouldWaitForFire = waitForSideFireDuration || forceWaitForStepCompletion;
            float remainingPathSeconds = Mathf.Max(0f, pathDuration - fireStartDelay);
            float remainingFireSeconds = Mathf.Max(0f, fireDuration);
            float remainingIndicatorSeconds = Mathf.Max(0f, indicatorDuration);
            float waitSeconds = Mathf.Max(
                shouldWaitForPath ? remainingPathSeconds : 0f,
                shouldWaitForFire ? remainingFireSeconds : 0f);
            float visualRetainSeconds = Mathf.Max(
                0f,
                Mathf.Max(Mathf.Max(remainingPathSeconds, remainingFireSeconds), remainingIndicatorSeconds) - waitSeconds);

            if (waitSeconds > 0f)
            {
                yield return WaitSecondsIfNeeded(context, waitSeconds);
            }

            if (gridIndicator == null)
            {
                yield break;
            }

            if (visualRetainSeconds <= 0f)
            {
                gridIndicator.ClearAndDestroy();
            }
            else
            {
                UnityEngine.Object.Destroy(
                    gridIndicator.gameObject,
                    Mathf.Max(0.01f, visualRetainSeconds + indicatorRetainSeconds));
            }
        }

        private ConductorScoreLaneRushIndicatorVisual CreatePrecastGridIndicator(
            BossActionContext context,
            IMinionPatternHost host,
            BossProjectileSettings projectile,
            Vector2 pathCenter,
            MinionGraphPlayerPathMode pathMode,
            List<PrecastIndicatorTiming> indicatorTimings)
        {
            if (!drawPrecastGridIndicator || projectile == null)
            {
                return null;
            }

            GameObject indicatorObject = new("ConductorPlayerPathSideFireGridIndicator");
            ConductorScoreLaneRushIndicatorVisual visual =
                indicatorObject.AddComponent<ConductorScoreLaneRushIndicatorVisual>();
            visual.Configure(gridIndicatorColor, gridIndicatorWidth, gridIndicatorSortingOrder);
            visual.ConfigureClearOnExecutionCinematic(true);

            List<PlayerPathPrediction> pathPredictions = CreatePathPredictions(host, pathMode);
            int pathCount = Mathf.Min(Mathf.Max(1, indicatorPathCount), pathPredictions.Count);
            if (pathCount <= 0)
            {
                return visual;
            }

            int shotCount = GetIndicatorShotCount();
            float indicatorLength = Mathf.Max(0.1f, projectile.Speed * projectile.Lifetime);
            float indicatorTravelSeconds = Mathf.Max(0.05f, projectile.Lifetime);
            float safeSideAngle = Mathf.Clamp(sideFireAngleDegrees, 1f, 179f);
            int lineIndex = 0;

            for (int pathIndex = 0; pathIndex < pathCount; pathIndex++)
            {
                PlayerPathPrediction prediction = pathPredictions[pathIndex];

                for (int shotIndex = 0; shotIndex < shotCount; shotIndex++)
                {
                    float shotSeconds = Mathf.Max(0.01f, fireInterval) * shotIndex;
                    float pathElapsed = GetFireStartDelaySeconds() + shotSeconds;
                    Vector2 root = pathCenter + GetPathOffsetAtTime(
                        prediction.StartOffset,
                        prediction.EndOffset,
                        pathElapsed);
                    Vector2 aimOrigin = GetPredictedAimOrigin(
                        prediction.Minion,
                        root,
                        prediction.PathDirection,
                        shotIndex);
                    Vector2 aimDirection = ResolvePredictedAimDirection(
                        context,
                        pathPredictions,
                        pathCenter,
                        pathElapsed,
                        aimOrigin);
                    Vector2 spawnOrigin = GetPredictedSpawnOrigin(
                        prediction.Minion,
                        aimOrigin,
                        aimDirection,
                        shotIndex);
                    Vector2 forward = ResolvePredictedSideFireForward(aimDirection, prediction.PathDirection);

                    GetSideFireOrigins(
                        spawnOrigin,
                        forward,
                        out Vector2 firstOrigin,
                        out Vector2 secondOrigin);
                    int firstLineIndex = lineIndex++;
                    AddDashedIndicatorLine(
                        visual,
                        firstLineIndex,
                        firstOrigin,
                        RotateDirection(forward, safeSideAngle),
                        indicatorLength);
                    indicatorTimings?.Add(new PrecastIndicatorTiming(firstLineIndex, shotSeconds, indicatorTravelSeconds));

                    int secondLineIndex = lineIndex++;
                    AddDashedIndicatorLine(
                        visual,
                        secondLineIndex,
                        secondOrigin,
                        RotateDirection(forward, -safeSideAngle),
                        indicatorLength);
                    indicatorTimings?.Add(new PrecastIndicatorTiming(secondLineIndex, shotSeconds, indicatorTravelSeconds));
                }
            }

            return visual;
        }

        private List<PlayerPathPrediction> CreatePathPredictions(
            IMinionPatternHost host,
            MinionGraphPlayerPathMode pathMode)
        {
            List<PlayerPathPrediction> predictions = new();
            IReadOnlyList<Minion> minions = host?.GetControlledMinionsForGraph();
            if (minions == null)
            {
                return predictions;
            }

            int commandIndex = 0;
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                MinionGraphPlayerPathType pathType = GetPlayerPathType(pathMode, commandIndex);
                GetPlayerPathOffsets(pathType, distanceFromPlayer, out Vector2 startOffset, out Vector2 endOffset);
                Vector2 pathDirection = (endOffset - startOffset).sqrMagnitude > 0.0001f
                    ? (endOffset - startOffset).normalized
                    : Vector2.right;
                predictions.Add(new PlayerPathPrediction(minion, startOffset, endOffset, pathDirection));
                commandIndex++;
            }

            return predictions;
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

        private float GetFireStartDelaySeconds()
        {
            return Mathf.Max(0f, moveToStartSeconds) + Mathf.Max(0f, windupSeconds);
        }

        private float GetForcedStepCompletionSeconds()
        {
            float pathDuration = Mathf.Max(0f, moveToStartSeconds) + Mathf.Max(0.05f, moveSeconds);
            float fireCompletionSeconds = GetFireStartDelaySeconds() + Mathf.Max(0.05f, fireSeconds);
            return Mathf.Max(pathDuration, fireCompletionSeconds);
        }

        private float GetLastProjectileFireOffsetSeconds()
        {
            float duration = Mathf.Max(0.05f, fireSeconds);
            float interval = Mathf.Max(0.01f, fireInterval);
            int shotCount = Mathf.Max(1, Mathf.CeilToInt(duration / interval));
            return (shotCount - 1) * interval;
        }

        // SideFire는 미니언마다 독립 코루틴(Minion.RunSideFire)이 같은 fireInterval 박자로 좌우 2발씩 쏘기 때문에,
        // 같은 shotIndex에서 처음 실제로 발사에 성공한 투사체 하나만 대표로 삼아 틱당 사운드가 한 번만 나게 한다.
        // 발사음은 그 투사체의 실제 Launched 이벤트에 걸리므로, 차징 중 패링/파괴되면 소리가 나지 않는다.
        private Action<int, EnemyProjectile> CreateShotSfxHandler(BossActionContext context, int shotCount)
        {
            bool[] handledShots = new bool[Mathf.Max(1, shotCount)];
            return (shotIndex, firedProjectile) =>
            {
                if (shotIndex < 0 || shotIndex >= handledShots.Length || handledShots[shotIndex])
                {
                    return;
                }

                handledShots[shotIndex] = true;
                context.PlaySfx(SoundEvent.Conductor_Fire);
                context.PlaySfxOnLaunch(firedProjectile, SoundEvent.Conductor_Launch);
            };
        }

        private int GetSideFireShotCount()
        {
            float duration = Mathf.Max(0.05f, fireSeconds);
            float interval = Mathf.Max(0.01f, fireInterval);
            return Mathf.Max(1, Mathf.CeilToInt(duration / interval));
        }

        private static IEnumerator WaitSecondsIfNeeded(BossActionContext context, float seconds)
        {
            if (context == null || seconds <= 0f)
            {
                yield break;
            }

            yield return context.WaitSeconds(seconds);
        }

        private IEnumerator AdvancePrecastIndicators(
            ConductorScoreLaneRushIndicatorVisual visual,
            IReadOnlyList<PrecastIndicatorTiming> timings)
        {
            if (visual == null || timings == null || timings.Count <= 0)
            {
                yield break;
            }

            float elapsed = 0f;
            float endSeconds = GetIndicatorDurationAfterFire(timings);
            while (elapsed < endSeconds)
            {
                for (int i = 0; i < timings.Count; i++)
                {
                    PrecastIndicatorTiming timing = timings[i];
                    float progress = elapsed <= timing.FireSeconds
                        ? 0f
                        : Mathf.Clamp01((elapsed - timing.FireSeconds) / timing.TravelSeconds);
                    visual.SetTravelProgress(timing.LineIndex, progress);
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            for (int i = 0; i < timings.Count; i++)
            {
                visual.SetTravelProgress(timings[i].LineIndex, 1f);
            }
        }

        private static float GetIndicatorDurationAfterFire(IReadOnlyList<PrecastIndicatorTiming> timings)
        {
            float duration = 0f;
            if (timings == null)
            {
                return duration;
            }

            for (int i = 0; i < timings.Count; i++)
            {
                duration = Mathf.Max(duration, timings[i].FireSeconds + timings[i].TravelSeconds);
            }

            return duration;
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

        private Vector2 ResolveRandomPathCenterOffset()
        {
            float maxDistance = Mathf.Max(0f, maxPathCenterOffset);
            float minDistance = Mathf.Clamp(minPathCenterOffset, 0f, maxDistance);
            if (maxDistance <= 0f)
            {
                return Vector2.zero;
            }

            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float distance = UnityEngine.Random.Range(minDistance, maxDistance);
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }

        private static MinionGraphPlayerPathMode ResolveFirstPathMode(ConductorPlayerPathSideFireMode actionMode)
        {
            return actionMode == ConductorPlayerPathSideFireMode.Diagonal
                ? MinionGraphPlayerPathMode.Diagonal
                : MinionGraphPlayerPathMode.HorizontalVertical;
        }

        private Vector2 ResolveAimDirection(BossActionContext context, Vector2 origin)
        {
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            Vector2 direction = aimSpec.GetDirection(context, origin);
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
        }

        private Vector2 ResolvePredictedAimDirection(
            BossActionContext context,
            IReadOnlyList<PlayerPathPrediction> predictions,
            Vector2 pathCenter,
            float pathElapsed,
            Vector2 origin)
        {
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            if (aimSpec.Mode == BossGraphProjectileAimMode.ClosestMinionToPlayer &&
                TryResolveClosestPredictedMinionAim(context, predictions, pathCenter, pathElapsed, out Vector2 closestDirection))
            {
                return closestDirection;
            }

            return ResolveAimDirection(context, origin);
        }

        private bool TryResolveClosestPredictedMinionAim(
            BossActionContext context,
            IReadOnlyList<PlayerPathPrediction> predictions,
            Vector2 pathCenter,
            float pathElapsed,
            out Vector2 direction)
        {
            direction = Vector2.zero;
            if (!TryGetPlayerPosition(context, out Vector2 playerPosition) || predictions == null || predictions.Count == 0)
            {
                return false;
            }

            PlayerPathPrediction closest = default;
            Vector2 closestRoot = Vector2.zero;
            float closestSqrDistance = float.PositiveInfinity;
            bool found = false;
            for (int i = 0; i < predictions.Count; i++)
            {
                PlayerPathPrediction prediction = predictions[i];
                Vector2 root = pathCenter + GetPathOffsetAtTime(prediction.StartOffset, prediction.EndOffset, pathElapsed);
                float sqrDistance = (root - playerPosition).sqrMagnitude;
                if (sqrDistance >= closestSqrDistance)
                {
                    continue;
                }

                closest = prediction;
                closestRoot = root;
                closestSqrDistance = sqrDistance;
                found = true;
            }

            if (!found)
            {
                return false;
            }

            Vector2 aimOrigin = GetPredictedAimOrigin(closest.Minion, closestRoot, closest.PathDirection, 0);
            direction = playerPosition - aimOrigin;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.left;
            }
            else
            {
                direction.Normalize();
            }

            return true;
        }

        private static bool TryGetPlayerPosition(BossActionContext context, out Vector2 playerPosition)
        {
            playerPosition = Vector2.zero;
            if (context == null || context.Boss == null || context.Boss.Player == null)
            {
                return false;
            }

            playerPosition = context.Boss.Player.position;
            return true;
        }

        private Vector2 GetPredictedAimOrigin(
            Minion minion,
            Vector2 root,
            Vector2 facingDirection,
            int shotIndex)
        {
            if (minion == null)
            {
                return root;
            }

            Vector2 currentRoot = minion.transform.position;
            Vector2 currentAimOrigin = minionOrigin.GetAimOrigin(minion, shotIndex);
            Vector2 localOffset = minion.transform.InverseTransformVector(currentAimOrigin - currentRoot);
            return root + TransformLocalOffset(localOffset, facingDirection);
        }

        private Vector2 GetPredictedSpawnOrigin(
            Minion minion,
            Vector2 predictedAimOrigin,
            Vector2 aimDirection,
            int shotIndex)
        {
            if (minion == null)
            {
                return predictedAimOrigin;
            }

            Vector2 currentAimOrigin = minionOrigin.GetAimOrigin(minion, shotIndex);
            Vector2 currentSpawnOrigin = minionOrigin.GetSpawnOrigin(minion, shotIndex, aimDirection);
            return predictedAimOrigin + currentSpawnOrigin - currentAimOrigin;
        }

        private Vector2 ResolvePredictedSideFireForward(Vector2 aimDirection, Vector2 pathDirection)
        {
            if (originMode != MinionGraphSideFireOriginMode.BodySides)
            {
                return aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.left;
            }

            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            if (aimSpec.Mode == BossGraphProjectileAimMode.ClosestMinionToPlayer)
            {
                return aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.left;
            }

            return pathDirection.sqrMagnitude > 0.0001f ? pathDirection.normalized : Vector2.right;
        }

        private static Vector2 TransformLocalOffset(Vector2 localOffset, Vector2 facingDirection)
        {
            Vector2 right = facingDirection.sqrMagnitude > 0.0001f ? facingDirection.normalized : Vector2.right;
            Vector2 up = new(-right.y, right.x);
            return right * localOffset.x + up * localOffset.y;
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

        private void AddDashedIndicatorLine(
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
            visual.SetDashedLane(
                index,
                origin,
                origin + safeDirection * Mathf.Max(0.1f, length),
                gridIndicatorDashLength,
                gridIndicatorDashGap);
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
            float diagonalDistance = distance * 0.70710678f;
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
                    startOffset = new Vector2(-diagonalDistance, diagonalDistance);
                    endOffset = new Vector2(diagonalDistance, -diagonalDistance);
                    break;
                case MinionGraphPlayerPathType.DiagonalRightTopToLeftBottom:
                    startOffset = new Vector2(diagonalDistance, diagonalDistance);
                    endOffset = new Vector2(-diagonalDistance, -diagonalDistance);
                    break;
                case MinionGraphPlayerPathType.DiagonalRightBottomToLeftTop:
                    startOffset = new Vector2(diagonalDistance, -diagonalDistance);
                    endOffset = new Vector2(-diagonalDistance, diagonalDistance);
                    break;
                case MinionGraphPlayerPathType.DiagonalLeftBottomToRightTop:
                    startOffset = new Vector2(-diagonalDistance, -diagonalDistance);
                    endOffset = new Vector2(diagonalDistance, diagonalDistance);
                    break;
                default:
                    startOffset = Vector2.left * distance;
                    endOffset = Vector2.right * distance;
                    break;
            }
        }

        private readonly struct PlayerPathPrediction
        {
            public PlayerPathPrediction(
                Minion minion,
                Vector2 startOffset,
                Vector2 endOffset,
                Vector2 pathDirection)
            {
                Minion = minion;
                StartOffset = startOffset;
                EndOffset = endOffset;
                PathDirection = pathDirection.sqrMagnitude > 0.0001f ? pathDirection.normalized : Vector2.right;
            }

            public Minion Minion { get; }
            public Vector2 StartOffset { get; }
            public Vector2 EndOffset { get; }
            public Vector2 PathDirection { get; }
        }

        private readonly struct PrecastIndicatorTiming
        {
            public PrecastIndicatorTiming(int lineIndex, float fireSeconds, float travelSeconds)
            {
                LineIndex = lineIndex;
                FireSeconds = Mathf.Max(0f, fireSeconds);
                TravelSeconds = Mathf.Max(0.05f, travelSeconds);
            }

            public int LineIndex { get; }
            public float FireSeconds { get; }
            public float TravelSeconds { get; }
        }
    }
}
