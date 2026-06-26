using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.UI;

namespace Week14.Enemy
{
    [RequireComponent(typeof(Rigidbody2D), typeof(Health), typeof(BulletGauge))]
    [RequireComponent(typeof(ExecutionTarget))]
    public sealed class Minion : MonoBehaviour
    {
        private const string DynamicStatusViewName = "MinionStatusView";

        public enum IdleMovementMode
        {
            Wander,
            KeepPlayerDistance,
            StrafeAroundPlayer,
            PingPong
        }

        private static readonly List<Minion> ActiveMinions = new();

        [Header("Owner")]
        [SerializeField] private MonoBehaviour owner;

        [Header("Bullet")]
        [SerializeField, Min(1)] private int maxBullets = 12;
        [SerializeField, Min(0f)] private float bulletEmptyExecutionSeconds = 1.4f;

        [Header("Color")]
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color bulletEmptyColor = new(0.45f, 0.65f, 1f, 1f);
        [SerializeField] private Color staggeredColor = new(1f, 0.95f, 0.35f, 1f);
        [SerializeField] private Color bodyHitColor = new(1f, 0.35f, 0.25f, 1f);
        [SerializeField, Min(0f)] private float bodyHitColorSeconds = 0.08f;
        [SerializeField] private Color bulletBarColor = new(1f, 0.55f, 0.1f, 1f);
        [SerializeField] private Color emptyBulletBarColor = Color.red;
        [SerializeField] private Color lockOnIndicatorColor = Color.white;
        [SerializeField] private Color executionIndicatorColor = Color.red;

        [Header("Status UI")]
        [SerializeField] private Vector2 statusBulletBarSize = new(1.88f, 0.32f);

        [Header("Effect")]
        [SerializeField] private CombatEffectData effectData;
        [SerializeField, Min(0f)] private float staggerSeconds = 0.18f;
        [SerializeField, Min(0f)] private float staggerShakeDistance = 0.06f;
        [SerializeField, Min(0f)] private float staggerShakeFrequency = 32f;

        [Header("Movement")]
        [SerializeField] private IdleMovementMode idleMovement = IdleMovementMode.Wander;
        [SerializeField, Min(0f)] private float moveSpeed = 3.2f;
        [SerializeField, Min(0f)] private float patternCatchUpSpeed = 24f;
        [SerializeField, Min(0f)] private float keepPlayerDistance = 3.5f;
        [SerializeField, Min(0f)] private float keepDistanceTolerance = 0.35f;
        [SerializeField, Min(0f)] private float wanderRadius = 2.8f;
        [SerializeField, Min(0.1f)] private float wanderRetargetSeconds = 1.5f;
        [SerializeField, Min(0f)] private float strafeAngularSpeedDegrees = 85f;
        [SerializeField, Min(0.1f)] private float pingPongSeconds = 2f;

        [Header("Scene References")]
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private EnemyStatusView statusView;
        [SerializeField] private SpriteRenderer lockOnIndicator;
        [SerializeField] private SpriteRenderer executionIndicator;

        private readonly List<EnemyProjectile> activeProjectiles = new();
        private Coroutine commandRoutine;
        private Coroutine summonRoutine;
        private Health health;
        private BulletGauge bullets;
        private ExecutionTarget executionTarget;
        private SpriteRenderer[] renderers;
        private Collider2D[] colliders;
        private bool[] authoredColliderStates;
        private Vector2 spawnPosition;
        private Vector2 wanderTarget;
        private Vector3 authoredScale = Vector3.one;
        private float nextWanderRetargetAt;
        private float strafeAngle;
        private bool isBulletEmpty;
        private float bulletEmptyEndsAt;
        private bool isBodyHitColorActive;
        private float bodyHitColorEndsAt;
        private bool isStaggered;
        private float staggerEndsAt;
        private Vector3 staggerBaseLocalPosition;
        private bool isSummoning;
        private bool destroyAfterDeathQueued;
        private bool ownsStatusView;
        private bool isExecutionLocked;
        private bool suppressBodyContactDamage;
        private IMinionOwner runtimeOwner;

        public IMinionOwner Owner => ResolveOwner();
        public Health Health => health;
        public BulletGauge Bullets => bullets;
        public bool IsCommanded => commandRoutine != null;
        public bool SuppressesBodyContactDamage => suppressBodyContactDamage;
        public bool IsBulletEmpty => isBulletEmpty || (bullets != null && bullets.IsEmpty);
        public bool IsExecutionLocked => isExecutionLocked;
        public Color BulletBarColor => bulletBarColor;
        public Color EmptyBulletBarColor => emptyBulletBarColor;
        public Color LockOnIndicatorColor => lockOnIndicatorColor;
        public Color ExecutionIndicatorColor => executionIndicatorColor;
        public Vector2 StatusBulletBarSize => new(Mathf.Max(0.01f, statusBulletBarSize.x), Mathf.Max(0.01f, statusBulletBarSize.y));
        public static IReadOnlyList<Minion> All => ActiveMinions;

