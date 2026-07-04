using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class FireRadialEmissionAction : BossAction, ISerializationCallbackReceiver
    {
        [Serializable]
        public sealed class Volley
        {
            [SerializeField, Min(1)] private int bulletCount = 8;
            [SerializeField, Range(0f, 360f)] private float arcDegrees = 360f;
            [SerializeField] private float startAngleOffset;
            [SerializeField] private bool randomizeStartAngle;
            [SerializeField, Min(0f)] private float spawnRadius;
            [SerializeField, Min(0f)] private float fireInterval;
            [SerializeField, Min(0f)] private float restSeconds = 0.35f;

            public Volley()
            {
            }

            public Volley(
                int bulletCount,
                float arcDegrees,
                float startAngleOffset,
                bool randomizeStartAngle,
                float spawnRadius,
                float fireInterval,
                float restSeconds)
            {
                this.bulletCount = Mathf.Max(1, bulletCount);
                this.arcDegrees = Mathf.Clamp(arcDegrees, 0f, 360f);
                this.startAngleOffset = startAngleOffset;
                this.randomizeStartAngle = randomizeStartAngle;
                this.spawnRadius = Mathf.Max(0f, spawnRadius);
                this.fireInterval = Mathf.Max(0f, fireInterval);
                this.restSeconds = Mathf.Max(0f, restSeconds);
            }

            public int BulletCount => Mathf.Max(1, bulletCount);
            public float ArcDegrees => Mathf.Clamp(arcDegrees, 0f, 360f);
            public float StartAngleOffset => startAngleOffset;
            public bool RandomizeStartAngle => randomizeStartAngle;
            public float SpawnRadius => Mathf.Max(0f, spawnRadius);
            public float FireInterval => Mathf.Max(0f, fireInterval);
            public float RestSeconds => Mathf.Max(0f, restSeconds);
        }

        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();
        [FormerlySerializedAs("bulletCount")]
        [SerializeField, HideInInspector] private int legacyBulletCount;
        [FormerlySerializedAs("arcDegrees")]
        [SerializeField, HideInInspector] private float legacyArcDegrees = -1f;
        [FormerlySerializedAs("startAngleOffset")]
        [SerializeField, HideInInspector] private float legacyStartAngleOffset;
        [FormerlySerializedAs("randomizeStartAngle")]
        [SerializeField, HideInInspector] private bool legacyRandomizeStartAngle;
        [FormerlySerializedAs("spawnRadius")]
        [SerializeField, HideInInspector] private float legacySpawnRadius = -1f;
        [FormerlySerializedAs("fireInterval")]
        [SerializeField, HideInInspector] private float legacyFireInterval = -1f;
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

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            int shotIndex = 0;
            for (int volleyIndex = 0; volleyIndex < volleys.Count; volleyIndex++)
            {
                Volley volley = volleys[volleyIndex];
                if (volley == null)
                {
                    continue;
                }

                int count = volley.BulletCount;
                float step = GetAngleStep(count, volley.ArcDegrees);
                float angleOffset = volley.RandomizeStartAngle
                    ? UnityEngine.Random.Range(0f, 360f)
                    : volley.StartAngleOffset;

                for (int bulletIndex = 0; bulletIndex < count; bulletIndex++)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        bulletIndex--;
                        continue;
                    }

                    FireShot(context, originSpec, aimSpec, volley, shotIndex, bulletIndex, step, angleOffset);
                    shotIndex++;

                    if (bulletIndex < count - 1 && volley.FireInterval > 0f)
                    {
                        yield return context.WaitSeconds(volley.FireInterval);
                    }
                }

                if (volleyIndex < volleys.Count - 1 && volley.RestSeconds > 0f)
                {
                    yield return context.WaitSeconds(volley.RestSeconds);
                }
            }
        }

        public void OnBeforeSerialize()
        {
            legacyBulletCount = 0;
            legacyArcDegrees = -1f;
            legacyStartAngleOffset = 0f;
            legacyRandomizeStartAngle = false;
            legacySpawnRadius = -1f;
            legacyFireInterval = -1f;
        }

        public void OnAfterDeserialize()
        {
            if (legacyBulletCount <= 0
                && legacyArcDegrees < 0f
                && legacySpawnRadius < 0f
                && legacyFireInterval < 0f)
            {
                return;
            }

            volleys = new List<Volley>
            {
                new(
                    legacyBulletCount > 0 ? legacyBulletCount : 8,
                    legacyArcDegrees >= 0f ? legacyArcDegrees : 360f,
                    legacyStartAngleOffset,
                    legacyRandomizeStartAngle,
                    legacySpawnRadius >= 0f ? legacySpawnRadius : 0f,
                    legacyFireInterval >= 0f ? legacyFireInterval : 0f,
                    0f)
            };

            legacyBulletCount = 0;
            legacyArcDegrees = -1f;
            legacyStartAngleOffset = 0f;
            legacyRandomizeStartAngle = false;
            legacySpawnRadius = -1f;
            legacyFireInterval = -1f;
        }

        private void FireShot(
            BossActionContext context,
            BossGraphProjectileOriginSpec originSpec,
            BossGraphProjectileAimSpec aimSpec,
            Volley volley,
            int shotIndex,
            int bulletIndex,
            float angleStep,
            float angleOffset)
        {
            Vector3 center = originSpec.GetAimOrigin(context, shotIndex);
            Vector2 centerDirection = aimSpec.GetDirection(context, center);
            float baseAngle = Mathf.Atan2(centerDirection.y, centerDirection.x) * Mathf.Rad2Deg;
            float firstAngle = volley.ArcDegrees >= 360f
                ? baseAngle + angleOffset
                : baseAngle - volley.ArcDegrees * 0.5f + angleOffset;
            Vector2 direction = BossActionContext.AngleToDirection(firstAngle + angleStep * bulletIndex);
            Vector3 spawnOrigin = volley.SpawnRadius > 0f
                ? center + (Vector3)(direction * volley.SpawnRadius)
                : center;

            EnemyProjectile firedProjectile = context.FireProjectile(
                projectile,
                spawnOrigin,
                direction,
                0.9f,
                projectileName: projectileName);

            if (firedProjectile == null)
            {
                return;
            }

            context.PlaySfx(fireSfxId);
            context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
            context.PlayOriginBurst(effects, spawnOrigin);
            context.PlayMuzzleFlashIfEnabled(effects, spawnOrigin, direction);
            context.PlayCameraShakeIfEnabled(effects, direction);
        }

        private static float GetAngleStep(int count, float arcDegrees)
        {
            if (count <= 1)
            {
                return 0f;
            }

            return arcDegrees >= 360f
                ? 360f / count
                : arcDegrees / (count - 1);
        }
    }
}
