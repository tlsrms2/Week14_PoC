using System;
using System.Collections;
using UnityEngine;
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
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(0f)] private float spawnForwardOffset;
        [SerializeField, Tooltip("0 이상이면 Projectile Settings의 Charge Seconds 대신 이 값을 사용합니다. 음수(-1)면 오버라이드하지 않습니다.")]
        private float chargeSecondsOverride = -1f;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

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

        [Header("Charge")]
        [SerializeField, Min(0f)] private float windupSeconds = 0.8f;
        [SerializeField, Min(0.01f)] private float chargeStartRadius = 1.1f;
        [SerializeField, Min(0.01f)] private float chargeEndRadius = 0.08f;
        [SerializeField, Min(0.005f)] private float chargeLineWidth = 0.05f;
        [SerializeField] private Color chargeColor = new(0.32f, 0.8f, 1f, 0.9f);
        [SerializeField] private int chargeSortingOrder = 20;

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

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            context.BeginSnipingTelegraph(ShootAnimationTrigger, HoldTelegraphAnimationParameter);
            if (windupSeconds > 0f)
            {
                if (context.Boss is HackerHologramBoss hologram)
                {
                    hologram.FreezeRecordedPose(windupSeconds);
                }

                Transform chargeFollowTarget = originSpec.GetAimOriginTransform(context, 0) ?? context.Boss?.transform;
                HackerSnipingChargeIndicator chargeIndicator = HackerSnipingChargeIndicator.Create(
                    chargeFollowTarget,
                    originSpec.GetAimOrigin(context, 0),
                    chargeStartRadius,
                    chargeEndRadius,
                    chargeLineWidth,
                    chargeColor,
                    chargeSortingOrder);
                ResolveShot(context, originSpec, aimSpec, out Vector3 indicatorOrigin, out Vector2 indicatorDirection);
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
                        ResolveShot(context, originSpec, aimSpec, out indicatorOrigin, out indicatorDirection);
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

            ResolveShot(context, originSpec, aimSpec, out Vector3 spawnOrigin, out Vector2 finalDirection);
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
            context.PlayMuzzleFlashIfEnabled(effects, firedProjectile, finalDirection);
            context.PlayCameraShakeIfEnabled(effects, finalDirection);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, windupSeconds);
            return true;
        }

        private void ResolveShot(
            BossActionContext context,
            BossGraphProjectileOriginSpec originSpec,
            BossGraphProjectileAimSpec aimSpec,
            out Vector3 spawnOrigin,
            out Vector2 finalDirection)
        {
            Vector3 aimOrigin = originSpec.GetAimOrigin(context, 0);
            Vector2 direction = GetSnipingDirection(context, aimSpec, aimOrigin);
            spawnOrigin = originSpec.GetSpawnOrigin(context, 0, direction);
            finalDirection = GetSnipingDirection(context, aimSpec, spawnOrigin);
            if (spawnForwardOffset > 0f)
            {
                spawnOrigin += (Vector3)(finalDirection.normalized * spawnForwardOffset);
            }
        }

        private static Vector2 GetSnipingDirection(
            BossActionContext context,
            BossGraphProjectileAimSpec aimSpec,
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

            return aimSpec.GetDirection(context, origin);
        }
    }

    internal sealed class HackerSnipingChargeIndicator : MonoBehaviour
    {
        private const int CircleSegments = 40;

        private Transform followTarget;
        private Vector3 fallbackPosition;
        private float startRadius;
        private float endRadius;
        private LineRenderer line;
        private Material lineMaterial;

        internal static HackerSnipingChargeIndicator Create(
            Transform followTarget,
            Vector3 fallbackPosition,
            float startRadius,
            float endRadius,
            float lineWidth,
            Color color,
            int sortingOrder)
        {
            GameObject indicatorObject = new("HackerSnipingChargeIndicator");
            HackerSnipingChargeIndicator indicator = indicatorObject.AddComponent<HackerSnipingChargeIndicator>();
            indicator.followTarget = followTarget;
            indicator.fallbackPosition = fallbackPosition;
            indicator.startRadius = Mathf.Max(0.01f, startRadius);
            indicator.endRadius = Mathf.Max(0.01f, endRadius);
            indicator.transform.position = followTarget != null ? followTarget.position : fallbackPosition;
            indicator.CreateLine(lineWidth, color, sortingOrder);
            indicator.SetProgress(0f);
            return indicator;
        }

        internal void SetProgress(float progress)
        {
            SetRadius(Mathf.Lerp(startRadius, endRadius, Mathf.Clamp01(progress)));
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