        private void Awake()
        {
            health = GetComponent<Health>();
            bullets = GetComponent<BulletGauge>();
            executionTarget = GetComponent<ExecutionTarget>();
            if (executionTarget == null)
            {
                executionTarget = gameObject.AddComponent<ExecutionTarget>();
            }

            body ??= GetComponent<Rigidbody2D>();
            bodyRoot ??= FindChild("Visual") ?? transform;
            projectileOrigin ??= transform;
            statusView ??= GetComponentInChildren<EnemyStatusView>();
            lockOnIndicator ??= FindChild("LockOnIndicator")?.GetComponent<SpriteRenderer>();
            executionIndicator ??= FindChild("ExecutionIndicator")?.GetComponent<SpriteRenderer>();
            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            colliders = GetComponentsInChildren<Collider2D>(true);
            authoredColliderStates = new bool[colliders.Length];
            for (int i = 0; i < colliders.Length; i++)
            {
                authoredColliderStates[i] = colliders[i] != null && colliders[i].enabled;
            }
            spawnPosition = transform.position;
            authoredScale = transform.localScale;
            if (body != null)
            {
                body.gravityScale = 0f;
                body.freezeRotation = true;
            }
        }

        private void OnEnable()
        {
            if (!ActiveMinions.Contains(this))
            {
                ActiveMinions.Add(this);
            }

            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            ActiveMinions.Remove(this);
            if (health != null)
            {
                health.Died -= HandleDied;
            }

            if (ownsStatusView && statusView != null)
            {
                Destroy(statusView.gameObject);
                statusView = null;
                ownsStatusView = false;
            }
        }

        private void Start()
        {
            bullets.Configure(maxBullets, true);
            EnsureStatusView();
            ApplyBodyStateColor();
        }

        private void Update()
        {
            UpdateBodyHitColor();
            UpdateStagger();

            if (health != null && health.IsDead)
            {
                StopBody();
                return;
            }

            if (isExecutionLocked)
            {
                StopBody();
                return;
            }

            if (IsExecutionPaused)
            {
                StopBody();
                return;
            }

            if (isSummoning)
            {
                StopBody();
                FaceMovementDirection();
                return;
            }

            if (IsBulletEmpty)
            {
                TickBulletEmpty();
                return;
            }

            if (commandRoutine != null)
            {
                FaceMovementDirection();
                return;
            }

            TickIdleMovement();
            FaceMovementDirection();
        }

        private IMinionOwner ResolveOwner()
        {
            if (runtimeOwner is UnityEngine.Object runtimeObject && runtimeObject == null)
            {
                runtimeOwner = null;
            }

            if (runtimeOwner != null)
            {
                return runtimeOwner;
            }

            if (owner == null)
            {
                return null;
            }

            runtimeOwner = owner as IMinionOwner;
            if (runtimeOwner == null)
            {
                owner = null;
            }

            return runtimeOwner;
        }

        public void SetOwner(IMinionOwner nextOwner)
        {
            runtimeOwner = nextOwner;
            owner = nextOwner as MonoBehaviour;
        }

        public float BeginSummonIntro(Vector3 startPosition, Vector3 targetPosition, float duration, float startScale)
        {
            StopCommand();
            if (summonRoutine != null)
            {
                StopCoroutine(summonRoutine);
            }

            float safeDuration = Mathf.Max(0f, duration);
            summonRoutine = StartCoroutine(RunSummonIntro(startPosition, targetPosition, safeDuration, startScale));
            return safeDuration;
        }

        public bool CanSpawnEnemyProjectile()
        {
            if (IsExecutionPaused || isSummoning || health == null || health.IsDead)
            {
                return false;
            }

            return true;
        }

        public void RegisterActiveProjectile(EnemyProjectile projectile)
        {
            PruneInactiveProjectiles();
            if (projectile == null || activeProjectiles.Contains(projectile))
            {
                return;
            }

            activeProjectiles.Add(projectile);
        }

        public void UnregisterActiveProjectile(EnemyProjectile projectile)
        {
            if (projectile != null)
            {
                activeProjectiles.Remove(projectile);
            }
        }

