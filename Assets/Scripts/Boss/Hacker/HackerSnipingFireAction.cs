using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerSnipingFireAction : BossAction, IBossActionDurationProvider
    {
        private const string ShootAnimationTrigger = "Shoot";
        private const string ReleaseAnimationTrigger = "Release";
        private const string HoldTelegraphAnimationParameter = "HoldShootTelegraph";

        [Header("Projectile")]
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField, BossGraphBossChildPath] private string firePointPath;
        [SerializeField, Tooltip("0 이상이면 Projectile Settings의 Charge Seconds 대신 이 값을 사용합니다. 음수(-1)면 오버라이드하지 않습니다.")]
        private float chargeSecondsOverride = -1f;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;

        [Header("Fire Effects (Muzzle Flash)")]
        [SerializeField] private BossGraphEffectSettings effects = new();
        [Tooltip("Muzzle Flash 프리팹이 로컬 -X 방향을 바라보도록 제작됐다면 활성화합니다.")]
        [SerializeField] private bool muzzleFlashPrefabFacesLeft = true;

        [Header("Slight Homing")]
        [SerializeField] private bool enableSlightHoming = true;
        [SerializeField, Min(0.01f)] private float homingSeconds = 0.7f;
        [SerializeField, Min(0.01f)] private float homingTurnDegreesPerSecond = 24f;

        [Header("Knockback")]
        [SerializeField, Min(0f)] private float knockbackSpeed = 9f;
        [SerializeField, Min(0f)] private float knockbackStaggerSeconds = 0.16f;

        [Header("Flight Speed")]
        [SerializeField, Min(0f)] private float initialZeroSpeedSeconds;
        [SerializeField, Min(0.01f)] private float flightSpeedCurveSeconds = 1f;
        [SerializeField] private AnimationCurve flightSpeedCurve = AnimationCurve.Linear(0f, 1f, 1f, 1f);

        [Header("Facing")]
        [FormerlySerializedAs("facingSeconds")]
        [SerializeField, Min(0f)] private float preChargeFacingSeconds = 0.1f;
        [SerializeField, Min(0f)] private float postFireFacingSeconds = 0.3f;

        [Header("Charge")]
        [SerializeField, Min(0f)] private float windupSeconds = 0.8f;
        [SerializeField, Min(0.01f)] private float chargeStartRadius = 1.1f;
        [SerializeField, Min(0.01f)] private float chargeEndRadius = 0.08f;
        [SerializeField, Min(0.005f)] private float chargeLineWidth = 0.05f;
        [SerializeField] private Color chargeColor = new(0.32f, 0.8f, 1f, 0.9f);
        [SerializeField, Range(0f, 1f)] private float chargeStartAlphaMultiplier = 0.15f;
        [SerializeField, Range(0f, 1f)] private float chargeEndAlphaMultiplier = 1f;
        [SerializeField] private int chargeSortingOrder = 20;

        [Header("Charge Convergence Lines")]
        [SerializeField, Tooltip("차지 중 원 바깥에서 중심으로 모이는 랜덤 선 연출을 사용합니다.")]
        private bool enableChargeConvergenceLines = true;
        [SerializeField, Min(1)] private int chargeStartLineCount = 4;
        [SerializeField, Min(1)] private int chargeEndLineCount = 24;
        [SerializeField, Min(0.005f)] private float chargeConvergenceLineWidth = 0.025f;
        [SerializeField, Range(0.05f, 0.95f)] private float chargeConvergenceMaxLengthRatio = 0.35f;
        [SerializeField, Min(0.05f)] private float chargeConvergenceMinCycleSeconds = 0.12f;
        [SerializeField, Min(0.05f)] private float chargeConvergenceMaxCycleSeconds = 0.24f;
        [SerializeField, Min(0f)] private float chargeConvergenceMaxRepeatDelaySeconds = 0.08f;

        [Header("Aim Indicator")]
        [SerializeField, Min(0.05f)] private float aimIndicatorLength = 12f;
        [SerializeField, Min(0.005f)] private float aimIndicatorDashWidth = 0.035f;
        [SerializeField, Min(0.01f)] private float aimIndicatorDashLength = 0.22f;
        [SerializeField, Min(0f)] private float aimIndicatorDashGap = 0.16f;
        [SerializeField] private Color aimIndicatorColor = new(1f, 0.25f, 0.08f, 0.58f);
        [SerializeField] private int aimIndicatorSortingOrder = 17;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            Transform firePoint = context.GetBossChildTransform(firePointPath);
            if (firePoint == null)
            {
                Debug.LogWarning($"{nameof(HackerSnipingFireAction)}: 설정된 발사 Point '{firePointPath}'를 찾을 수 없습니다.", context.Boss);
                yield break;
            }

            yield return HackerMeleeAttackAction.Wait(context, preChargeFacingSeconds);
            using (context.AcquireFacingLock())
            {
                yield return ExecuteFacingLocked(context, firePoint);
            }

            yield return HackerMeleeAttackAction.Wait(context, postFireFacingSeconds);
        }

        private IEnumerator ExecuteFacingLocked(BossActionContext context, Transform firePoint)
        {
            context.BeginSnipingTelegraph(ShootAnimationTrigger, HoldTelegraphAnimationParameter);
            if (windupSeconds > 0f)
            {
                if (context.Boss is HackerHologramBoss hologram)
                {
                    hologram.FreezeRecordedPose(windupSeconds);
                }

                HackerSnipingChargeIndicator chargeIndicator = HackerSnipingChargeIndicator.Create(
                    firePoint,
                    firePoint.position,
                    chargeStartRadius,
                    chargeEndRadius,
                    chargeLineWidth,
                    chargeColor,
                    chargeStartAlphaMultiplier,
                    chargeEndAlphaMultiplier,
                    enableChargeConvergenceLines,
                    chargeStartLineCount,
                    chargeEndLineCount,
                    chargeConvergenceLineWidth,
                    chargeConvergenceMaxLengthRatio,
                    chargeConvergenceMinCycleSeconds,
                    chargeConvergenceMaxCycleSeconds,
                    chargeConvergenceMaxRepeatDelaySeconds,
                    windupSeconds,
                    chargeSortingOrder);
                ResolveShot(context, firePoint, out Vector3 indicatorOrigin, out Vector2 indicatorDirection);
                HackerDashedAimIndicator aimIndicator = HackerDashedAimIndicator.Create(
                    indicatorOrigin,
                    indicatorDirection,
                    aimIndicatorLength,
                    aimIndicatorDashWidth,
                    aimIndicatorDashLength,
                    aimIndicatorDashGap,
                    aimIndicatorColor,
                    aimIndicatorSortingOrder);

                try
                {
                    float elapsed = 0f;
                    while (elapsed < windupSeconds)
                    {
                        if (context.IsExecutionPaused)
                        {
                            context.Stop();
                            yield return null;
                            continue;
                        }

                        chargeIndicator?.SetProgress(elapsed / windupSeconds);
                        ResolveShot(context, firePoint, out indicatorOrigin, out indicatorDirection);
                        aimIndicator?.SetLine(
                            indicatorOrigin,
                            indicatorDirection,
                            aimIndicatorLength);
                        elapsed += EnemyTimeScale.DeltaTime;
                        yield return null;
                    }

                    chargeIndicator?.SetProgress(1f);
                }
                finally
                {
                    if (chargeIndicator != null)
                    {
                        UnityEngine.Object.Destroy(chargeIndicator.gameObject);
                    }

                    if (aimIndicator != null)
                    {
                        UnityEngine.Object.Destroy(aimIndicator.gameObject);
                    }
                }
            }

            ResolveShot(context, firePoint, out Vector3 spawnOrigin, out Vector2 finalDirection);
            context.ReleaseSnipingShot(HoldTelegraphAnimationParameter, ReleaseAnimationTrigger);
            EnemyProjectile firedProjectile = context.FireProjectile(
                projectile,
                spawnOrigin,
                finalDirection,
                0f,
                chargeSecondsOverride: chargeSecondsOverride,
                projectileName: projectileName);
            if (firedProjectile == null)
            {
                yield break;
            }

            firedProjectile.ConfigurePathIndicatorSuppressed(true);
            firedProjectile.ConfigurePlayerHitKnockback(knockbackSpeed, knockbackStaggerSeconds);
            firedProjectile.ConfigureFlightSpeedCurve(
                flightSpeedCurve,
                flightSpeedCurveSeconds,
                initialZeroSpeedSeconds);
            if (enableSlightHoming)
            {
                firedProjectile.ConfigureHomingOverride(
                    homingSeconds,
                    homingTurnDegreesPerSecond);
            }

            context.PlaySfx(fireSfxId);
            context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
            context.PlayOriginBurst(effects, spawnOrigin);
            PlayMuzzleFlash(context, firePoint, finalDirection);
            context.PlayCameraShakeIfEnabled(effects, finalDirection);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, preChargeFacingSeconds)
                + Mathf.Max(0f, windupSeconds)
                + Mathf.Max(0f, postFireFacingSeconds);
            return true;
        }

        private void ResolveShot(
            BossActionContext context,
            Transform firePoint,
            out Vector3 spawnOrigin,
            out Vector2 finalDirection)
        {
            spawnOrigin = firePoint != null ? firePoint.position : context.OriginPosition;
            finalDirection = GetSnipingDirection(context, spawnOrigin);
        }

        private void PlayMuzzleFlash(
            BossActionContext context,
            Transform firePoint,
            Vector2 shotDirection)
        {
            BossGraphPrefabEffectSettings muzzleFlash = effects?.MuzzleFlash;
            if (muzzleFlash == null || !muzzleFlash.Enabled || firePoint == null)
            {
                return;
            }

            bool playerIsLeft = Mathf.Abs(shotDirection.x) > 0.0001f
                ? shotDirection.x < 0f
                : context.Boss is HackerBossAI hacker && hacker.IsFacingLeft;
            bool flipHorizontally = playerIsLeft != muzzleFlashPrefabFacesLeft;
            GameObject muzzleFlashInstance = ProjectileVfx.PlayPrefab(
                muzzleFlash.Prefab,
                firePoint.position,
                Quaternion.identity,
                firePoint,
                muzzleFlash.Scale,
                followRotation: false);
            if (flipHorizontally && muzzleFlashInstance != null)
            {
                Vector3 scale = muzzleFlashInstance.transform.localScale;
                scale.x *= -1f;
                muzzleFlashInstance.transform.localScale = scale;
            }
        }

        private static Vector2 GetSnipingDirection(
            BossActionContext context,
            Vector3 origin)
        {
            if (context?.Boss is HackerHologramBoss hologram && hologram.Player != null)
            {
                Vector2 playerDirection = (Vector2)hologram.Player.position - (Vector2)origin;
                if (playerDirection.sqrMagnitude > 0.0001f)
                {
                    return playerDirection.normalized;
                }
            }

            return context.GetDirectionToPlayer(origin);
        }
    }

    internal sealed class HackerSnipingChargeIndicator : MonoBehaviour
    {
        private const int CircleSegments = 40;

        private Transform followTarget;
        private Vector3 fallbackPosition;
        private float startRadius;
        private float endRadius;
        private Color startColor;
        private Color endColor;
        private readonly List<ConvergenceLine> convergenceLines = new();
        private bool enableConvergenceLines;
        private int startLineCount;
        private int endLineCount;
        private float convergenceLineWidth;
        private float convergenceMaxLengthRatio;
        private float minCycleProgress;
        private float maxCycleProgress;
        private float maxRepeatDelayProgress;
        private int sortingOrder;
        private LineRenderer line;
        private Material lineMaterial;

        internal static HackerSnipingChargeIndicator Create(
            Transform followTarget,
            Vector3 fallbackPosition,
            float startRadius,
            float endRadius,
            float lineWidth,
            Color color,
            float startAlphaMultiplier,
            float endAlphaMultiplier,
            bool enableConvergenceLines,
            int startLineCount,
            int endLineCount,
            float convergenceLineWidth,
            float convergenceMaxLengthRatio,
            float minCycleSeconds,
            float maxCycleSeconds,
            float maxRepeatDelaySeconds,
            float chargeSeconds,
            int sortingOrder)
        {
            GameObject indicatorObject = new("HackerSnipingChargeIndicator");
            HackerSnipingChargeIndicator indicator = indicatorObject.AddComponent<HackerSnipingChargeIndicator>();
            indicator.followTarget = followTarget;
            indicator.fallbackPosition = fallbackPosition;
            indicator.startRadius = Mathf.Max(0.01f, startRadius);
            indicator.endRadius = Mathf.Max(0.01f, endRadius);
            indicator.startColor = WithAlpha(
                color,
                color.a * Mathf.Clamp01(startAlphaMultiplier));
            indicator.endColor = WithAlpha(
                color,
                color.a * Mathf.Clamp01(endAlphaMultiplier));
            indicator.enableConvergenceLines = enableConvergenceLines;
            indicator.startLineCount = Mathf.Max(1, startLineCount);
            indicator.endLineCount = Mathf.Max(indicator.startLineCount, endLineCount);
            indicator.convergenceLineWidth = Mathf.Max(0.005f, convergenceLineWidth);
            indicator.convergenceMaxLengthRatio = Mathf.Clamp(
                convergenceMaxLengthRatio,
                0.05f,
                0.95f);
            float safeChargeSeconds = Mathf.Max(0.01f, chargeSeconds);
            indicator.minCycleProgress = Mathf.Max(0.01f, minCycleSeconds / safeChargeSeconds);
            indicator.maxCycleProgress = Mathf.Max(
                indicator.minCycleProgress,
                maxCycleSeconds / safeChargeSeconds);
            indicator.maxRepeatDelayProgress = Mathf.Max(0f, maxRepeatDelaySeconds / safeChargeSeconds);
            indicator.sortingOrder = sortingOrder;
            indicator.transform.position = followTarget != null ? followTarget.position : fallbackPosition;
            indicator.CreateLine(lineWidth, indicator.startColor, sortingOrder);
            indicator.SetProgress(0f);
            return indicator;
        }

        internal void SetProgress(float progress)
        {
            float clampedProgress = Mathf.Clamp01(progress);
            float convergence = Mathf.SmoothStep(0f, 1f, clampedProgress);
            float radius = Mathf.Lerp(startRadius, endRadius, convergence);
            SetRadius(radius);
            Color color = Color.Lerp(startColor, endColor, convergence);
            SetColor(color);
            if (enableConvergenceLines)
            {
                UpdateConvergenceLines(clampedProgress, convergence, radius);
            }
        }

        private void LateUpdate()
        {
            transform.position = followTarget != null ? followTarget.position : fallbackPosition;
        }

        private void SetRadius(float radius)
        {
            if (line == null)
            {
                return;
            }

            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = Mathf.PI * 2f * i / CircleSegments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius));
            }
        }

        private void SetColor(Color color)
        {
            if (line == null)
            {
                return;
            }

            line.startColor = color;
            line.endColor = color;
        }

        private void UpdateConvergenceLines(
            float chargeProgress,
            float convergenceProgress,
            float circleRadius)
        {
            int lineCount = Mathf.RoundToInt(
                Mathf.Lerp(startLineCount, endLineCount, convergenceProgress));
            for (int i = 0; i < lineCount; i++)
            {
                ConvergenceLine convergenceLine = EnsureConvergenceLine(i);
                if (!convergenceLine.IsScheduled)
                {
                    ScheduleConvergenceLine(convergenceLine, chargeProgress, circleRadius);
                }

                UpdateConvergenceLine(convergenceLine, chargeProgress, circleRadius);
            }

            for (int i = lineCount; i < convergenceLines.Count; i++)
            {
                convergenceLines[i].Renderer.enabled = false;
                convergenceLines[i].IsScheduled = false;
            }
        }

        private void UpdateConvergenceLine(
            ConvergenceLine convergenceLine,
            float chargeProgress,
            float circleRadius)
        {
            if (chargeProgress < convergenceLine.StartProgress)
            {
                convergenceLine.Renderer.enabled = false;
                return;
            }

            float cycleProgress = (chargeProgress - convergenceLine.StartProgress)
                / convergenceLine.DurationProgress;
            if (cycleProgress >= 1f)
            {
                ScheduleConvergenceLine(convergenceLine, chargeProgress, circleRadius);
                convergenceLine.Renderer.enabled = false;
                return;
            }

            Vector3 direction = new(
                Mathf.Cos(convergenceLine.AngleRadians),
                Mathf.Sin(convergenceLine.AngleRadians),
                0f);
            Vector3 outerPoint = direction * convergenceLine.OuterRadius;
            Vector3 centerPoint = direction * (endRadius * 0.1f);
            float headProgress = cycleProgress < 0.15f
                ? 0.01f
                : Mathf.SmoothStep(0f, 1f, (cycleProgress - 0.15f) / 0.85f);
            float tailProgress = headProgress <= convergenceMaxLengthRatio
                ? 0f
                : (headProgress - convergenceMaxLengthRatio)
                    / (1f - convergenceMaxLengthRatio);
            Vector3 head = Vector3.Lerp(outerPoint, centerPoint, headProgress);
            Vector3 tail = Vector3.Lerp(outerPoint, centerPoint, tailProgress);

            Color tailColor = endColor;
            tailColor.a = startColor.a;
            Color headColor = endColor;
            headColor.a = 1f;
            convergenceLine.Renderer.enabled = true;
            convergenceLine.Renderer.startColor = tailColor;
            convergenceLine.Renderer.endColor = headColor;
            convergenceLine.Renderer.SetPosition(0, tail);
            convergenceLine.Renderer.SetPosition(1, head);
        }

        private void ScheduleConvergenceLine(
            ConvergenceLine convergenceLine,
            float chargeProgress,
            float circleRadius)
        {
            convergenceLine.IsScheduled = true;
            convergenceLine.StartProgress = chargeProgress
                + UnityEngine.Random.Range(0f, maxRepeatDelayProgress);
            convergenceLine.DurationProgress = UnityEngine.Random.Range(
                minCycleProgress,
                maxCycleProgress);
            convergenceLine.AngleRadians = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            convergenceLine.OuterRadius = Mathf.Max(endRadius, circleRadius)
                * UnityEngine.Random.Range(0.95f, 1.3f);
        }

        private ConvergenceLine EnsureConvergenceLine(int index)
        {
            while (convergenceLines.Count <= index)
            {
                GameObject lineObject = new($"HackerSnipingConvergenceLine_{convergenceLines.Count:00}");
                lineObject.transform.SetParent(transform, false);
                LineRenderer convergenceLine = lineObject.AddComponent<LineRenderer>();
                convergenceLine.useWorldSpace = false;
                convergenceLine.positionCount = 2;
                convergenceLine.startWidth = convergenceLineWidth;
                convergenceLine.endWidth = convergenceLineWidth;
                convergenceLine.numCapVertices = 4;
                BossSorting.Apply(convergenceLine);
                convergenceLine.sortingOrder = sortingOrder + 1;
                convergenceLine.sharedMaterial = lineMaterial;
                convergenceLines.Add(new ConvergenceLine(convergenceLine));
            }

            return convergenceLines[index];
        }

        private sealed class ConvergenceLine
        {
            internal ConvergenceLine(LineRenderer renderer)
            {
                Renderer = renderer;
            }

            internal LineRenderer Renderer { get; }
            internal bool IsScheduled { get; set; }
            internal float StartProgress { get; set; }
            internal float DurationProgress { get; set; }
            internal float AngleRadians { get; set; }
            internal float OuterRadius { get; set; }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = Mathf.Clamp01(alpha);
            return color;
        }

        private void CreateLine(float width, Color color, int sortingOrder)
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = CircleSegments;
            line.startWidth = Mathf.Max(0.005f, width);
            line.endWidth = Mathf.Max(0.005f, width);
            line.startColor = color;
            line.endColor = color;
            BossSorting.Apply(line);
            line.sortingOrder = sortingOrder;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                lineMaterial = new Material(shader);
                line.material = lineMaterial;
            }
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }
        }
    }

    internal sealed class HackerDashedAimIndicator : MonoBehaviour
    {
        private const int MaxDashCount = 128;

        private readonly System.Collections.Generic.List<LineRenderer> dashes = new();
        private float dashLength;
        private float dashGap;
        private float dashWidth;
        private Color color;
        private int sortingOrder;
        private Material material;

        internal static HackerDashedAimIndicator Create(
            Vector2 origin,
            Vector2 direction,
            float length,
            float width,
            float dashLength,
            float dashGap,
            Color color,
            int sortingOrder)
        {
            GameObject indicatorObject = new("HackerSnipingAimIndicator");
            HackerDashedAimIndicator indicator = indicatorObject.AddComponent<HackerDashedAimIndicator>();
            indicator.dashLength = Mathf.Max(0.01f, dashLength);
            indicator.dashGap = Mathf.Max(0f, dashGap);
            indicator.dashWidth = Mathf.Max(0.005f, width);
            indicator.color = color;
            indicator.sortingOrder = sortingOrder;
            indicator.CreateMaterial();
            indicator.SetLine(origin, direction, length);
            return indicator;
        }

        internal void SetLine(Vector2 origin, Vector2 direction, float length)
        {
            Vector2 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
            float safeLength = Mathf.Max(0f, length);
            float period = dashLength + dashGap;
            int requestedDashCount = period > 0f ? Mathf.CeilToInt(safeLength / period) : 0;
            int dashCount = Mathf.Min(MaxDashCount, requestedDashCount);
            float renderedDashLength = dashLength;
            if (requestedDashCount > MaxDashCount)
            {
                float dashRatio = dashLength / period;
                period = safeLength / MaxDashCount;
                renderedDashLength = period * dashRatio;
            }

            for (int i = 0; i < dashCount; i++)
            {
                float startDistance = i * period;
                float endDistance = Mathf.Min(startDistance + renderedDashLength, safeLength);
                LineRenderer dash = EnsureDash(i);
                if (dash == null)
                {
                    continue;
                }

                dash.enabled = endDistance > startDistance;
                dash.SetPosition(0, origin + normalized * startDistance);
                dash.SetPosition(1, origin + normalized * endDistance);
            }

            for (int i = dashCount; i < dashes.Count; i++)
            {
                if (dashes[i] != null)
                {
                    dashes[i].enabled = false;
                }
            }
        }

        private LineRenderer EnsureDash(int index)
        {
            while (dashes.Count <= index)
            {
                GameObject dashObject = new($"HackerSnipingAimDash_{dashes.Count:00}");
                dashObject.transform.SetParent(transform, false);
                LineRenderer dash = dashObject.AddComponent<LineRenderer>();
                dash.useWorldSpace = true;
                dash.loop = false;
                dash.positionCount = 2;
                dash.numCornerVertices = 0;
                dash.numCapVertices = 1;
                dash.startWidth = dashWidth;
                dash.endWidth = dashWidth;
                dash.startColor = color;
                dash.endColor = color;
                BossSorting.Apply(dash);
                dash.sortingOrder = sortingOrder;
                dash.sharedMaterial = material;
                dashes.Add(dash);
            }

            return dashes[index];
        }

        private void CreateMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                material = new Material(shader);
            }
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }
    }
}
