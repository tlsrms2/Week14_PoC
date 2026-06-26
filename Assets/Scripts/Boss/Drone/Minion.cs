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

        [Header("Scene References")]
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private EnemyStatusView statusView;
        [SerializeField] private SpriteRenderer lockOnIndicator;
        [SerializeField] private SpriteRenderer executionIndicator;

        private readonly List<EnemyProjectile> activeProjectiles = new();
        private Coroutine movementRoutine;
        private Coroutine fireRoutine;
        private Coroutine summonRoutine;
        private MinionGraphProjectileFireSpec commandFireSpec;
        private bool isFormationCommand;
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
        public bool IsCommanded => movementRoutine != null || fireRoutine != null;
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

            RefreshIgnoredCollisionPairs();

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

            if (IsCommanded)
            {
                if (TryFaceSharedMinionAim())
                {
                    if (isFormationCommand)
                    {
                        StopBody();
                    }
                }
                else if (isFormationCommand)
                {
                    StopBody();
                    FacePlayer();
                }
                else if (movementRoutine != null)
                {
                    FaceMovementDirection();
                }
                else
                {
                    StopBody();
                }

                return;
            }

            StopBody();
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
            RefreshIgnoredCollisionPairs();
        }

        private void RefreshIgnoredCollisionPairs()
        {
            IgnoreOtherMinionCollisions();
            IgnoreOwnerCollisions();
        }

        private void IgnoreOtherMinionCollisions()
        {
            if (colliders == null)
            {
                return;
            }

            for (int i = 0; i < ActiveMinions.Count; i++)
            {
                Minion other = ActiveMinions[i];
                if (other == null || other == this)
                {
                    continue;
                }

                IgnoreColliderPairs(colliders, other.colliders);
            }
        }

        private void IgnoreOwnerCollisions()
        {
            if (colliders == null)
            {
                return;
            }

            Transform ownerTransform = Owner?.MinionOwnerTransform;
            if (ownerTransform == null)
            {
                return;
            }

            Collider2D[] ownerColliders = ownerTransform.GetComponentsInChildren<Collider2D>(true);
            IgnoreColliderPairs(colliders, ownerColliders);
        }

        private static void IgnoreColliderPairs(Collider2D[] sourceColliders, Collider2D[] targetColliders)
        {
            if (sourceColliders == null || targetColliders == null)
            {
                return;
            }

            for (int i = 0; i < sourceColliders.Length; i++)
            {
                Collider2D source = sourceColliders[i];
                if (source == null)
                {
                    continue;
                }

                for (int j = 0; j < targetColliders.Length; j++)
                {
                    Collider2D target = targetColliders[j];
                    if (target == null || target == source)
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(source, target, true);
                }
            }
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
            StopMovementCommand();
            StopFireCommand();

            isFormationCommand = false;
            suppressBodyContactDamage = false;
            StopBody();
        }

        private void StopMovementCommand()
        {
            if (movementRoutine != null)
            {
                StopCoroutine(movementRoutine);
                movementRoutine = null;
            }

            isFormationCommand = false;
            suppressBodyContactDamage = false;
        }

        private void StopFireCommand()
        {
            if (fireRoutine != null)
            {
                StopCoroutine(fireRoutine);
                fireRoutine = null;
            }

            commandFireSpec = default;
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
            FaceClosestMinionAim(fireSpec, finalDirection);
            FireCommandProjectile(projectile, spawnOrigin, finalDirection, !fireSpec.HasEffects, fireSpec);
        }

        public float CommandRepeatFire(BossProjectileSettings projectile, int bulletCount, float fireInterval)
        {
            return CommandRepeatFire(projectile, bulletCount, fireInterval, default);
        }

        public float CommandRepeatFire(
            BossProjectileSettings projectile,
            int bulletCount,
            float fireInterval,
            MinionGraphProjectileFireSpec fireSpec)
        {
            StopFireCommand();
            commandFireSpec = fireSpec;
            float duration = GetSequentialFireDuration(bulletCount, fireInterval);
            fireRoutine = StartCoroutine(RunRepeatFire(projectile, bulletCount, fireInterval, fireSpec));
            return duration;
        }

        public float CommandOrbit(
            float orbitRadius,
            float orbitSeconds,
            bool clockwise)
        {
            return CommandOrbit(orbitRadius, orbitSeconds, clockwise, 24f, 0f);
        }

        public float CommandOrbit(
            float orbitRadius,
            float orbitSeconds,
            bool clockwise,
            float moveSpeed,
            float angleOffsetDegrees)
        {
            StopMovementCommand();
            float duration = Mathf.Max(0.1f, orbitSeconds);
            movementRoutine = StartCoroutine(RunOrbit(
                orbitRadius,
                duration,
                clockwise,
                Mathf.Max(0f, moveSpeed),
                angleOffsetDegrees));
            return duration;
        }

        public float CommandWander(
            float wanderSeconds,
            float wanderSpeed,
            float wanderRadius,
            float wanderRetargetSeconds)
        {
            StopMovementCommand();
            float duration = Mathf.Max(0f, wanderSeconds);
            movementRoutine = StartCoroutine(RunWander(
                duration,
                Mathf.Max(0f, wanderSpeed),
                Mathf.Max(0.1f, wanderRadius),
                Mathf.Max(0.1f, wanderRetargetSeconds)));
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
            StopFireCommand();
            commandFireSpec = fireSpec;
            float duration = GetSequentialFireDuration(volleyCount, volleyInterval);
            fireRoutine = StartCoroutine(RunRadialBurst(projectile, volleyCount, directionCount, volleyInterval, spreadDegrees, fireSpec));
            return duration;
        }

        public float CommandCharge(
            float chargeSeconds,
            float chargeSpeed,
            float aimOffsetDegrees,
            MinionGraphProjectileFireSpec aimSpec)
        {
            StopMovementCommand();
            float duration = Mathf.Max(0.05f, chargeSeconds);
            movementRoutine = StartCoroutine(RunCharge(duration, chargeSpeed, aimOffsetDegrees, aimSpec));
            return duration;
        }

        public float CommandSideFire(
            BossProjectileSettings projectile,
            float fireSeconds,
            float fireInterval,
            float sideFireAngleDegrees,
            MinionGraphSideFireOriginMode sideFireOriginMode,
            float sideFireOriginSpacing,
            MinionGraphProjectileFireSpec fireSpec)
        {
            StopFireCommand();
            commandFireSpec = fireSpec;
            float duration = Mathf.Max(0.05f, fireSeconds);
            fireRoutine = StartCoroutine(RunSideFire(
                projectile,
                duration,
                fireInterval,
                sideFireAngleDegrees,
                sideFireOriginMode,
                sideFireOriginSpacing,
                fireSpec));
            return duration;
        }

        public float CommandHoldPosition(float holdSeconds)
        {
            StopMovementCommand();
            float duration = Mathf.Max(0f, holdSeconds);
            movementRoutine = StartCoroutine(RunHoldPosition(duration));
            return duration;
        }

        public void CommandFormationCircle(
            float angleOffsetDegrees,
            float radius,
            bool sideBySide,
            float moveSpeed)
        {
            StopMovementCommand();
            isFormationCommand = true;
            movementRoutine = StartCoroutine(RunFormationCircle(angleOffsetDegrees, radius, sideBySide, moveSpeed));
        }

        public void CommandFormationStraight(
            float lateralOffset,
            float distanceFromPlayer,
            MinionGraphFormationStraightMode mode,
            float moveSpeed)
        {
            StopMovementCommand();
            isFormationCommand = true;
            movementRoutine = StartCoroutine(RunFormationStraight(lateralOffset, distanceFromPlayer, mode, moveSpeed));
        }

        public void CommandAngleDistance(float angleDegrees, float distanceFromPlayer, float moveSpeed)
        {
            StopMovementCommand();
            isFormationCommand = true;
            movementRoutine = StartCoroutine(RunAngleDistance(angleDegrees, distanceFromPlayer, moveSpeed));
        }

        public float CommandPlayerPath(
            MinionGraphPlayerPathType pathType,
            Vector2 pathCenter,
            float distanceFromPlayer,
            float moveToStartSeconds,
            float moveSeconds)
        {
            StopMovementCommand();
            float safeMoveToStartSeconds = Mathf.Max(0f, moveToStartSeconds);
            float safeMoveSeconds = Mathf.Max(0.05f, moveSeconds);
            movementRoutine = StartCoroutine(RunPlayerPath(
                pathType,
                pathCenter,
                Mathf.Max(0.1f, distanceFromPlayer),
                safeMoveToStartSeconds,
                safeMoveSeconds));
            return safeMoveToStartSeconds + safeMoveSeconds;
        }

        private IEnumerator RunRepeatFire(
            BossProjectileSettings projectile,
            int bulletCount,
            float fireInterval,
            MinionGraphProjectileFireSpec fireSpec)
        {
            int count = Mathf.Max(0, bulletCount);
            for (int i = 0; i < count; i++)
            {
                yield return WaitWhileExecutionPaused();
                FireOnce(projectile, fireSpec, i);
                if (i < count - 1)
                {
                    yield return WaitCommandSeconds(fireInterval);
                }
            }

            FinishFireCommand();
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

        private IEnumerator RunOrbit(
            float orbitRadius,
            float orbitSeconds,
            bool clockwise,
            float moveSpeed,
            float angleOffsetDegrees)
        {
            Transform player = GetPlayer();
            if (player == null)
            {
                FinishMovementCommand();
                yield break;
            }

            float radius = Mathf.Max(0.1f, orbitRadius);
            float duration = Mathf.Max(0.1f, orbitSeconds);
            float signedSpeed = 360f / duration * (clockwise ? -1f : 1f);
            float angle = GetAngleFromPlayer(player) + angleOffsetDegrees;
            float travelled = 0f;
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

                angle += signedSpeed * Time.deltaTime;
                travelled += Mathf.Abs(signedSpeed * Time.deltaTime);
                Vector2 target = (Vector2)player.position + AngleToDirection(angle) * radius;
                SetPatternPosition(target, ref lockedToPattern, moveSpeed);
                yield return null;
            }

            StopBody();
            FinishMovementCommand();
        }

        private IEnumerator RunWander(
            float wanderSeconds,
            float wanderSpeed,
            float wanderRadius,
            float wanderRetargetSeconds)
        {
            spawnPosition = transform.position;
            wanderTarget = transform.position;
            nextWanderRetargetAt = 0f;

            float elapsed = 0f;
            while (wanderSeconds <= 0f || elapsed < wanderSeconds)
            {
                if (IsExecutionPaused)
                {
                    StopBody();
                    yield return null;
                    continue;
                }

                TickWander(wanderSpeed, wanderRadius, wanderRetargetSeconds);
                elapsed += Time.deltaTime;
                yield return null;
            }

            FinishMovementCommand();
        }

        private IEnumerator RunRadialBurst(
            BossProjectileSettings projectile,
            int volleyCount,
            int directionCount,
            float volleyInterval,
            float spreadDegrees,
            MinionGraphProjectileFireSpec fireSpec)
        {
            int volleys = Mathf.Max(1, volleyCount);
            int directions = Mathf.Max(1, directionCount);
            for (int volley = 0; volley < volleys; volley++)
            {
                Vector3 aimOrigin = fireSpec.GetAimOrigin(this, volley);
                Vector2 playerDirection = fireSpec.GetDirection(this, aimOrigin);
                FaceClosestMinionAim(fireSpec, playerDirection);
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

            FinishFireCommand();
        }

        private IEnumerator RunCharge(
            float chargeSeconds,
            float chargeSpeed,
            float aimOffsetDegrees,
            MinionGraphProjectileFireSpec aimSpec)
        {
            Vector3 aimOrigin = transform.position;
            Vector2 direction = RotateDirection(aimSpec.GetDirection(this, aimOrigin), aimOffsetDegrees);
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.left;
            }

            RotateToDirection(direction);
            float elapsed = 0f;
            while (elapsed < chargeSeconds)
            {
                if (IsExecutionPaused)
                {
                    StopBody();
                    yield return null;
                    continue;
                }

                SetVelocity(direction.normalized * Mathf.Max(0f, chargeSpeed));
                elapsed += Time.deltaTime;
                yield return null;
            }

            StopBody();
            FinishMovementCommand();
        }

        private IEnumerator RunSideFire(
            BossProjectileSettings projectile,
            float fireSeconds,
            float fireInterval,
            float sideFireAngleDegrees,
            MinionGraphSideFireOriginMode sideFireOriginMode,
            float sideFireOriginSpacing,
            MinionGraphProjectileFireSpec fireSpec)
        {
            float elapsed = 0f;
            float nextFireAt = 0f;
            float interval = Mathf.Max(0.01f, fireInterval);
            float sideAngle = Mathf.Max(1f, sideFireAngleDegrees);
            while (elapsed < fireSeconds)
            {
                if (IsExecutionPaused)
                {
                    yield return null;
                    continue;
                }

                if (elapsed >= nextFireAt)
                {
                    int shotIndex = Mathf.RoundToInt(nextFireAt / interval);
                    Vector3 aimOrigin = fireSpec.GetAimOrigin(this, shotIndex);
                    Vector2 direction = fireSpec.GetDirection(this, aimOrigin);
                    if (direction.sqrMagnitude <= 0.0001f)
                    {
                        direction = Vector2.left;
                    }

                    Vector3 origin = fireSpec.GetSpawnOrigin(this, shotIndex, direction);
                    FaceClosestMinionAim(fireSpec, direction);
                    Vector2 sideFireForward = GetSideFireForward(direction, sideFireOriginMode);
                    GetSideFireOrigins(
                        origin,
                        sideFireForward,
                        sideFireOriginMode,
                        sideFireOriginSpacing,
                        out Vector3 firstOrigin,
                        out Vector3 secondOrigin);
                    FireCommandProjectile(
                        projectile,
                        firstOrigin,
                        RotateDirection(sideFireForward, sideAngle),
                        !fireSpec.HasEffects,
                        fireSpec);
                    FireCommandProjectile(
                        projectile,
                        secondOrigin,
                        RotateDirection(sideFireForward, -sideAngle),
                        false,
                        fireSpec);

                    nextFireAt += interval;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            FinishFireCommand();
        }

        private Vector2 GetSideFireForward(Vector2 aimDirection, MinionGraphSideFireOriginMode sideFireOriginMode)
        {
            if (sideFireOriginMode == MinionGraphSideFireOriginMode.BodySides && transform.right.sqrMagnitude > 0.0001f)
            {
                return (Vector2)transform.right.normalized;
            }

            return aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.left;
        }

        private void GetSideFireOrigins(
            Vector3 origin,
            Vector2 direction,
            MinionGraphSideFireOriginMode sideFireOriginMode,
            float sideFireOriginSpacing,
            out Vector3 firstOrigin,
            out Vector3 secondOrigin)
        {
            firstOrigin = origin;
            secondOrigin = origin;
            if (sideFireOriginMode != MinionGraphSideFireOriginMode.BodySides || sideFireOriginSpacing <= 0f)
            {
                return;
            }

            Vector2 facing = transform.right.sqrMagnitude > 0.0001f
                ? (Vector2)transform.right.normalized
                : direction.normalized;
            Vector2 side = new(-facing.y, facing.x);
            Vector3 offset = (Vector3)(side * sideFireOriginSpacing);
            firstOrigin += offset;
            secondOrigin -= offset;
        }

        private IEnumerator RunHoldPosition(float holdSeconds)
        {
            float elapsed = 0f;
            while (holdSeconds <= 0f || elapsed < holdSeconds)
            {
                if (IsExecutionPaused)
                {
                    StopBody();
                    yield return null;
                    continue;
                }

                StopBody();
                elapsed += Time.deltaTime;
                yield return null;
            }

            FinishMovementCommand();
        }

        private IEnumerator RunFormationCircle(
            float angleOffsetDegrees,
            float radius,
            bool sideBySide,
            float moveSpeed)
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

                Vector2 target = GetFormationTarget(
                    currentOwner,
                    player,
                    angleOffsetDegrees,
                    radius,
                    sideBySide);
                SetPatternPosition(target, ref lockedToPattern, moveSpeed);
                FacePlayer();
                yield return null;
            }

            FinishMovementCommand();
        }

        private IEnumerator RunFormationStraight(
            float lateralOffset,
            float distanceFromPlayer,
            MinionGraphFormationStraightMode mode,
            float moveSpeed)
        {
            bool lockedToPattern = false;
            bool hasLastPlayerPosition = false;
            Vector2 lastPlayerPosition = Vector2.zero;
            Vector2 trackedPlayerForward = Vector2.zero;
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

                Vector2 playerPosition = player.position;
                if (mode == MinionGraphFormationStraightMode.PlayerForward)
                {
                    if (hasLastPlayerPosition)
                    {
                        Vector2 delta = playerPosition - lastPlayerPosition;
                        if (delta.sqrMagnitude > 0.0001f)
                        {
                            trackedPlayerForward = delta.normalized;
                        }
                    }

                    if (trackedPlayerForward.sqrMagnitude <= 0.0001f)
                    {
                        trackedPlayerForward = GetBossToPlayerDirection(currentOwner, player);
                    }

                    lastPlayerPosition = playerPosition;
                    hasLastPlayerPosition = true;
                }

                Vector2 target = GetFormationStraightTarget(
                    currentOwner,
                    player,
                    lateralOffset,
                    distanceFromPlayer,
                    mode,
                    trackedPlayerForward);
                SetPatternPosition(target, ref lockedToPattern, moveSpeed);
                FacePlayer();
                yield return null;
            }

            FinishMovementCommand();
        }

        private IEnumerator RunAngleDistance(
            float angleDegrees,
            float distanceFromPlayer,
            float moveSpeed)
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

                Vector2 target = (Vector2)player.position
                    + AngleToDirection(angleDegrees) * Mathf.Max(0.1f, distanceFromPlayer);
                SetPatternPosition(target, ref lockedToPattern, moveSpeed);
                FacePlayer();
                yield return null;
            }

            FinishMovementCommand();
        }

        private IEnumerator RunPlayerPath(
            MinionGraphPlayerPathType pathType,
            Vector2 pathCenter,
            float distanceFromPlayer,
            float moveToStartSeconds,
            float moveSeconds)
        {
            GetPlayerPathOffsets(
                pathType,
                distanceFromPlayer,
                out Vector2 startOffset,
                out Vector2 endOffset);

            Vector2 startPosition = pathCenter + startOffset;
            yield return MoveToPlayerPathStart(startPosition, moveToStartSeconds);

            float elapsed = 0f;
            while (elapsed < moveSeconds)
            {
                if (IsExecutionPaused)
                {
                    StopBody();
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / moveSeconds);
                Vector2 target = pathCenter + Vector2.Lerp(startOffset, endOffset, t);
                SetPatternPosition(target, true);

                elapsed += Time.deltaTime;
                yield return null;
            }

            SetPatternPosition(pathCenter + endOffset, true);
            FinishMovementCommand();
        }

        private IEnumerator MoveToPlayerPathStart(Vector2 target, float moveToStartSeconds)
        {
            if (moveToStartSeconds <= 0f)
            {
                SetPatternPosition(target, true);
                yield break;
            }

            Vector2 start = transform.position;
            float elapsed = 0f;
            while (elapsed < moveToStartSeconds)
            {
                if (IsExecutionPaused)
                {
                    StopBody();
                    yield return null;
                    continue;
                }

                float t = Mathf.Clamp01(elapsed / moveToStartSeconds);
                SetPatternPosition(Vector2.Lerp(start, target, t), true);
                elapsed += Time.deltaTime;
                yield return null;
            }

            SetPatternPosition(target, true);
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

        private Vector2 GetFormationTarget(
            IMinionOwner currentOwner,
            Transform player,
            float angleOffsetDegrees,
            float radius,
            bool sideBySide)
        {
            Transform ownerTransform = currentOwner.MinionOwnerTransform;
            Vector2 bossToPlayer = ownerTransform != null
                ? (Vector2)(player.position - ownerTransform.position)
                : Vector2.right;
            Vector2 playerToBoss = -bossToPlayer;
            float bossPlayerDistance = playerToBoss.magnitude;
            Vector2 baseDirection;
            float targetRadius;
            if (sideBySide)
            {
                baseDirection = bossPlayerDistance > 0.0001f
                    ? playerToBoss / bossPlayerDistance
                    : Vector2.left;
                targetRadius = bossPlayerDistance > 0.0001f
                    ? Mathf.Max(0.1f, bossPlayerDistance)
                    : Mathf.Max(0.1f, radius);
            }
            else
            {
                baseDirection = bossToPlayer.sqrMagnitude > 0.0001f
                    ? bossToPlayer.normalized
                    : Vector2.right;
                targetRadius = Mathf.Max(0.1f, radius);
            }

            Vector2 targetDirection = RotateDirection(baseDirection, angleOffsetDegrees);
            return (Vector2)player.position + targetDirection * targetRadius;
        }

        private Vector2 GetFormationStraightTarget(
            IMinionOwner currentOwner,
            Transform player,
            float lateralOffset,
            float distanceFromPlayer,
            MinionGraphFormationStraightMode mode,
            Vector2 trackedPlayerForward)
        {
            Vector2 centerDirection = mode == MinionGraphFormationStraightMode.BetweenBossAndPlayer
                ? -GetBossToPlayerDirection(currentOwner, player)
                : trackedPlayerForward;
            if (centerDirection.sqrMagnitude <= 0.0001f)
            {
                centerDirection = GetBossToPlayerDirection(currentOwner, player);
            }

            centerDirection.Normalize();
            Vector2 lineAxis = new(-centerDirection.y, centerDirection.x);
            Vector2 center = (Vector2)player.position + centerDirection * Mathf.Max(0.1f, distanceFromPlayer);
            return center + lineAxis * lateralOffset;
        }

        private Vector2 GetBossToPlayerDirection(IMinionOwner currentOwner, Transform player)
        {
            Transform ownerTransform = currentOwner?.MinionOwnerTransform;
            if (ownerTransform == null || player == null)
            {
                return Vector2.right;
            }

            Vector2 bossToPlayer = (Vector2)(player.position - ownerTransform.position);
            return bossToPlayer.sqrMagnitude > 0.0001f ? bossToPlayer.normalized : Vector2.right;
        }

        private void TickWander(float wanderSpeed, float wanderRadius, float wanderRetargetSeconds)
        {
            if (Time.time >= nextWanderRetargetAt || Vector2.Distance(transform.position, wanderTarget) < 0.2f)
            {
                wanderTarget = spawnPosition + Random.insideUnitCircle * Mathf.Max(0.1f, wanderRadius);
                nextWanderRetargetAt = Time.time + Mathf.Max(0.1f, wanderRetargetSeconds);
            }

            MoveTo(wanderTarget, wanderSpeed);
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
            if (!TryFaceSharedMinionAim())
            {
                RotateToDirection(delta);
            }
        }

        private void SetPatternPosition(Vector2 target, ref bool lockedToPattern, float moveSpeed)
        {
            if (lockedToPattern)
            {
                SetPatternPosition(target);
                return;
            }

            Vector2 current = transform.position;
            float maxDistance = Mathf.Max(0f, moveSpeed) * Time.deltaTime;
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
            RefreshIgnoredCollisionPairs();
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

        private void FacePlayer()
        {
            RotateToDirection(GetDirectionToPlayer(transform.position));
        }

        private bool TryFaceSharedMinionAim()
        {
            if (!commandFireSpec.TryGetSharedMinionAimDirection(out Vector2 direction))
            {
                return false;
            }

            RotateToDirection(direction);
            return true;
        }

        private void FaceClosestMinionAim(MinionGraphProjectileFireSpec fireSpec, Vector2 fallbackDirection)
        {
            if (fireSpec.TryGetSharedMinionAimDirection(out Vector2 direction))
            {
                RotateToDirection(direction);
                return;
            }

            if (fireSpec.UsesClosestMinionAim)
            {
                RotateToDirection(fallbackDirection);
            }
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

        private void FinishMovementCommand()
        {
            movementRoutine = null;
            isFormationCommand = false;
            suppressBodyContactDamage = false;
            StopBody();
        }

        private void FinishFireCommand()
        {
            fireRoutine = null;
            commandFireSpec = default;
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