        public bool ReceivePlayerHit(int bulletDamage, bool strongHit, Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
        {
            if (health == null || health.IsDead || isSummoning)
            {
                return false;
            }

            if (IsBulletEmpty)
            {
                health.Kill();
                QueueDestroyAfterDeath();
                return true;
            }

            bullets.TrySpend(bulletDamage, BulletChangeSource.Hit);
            if (bullets.IsEmpty)
            {
                BeginBulletEmpty();
            }

            FlashBodyHitColor();
            if (strongHit)
            {
                BeginStagger();
            }

            ProjectileVfx.PlayPlayerAttackImpact(
                hitPosition,
                hitDirection,
                GetAttackImpactSparkColor(hitColor),
                GetAttackImpactBackSparkColor(hitColor),
                GetAttackImpactFlameColor(hitColor),
                GetAttackImpactRingColor(hitColor),
                effectData != null ? effectData.AttackImpactSparkCount : 14,
                effectData != null ? effectData.AttackImpactBackSparkCount : 6,
                effectData != null ? effectData.AttackImpactFlameCount : 8,
                effectData != null ? effectData.AttackImpactEffectScale : 0.65f);
            PlayEnemyHitCameraImpact(hitDirection);
            return true;
        }

        public void PlayExecutionHitReaction(Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
        {
            if (health == null || health.IsDead)
            {
                return;
            }

            FlashBodyHitColor();
            ProjectileVfx.PlayPlayerAttackImpact(
                hitPosition,
                hitDirection,
                GetAttackImpactSparkColor(hitColor),
                GetAttackImpactBackSparkColor(hitColor),
                GetAttackImpactFlameColor(hitColor),
                GetAttackImpactRingColor(hitColor),
                effectData != null ? effectData.AttackImpactSparkCount : 14,
                effectData != null ? effectData.AttackImpactBackSparkCount : 6,
                effectData != null ? effectData.AttackImpactFlameCount : 8,
                effectData != null ? effectData.AttackImpactEffectScale : 0.65f);
            PlayEnemyHitCameraImpact(hitDirection);
        }

        public void SetExecutionLocked(bool locked)
        {
            isExecutionLocked = locked;
            if (locked)
            {
                StopCommand();
                StopBody();
            }

            ApplyBodyStateColor();
        }

        public void ClearOwner(IMinionOwner expectedOwner)
        {
            if (ResolveOwner() == expectedOwner)
            {
                runtimeOwner = null;
                owner = null;
            }
        }

        public void ResumeIdle()
        {
            StopCommand();
        }

        public void StopCommand()
        {
            if (commandRoutine != null)
            {
                StopCoroutine(commandRoutine);
                commandRoutine = null;
            }

            suppressBodyContactDamage = false;
            StopBody();
        }

        public void FireOnceAtPlayer(BossProjectileSettings projectile)
        {
            FireOnce(projectile, default, 0);
        }

        public void FireOnce(BossProjectileSettings projectile, MinionGraphProjectileFireSpec fireSpec, int shotIndex)
        {
            if (Owner == null || projectile == null)
            {
                return;
            }

            Vector3 aimOrigin = fireSpec.GetAimOrigin(this, shotIndex);
            Vector2 direction = fireSpec.GetDirection(this, aimOrigin);
            Vector3 spawnOrigin = fireSpec.GetSpawnOrigin(this, shotIndex, direction);
            Vector2 finalDirection = fireSpec.GetDirection(this, spawnOrigin);
            FireCommandProjectile(projectile, spawnOrigin, finalDirection, !fireSpec.HasEffects, fireSpec);
        }

        public float CommandStopAndFire(BossProjectileSettings projectile, int bulletCount, float fireInterval, bool resumeIdle)
        {
            return CommandStopAndFire(projectile, bulletCount, fireInterval, resumeIdle, default);
        }

        public float CommandStopAndFire(
            BossProjectileSettings projectile,
            int bulletCount,
            float fireInterval,
            bool resumeIdle,
            MinionGraphProjectileFireSpec fireSpec)
        {
            StopCommand();
            float duration = GetSequentialFireDuration(bulletCount, fireInterval);
            commandRoutine = StartCoroutine(RunStopAndFire(projectile, bulletCount, fireInterval, resumeIdle, fireSpec));
            return duration;
        }

        public float CommandOrbitFire(
            BossProjectileSettings projectile,
            float orbitRadius,
            float orbitSeconds,
            float fireAngleStepDegrees,
            bool clockwise)
        {
            return CommandOrbitFire(projectile, orbitRadius, orbitSeconds, fireAngleStepDegrees, clockwise, default);
        }

        public float CommandOrbitFire(
            BossProjectileSettings projectile,
            float orbitRadius,
            float orbitSeconds,
            float fireAngleStepDegrees,
            bool clockwise,
            MinionGraphProjectileFireSpec fireSpec)
        {
            StopCommand();
            float duration = Mathf.Max(0.1f, orbitSeconds);
            commandRoutine = StartCoroutine(RunOrbitFire(projectile, orbitRadius, duration, fireAngleStepDegrees, clockwise, fireSpec));
            return duration;
        }

        public float CommandRadialBurst(
            BossProjectileSettings projectile,
            int volleyCount,
            int directionCount,
            float volleyInterval,
            float spreadDegrees,
            bool resumeIdle)
        {
            return CommandRadialBurst(projectile, volleyCount, directionCount, volleyInterval, spreadDegrees, resumeIdle, default);
        }

        public float CommandRadialBurst(
            BossProjectileSettings projectile,
            int volleyCount,
            int directionCount,
            float volleyInterval,
            float spreadDegrees,
            bool resumeIdle,
            MinionGraphProjectileFireSpec fireSpec)
        {
            StopCommand();
            float duration = GetSequentialFireDuration(volleyCount, volleyInterval);
            commandRoutine = StartCoroutine(RunRadialBurst(projectile, volleyCount, directionCount, volleyInterval, spreadDegrees, resumeIdle, fireSpec));
            return duration;
        }

        public float CommandChargeSideFire(
            BossProjectileSettings projectile,
            float chargeSeconds,
            float chargeSpeed,
            float aimOffsetDegrees,
            float sideFireInterval,
            float sideFireAngleDegrees)
        {
            return CommandChargeSideFire(projectile, chargeSeconds, chargeSpeed, aimOffsetDegrees, sideFireInterval, sideFireAngleDegrees, default);
        }

        public float CommandChargeSideFire(
            BossProjectileSettings projectile,
            float chargeSeconds,
            float chargeSpeed,
            float aimOffsetDegrees,
            float sideFireInterval,
            float sideFireAngleDegrees,
            MinionGraphProjectileFireSpec fireSpec)
        {
            StopCommand();
            float duration = Mathf.Max(0.05f, chargeSeconds);
            commandRoutine = StartCoroutine(RunChargeSideFire(projectile, duration, chargeSpeed, aimOffsetDegrees, sideFireInterval, sideFireAngleDegrees, fireSpec));
            return duration;
        }

        public void CommandFormation(float angleOffsetDegrees, float radius, float speedMultiplier)
        {
            StopCommand();
            commandRoutine = StartCoroutine(RunFormation(angleOffsetDegrees, radius, speedMultiplier));
        }

        private IEnumerator RunStopAndFire(
            BossProjectileSettings projectile,
            int bulletCount,
            float fireInterval,
            bool resumeIdle,
            MinionGraphProjectileFireSpec fireSpec)
        {
            StopBody();
            int count = Mathf.Max(0, bulletCount);
            for (int i = 0; i < count; i++)
            {
                yield return WaitWhileExecutionPaused();
                StopBody();
                FireOnce(projectile, fireSpec, i);
                if (i < count - 1)
                {
                    yield return WaitCommandSeconds(fireInterval);
                }
            }

            FinishCommand(resumeIdle);
        }

        private IEnumerator RunSummonIntro(Vector3 startPosition, Vector3 targetPosition, float duration, float startScale)
        {
            isSummoning = true;
            SetCollidersEnabled(false);
            StopBody();

            transform.position = startPosition;
            transform.localScale = authoredScale * Mathf.Max(0f, startScale);
            RotateToDirection(targetPosition - startPosition);

            if (duration <= 0f)
            {
                FinishSummonIntro(targetPosition);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (IsExecutionPaused)
                {
                    StopBody();
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / duration);
                float eased = t * t * (3f - 2f * t);
                transform.position = Vector3.Lerp(startPosition, targetPosition, eased);
                transform.localScale = Vector3.Lerp(authoredScale * Mathf.Max(0f, startScale), authoredScale, eased);
                RotateToDirection(targetPosition - startPosition);
                StopBody();
                elapsed += Time.deltaTime;
                yield return null;
            }

            FinishSummonIntro(targetPosition);
        }

        private IEnumerator RunOrbitFire(
            BossProjectileSettings projectile,
            float orbitRadius,
            float orbitSeconds,
            float fireAngleStepDegrees,
            bool clockwise,
            MinionGraphProjectileFireSpec fireSpec)
        {
            Transform player = GetPlayer();
            if (player == null)
            {
                FinishCommand(true);
                yield break;
            }

            float radius = Mathf.Max(0.1f, orbitRadius);
            float duration = Mathf.Max(0.1f, orbitSeconds);
            float signedSpeed = 360f / duration * (clockwise ? -1f : 1f);
            float angle = GetAngleFromPlayer(player);
            float travelled = 0f;
            float nextFireAt = 0f;
            float step = Mathf.Max(1f, fireAngleStepDegrees);
            bool lockedToPattern = false;

            while (travelled < 360f)
            {
                if (IsExecutionPaused)
                {
                    StopBody();
                    yield return null;
                    continue;
                }

                if (player == null)
                {
                    break;
                }

                if (travelled >= nextFireAt)
                {
                    FireOnce(projectile, fireSpec, Mathf.RoundToInt(nextFireAt / step));
                    nextFireAt += step;
                }

                angle += signedSpeed * Time.deltaTime;
                travelled += Mathf.Abs(signedSpeed * Time.deltaTime);
                Vector2 target = (Vector2)player.position + AngleToDirection(angle) * radius;
                SetPatternPosition(target, ref lockedToPattern);
                yield return null;
            }

            StopBody();
            FinishCommand(true);
        }

        private IEnumerator RunRadialBurst(
            BossProjectileSettings projectile,
            int volleyCount,
            int directionCount,
            float volleyInterval,
            float spreadDegrees,
            bool resumeIdle,
            MinionGraphProjectileFireSpec fireSpec)
        {
            StopBody();
            int volleys = Mathf.Max(1, volleyCount);
            int directions = Mathf.Max(1, directionCount);
            for (int volley = 0; volley < volleys; volley++)
            {
                Vector3 aimOrigin = fireSpec.GetAimOrigin(this, volley);
                Vector2 playerDirection = fireSpec.GetDirection(this, aimOrigin);
                Vector3 origin = fireSpec.GetSpawnOrigin(this, volley, playerDirection);
                float centerAngle = DirectionToAngle(playerDirection);
                float arc = spreadDegrees <= 0f ? 360f : Mathf.Min(360f, spreadDegrees);
                float step = directions <= 1 ? 0f : arc / (directions - 1);
                float start = directions <= 1 ? centerAngle : centerAngle - arc * 0.5f;

                for (int i = 0; i < directions; i++)
                {
                    FireCommandProjectile(
                        projectile,
                        origin,
                        AngleToDirection(start + step * i),
                        i == 0 && !fireSpec.HasEffects,
                        fireSpec);
                }

                if (volley < volleys - 1)
                {
                    yield return WaitCommandSeconds(volleyInterval);
                }
            }

            FinishCommand(resumeIdle);
        }

        private IEnumerator RunChargeSideFire(
            BossProjectileSettings projectile,
            float chargeSeconds,
            float chargeSpeed,
            float aimOffsetDegrees,
            float sideFireInterval,
            float sideFireAngleDegrees,
            MinionGraphProjectileFireSpec fireSpec)
        {
            Vector3 aimOrigin = fireSpec.GetAimOrigin(this, 0);
            Vector2 direction = RotateDirection(fireSpec.GetDirection(this, aimOrigin), aimOffsetDegrees);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.left;
            }

            float elapsed = 0f;
            float nextFireAt = 0f;
            float interval = Mathf.Max(0.01f, sideFireInterval);
            float sideAngle = Mathf.Max(1f, sideFireAngleDegrees);
            while (elapsed < chargeSeconds)
            {
                if (IsExecutionPaused)
                {
                    StopBody();
                    yield return null;
                    continue;
                }

                SetVelocity(direction.normalized * Mathf.Max(0f, chargeSpeed));
                if (elapsed >= nextFireAt)
                {
                    int shotIndex = Mathf.RoundToInt(nextFireAt / interval);
                    Vector3 origin = fireSpec.GetSpawnOrigin(this, shotIndex, direction);
                    FireCommandProjectile(projectile, origin, RotateDirection(direction, sideAngle), !fireSpec.HasEffects, fireSpec);
                    FireCommandProjectile(projectile, origin, RotateDirection(direction, -sideAngle), false, fireSpec);

                    nextFireAt += interval;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            StopBody();
            FinishCommand(true);
        }

        private IEnumerator RunFormation(float angleOffsetDegrees, float radius, float speedMultiplier)
        {
            bool lockedToPattern = false;
            while (true)
            {
                IMinionOwner currentOwner = Owner;
                Transform player = currentOwner?.MinionTarget;
                if (currentOwner == null || player == null)
                {
                    break;
                }

                if (IsExecutionPaused)
                {
                    StopBody();
                    yield return null;
                    continue;
                }

                Transform ownerTransform = currentOwner.MinionOwnerTransform;
                Vector2 bossToPlayer = ownerTransform != null
                    ? (Vector2)(player.position - ownerTransform.position)
                    : Vector2.right;
                Vector2 baseDirection = bossToPlayer.sqrMagnitude > 0.0001f ? bossToPlayer.normalized : Vector2.right;
                Vector2 targetDirection = RotateDirection(baseDirection, angleOffsetDegrees);
                Vector2 target = (Vector2)player.position + targetDirection * Mathf.Max(0.1f, radius);
                SetPatternPosition(target, ref lockedToPattern);
                yield return null;
            }

            FinishCommand(true);
        }

        private void TickIdleMovement()
        {
            Transform player = GetPlayer();
            switch (idleMovement)
            {
                case IdleMovementMode.KeepPlayerDistance:
                    TickKeepPlayerDistance(player);
                    break;
                case IdleMovementMode.StrafeAroundPlayer:
                    TickStrafeAroundPlayer(player);
                    break;
                case IdleMovementMode.PingPong:
                    TickPingPong(player);
                    break;
                default:
                    TickWander();
                    break;
            }
        }

        private void TickWander()
        {
            if (Time.time >= nextWanderRetargetAt || Vector2.Distance(transform.position, wanderTarget) < 0.2f)
            {
                wanderTarget = spawnPosition + Random.insideUnitCircle * Mathf.Max(0.1f, wanderRadius);
                nextWanderRetargetAt = Time.time + Mathf.Max(0.1f, wanderRetargetSeconds);
            }

            MoveTo(wanderTarget, moveSpeed);
        }

        private void TickKeepPlayerDistance(Transform player)
        {
            if (player == null)
            {
                TickWander();
                return;
            }

            Vector2 offset = (Vector2)transform.position - (Vector2)player.position;
            float distance = offset.magnitude;
            if (Mathf.Abs(distance - keepPlayerDistance) <= keepDistanceTolerance)
            {
                StopBody();
                return;
            }

            Vector2 direction = distance > keepPlayerDistance
                ? -offset.normalized
                : offset.sqrMagnitude > 0.0001f ? offset.normalized : Random.insideUnitCircle.normalized;
            SetVelocity(direction * moveSpeed);
        }

        private void TickStrafeAroundPlayer(Transform player)
        {
            if (player == null)
            {
                TickWander();
                return;
            }

            strafeAngle += strafeAngularSpeedDegrees * Time.deltaTime;
            Vector2 target = (Vector2)player.position + AngleToDirection(strafeAngle) * Mathf.Max(0.1f, keepPlayerDistance);
            MoveTo(target, moveSpeed);
        }

        private void TickPingPong(Transform player)
        {
            Vector2 forward = player != null
                ? (Vector2)player.position - (Vector2)transform.position
                : Vector2.right;
            if (forward.sqrMagnitude <= 0.0001f)
            {
                forward = Vector2.right;
            }

            Vector2 side = new(-forward.normalized.y, forward.normalized.x);
            float phase = Mathf.Sin(Time.time / Mathf.Max(0.1f, pingPongSeconds) * Mathf.PI * 2f);
            Vector2 target = spawnPosition + side * phase * Mathf.Max(0.1f, wanderRadius);
            MoveTo(target, moveSpeed);
        }

        private void MoveTo(Vector2 target, float speed)
        {
            Vector2 delta = target - (Vector2)transform.position;
            if (delta.sqrMagnitude <= 0.01f)
            {
                StopBody();
                return;
            }

            SetVelocity(delta.normalized * Mathf.Max(0f, speed));
        }

        private void SetVelocity(Vector2 velocity)
        {
            if (body != null)
            {
                body.linearVelocity = velocity;
            }
        }

        private void SetPatternPosition(Vector2 target)
        {
            SetPatternPosition(target, false);
        }

        private void SetPatternPosition(Vector2 target, bool suppressContactDamage)
        {
            Vector2 current = transform.position;
            Vector2 delta = target - current;
            suppressBodyContactDamage = suppressContactDamage;
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.position = target;
            }

            transform.position = new Vector3(target.x, target.y, transform.position.z);
            RotateToDirection(delta);
        }

        private void SetPatternPosition(Vector2 target, ref bool lockedToPattern)
        {
            if (lockedToPattern)
            {
                SetPatternPosition(target);
                return;
            }

            Vector2 current = transform.position;
            float maxDistance = patternCatchUpSpeed * Time.deltaTime;
            if (maxDistance <= 0f)
            {
                lockedToPattern = true;
                SetPatternPosition(target, true);
                return;
            }

            Vector2 next = Vector2.MoveTowards(current, target, maxDistance);
            if (Vector2.Distance(next, target) <= 0.03f)
            {
                lockedToPattern = true;
                next = target;
            }

            SetPatternPosition(next, true);
        }

        private void StopBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void TickBulletEmpty()
        {
            if (!isBulletEmpty)
            {
                BeginBulletEmpty();
            }

            StopBody();
            if (Time.time >= bulletEmptyEndsAt)
            {
                RecoverFromBulletEmpty();
            }
        }

        private void BeginBulletEmpty()
        {
            if (isBulletEmpty || health == null || health.IsDead)
            {
                return;
            }

            isBulletEmpty = true;
            bulletEmptyEndsAt = Time.time + Mathf.Max(0f, bulletEmptyExecutionSeconds);
            StopCommand();
            ApplyBodyStateColor();
        }

        private void RecoverFromBulletEmpty()
        {
            if (health == null || health.IsDead || bullets == null)
            {
                return;
            }

            isBulletEmpty = false;
            bulletEmptyEndsAt = 0f;
            bullets.Restore(Mathf.Max(1, (bullets.MaxBullets + 2) / 3), BulletChangeSource.Generic);
            ApplyBodyStateColor();
        }

        private void FlashBodyHitColor()
        {
            isBodyHitColorActive = true;
            bodyHitColorEndsAt = Time.time + Mathf.Max(0f, GetBodyHitColorSeconds());
            ApplyBodyStateColor();
        }

        private void UpdateBodyHitColor()
        {
            if (!isBodyHitColorActive || Time.time < bodyHitColorEndsAt)
            {
                return;
            }

            isBodyHitColorActive = false;
            ApplyBodyStateColor();
        }

        private void BeginStagger()
        {
            if (staggerSeconds <= 0f || isExecutionLocked || IsBulletEmpty)
            {
                return;
            }

            if (!isStaggered)
            {
                staggerBaseLocalPosition = bodyRoot != null ? bodyRoot.localPosition : Vector3.zero;
            }

            isStaggered = true;
            staggerEndsAt = Time.time + staggerSeconds;
            ApplyBodyStateColor();
        }

        private void UpdateStagger()
        {
            if (!isStaggered)
            {
                return;
            }

            if (Time.time >= staggerEndsAt)
            {
                isStaggered = false;
                if (bodyRoot != null && bodyRoot != transform)
                {
                    bodyRoot.localPosition = staggerBaseLocalPosition;
                }

                ApplyBodyStateColor();
                return;
            }

            if (bodyRoot == null || bodyRoot == transform || staggerShakeDistance <= 0f || staggerShakeFrequency <= 0f)
            {
                return;
            }

            float sign = Mathf.Sin(Time.time * staggerShakeFrequency * Mathf.PI * 2f) >= 0f ? 1f : -1f;
            bodyRoot.localPosition = staggerBaseLocalPosition + Vector3.right * (staggerShakeDistance * sign);
        }

        private void ApplyBodyStateColor()
        {
            if (renderers == null)
            {
                return;
            }

            Color color = normalColor;
            if (isStaggered)
            {
                color = staggeredColor;
            }
            else if (isBodyHitColorActive)
            {
                color = GetBodyHitColor();
            }
            else if (isExecutionLocked || IsBulletEmpty)
            {
                color = bulletEmptyColor;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || (statusView != null && statusView.OwnsRenderer(renderer)))
                {
                    continue;
                }

                renderer.color = color;
            }
        }

        private Color GetAttackImpactSparkColor(Color hitColor)
        {
            return effectData != null ? effectData.AttackImpactSparkColor : Color.Lerp(hitColor, Color.white, 0.35f);
        }

        private Color GetAttackImpactBackSparkColor(Color hitColor)
        {
            return effectData != null ? effectData.AttackImpactBackSparkColor : Color.Lerp(hitColor, new Color(1f, 0.72f, 0.12f, 1f), 0.55f);
        }

        private Color GetAttackImpactFlameColor(Color hitColor)
        {
            return effectData != null ? effectData.AttackImpactFlameColor : GetAttackImpactBackSparkColor(hitColor);
        }

        private Color GetAttackImpactRingColor(Color hitColor)
        {
            return effectData != null ? effectData.AttackImpactRingColor : Color.Lerp(hitColor, Color.white, 0.35f);
        }

        private Color GetBodyHitColor()
        {
            return effectData != null ? effectData.EnemyBodyHitColor : bodyHitColor;
        }

        private float GetBodyHitColorSeconds()
        {
            return effectData != null ? effectData.BodyHitColorSeconds : bodyHitColorSeconds;
        }

        private static void PlayEnemyHitCameraImpact(Vector2 direction)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            CameraFollow2D cameraFollow = mainCamera.GetComponent<CameraFollow2D>();
            cameraFollow?.PlayImpact(direction, 0.08f, 0.12f, 0.05f);
        }

