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

        [SerializeField, Min(0.05f)] private float ignitedOilDuration = 3.5f;
        [SerializeField, Min(0.05f)] private float oilConnectionRadius = 0.95f;
        [SerializeField, Min(0.01f)] private float oilIgnitionSpreadInterval = 0.06f;

        [SerializeField, Min(1)] private int fireDamage = 1;
        [SerializeField, Min(0.05f)] private float fireDamageInterval = 0.45f;
        [SerializeField] private List<ArsonistSprinklerProjectile> sprinklers = new();

        private readonly List<ArsonistOilPatch> oilPatches = new();
        private readonly List<ArsonistFireArea> fireAreas = new();
        private readonly List<ArsonistWaterArea> waterAreas = new();
        private readonly List<ArsonistFireAreaBatch> activeFireAreaBatches = new();
        private readonly Dictionary<PlayerCombatController, float> nextFireDamageAtByPlayer = new();
        private int oilIgnitionVersion;

        protected override bool RotatesBodyToPlayer => false;

        protected override void OnCombatStarted()
        {
            ResetSprinklersForCombat();
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
            Color oilColor)
        {
            GameObject patchObject = new("ArsonistOilPatch");
            patchObject.transform.position = FlattenPosition(position);
            CircleCollider2D collider = patchObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.05f, radius);

            ArsonistOilPatch patch = patchObject.AddComponent<ArsonistOilPatch>();
            patch.Initialize(this, Mathf.Max(0.05f, radius), Mathf.Max(0.05f, duration), oilColor);
            oilPatches.Add(patch);

            TryRemoveWaterAt(position, radius);
            TryIgniteNewOilPatchFromActiveFire(patch);
            return patch;
        }

        internal ArsonistFireArea CreateFireArea(
            Vector3 position,
            float radius,
            float duration,
            Color fireColor,
            float playerDamageDelay = 0f,
            float spreadSeconds = 0f)
        {
            GameObject fireObject = new("ArsonistFireArea");
            fireObject.transform.position = FlattenPosition(position);
            CircleCollider2D collider = fireObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.05f, radius);

            ArsonistFireArea fireArea = fireObject.AddComponent<ArsonistFireArea>();
            fireAreas.Add(fireArea);
            fireArea.Initialize(this, Mathf.Max(0.05f, radius), Mathf.Max(0.05f, duration), fireColor, playerDamageDelay, spreadSeconds);
            TrackFireAreaBatch(fireArea);
            return fireArea;
        }

        internal ArsonistWaterArea CreateWaterArea(
            Vector3 position,
            float radius,
            float duration,
            Color waterColor)
        {
            GameObject waterObject = new("ArsonistWaterArea");
            waterObject.transform.position = FlattenPosition(position);
            CircleCollider2D collider = waterObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.05f, radius);

            ArsonistWaterArea waterArea = waterObject.AddComponent<ArsonistWaterArea>();
            waterAreas.Add(waterArea);
            waterArea.Initialize(this, Mathf.Max(0.05f, radius), Mathf.Max(0.05f, duration), waterColor);
            if (HasOilOrFireAt(position, radius))
            {
                waterArea.FadeOutNow();
            }

            return waterArea;
        }

        internal ArsonistFireAreaBatch BeginFireAreaBatch()
        {
            ArsonistFireAreaBatch batch = new();
            activeFireAreaBatches.Add(batch);
            return batch;
        }

        internal void ReleaseFireAreaBatch(ArsonistFireAreaBatch batch)
        {
            if (batch == null)
            {
                return;
            }

            activeFireAreaBatches.Remove(batch);
            batch.FadeOutAll();
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

            player.ReceiveAttack(fireDamage, sourcePosition, direction.normalized);

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
            float ignitionDuration = Mathf.Max(0.05f, ignitedOilDuration);
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

                    patch.IgniteLocal(ignitionDuration, fireColor);
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
        }

        internal void UnregisterFireArea(ArsonistFireArea fireArea)
        {
            fireAreas.Remove(fireArea);
            for (int i = activeFireAreaBatches.Count - 1; i >= 0; i--)
            {
                activeFireAreaBatches[i]?.Remove(fireArea);
            }
        }

        internal void UnregisterWaterArea(ArsonistWaterArea waterArea)
        {
            waterAreas.Remove(waterArea);
        }

        internal void TryRemoveWaterAt(Vector3 position, float radius)
        {
            for (int i = waterAreas.Count - 1; i >= 0; i--)
            {
                ArsonistWaterArea waterArea = waterAreas[i];
                if (waterArea == null)
                {
                    waterAreas.RemoveAt(i);
                    continue;
                }

                if (waterArea.CanBeRemovedByHazardAt(position, radius))
                {
                    waterArea.FadeOutNow();
                }
            }
        }

        internal void SetSprinklerActive(int sprinklerIndex, bool active)
        {
            if (sprinklerIndex < 0 || sprinklerIndex >= sprinklers.Count)
            {
                return;
            }

            sprinklers[sprinklerIndex]?.SetFunctionalActive(this, active);
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

        private bool HasOilOrFireAt(Vector3 position, float radius)
        {
            for (int i = 0; i < oilPatches.Count; i++)
            {
                ArsonistOilPatch patch = oilPatches[i];
                if (patch != null && patch.OverlapsCircle(position, radius))
                {
                    return true;
                }
            }

            for (int i = 0; i < fireAreas.Count; i++)
            {
                ArsonistFireArea fireArea = fireAreas[i];
                if (fireArea != null && fireArea.CanIgniteOilAt(position, radius))
                {
                    return true;
                }
            }

            return false;
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
            DeactivateAllSprinklers();

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

            for (int i = waterAreas.Count - 1; i >= 0; i--)
            {
                if (waterAreas[i] != null)
                {
                    Destroy(waterAreas[i].gameObject);
                }
            }

            oilPatches.Clear();
            fireAreas.Clear();
            waterAreas.Clear();
            activeFireAreaBatches.Clear();
            nextFireDamageAtByPlayer.Clear();
        }

        private void ResetSprinklersForCombat()
        {
            for (int i = 0; i < sprinklers.Count; i++)
            {
                sprinklers[i]?.ResetForCombat(this);
            }
        }

        private void DeactivateAllSprinklers()
        {
            for (int i = 0; i < sprinklers.Count; i++)
            {
                sprinklers[i]?.SetFunctionalActive(this, false);
            }
        }

        private void TrackFireAreaBatch(ArsonistFireArea fireArea)
        {
            if (fireArea == null || activeFireAreaBatches.Count == 0)
            {
                return;
            }

            for (int i = 0; i < activeFireAreaBatches.Count; i++)
            {
                activeFireAreaBatches[i]?.Add(fireArea);
            }
        }

        private static Vector3 FlattenPosition(Vector3 position)
        {
            position.z = 0f;
            return position;
        }
    }

    internal sealed class ArsonistFireAreaBatch
    {
        private readonly List<ArsonistFireArea> fireAreas = new();

        public void Add(ArsonistFireArea fireArea)
        {
            if (fireArea == null || fireAreas.Contains(fireArea))
            {
                return;
            }

            fireArea.HoldUntilReleased();
            fireAreas.Add(fireArea);
        }

        public void Remove(ArsonistFireArea fireArea)
        {
            fireAreas.Remove(fireArea);
        }

        public void FadeOutAll()
        {
            for (int i = fireAreas.Count - 1; i >= 0; i--)
            {
                if (fireAreas[i] != null)
                {
                    fireAreas[i].FadeOutNow();
                }
            }

            fireAreas.Clear();
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
        private const float IndicatorLeadSeconds = 1f;

        [Serializable]
        private sealed class LeadingPlayerCircleSettings
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

            public string ProjectileName => projectileName;
            public BossProjectileSettings Projectile => projectile;
            public int BulletCount => Mathf.Max(1, bulletCount);
            public float CircleRadius => Mathf.Max(0.1f, circleRadius);
            public float AngularSpeedDegrees => angularSpeedDegrees;
            public float StartAngleOffset => startAngleOffset;
            public bool RandomizeStartAngle => randomizeStartAngle;
            public float FireInterval => Mathf.Max(0f, fireInterval);
            public float MotionDurationSeconds => Mathf.Max(0f, motionDurationSeconds);
            public bool DestroyOnMotionEnd => destroyOnMotionEnd;
            public bool WaitForMotionEnd => waitForMotionEnd;
            public float WindupSeconds => Mathf.Max(0f, windupSeconds);
            public string FireSfxId => fireSfxId;
            public string LaunchSfxId => launchSfxId;
            public BossGraphEffectSettings Effects => effects;
        }

        private static readonly Vector2[][] StrokePoints =
        {
            new[] { new Vector2(-0.16f, 0.30f), new Vector2(-0.48f, -0.02f) },
            new[] { new Vector2(0.16f, 0.30f), new Vector2(0.48f, -0.02f) },
            new[] { new Vector2(0.02f, 0.50f), new Vector2(-0.12f, -0.05f), new Vector2(-0.54f, -0.54f) },
            new[] { new Vector2(0.02f, -0.02f), new Vector2(0.24f, -0.28f), new Vector2(0.56f, -0.56f) }
        };

        [SerializeField, Min(0)] private int leadingCircleRepeatCount;
        [SerializeField] private List<LeadingPlayerCircleSettings> leadingCircles = new();
        [SerializeField, HideInInspector] private LeadingPlayerCircleSettings leadingCircle;
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings projectile = new();
        [SerializeField] private BossGraphProjectileOriginSpec origin = new();
        [SerializeField, Min(0.1f)] private float characterSize = 3f;
        [SerializeField, Min(0f)] private float windupSeconds;
        [SerializeField, Min(0.05f)] private float strokeDuration = 0.45f;
        [SerializeField, Min(0f)] private float strokeInterval = 0.12f;
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

            BossGraphProjectileOriginSpec originSpec = origin ?? new BossGraphProjectileOriginSpec();
            Vector3 aimOrigin = originSpec.GetAimOrigin(context, 0);
            Vector2 lockedCenter = context.Boss != null && context.Boss.Player != null
                ? context.Boss.Player.position
                : aimOrigin;
            ArsonistBossAI arsonist = context.Boss as ArsonistBossAI;
            ArsonistFireAreaBatch fireAreaBatch = arsonist?.BeginFireAreaBatch();
            try
            {
                yield return ExecuteLeadingPlayerCirclePatterns(context, lockedCenter);

                float rotation = rotationOffsetDegrees;
                BossProjectileSettings projectileSettings = !string.IsNullOrWhiteSpace(projectileName)
                    ? context.ResolveGraphProjectileSettings(projectileName)
                    : context.ResolveGraphProjectileSettings(null) ?? projectile;
                Vector2[][] strokeWorldPoints = BuildStrokes(lockedCenter, rotation);

                float safeWindup = Mathf.Max(0f, windupSeconds);
                float waitBeforeIndicatorSeconds = Mathf.Max(0f, safeWindup - IndicatorLeadSeconds);
                float waitAfterIndicatorSeconds = safeWindup - waitBeforeIndicatorSeconds;
                if (waitBeforeIndicatorSeconds > 0f)
                {
                    yield return context.WaitSeconds(waitBeforeIndicatorSeconds);
                }

                if (projectileSettings?.Prefab != null)
                {
                    CreateEarlyPathIndicators(strokeWorldPoints, projectileSettings.Radius, waitAfterIndicatorSeconds);
                }

                if (waitAfterIndicatorSeconds > 0f)
                {
                    yield return context.WaitSeconds(waitAfterIndicatorSeconds);
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

                    Vector2[] worldPoints = strokeWorldPoints[i];
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
                        firedProjectile.ConfigurePathIndicatorSuppressed(true);
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

                if (strokeDuration > 0f)
                {
                    yield return context.WaitSeconds(strokeDuration);
                }
            }
            finally
            {
                arsonist?.ReleaseFireAreaBatch(fireAreaBatch);
            }
        }

        private IEnumerator ExecuteLeadingPlayerCirclePatterns(BossActionContext context, Vector2 lockedCenter)
        {
            int repeatCount = Mathf.Max(0, leadingCircleRepeatCount);
            IReadOnlyList<LeadingPlayerCircleSettings> settingsList = GetLeadingCircleSettings();
            if (repeatCount <= 0 || settingsList.Count <= 0)
            {
                yield break;
            }

            if (context == null)
            {
                yield break;
            }

            for (int repeatIndex = 0; repeatIndex < repeatCount; repeatIndex++)
            {
                for (int settingsIndex = 0; settingsIndex < settingsList.Count; settingsIndex++)
                {
                    yield return ExecuteLeadingPlayerCirclePattern(context, settingsList[settingsIndex], lockedCenter);
                }
            }
        }

        private IReadOnlyList<LeadingPlayerCircleSettings> GetLeadingCircleSettings()
        {
            if (leadingCircles != null && leadingCircles.Count > 0)
            {
                return leadingCircles;
            }

            if (leadingCircle != null)
            {
                return new[] { leadingCircle };
            }

            return Array.Empty<LeadingPlayerCircleSettings>();
        }

        private IEnumerator ExecuteLeadingPlayerCirclePattern(
            BossActionContext context,
            LeadingPlayerCircleSettings settings,
            Vector2 lockedCenter)
        {
            if (context == null || settings == null)
            {
                yield break;
            }

            if (settings.WindupSeconds > 0f)
            {
                yield return context.WaitSeconds(settings.WindupSeconds);
            }

            int count = settings.BulletCount;
            float firstAngle = settings.RandomizeStartAngle
                ? UnityEngine.Random.Range(0f, 360f)
                : settings.StartAngleOffset;
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
                Vector2 tangentDirection = BossActionContext.AngleToDirection(angle
                    + Mathf.Sign(settings.AngularSpeedDegrees == 0f ? 1f : settings.AngularSpeedDegrees) * 90f);
                Vector3 spawnPosition = (Vector3)(lockedCenter + radialDirection * settings.CircleRadius);
                EnemyProjectile firedProjectile = context.FireProjectile(
                    settings.Projectile,
                    spawnPosition,
                    tangentDirection,
                    0f,
                    aimAtPlayerWhileChargingOverride: false,
                    aimAtPlayerOnLaunchOverride: false,
                    chargeSecondsOverride: 0f,
                    suppressHoming: true,
                    projectileName: settings.ProjectileName);

                if (firedProjectile != null)
                {
                    firedProjectile.gameObject.AddComponent<BossPointOrbitProjectileMotion>().Initialize(
                        lockedCenter,
                        settings.CircleRadius,
                        angle,
                        settings.AngularSpeedDegrees,
                        settings.MotionDurationSeconds,
                        settings.DestroyOnMotionEnd);
                    context.PlaySfx(settings.FireSfxId);
                    context.PlaySfxOnLaunch(firedProjectile, settings.LaunchSfxId);
                    context.PlayOriginBurst(settings.Effects, spawnPosition);
                    context.PlayMuzzleFlashIfEnabled(settings.Effects, spawnPosition, tangentDirection);
                    context.PlayCameraShakeIfEnabled(settings.Effects, tangentDirection);
                }

                if (settings.FireInterval > 0f && i < count - 1)
                {
                    yield return context.WaitSeconds(settings.FireInterval);
                }
            }

            if (settings.WaitForMotionEnd && settings.MotionDurationSeconds > 0f)
            {
                yield return context.WaitSeconds(settings.MotionDurationSeconds);
            }
        }

        private Vector2[][] BuildStrokes(Vector2 center, float rotationDegrees)
        {
            Vector2[][] worldStrokes = new Vector2[StrokePoints.Length][];
            for (int i = 0; i < StrokePoints.Length; i++)
            {
                worldStrokes[i] = BuildStroke(center, StrokePoints[i], rotationDegrees);
            }

            return worldStrokes;
        }

        private void CreateEarlyPathIndicators(
            Vector2[][] strokeWorldPoints,
            float projectileRadius,
            float firstStrokeDelaySeconds)
        {
            if (strokeWorldPoints == null)
            {
                return;
            }

            float safeFirstStrokeDelay = Mathf.Max(0f, firstStrokeDelaySeconds);
            float safeInterval = Mathf.Max(0f, strokeInterval);
            for (int i = 0; i < strokeWorldPoints.Length; i++)
            {
                GameObject indicatorObject = new("ArsonistFireCharacterPathIndicator");
                indicatorObject.AddComponent<ArsonistFireCharacterPathIndicator>().Initialize(
                    strokeWorldPoints[i],
                    safeFirstStrokeDelay + safeInterval * i,
                    strokeDuration,
                    projectileRadius,
                    true);
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

    [AddComponentMenu("")]
    internal sealed class ArsonistFireCharacterPathIndicator : MonoBehaviour
    {
        private const string IndicatorRootName = "FireCharacterPathIndicator";
        private const float DashLength = 0.2f;
        private const float DashGap = 0.14f;
        private const int MaxDashCount = 160;

        private static Material indicatorMaterial;

        private readonly List<LineRenderer> dashes = new();
        private Transform indicatorRoot;
        private Vector2[] points;
        private float[] cumulativeLengths;
        private float totalLength;
        private float durationSeconds;
        private float elapsedSeconds;
        private float lineWidth;
        private float startDelaySeconds;
        private bool destroyGameObjectOnComplete;
        private bool initialized;

        public void Initialize(Vector2[] nextPoints, float nextDurationSeconds, float projectileRadius)
        {
            Initialize(nextPoints, 0f, nextDurationSeconds, projectileRadius, false);
        }

        public void Initialize(
            Vector2[] nextPoints,
            float nextStartDelaySeconds,
            float nextDurationSeconds,
            float projectileRadius,
            bool nextDestroyGameObjectOnComplete)
        {
            if (nextPoints == null || nextPoints.Length < 2)
            {
                enabled = false;
                return;
            }

            points = new Vector2[nextPoints.Length];
            Array.Copy(nextPoints, points, nextPoints.Length);
            startDelaySeconds = Mathf.Max(0f, nextStartDelaySeconds);
            durationSeconds = Mathf.Max(0.01f, nextDurationSeconds);
            lineWidth = Mathf.Max(0.013f, projectileRadius * 0.14f);
            destroyGameObjectOnComplete = nextDestroyGameObjectOnComplete;
            BuildLengths();
            initialized = totalLength > 0.01f;
            if (!initialized)
            {
                enabled = false;
                return;
            }

            Draw(0f);
        }

        private void LateUpdate()
        {
            if (!initialized)
            {
                return;
            }

            if (PlayerCombatController.IsExecutionCinematicActive)
            {
                return;
            }

            elapsedSeconds += Time.deltaTime;
            if (elapsedSeconds < startDelaySeconds)
            {
                Draw(0f);
                return;
            }

            float activeElapsed = elapsedSeconds - startDelaySeconds;
            float travelled = totalLength * Mathf.Clamp01(activeElapsed / durationSeconds);
            Draw(travelled);

            if (activeElapsed >= durationSeconds)
            {
                SetVisible(false);
                if (destroyGameObjectOnComplete)
                {
                    Destroy(gameObject);
                    return;
                }

                enabled = false;
            }
        }

        private void BuildLengths()
        {
            cumulativeLengths = new float[points.Length];
            totalLength = 0f;
            for (int i = 1; i < points.Length; i++)
            {
                totalLength += Vector2.Distance(points[i - 1], points[i]);
                cumulativeLengths[i] = totalLength;
            }
        }

        private void Draw(float travelled)
        {
            int dashCount = Mathf.Min(MaxDashCount, Mathf.CeilToInt(totalLength / (DashLength + DashGap)));
            Color color = new(1f, 0.22f, 0.03f, 0.62f);
            for (int i = 0; i < dashCount; i++)
            {
                float segmentStart = i * (DashLength + DashGap);
                float segmentEnd = Mathf.Min(segmentStart + DashLength, totalLength);
                if (segmentEnd <= travelled)
                {
                    SetDashVisible(i, false);
                    continue;
                }

                LineRenderer dash = EnsureDash(i);
                if (dash == null)
                {
                    continue;
                }

                segmentStart = Mathf.Max(segmentStart, travelled);
                dash.enabled = true;
                dash.startColor = color;
                dash.endColor = color;
                dash.startWidth = lineWidth;
                dash.endWidth = lineWidth;
                dash.SetPosition(0, EvaluateDistance(segmentStart));
                dash.SetPosition(1, EvaluateDistance(segmentEnd));
            }

            for (int i = dashCount; i < dashes.Count; i++)
            {
                SetDashVisible(i, false);
            }
        }

        private Vector2 EvaluateDistance(float distance)
        {
            if (points == null || points.Length == 0)
            {
                return transform.position;
            }

            float clamped = Mathf.Clamp(distance, 0f, totalLength);
            for (int i = 1; i < points.Length; i++)
            {
                if (clamped > cumulativeLengths[i])
                {
                    continue;
                }

                float segmentLength = cumulativeLengths[i] - cumulativeLengths[i - 1];
                float t = segmentLength > 0.0001f
                    ? (clamped - cumulativeLengths[i - 1]) / segmentLength
                    : 1f;
                return Vector2.Lerp(points[i - 1], points[i], t);
            }

            return points[^1];
        }

        private LineRenderer EnsureDash(int index)
        {
            EnsureRoot();
            if (indicatorRoot == null)
            {
                return null;
            }

            while (dashes.Count <= index)
            {
                GameObject dashObject = new($"{IndicatorRootName}_{dashes.Count:00}");
                dashObject.transform.SetParent(indicatorRoot, false);
                LineRenderer dash = dashObject.AddComponent<LineRenderer>();
                dash.useWorldSpace = true;
                dash.loop = false;
                dash.positionCount = 2;
                dash.numCornerVertices = 0;
                dash.numCapVertices = 1;
                BossSorting.Apply(dash);
                dash.sortingOrder = 17;
                dash.material = GetIndicatorMaterial();
                dashes.Add(dash);
            }

            return dashes[index];
        }

        private void EnsureRoot()
        {
            if (indicatorRoot != null)
            {
                return;
            }

            GameObject rootObject = new(IndicatorRootName);
            rootObject.transform.SetParent(transform, false);
            rootObject.transform.localPosition = Vector3.zero;
            rootObject.transform.localRotation = Quaternion.identity;
            rootObject.transform.localScale = Vector3.one;
            indicatorRoot = rootObject.transform;
        }

        private static Material GetIndicatorMaterial()
        {
            if (indicatorMaterial != null)
            {
                return indicatorMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            indicatorMaterial = shader != null ? new Material(shader) : null;
            return indicatorMaterial;
        }

        private void SetDashVisible(int index, bool visible)
        {
            if (index >= 0 && index < dashes.Count && dashes[index] != null)
            {
                dashes[index].enabled = visible;
            }
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < dashes.Count; i++)
            {
                SetDashVisible(i, visible);
            }
        }
    }
}
