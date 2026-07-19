using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum ConductorFanCenterMode
    {
        PlayerStart,
        BossStart,
        World
    }

    public enum ConductorFanProjectileDirection
    {
        RadialOut,
        RadialIn,
        TangentClockwise,
        TangentCounterClockwise
    }

    [Serializable]
    public sealed class MinionConductorFanBladeAction : BossAction, IBossActionContextDurationProvider, IBossProjectileEmissionAction
    {
        [Serializable]
        public sealed class Volley
        {
            [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
            [SerializeField, Min(0f)] private float startSeconds;
            [SerializeField, Min(0f)] private float durationSeconds = 1f;
            [SerializeField, Min(0f)] private float fireInterval = 0.12f;
            [SerializeField] private ConductorFanProjectileDirection direction = ConductorFanProjectileDirection.RadialOut;
            [SerializeField] private float angleOffsetDegrees;
            [SerializeField] private List<int> minionNumbers = new();
            [SerializeField, BossGraphSfxId] private string fireSfxId;
            [SerializeField, BossGraphSfxId] private string launchSfxId;

            public string ProjectileName => projectileName?.Trim();
            public float StartSeconds => Mathf.Max(0f, startSeconds);
            public float DurationSeconds => Mathf.Max(0f, durationSeconds);
            public float FireInterval => Mathf.Max(0f, fireInterval);
            public ConductorFanProjectileDirection Direction => direction;
            public float AngleOffsetDegrees => angleOffsetDegrees;
            public IReadOnlyList<int> MinionNumbers => minionNumbers;
            public string FireSfxId => fireSfxId;
            public string LaunchSfxId => launchSfxId;
        }

        [SerializeField] private ConductorFanCenterMode centerMode = ConductorFanCenterMode.PlayerStart;
        [SerializeField] private Vector2 worldCenter;
        [SerializeField] private Vector2 centerOffset;
        [SerializeField, Min(0.1f)] private float radius = 2.4f;
        [SerializeField, Min(0f)] private float alignSeconds = 0.45f;
        [SerializeField, Min(0f)] private float rotateSeconds = 5f;
        [SerializeField] private float angularSpeedDegrees = 180f;
        [SerializeField] private float startAngleOffsetDegrees;
        [SerializeField, Range(1, 4)] private int maxMinionCount = 4;
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField, InspectorName("Volleys")] private List<Volley> volleys = new() { new Volley() };
        [SerializeField] private bool waitForDuration = true;
        [Header("Placement Indicators")]
        [SerializeField] private bool drawPlacementIndicators = true;
        [SerializeField] private Color placementIndicatorColor = new(1f, 0.78f, 0.24f, 0.78f);
        [SerializeField, Min(0.001f)] private float placementIndicatorWidth = 0.035f;
        [SerializeField, Min(0f)] private float placementIndicatorInnerRadius = 0.08f;
        [SerializeField, Min(0.01f)] private float placementIndicatorOuterRadius = 0.42f;
        [SerializeField, Range(4, 24)] private int placementIndicatorSpikeCount = 12;
        [SerializeField] private int placementIndicatorSortingOrder = 67;

        private ConductorScoreLaneRushIndicatorVisual activePlacementIndicators;

        public bool TryGetDurationSeconds(BossActionContext context, out float seconds)
        {
            seconds = 0f;
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context?.Boss == null
                || volleys == null
                || volleys.Count == 0)
            {
                return false;
            }

            List<Minion> minions = GetFanMinions(host.GetControlledMinionsForGraph());
            if (minions.Count == 0)
            {
                return false;
            }

            float fireDuration = GetMaxVolleyEndSeconds();
            float bladeRotateSeconds = Mathf.Max(rotateSeconds, fireDuration);
            float totalMovementDuration = Mathf.Max(0f, alignSeconds) + bladeRotateSeconds;
            float totalFireDuration = Mathf.Max(0f, alignSeconds) + fireDuration;
            seconds = Mathf.Max(0f, windupSeconds)
                + GetCompletionTimelineSeconds(totalMovementDuration, totalFireDuration);
            return seconds > 0f;
        }

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host)
                || context.Boss == null
                || volleys == null
                || volleys.Count == 0)
            {
                yield break;
            }

            Vector2 center = ResolveCenter(context);
            yield return MinionGraphCommandRunner.WaitWindupIfNeeded(context, windupSeconds);

            List<Minion> minions = GetFanMinions(host.GetControlledMinionsForGraph());
            if (minions.Count == 0)
            {
                yield break;
            }

            ClearPlacementIndicators();
            activePlacementIndicators = CreatePlacementIndicators(center);
            float fireDuration = GetMaxVolleyEndSeconds();
            float bladeRotateSeconds = Mathf.Max(rotateSeconds, fireDuration);
            float movementDuration = CommandFanBlades(minions, center, bladeRotateSeconds);
            float totalMovementDuration = Mathf.Max(movementDuration, alignSeconds + bladeRotateSeconds);
            float totalFireDuration = alignSeconds + fireDuration;
            float timelineDuration = GetCompletionTimelineSeconds(totalMovementDuration, totalFireDuration);

            yield return RunVolleyTimeline(context, host, minions, center, timelineDuration);
            ClearPlacementIndicators();
        }

        private float GetCompletionTimelineSeconds(float totalMovementDuration, float totalFireDuration)
        {
            float configuredTimeline = waitForDuration
                ? Mathf.Max(totalMovementDuration, totalFireDuration)
                : totalFireDuration;
            return Mathf.Max(configuredTimeline, totalMovementDuration, totalFireDuration);
        }

        private IEnumerator RunVolleyTimeline(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> minions,
            Vector2 center,
            float totalDuration)
        {
            float[] nextFireTimes = new float[volleys.Count];
            bool[] completedSingleShots = new bool[volleys.Count];
            for (int i = 0; i < volleys.Count; i++)
            {
                nextFireTimes[i] = volleys[i] != null ? volleys[i].StartSeconds : float.PositiveInfinity;
            }

            float elapsed = 0f;
            bool placementIndicatorsCleared = false;
            while (elapsed < totalDuration)
            {
                if (context.IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                if (elapsed >= alignSeconds)
                {
                    if (!placementIndicatorsCleared)
                    {
                        ClearPlacementIndicators();
                        placementIndicatorsCleared = true;
                    }

                    float rotateElapsed = elapsed - alignSeconds;
                    TickVolleys(context, host, minions, center, rotateElapsed, nextFireTimes, completedSingleShots);
                }

                elapsed += EnemyTimeScale.DeltaTime;
                yield return null;
            }

            if (totalDuration >= alignSeconds)
            {
                TickVolleys(context, host, minions, center, totalDuration - alignSeconds, nextFireTimes, completedSingleShots);
            }
        }

        private float GetMaxVolleyEndSeconds()
        {
            float maxSeconds = 0f;
            for (int i = 0; i < volleys.Count; i++)
            {
                Volley volley = volleys[i];
                if (volley == null)
                {
                    continue;
                }

                maxSeconds = Mathf.Max(maxSeconds, volley.StartSeconds + volley.DurationSeconds);
            }

            return maxSeconds;
        }

        private void TickVolleys(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> minions,
            Vector2 center,
            float rotateElapsed,
            float[] nextFireTimes,
            bool[] completedSingleShots)
        {
            for (int i = 0; i < volleys.Count; i++)
            {
                Volley volley = volleys[i];
                if (volley == null)
                {
                    continue;
                }

                float endSeconds = volley.StartSeconds + volley.DurationSeconds;
                if (volley.DurationSeconds <= 0f)
                {
                    if (!completedSingleShots[i] && rotateElapsed >= volley.StartSeconds)
                    {
                        FireVolley(context, host, minions, center, volley);
                        completedSingleShots[i] = true;
                    }

                    continue;
                }

                if (volley.FireInterval <= 0f)
                {
                    if (!completedSingleShots[i] && rotateElapsed >= volley.StartSeconds)
                    {
                        FireVolley(context, host, minions, center, volley);
                        completedSingleShots[i] = true;
                    }

                    continue;
                }

                while (rotateElapsed >= nextFireTimes[i] && nextFireTimes[i] <= endSeconds)
                {
                    FireVolley(context, host, minions, center, volley);
                    nextFireTimes[i] += volley.FireInterval;
                }
            }
        }

        private void FireVolley(
            BossActionContext context,
            IMinionPatternHost host,
            IReadOnlyList<Minion> minions,
            Vector2 center,
            Volley volley)
        {
            BossProjectileSettings projectile = host.ResolveMinionProjectileSettings(volley.ProjectileName);
            if (projectile == null)
            {
                return;
            }

            bool firedAny = false;
            EnemyProjectile launchSfxTarget = null;
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null || !ShouldFireFromMinion(volley, minion, i))
                {
                    continue;
                }

                Vector3 origin = minion.GetGraphProjectileOrigin();
                Vector2 direction = GetProjectileDirection(volley, center, origin);
                EnemyProjectile firedProjectile = host.FireMinionProjectile(minion, projectile, origin, direction, true);
                if (firedProjectile == null)
                {
                    continue;
                }

                firedProjectile.ConfigurePathIndicatorSuppressed(true);
                context.PlayOriginBurst(effects, origin);
                context.PlayMuzzleFlashIfEnabled(effects, firedProjectile, direction);
                context.PlayCameraShakeIfEnabled(effects, direction);
                firedAny = true;
                launchSfxTarget ??= firedProjectile;
            }

            // 이 볼리에 몇 마리가 걸려 동시에 쐈든, 사운드는 볼리당 한 번만 재생한다.
            // launchSfxId는 대표 투사체 1개의 실제 Launched 이벤트에 걸어서, 그 사이 패링/파괴되면 소리가 안 나게 한다.
            if (firedAny)
            {
                context.PlaySfx(volley.FireSfxId);
                context.PlaySfxOnLaunch(launchSfxTarget, volley.LaunchSfxId);
            }
        }

        private float CommandFanBlades(IReadOnlyList<Minion> minions, Vector2 center, float bladeRotateSeconds)
        {
            float maxDuration = 0f;
            for (int i = 0; i < minions.Count; i++)
            {
                Minion minion = minions[i];
                if (minion == null)
                {
                    continue;
                }

                float angle = startAngleOffsetDegrees + 90f * i;
                float duration = minion.CommandConductorFanBlade(
                    center,
                    angle,
                    radius,
                    alignSeconds,
                    bladeRotateSeconds,
                    angularSpeedDegrees);
                maxDuration = Mathf.Max(maxDuration, duration);
            }

            return maxDuration;
        }

        private ConductorScoreLaneRushIndicatorVisual CreatePlacementIndicators(Vector2 center)
        {
            if (!drawPlacementIndicators)
            {
                return null;
            }

            GameObject indicatorObject = new("ConductorFanBladePlacementIndicators");
            ConductorScoreLaneRushIndicatorVisual visual = indicatorObject.AddComponent<ConductorScoreLaneRushIndicatorVisual>();
            visual.Configure(placementIndicatorColor, placementIndicatorWidth, placementIndicatorSortingOrder);

            int lineIndex = 0;
            int spikeCount = Mathf.Max(4, placementIndicatorSpikeCount);
            float innerRadius = Mathf.Max(0f, placementIndicatorInnerRadius);
            float outerRadius = Mathf.Max(innerRadius + 0.01f, placementIndicatorOuterRadius);
            for (int spikeIndex = 0; spikeIndex < spikeCount; spikeIndex++)
            {
                Vector2 direction = BossActionContext.AngleToDirection(360f * spikeIndex / spikeCount);
                visual.SetLane(
                    lineIndex,
                    center + direction * innerRadius,
                    center + direction * outerRadius);
                visual.SetProgress(lineIndex, 1f);
                lineIndex++;
            }

            return visual;
        }

        private void ClearPlacementIndicators()
        {
            if (activePlacementIndicators == null)
            {
                return;
            }

            activePlacementIndicators.ClearAndDestroy();
            activePlacementIndicators = null;
        }

        private Vector2 ResolveCenter(BossActionContext context)
        {
            Vector2 center = centerMode switch
            {
                ConductorFanCenterMode.World => worldCenter,
                ConductorFanCenterMode.BossStart => (Vector2)context.OriginPosition,
                _ => context.Boss != null && context.Boss.Player != null
                    ? (Vector2)context.Boss.Player.position
                    : (Vector2)context.OriginPosition
            };

            return center + centerOffset;
        }

        private List<Minion> GetFanMinions(IReadOnlyList<Minion> source)
        {
            List<Minion> results = new();
            if (source == null)
            {
                return results;
            }

            for (int i = 0; i < source.Count; i++)
            {
                Minion minion = source[i];
                if (minion != null && minion.Health != null && !minion.Health.IsDead)
                {
                    results.Add(minion);
                }
            }

            results.Sort((a, b) => GetSortNumber(a).CompareTo(GetSortNumber(b)));
            int count = Mathf.Clamp(maxMinionCount, 1, 4);
            if (results.Count > count)
            {
                results.RemoveRange(count, results.Count - count);
            }

            return results;
        }

        private static int GetSortNumber(Minion minion)
        {
            return minion != null && minion.HasOwnerSlotNumber ? minion.OwnerSlotNumber : int.MaxValue;
        }

        private static bool ShouldFireFromMinion(Volley volley, Minion minion, int fallbackIndex)
        {
            IReadOnlyList<int> numbers = volley.MinionNumbers;
            if (numbers == null || numbers.Count == 0)
            {
                return true;
            }

            int minionNumber = minion != null && minion.HasOwnerSlotNumber
                ? minion.OwnerSlotNumber
                : fallbackIndex + 1;
            for (int i = 0; i < numbers.Count; i++)
            {
                if (numbers[i] == minionNumber)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2 GetProjectileDirection(Volley volley, Vector2 center, Vector2 origin)
        {
            Vector2 radial = origin - center;
            if (radial.sqrMagnitude <= 0.0001f)
            {
                radial = Vector2.right;
            }

            radial.Normalize();
            Vector2 direction = volley.Direction switch
            {
                ConductorFanProjectileDirection.RadialIn => -radial,
                ConductorFanProjectileDirection.TangentClockwise => new Vector2(radial.y, -radial.x),
                ConductorFanProjectileDirection.TangentCounterClockwise => new Vector2(-radial.y, radial.x),
                _ => radial
            };

            if (!Mathf.Approximately(volley.AngleOffsetDegrees, 0f))
            {
                float radians = volley.AngleOffsetDegrees * Mathf.Deg2Rad;
                float cos = Mathf.Cos(radians);
                float sin = Mathf.Sin(radians);
                direction = new Vector2(
                    direction.x * cos - direction.y * sin,
                    direction.x * sin + direction.y * cos);
            }

            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        }
    }
}