        private void EnsureStatusView()
        {
            if (statusView != null && statusView.transform.IsChildOf(transform))
            {
                statusView.gameObject.SetActive(false);
                statusView = null;
            }

            if (statusView == null)
            {
                GameObject viewObject = new(DynamicStatusViewName);
                viewObject.SetActive(false);
                viewObject.transform.SetParent(transform.parent, false);
                viewObject.transform.position = transform.position;
                statusView = viewObject.AddComponent<EnemyStatusView>();
                ownsStatusView = true;
            }

            statusView.transform.position = transform.position;
            statusView.SetWorldTarget(transform);
            statusView.SetSuppressed(false);
            statusView.SetIndicators(lockOnIndicator, executionIndicator);
            statusView.Configure(this);
            statusView.SetTarget(health);
            statusView.gameObject.SetActive(true);
        }

        private void FinishSummonIntro(Vector3 targetPosition)
        {
            transform.position = targetPosition;
            transform.localScale = authoredScale;
            spawnPosition = targetPosition;
            SetCollidersEnabled(true);
            isSummoning = false;
            summonRoutine = null;
            StopBody();
        }

        private void SetCollidersEnabled(bool enabled)
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = enabled && (authoredColliderStates == null || i >= authoredColliderStates.Length || authoredColliderStates[i]);
                }
            }
        }

        private void HandleDied(Health _)
        {
            DestroyActiveProjectiles();
            StopCommand();
            QueueDestroyAfterDeath();
        }

        private EnemyProjectile FireCommandProjectile(
            BossProjectileSettings projectile,
            Vector3 origin,
            Vector2 direction,
            bool playMuzzleFlash)
        {
            return FireCommandProjectile(projectile, origin, direction, playMuzzleFlash, default);
        }

        private EnemyProjectile FireCommandProjectile(
            BossProjectileSettings projectile,
            Vector3 origin,
            Vector2 direction,
            bool playMuzzleFlash,
            MinionGraphProjectileFireSpec fireSpec)
        {
            IMinionOwner currentOwner = Owner;
            if (IsExecutionPaused || currentOwner == null)
            {
                return null;
            }

            EnemyProjectile firedProjectile = currentOwner.FireMinionProjectile(this, projectile, origin, direction, playMuzzleFlash);
            if (firedProjectile != null)
            {
                fireSpec.PlayEffects(origin, direction);
            }

            return firedProjectile;
        }

        private static bool IsExecutionPaused => PlayerCombatController.IsExecutionCinematicActive;

        private IEnumerator WaitCommandSeconds(float seconds)
        {
            float remaining = Mathf.Max(0f, seconds);
            while (remaining > 0f)
            {
                if (IsExecutionPaused)
                {
                    StopBody();
                    yield return null;
                    continue;
                }

                remaining -= Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitWhileExecutionPaused()
        {
            while (IsExecutionPaused)
            {
                StopBody();
                yield return null;
            }
        }

        private void DestroyActiveProjectiles()
        {
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (activeProjectiles[i] != null)
                {
                    activeProjectiles[i].DestroyFromOwner();
                }
            }

            activeProjectiles.Clear();
        }

        private void PruneInactiveProjectiles()
        {
            for (int i = activeProjectiles.Count - 1; i >= 0; i--)
            {
                if (activeProjectiles[i] == null)
                {
                    activeProjectiles.RemoveAt(i);
                }
            }
        }

        private void QueueDestroyAfterDeath()
        {
            if (destroyAfterDeathQueued)
            {
                return;
            }

            destroyAfterDeathQueued = true;
            Destroy(gameObject);
        }

        private void FaceMovementDirection()
        {
            Vector2 direction = body != null ? body.linearVelocity : Vector2.zero;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            RotateToDirection(direction);
        }

        private void RotateToDirection(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            transform.right = direction.normalized;
        }

        private Vector3 GetProjectileOrigin()
        {
            return projectileOrigin != null ? projectileOrigin.position : transform.position;
        }

        internal Vector3 GetGraphProjectileOrigin()
        {
            return GetProjectileOrigin();
        }

        internal Vector3 GetGraphChildPosition(string childPath)
        {
            Transform child = FindChildPathOrName(childPath);
            return child != null ? child.position : GetProjectileOrigin();
        }

        internal Vector2 GetGraphDirectionToPlayer(Vector3 origin)
        {
            return GetDirectionToPlayer(origin);
        }

        private Transform GetPlayer()
        {
            IMinionOwner currentOwner = Owner;
            if (currentOwner?.MinionTarget != null)
            {
                return currentOwner.MinionTarget;
            }

            return PlayerCombatController.Active != null ? PlayerCombatController.Active.transform : null;
        }

        private Vector2 GetDirectionToPlayer(Vector3 origin)
        {
            Transform player = GetPlayer();
            if (player == null)
            {
                return transform.right.sqrMagnitude > 0.0001f ? transform.right : Vector2.left;
            }

            Vector2 direction = (Vector2)player.position - (Vector2)origin;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.left;
        }

        private float GetAngleFromPlayer(Transform player)
        {
            Vector2 offset = (Vector2)transform.position - (Vector2)player.position;
            if (offset.sqrMagnitude <= 0.0001f)
            {
                return Random.Range(0f, 360f);
            }

            return DirectionToAngle(offset);
        }

        private void FinishCommand(bool resumeIdle)
        {
            commandRoutine = null;
            suppressBodyContactDamage = false;
            if (!resumeIdle)
            {
                StopBody();
            }
        }

        private static float GetSequentialFireDuration(int count, float interval)
        {
            int safeCount = Mathf.Max(0, count);
            return safeCount <= 0 ? 0f : Mathf.Max(0f, interval) * Mathf.Max(0, safeCount - 1);
        }

        private static Vector2 AngleToDirection(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        }

        private static float DirectionToAngle(Vector2 direction)
        {
            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private static Vector2 RotateDirection(Vector2 direction, float degrees)
        {
            Vector2 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float radians = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            return new Vector2(normalized.x * cos - normalized.y * sin, normalized.x * sin + normalized.y * cos);
        }

        private Transform FindChild(string childName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == childName)
                {
                    return children[i];
                }
            }

            return null;
        }

        private Transform FindChildPathOrName(string childPath)
        {
            if (string.IsNullOrWhiteSpace(childPath))
            {
                return null;
            }

            Transform child = transform.Find(childPath);
            return child != null ? child : FindChild(childPath);
        }
    }
}
