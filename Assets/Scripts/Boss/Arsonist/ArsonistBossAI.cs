using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.Combat;

namespace Week14.Enemy
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Boss/Arsonist Boss")]
    public sealed class ArsonistBossAI : GraphBossAI
    {
        private const string BgmId = "ArsonistBgm";

        [SerializeField, Min(0.05f)] private float oilTrailDuration = 3.5f;
        [SerializeField, Min(0.05f)] private float ignitedOilDuration = 3.5f;
        [SerializeField, Min(0.05f)] private float oilConnectionRadius = 0.95f;
        [SerializeField, Min(0.01f)] private float oilIgnitionSpreadInterval = 0.06f;

        [SerializeField, Min(0.05f)] private float oilSoakedDuration = 4f;
        [SerializeField, Min(0.01f)] private float oilTrailInterval = 0.18f;

        [SerializeField, Min(1)] private int fireDamage = 1;
        [SerializeField, Min(0.05f)] private float fireDamageInterval = 0.45f;

        [SerializeField, Min(1)] private int burnTotalDamage = 3;
        [SerializeField, Min(0.05f)] private float burnTickInterval = 0.75f;
        [SerializeField, Min(0.05f)] private float burnDuration = 3f;

        private readonly List<ArsonistOilPatch> oilPatches = new();
        private readonly List<ArsonistFireArea> fireAreas = new();
        private readonly Dictionary<PlayerCombatController, ArsonistOilSoakedStatus> oilSoakedStatuses = new();
        private readonly Dictionary<PlayerCombatController, float> nextFireDamageAtByPlayer = new();
        private readonly List<ArsonistOilSoakedStatus> statusBuffer = new();
        private int oilIgnitionVersion;

        protected override bool RotatesBodyToPlayer => false;

        protected override void OnCombatStarted()
        {
            SoundManager.PlayBgm(BgmId);
        }

        protected override void OnBossDied()
        {
            ClearArsonistHazards();
            base.OnBossDied();
        }

        protected override void OnDisable()
        {
            ClearArsonistHazards();
            base.OnDisable();
        }

        internal ArsonistOilPatch CreateOilPatch(
            Vector3 position,
            float radius,
            float duration,
            Color oilColor,
            float trailSpacing)
        {
            return CreateOilPatch(position, radius, duration, oilColor, trailSpacing, null);
        }

        internal ArsonistOilPatch CreateOilTrailPatch(
            Vector3 position,
            PlayerCombatController sourcePlayer,
            Color oilColor,
            float radius,
            float trailSpacing)
        {
            return CreateOilPatch(position, radius, oilTrailDuration, oilColor, trailSpacing, sourcePlayer);
        }

        internal ArsonistFireArea CreateFireArea(
            Vector3 position,
            float radius,
            float duration,
            Color fireColor,
            float playerDamageDelay = 0f)
        {
            GameObject fireObject = new("ArsonistFireArea");
            fireObject.transform.position = FlattenPosition(position);
            CircleCollider2D collider = fireObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.05f, radius);

            ArsonistFireArea fireArea = fireObject.AddComponent<ArsonistFireArea>();
            fireAreas.Add(fireArea);
            fireArea.Initialize(this, Mathf.Max(0.05f, radius), Mathf.Max(0.05f, duration), fireColor, playerDamageDelay);
            return fireArea;
        }

        internal void ApplyOilSoaked(PlayerCombatController player, Color oilColor, float trailRadius, float trailSpacing)
        {
            if (player == null || player.Health == null || player.Health.IsDead)
            {
                return;
            }

            if (!oilSoakedStatuses.TryGetValue(player, out ArsonistOilSoakedStatus status) || status == null)
            {
                status = player.gameObject.GetComponent<ArsonistOilSoakedStatus>();
                if (status == null)
                {
                    status = player.gameObject.AddComponent<ArsonistOilSoakedStatus>();
                }

                oilSoakedStatuses[player] = status;
            }

            status.Configure(this, player, oilColor, oilSoakedDuration, oilTrailInterval, trailRadius, trailSpacing);
        }

        internal void ApplyFireContact(
            PlayerCombatController player,
            Vector3 sourcePosition,
            Dictionary<PlayerCombatController, float> nextDamageAtByPlayer)
        {
            if (player == null || player.Health == null || player.Health.IsDead)
            {
                return;
            }

            float now = Time.time;
            if (nextFireDamageAtByPlayer.TryGetValue(player, out float globalNextDamageAt)
                && now < globalNextDamageAt)
            {
                return;
            }

            if (nextDamageAtByPlayer != null
                && nextDamageAtByPlayer.TryGetValue(player, out float nextDamageAt)
                && now < nextDamageAt)
            {
                return;
            }

            Vector2 direction = (Vector2)(player.transform.position - sourcePosition);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.up;
            }

            if (player.ReceiveAttack(fireDamage, sourcePosition, direction.normalized))
            {
                ApplyBurn(player);
            }

            if (nextDamageAtByPlayer != null)
            {
                nextDamageAtByPlayer[player] = now + fireDamageInterval;
            }

            nextFireDamageAtByPlayer[player] = now + fireDamageInterval;
        }

        internal void DelayFireDamageFor(PlayerCombatController player, float seconds)
        {
            if (player == null || seconds <= 0f)
            {
                return;
            }

            float nextDamageAt = Time.time + seconds;
            if (!nextFireDamageAtByPlayer.TryGetValue(player, out float currentNextDamageAt)
                || currentNextDamageAt < nextDamageAt)
            {
                nextFireDamageAtByPlayer[player] = nextDamageAt;
            }
        }

        internal void ApplyBurn(PlayerCombatController player)
        {
            if (player == null || player.Health == null || player.Health.IsDead)
            {
                return;
            }

            ArsonistBurnStatus burn = player.gameObject.GetComponent<ArsonistBurnStatus>();
            if (burn == null)
            {
                burn = player.gameObject.AddComponent<ArsonistBurnStatus>();
            }

            burn.Configure(player, burnTotalDamage, burnTickInterval, burnDuration);
        }

        internal void IgniteOilNetwork(ArsonistOilPatch seed, Color fireColor)
        {
            if (seed == null)
            {
                return;
            }

            List<List<ArsonistOilPatch>> ignitionLayers = BuildOilIgnitionLayers(seed);
            if (ignitionLayers.Count == 0)
            {
                return;
            }

            StartCoroutine(SpreadOilIgnition(ignitionLayers, fireColor, oilIgnitionVersion));
        }

        private List<List<ArsonistOilPatch>> BuildOilIgnitionLayers(ArsonistOilPatch seed)
        {
            List<List<ArsonistOilPatch>> layers = new();
            if (seed == null || !seed.TryReserveIgnition())
            {
                return layers;
            }

            Queue<ArsonistOilPatch> queue = new();
            Dictionary<ArsonistOilPatch, int> depths = new();
            queue.Enqueue(seed);
            depths[seed] = 0;
            AddOilPatchToIgnitionLayer(layers, seed, 0);

            while (queue.Count > 0)
            {
                ArsonistOilPatch current = queue.Dequeue();
                if (current == null)
                {
                    continue;
                }

                int nextDepth = depths[current] + 1;

                for (int i = 0; i < oilPatches.Count; i++)
                {
                    ArsonistOilPatch other = oilPatches[i];
                    if (other == null || !other.CanIgnite)
                    {
                        continue;
                    }

                    if (!AreOilPatchesConnected(current, other))
                    {
                        continue;
                    }

                    if (!other.TryReserveIgnition())
                    {
                        continue;
                    }

                    depths[other] = nextDepth;
                    AddOilPatchToIgnitionLayer(layers, other, nextDepth);
                    queue.Enqueue(other);
                }
            }

            return layers;
        }

        private IEnumerator SpreadOilIgnition(
            List<List<ArsonistOilPatch>> ignitionLayers,
            Color fireColor,
            int ignitionVersion)
        {
            float spreadInterval = Mathf.Max(0.01f, oilIgnitionSpreadInterval);
            for (int i = 0; i < ignitionLayers.Count; i++)
            {
                if (ignitionVersion != oilIgnitionVersion)
                {
                    yield break;
                }

                List<ArsonistOilPatch> layer = ignitionLayers[i];
                for (int j = 0; j < layer.Count; j++)
                {
                    ArsonistOilPatch patch = layer[j];
                    if (patch == null || patch.IsIgnited)
                    {
                        continue;
                    }

                    patch.IgniteLocal(ignitedOilDuration, fireColor);
                    NotifyOilPatchIgnited(patch);
                }

                if (i < ignitionLayers.Count - 1)
                {
                    yield return new WaitForSeconds(spreadInterval);
                }
            }
        }

        private static void AddOilPatchToIgnitionLayer(
            List<List<ArsonistOilPatch>> layers,
            ArsonistOilPatch patch,
            int depth)
        {
            while (layers.Count <= depth)
            {
                layers.Add(new List<ArsonistOilPatch>());
            }

            layers[depth].Add(patch);
        }

        internal void TryIgniteOilAt(Vector3 position, float radius, Color fireColor)
        {
            float igniteRadius = Mathf.Max(0f, radius) + oilConnectionRadius * 0.5f;
            while (TryFindClosestIgnitableOilPatch(position, igniteRadius, out ArsonistOilPatch patch))
            {
                IgniteOilNetwork(patch, fireColor);
            }
        }

        internal void UnregisterOilPatch(ArsonistOilPatch patch)
        {
            oilPatches.Remove(patch);
            foreach (ArsonistOilSoakedStatus status in oilSoakedStatuses.Values)
            {
                status?.RemoveTrailPatch(patch);
            }
        }

        internal void UnregisterFireArea(ArsonistFireArea fireArea)
        {
            fireAreas.Remove(fireArea);
        }

        internal void UnregisterOilSoakedStatus(PlayerCombatController player, ArsonistOilSoakedStatus status)
        {
            if (player == null)
            {
                return;
            }

            if (oilSoakedStatuses.TryGetValue(player, out ArsonistOilSoakedStatus current) && current == status)
            {
                oilSoakedStatuses.Remove(player);
            }
        }

        private ArsonistOilPatch CreateOilPatch(
            Vector3 position,
            float radius,
            float duration,
            Color oilColor,
            float trailSpacing,
            PlayerCombatController sourcePlayer)
        {
            GameObject patchObject = new("ArsonistOilPatch");
            patchObject.transform.position = FlattenPosition(position);
            CircleCollider2D collider = patchObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.05f, radius);

            ArsonistOilPatch patch = patchObject.AddComponent<ArsonistOilPatch>();
            patch.Initialize(this, Mathf.Max(0.05f, radius), Mathf.Max(0.05f, duration), oilColor, trailSpacing);
            oilPatches.Add(patch);

            if (sourcePlayer != null
                && oilSoakedStatuses.TryGetValue(sourcePlayer, out ArsonistOilSoakedStatus status)
                && status != null)
            {
                status.AddTrailPatch(patch);
            }

            TryIgniteNewOilPatchFromActiveFire(patch);
            return patch;
        }

        private void TryIgniteNewOilPatchFromActiveFire(ArsonistOilPatch patch)
        {
            if (patch == null || !patch.CanIgnite)
            {
                return;
            }

            for (int i = 0; i < fireAreas.Count; i++)
            {
                ArsonistFireArea fireArea = fireAreas[i];
                if (fireArea != null && fireArea.CanIgniteOilAt(patch.transform.position, patch.Radius))
                {
                    IgniteOilNetwork(patch, fireArea.FireColor);
                    return;
                }
            }

            for (int i = 0; i < oilPatches.Count; i++)
            {
                ArsonistOilPatch other = oilPatches[i];
                if (other != null && other.IsIgnited && AreOilPatchesConnected(patch, other))
                {
                    IgniteOilNetwork(patch, other.FireColor);
                    return;
                }
            }
        }

        private void NotifyOilPatchIgnited(ArsonistOilPatch patch)
        {
            statusBuffer.Clear();
            foreach (ArsonistOilSoakedStatus status in oilSoakedStatuses.Values)
            {
                if (status != null)
                {
                    statusBuffer.Add(status);
                }
            }

            for (int i = 0; i < statusBuffer.Count; i++)
            {
                ArsonistOilSoakedStatus status = statusBuffer[i];
                if (status != null && status.ShouldIgniteFromOilPatch(patch, oilConnectionRadius))
                {
                    status.IgniteFromOilPatch();
                }
            }

            statusBuffer.Clear();
        }

        private bool AreOilPatchesConnected(ArsonistOilPatch first, ArsonistOilPatch second)
        {
            if (first == null || second == null)
            {
                return false;
            }

            float maxDistance = first.Radius + second.Radius + oilConnectionRadius;
            return Vector2.SqrMagnitude((Vector2)first.transform.position - (Vector2)second.transform.position)
                <= maxDistance * maxDistance;
        }

        private bool TryFindClosestIgnitableOilPatch(
            Vector3 position,
            float igniteRadius,
            out ArsonistOilPatch closestPatch)
        {
            closestPatch = null;
            float bestSqrDistance = float.PositiveInfinity;
            for (int i = 0; i < oilPatches.Count; i++)
            {
                ArsonistOilPatch patch = oilPatches[i];
                if (patch == null || !patch.CanIgnite)
                {
                    continue;
                }

                float maxDistance = igniteRadius + patch.Radius;
                float sqrDistance = Vector2.SqrMagnitude((Vector2)patch.transform.position - (Vector2)position);
                if (sqrDistance > maxDistance * maxDistance || sqrDistance >= bestSqrDistance)
                {
                    continue;
                }

                bestSqrDistance = sqrDistance;
                closestPatch = patch;
            }

            return closestPatch != null;
        }

        private void ClearArsonistHazards()
        {
            oilIgnitionVersion++;

            for (int i = oilPatches.Count - 1; i >= 0; i--)
            {
                if (oilPatches[i] != null)
                {
                    Destroy(oilPatches[i].gameObject);
                }
            }

            for (int i = fireAreas.Count - 1; i >= 0; i--)
            {
                if (fireAreas[i] != null)
                {
                    Destroy(fireAreas[i].gameObject);
                }
            }

            foreach (ArsonistOilSoakedStatus status in oilSoakedStatuses.Values)
            {
                if (status != null)
                {
                    Destroy(status);
                }
            }

            oilPatches.Clear();
            fireAreas.Clear();
            oilSoakedStatuses.Clear();
            nextFireDamageAtByPlayer.Clear();
        }

        private static Vector3 FlattenPosition(Vector3 position)
        {
            position.z = 0f;
            return position;
        }
    }

    [Serializable]
    public sealed class ArsonistCircleOrbitAttackAction : BossAction
    {
        [Serializable]
        public sealed class AttackVolley
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

        [SerializeField, Min(0f)] private float windupSeconds;

        [Header("Circle Projectile")]
        [SerializeField, BossGraphProjectileName] private string circleProjectileName = "기름";
        [SerializeField, HideInInspector] private BossProjectileSettings circleProjectile = new();
        [SerializeField, Min(1)] private int circleBulletCount = 1;
        [SerializeField, Min(0.1f)] private float circleRadius = 8f;
        [SerializeField] private float circleAngularSpeedDegrees = 180f;
        [SerializeField] private float circleStartAngleOffset;
        [SerializeField] private bool randomizeCircleStartAngle;
        [SerializeField, Min(0f)] private float circleFireInterval;
        [SerializeField, Min(0f)] private float circleMotionDurationSeconds = 3f;
        [SerializeField] private bool destroyCircleProjectileOnEnd = true;
        [SerializeField] private bool waitForCircleMotionEnd = true;
        [SerializeField, BossGraphSfxId] private string circleFireSfxId;
        [SerializeField, BossGraphSfxId] private string circleLaunchSfxId;
        [SerializeField] private BossGraphEffectSettings circleEffects = new();

        [Header("Boss Orbit")]
        [SerializeField, Min(0f)] private float orbitEntrySeconds = 1f;
        [SerializeField, Min(0f)] private float orbitArriveDistance = 0.08f;
        [SerializeField, Min(0.01f)] private float orbitDurationSeconds = 6f;
        [SerializeField] private float bossAngularSpeedDegrees = 180f;
        [SerializeField, Min(0f)] private float bossSpeedMultiplier = 1f;
        [SerializeField, Min(0f)] private float orbitPositionCorrection = 8f;
        [SerializeField] private bool stopWhenFinished = true;

        [Header("Attack While Moving")]
        [SerializeField, Min(0f)] private float attackStartDelaySeconds;
        [SerializeField] private bool loopAttackVolleys;
        [SerializeField, BossGraphSfxId] private string attackFireSfxId;
        [SerializeField, BossGraphSfxId] private string attackLaunchSfxId;
        [SerializeField] private BossGraphEffectSettings attackEffects = new();
        [SerializeField] private List<AttackVolley> attackVolleys = new() { new AttackVolley() };

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null || context.Boss == null || context.Boss.Body == null || context.Boss.Player == null)
            {
                yield break;
            }

            if (windupSeconds > 0f)
            {
                yield return context.WaitSeconds(windupSeconds);
            }

            Vector2 lockedCenter = context.Boss.Player.position;
            float circleStartAngle = randomizeCircleStartAngle
                ? UnityEngine.Random.Range(0f, 360f)
                : circleStartAngleOffset;

            yield return SpawnCircleProjectiles(context, lockedCenter, circleStartAngle);
            if (waitForCircleMotionEnd && circleMotionDurationSeconds > 0f)
            {
                yield return context.WaitSeconds(circleMotionDurationSeconds);
            }

            yield return MoveToOrbitStart(context, lockedCenter, circleStartAngle);
            yield return OrbitAndAttack(context, lockedCenter, circleStartAngle);
        }

        private IEnumerator SpawnCircleProjectiles(BossActionContext context, Vector2 lockedCenter, float firstAngle)
        {
            int count = Mathf.Max(1, circleBulletCount);
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
                Vector2 tangentDirection = BossActionContext.AngleToDirection(
                    angle + Mathf.Sign(circleAngularSpeedDegrees == 0f ? 1f : circleAngularSpeedDegrees) * 90f);
                Vector3 spawnPosition = (Vector3)(lockedCenter + radialDirection * circleRadius);
                EnemyProjectile firedProjectile = context.FireProjectile(
                    circleProjectile,
                    spawnPosition,
                    tangentDirection,
                    0f,
                    aimAtPlayerWhileChargingOverride: false,
                    aimAtPlayerOnLaunchOverride: false,
                    chargeSecondsOverride: 0f,
                    suppressHoming: true,
                    projectileName: circleProjectileName);

                if (firedProjectile != null)
                {
                    firedProjectile.ConfigurePathIndicatorSuppressed(true);
                    firedProjectile.gameObject.AddComponent<BossPointOrbitProjectileMotion>().Initialize(
                        lockedCenter,
                        circleRadius,
                        angle,
                        circleAngularSpeedDegrees,
                        circleMotionDurationSeconds,
                        destroyCircleProjectileOnEnd);
                    context.PlaySfx(circleFireSfxId);
                    context.PlaySfxOnLaunch(firedProjectile, circleLaunchSfxId);
                    context.PlayOriginBurst(circleEffects, spawnPosition);
                    context.PlayMuzzleFlashIfEnabled(circleEffects, spawnPosition, tangentDirection);
                    context.PlayCameraShakeIfEnabled(circleEffects, tangentDirection);
                }

                if (circleFireInterval > 0f && i < count - 1)
                {
                    yield return context.WaitSeconds(circleFireInterval);
                }
            }
        }

        private IEnumerator OrbitAndAttack(BossActionContext context, Vector2 center, float fallbackStartAngle)
        {
            AttackState attackState = new(attackStartDelaySeconds);
            float elapsed = 0f;
            float angle = GetBossStartAngle(context, center, fallbackStartAngle);
            float duration = Mathf.Max(0.01f, orbitDurationSeconds);
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                float deltaTime = Time.deltaTime;
                MoveBossOnCircle(context, center, ref angle, deltaTime);
                FireDueAttacks(context, attackState, elapsed);

                elapsed += deltaTime;
                yield return null;
            }

            if (stopWhenFinished)
            {
                context.Stop();
            }
        }

        private IEnumerator MoveToOrbitStart(BossActionContext context, Vector2 center, float startAngle)
        {
            float duration = Mathf.Max(0f, orbitEntrySeconds);
            if (duration <= 0f)
            {
                yield break;
            }

            Vector2 target = GetOrbitPoint(center, startAngle);
            float arriveDistance = Mathf.Max(0f, orbitArriveDistance);
            float arriveDistanceSqr = arriveDistance * arriveDistance;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    continue;
                }

                Vector2 current = context.Boss.Body.position;
                Vector2 toTarget = target - current;
                if (toTarget.sqrMagnitude <= arriveDistanceSqr)
                {
                    break;
                }

                float remaining = Mathf.Max(0.0001f, duration - elapsed);
                context.Boss.SetMovementVelocity(toTarget / remaining);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private float GetBossStartAngle(BossActionContext context, Vector2 center, float fallbackStartAngle)
        {
            Vector2 startOffset = context.Boss.Body.position - center;
            return startOffset.sqrMagnitude > 0.0001f
                ? Mathf.Atan2(startOffset.y, startOffset.x) * Mathf.Rad2Deg
                : fallbackStartAngle;
        }

        private void MoveBossOnCircle(BossActionContext context, Vector2 center, ref float angle, float deltaTime)
        {
            float safeDeltaTime = Mathf.Max(0.0001f, deltaTime);
            float radius = Mathf.Max(0.1f, circleRadius);
            float angularSpeed = bossAngularSpeedDegrees * Mathf.Max(0f, bossSpeedMultiplier);
            angle += angularSpeed * safeDeltaTime;
            Vector2 radialDirection = BossActionContext.AngleToDirection(angle);
            Vector2 target = center + radialDirection * radius;
            Vector2 current = context.Boss.Body.position;
            Vector2 tangentDirection = new(-radialDirection.y, radialDirection.x);
            Vector2 tangentVelocity = tangentDirection * (angularSpeed * Mathf.Deg2Rad * radius);
            Vector2 correctionVelocity = (target - current) * Mathf.Max(0f, orbitPositionCorrection);
            context.Boss.SetMovementVelocity(tangentVelocity + correctionVelocity);
        }

        private Vector2 GetOrbitPoint(Vector2 center, float angle)
        {
            return center + BossActionContext.AngleToDirection(angle) * Mathf.Max(0.1f, circleRadius);
        }

        private void FireDueAttacks(BossActionContext context, AttackState state, float elapsed)
        {
            if (attackVolleys == null || attackVolleys.Count == 0 || state.Completed)
            {
                return;
            }

            int safety = 64;
            while (!state.Completed && elapsed >= state.NextFireTime && safety-- > 0)
            {
                AttackVolley volley = GetValidVolley(state);
                if (volley == null)
                {
                    state.Completed = true;
                    return;
                }

                FireAttackShot(context, volley, state.ShotIndex, state.BulletIndex);
                state.ShotIndex++;
                state.BulletIndex++;

                if (state.BulletIndex < volley.BulletCount)
                {
                    state.NextFireTime = elapsed + volley.FireInterval;
                    continue;
                }

                state.BulletIndex = 0;
                state.VolleyIndex++;
                bool wrapped = state.VolleyIndex >= attackVolleys.Count;
                if (wrapped && loopAttackVolleys)
                {
                    state.VolleyIndex = 0;
                }
                else if (wrapped)
                {
                    state.Completed = true;
                    return;
                }

                state.NextFireTime = elapsed + volley.RestSeconds;
            }
        }

        private AttackVolley GetValidVolley(AttackState state)
        {
            if (attackVolleys == null || attackVolleys.Count == 0)
            {
                return null;
            }

            int checkedCount = 0;
            while (checkedCount < attackVolleys.Count)
            {
                int index = Mathf.Clamp(state.VolleyIndex, 0, attackVolleys.Count - 1);
                AttackVolley volley = attackVolleys[index];
                if (volley != null)
                {
                    return volley;
                }

                state.VolleyIndex++;
                if (state.VolleyIndex >= attackVolleys.Count)
                {
                    if (!loopAttackVolleys)
                    {
                        return null;
                    }

                    state.VolleyIndex = 0;
                }

                checkedCount++;
            }

            return null;
        }

        private void FireAttackShot(BossActionContext context, AttackVolley volley, int shotIndex, int bulletIndex)
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

            context.PlaySfx(attackFireSfxId);
            context.PlaySfxOnLaunch(firedProjectile, attackLaunchSfxId);
            context.PlayOriginBurst(attackEffects, spawnPosition);
            context.PlayMuzzleFlashIfEnabled(attackEffects, spawnPosition, finalDirection);
            context.PlayCameraShakeIfEnabled(attackEffects, finalDirection);
        }

        private static float GetCenteredOffset(int index, int count, float spacing)
        {
            if (spacing <= 0f || count <= 1)
            {
                return 0f;
            }

            return (index - (count - 1) * 0.5f) * spacing;
        }

        private sealed class AttackState
        {
            public AttackState(float startDelaySeconds)
            {
                NextFireTime = Mathf.Max(0f, startDelaySeconds);
            }

            public int VolleyIndex;
            public int BulletIndex;
            public int ShotIndex;
            public float NextFireTime;
            public bool Completed;
        }
    }

    [Serializable]
    public sealed class ArsonistFireCharacterProjectileAction : BossAction
    {
        private static readonly Vector2[][] StrokePoints =
        {
            new[] { new Vector2(-0.16f, 0.28f), new Vector2(-0.48f, -0.12f) },
            new[] { new Vector2(0.16f, 0.30f), new Vector2(0.48f, -0.02f) },
            new[] { new Vector2(0.02f, 0.50f), new Vector2(-0.12f, -0.05f), new Vector2(-0.54f, -0.54f) },
            new[] { new Vector2(0.02f, -0.02f), new Vector2(0.24f, -0.28f), new Vector2(0.56f, -0.56f) }
        };

        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField] private BossGraphProjectileAimSpec aim = new();
        [SerializeField, Min(0.1f)] private float characterSize = 3f;
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, Min(0.05f)] private float strokeDuration = 0.45f;
        [SerializeField, Min(0f)] private float strokeInterval = 0.12f;
        [SerializeField] private bool orientToAim = true;
        [SerializeField] private float rotationOffsetDegrees;
        [SerializeField] private bool destroyOnStrokeEnd = true;
        [SerializeField, BossGraphSfxId] private string fireSfxId;
        [SerializeField, BossGraphSfxId] private string launchSfxId;
        [SerializeField] private BossGraphEffectSettings effects = new();

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            if (windupSeconds > 0f)
            {
                yield return context.WaitSeconds(windupSeconds);
            }

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            BossGraphProjectileAimSpec aimSpec = aim ?? new BossGraphProjectileAimSpec();
            Vector3 center = originSpec.GetAimOrigin(context, 0);
            Vector2 aimDirection = aimSpec.GetDirection(context, center);
            float rotation = rotationOffsetDegrees;
            if (orientToAim && aimDirection.sqrMagnitude > 0.0001f)
            {
                rotation += Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            }

            for (int i = 0; i < StrokePoints.Length; i++)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                    yield return null;
                    i--;
                    continue;
                }

                Vector2[] worldPoints = BuildStroke(center, StrokePoints[i], rotation);
                Vector2 direction = worldPoints.Length > 1
                    ? (worldPoints[1] - worldPoints[0]).normalized
                    : Vector2.left;
                EnemyProjectile firedProjectile = context.FireProjectile(
                    projectile,
                    worldPoints[0],
                    direction,
                    0f,
                    aimAtPlayerWhileChargingOverride: false,
                    aimAtPlayerOnLaunchOverride: false,
                    chargeSecondsOverride: 0f,
                    suppressHoming: true,
                    projectileName: projectileName);

                if (firedProjectile != null)
                {
                    firedProjectile.gameObject.AddComponent<BossPathProjectileMotion>().Initialize(
                        worldPoints,
                        strokeDuration,
                        destroyOnStrokeEnd);
                    context.PlaySfx(fireSfxId);
                    context.PlaySfxOnLaunch(firedProjectile, launchSfxId);
                    context.PlayOriginBurst(effects, worldPoints[0]);
                    context.PlayMuzzleFlashIfEnabled(effects, worldPoints[0], direction);
                    context.PlayCameraShakeIfEnabled(effects, direction);
                }

                if (strokeInterval > 0f && i < StrokePoints.Length - 1)
                {
                    yield return context.WaitSeconds(strokeInterval);
                }
            }
        }

        private Vector2[] BuildStroke(Vector2 center, Vector2[] localPoints, float rotationDegrees)
        {
            Vector2[] worldPoints = new Vector2[localPoints.Length];
            float radians = rotationDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            for (int i = 0; i < localPoints.Length; i++)
            {
                Vector2 scaled = localPoints[i] * characterSize;
                Vector2 rotated = new(scaled.x * cos - scaled.y * sin, scaled.x * sin + scaled.y * cos);
                worldPoints[i] = center + rotated;
            }

            return worldPoints;
        }
    }
}
