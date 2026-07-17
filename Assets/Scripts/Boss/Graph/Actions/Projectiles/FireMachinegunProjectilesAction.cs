using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class FireAttachedProjectilesAction : BossAction, IBossProjectileEmissionAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(1)] private int bulletCount = 1;
        [SerializeField, Min(0f)] private float radius = 0.5f;
        [SerializeField, Range(0f, 360f)] private float arcDegrees = 360f;
        [SerializeField] private float startAngleOffset;
        [SerializeField] private float rotateDegreesPerSecond;
        [SerializeField, Min(0f)] private float durationSeconds = 2f;
        [SerializeField] private bool destroyOnMotionEnd = true;
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || context.Boss == null)
            {
                yield break;
            }

            if (windupSeconds > 0f)
            {
                yield return context.WaitSeconds(windupSeconds);
            }

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            BossProjectileSettings settings = context.ResolveGraphProjectileSettings(projectileName) ?? projectile;
            Transform anchor = context.Boss.BodyRoot != null ? context.Boss.BodyRoot : context.Boss.transform;
            int count = Mathf.Max(1, bulletCount);
            float step = GetAngleStep(count);
            bool usesMolotovLob = settings?.Prefab is ArsonistMolotovProjectile;
            bool firedAny = false;

            for (int i = 0; i < count; i++)
            {
                Vector3 center = originSpec.GetAimOrigin(context, i);
                Vector2 aimDirection = aimSpec.GetDirection(context, center);
                float aimAngle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                float angle = aimAngle + startAngleOffset + step * i;
                Vector2 direction = BossActionContext.AngleToDirection(angle);
                Vector3 spawnPosition = usesMolotovLob
                    ? center
                    : (Vector3)((Vector2)anchor.position + direction * radius);
                EnemyProjectile firedProjectile = context.FireProjectile(
                    projectile,
                    spawnPosition,
                    direction,
                    0f,
                    aimAtPlayerWhileChargingOverride: false,
                    aimAtPlayerOnLaunchOverride: false,
                    chargeSecondsOverride: 0f,
                    suppressHoming: true,
                    projectileName: projectileName);

                if (firedProjectile == null)
                {
                    continue;
                }

                firedAny = true;
                if (firedProjectile is ArsonistMolotovProjectile molotovProjectile)
                {
                    Vector2 landingPosition = (Vector2)center + direction * radius;
                    molotovProjectile.BeginLobMotion(center, landingPosition);
                }
                else
                {
                    if (durationSeconds > 0f)
                    {
                        firedProjectile.EnsureProjectileLifetime(durationSeconds + Mathf.Max(Time.deltaTime, 0.02f));
                    }

                    firedProjectile.gameObject.AddComponent<BossAttachedProjectileMotion>().Initialize(
                        anchor,
                        radius,
                        angle,
                        rotateDegreesPerSecond,
                        durationSeconds,
                        destroyOnMotionEnd);
                }

                context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
            }

            if (!firedAny)
            {
                yield break;
            }

            context.PlaySfx(fireSfxId);
            context.PlayOriginBurst(effects, anchor.position);
            yield return context.WaitSeconds(durationSeconds);
        }

        private float GetAngleStep(int count)
        {
            if (count <= 1)
            {
                return 0f;
            }

            return arcDegrees >= 360f ? 360f / count : arcDegrees / (count - 1);
        }
    }

    [Serializable]
    public sealed class FireConfiguredVolleyProjectilesAction : BossAction, IBossProjectileEmissionAction
    {
        [Serializable]
        public sealed class Volley
        {
            [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
            [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
            [SerializeField] private BossGraphProjectileOriginSpec origin = new();
            [SerializeField] private BossGraphProjectileAimSpec aim = new();
            [SerializeField, Min(1)] private int bulletCount = 1;
            [SerializeField] private float angleOffsetDegrees;
            [SerializeField, Min(0f)] private float spawnSpacing;
            [SerializeField, Min(0f)] private float fireInterval;
            [SerializeField, Min(0f)] private float restSeconds = 0.2f;
            [SerializeField] private float chargeSecondsOverride = -1f;

            public string ProjectileName => projectileName;
            public BossProjectileSettings Projectile => projectile;
            public BossGraphProjectileOriginSpec Origin => origin ?? new BossGraphProjectileOriginSpec();
            public BossGraphProjectileAimSpec Aim => aim ?? new BossGraphProjectileAimSpec();
            public int BulletCount => Mathf.Max(1, bulletCount);
            public float AngleOffsetDegrees => angleOffsetDegrees;
            public float SpawnSpacing => Mathf.Max(0f, spawnSpacing);
            public float FireInterval => Mathf.Max(0f, fireInterval);
            public float RestSeconds => Mathf.Max(0f, restSeconds);
            public float ChargeSecondsOverride => chargeSecondsOverride;
        }

        [FormerlySerializedAs("startDelaySeconds")]
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();
        [SerializeField] private List<Volley> volleys = new() { new Volley() };

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || volleys == null || volleys.Count == 0)
            {
                yield break;
            }

            if (windupSeconds > 0f)
            {
                yield return context.WaitSeconds(windupSeconds);
            }

            int shotIndex = 0;
            for (int volleyIndex = 0; volleyIndex < volleys.Count; volleyIndex++)
            {
                Volley volley = volleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                for (int bulletIndex = 0; bulletIndex < volley.BulletCount; bulletIndex++)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        bulletIndex--;
                        continue;
                    }

                    FireShot(context, volley, shotIndex, bulletIndex);
                    shotIndex++;

                    if (volley.FireInterval > 0f && bulletIndex < volley.BulletCount - 1)
                    {
                        yield return context.WaitSeconds(volley.FireInterval);
                    }
                }

                if (volley.RestSeconds > 0f && volleyIndex < volleys.Count - 1)
                {
                    yield return context.WaitSeconds(volley.RestSeconds);
                }
            }
        }

        private void FireShot(BossActionContext context, Volley volley, int shotIndex, int bulletIndex)
        {
            Vector3 aimOrigin = volley.Origin.GetAimOrigin(context, shotIndex);
            Vector2 baseDirection = volley.Aim.GetDirection(context, aimOrigin);
            float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;
            Vector2 finalDirection = BossActionContext.AngleToDirection(baseAngle + volley.AngleOffsetDegrees);
            Vector3 spawnOrigin = volley.Origin.GetSpawnOrigin(context, shotIndex, finalDirection);
            Vector2 side = new(-finalDirection.y, finalDirection.x);
            Vector3 spawnPosition = spawnOrigin + (Vector3)(side * GetCenteredOffset(bulletIndex, volley.BulletCount, volley.SpawnSpacing));

            EnemyProjectile firedProjectile = context.FireProjectile(
                volley.Projectile,
                spawnPosition,
                finalDirection,
                0f,
                chargeSecondsOverride: volley.ChargeSecondsOverride,
                projectileName: volley.ProjectileName);

            if (firedProjectile == null)
            {
                return;
            }

            context.PlaySfx(fireSfxId);
            context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
            context.PlayOriginBurst(effects, spawnPosition);
            context.PlayMuzzleFlashIfEnabled(effects, firedProjectile, finalDirection);
            context.PlayCameraShakeIfEnabled(effects, finalDirection);
        }

        private static float GetCenteredOffset(int index, int count, float spacing)
        {
            if (spacing <= 0f || count <= 1)
            {
                return 0f;
            }

            return (index - (count - 1) * 0.5f) * spacing;
        }
    }

    [Serializable]
    public sealed class FirePlayerCircleProjectilesAction : BossAction, IBossProjectileEmissionAction
    {
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField, Min(1)] private int bulletCount = 8;
        [SerializeField, Min(0.1f)] private float circleRadius = 2.4f;
        [SerializeField] private float angularSpeedDegrees = 180f;
        [SerializeField] private float startAngleOffset;
        [SerializeField] private bool randomizeStartAngle;
        [SerializeField, Min(0f)] private float fireInterval;
        [SerializeField, Min(0f)] private float motionDurationSeconds = 3f;
        [SerializeField] private bool destroyOnMotionEnd = true;
        [SerializeField] private bool waitForMotionEnd;
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || context.Boss == null || context.Boss.Player == null)
            {
                yield break;
            }

            Vector2 lockedCenter = context.Boss.Player.position;
            if (windupSeconds > 0f)
            {
                yield return context.WaitSeconds(windupSeconds);
            }

            int count = Mathf.Max(1, bulletCount);
            float firstAngle = randomizeStartAngle ? UnityEngine.Random.Range(0f, 360f) : startAngleOffset;
            for (int i = 0; i < count; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                float angle = firstAngle + 360f / count * i;
                Vector2 radialDirection = BossActionContext.AngleToDirection(angle);
                Vector2 tangentDirection = BossActionContext.AngleToDirection(angle + Mathf.Sign(angularSpeedDegrees == 0f ? 1f : angularSpeedDegrees) * 90f);
                Vector3 spawnPosition = (Vector3)(lockedCenter + radialDirection * circleRadius);
                EnemyProjectile firedProjectile = context.FireProjectile(
                    projectile,
                    spawnPosition,
                    tangentDirection,
                    0f,
                    aimAtPlayerWhileChargingOverride: false,
                    aimAtPlayerOnLaunchOverride: false,
                    chargeSecondsOverride: 0f,
                    suppressHoming: true,
                    projectileName: projectileName);

                if (firedProjectile != null)
                {
                    firedProjectile.gameObject.AddComponent<BossPointOrbitProjectileMotion>().Initialize(
                        lockedCenter,
                        circleRadius,
                        angle,
                        angularSpeedDegrees,
                        motionDurationSeconds,
                        destroyOnMotionEnd);
                    context.PlaySfx(fireSfxId);
                    context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
                    context.PlayOriginBurst(effects, spawnPosition);
                    context.PlayMuzzleFlashIfEnabled(effects, firedProjectile, tangentDirection);
                    context.PlayCameraShakeIfEnabled(effects, tangentDirection);
                }

                if (fireInterval > 0f && i < count - 1)
                {
                    yield return context.WaitSeconds(fireInterval);
                }
            }

            if (waitForMotionEnd && motionDurationSeconds > 0f)
            {
                yield return context.WaitSeconds(motionDurationSeconds);
            }
        }
    }

}
