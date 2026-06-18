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
    public sealed class Drone : MonoBehaviour
    {
        private const string DynamicStatusViewName = "DroneStatusView";

        public enum IdleMovementMode
        {
            Wander,
            KeepPlayerDistance,
            StrafeAroundPlayer,
            PingPong
        }

        private static readonly List<Drone> ActiveDrones = new();

        [Header("Owner")]
        [SerializeField] private DronePilot owner;

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
        [SerializeField] private AttackTimingOutline attackTimingOutline;
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
        private int attackBulletTotal;
        private int attackBulletRemaining;

        public DronePilot Owner => owner;
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
        public static IReadOnlyList<Drone> All => ActiveDrones;

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
            if (!ActiveDrones.Contains(this))
            {
                ActiveDrones.Add(this);
            }

            if (health != null)
            {
                health.Died += HandleDied;
            }
        }

        private void OnDisable()
        {
            ActiveDrones.Remove(this);
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

            ClearAttackBullets();
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

        public void SetOwner(DronePilot nextOwner)
        {
            owner = nextOwner;
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
            if (bullets == null || bullets.IsEmpty || isSummoning)
            {
                return false;
            }

            PruneInactiveProjectiles();
            return activeProjectiles.Count < bullets.CurrentBullets;
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

        public void ClearOwner(DronePilot expectedOwner)
        {
            if (owner == expectedOwner)
            {
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

        public void FireOnceAtPlayer(DronePilot.ProjectileSettings projectile)
        {
            if (owner == null || projectile == null)
            {
                return;
            }

            Vector3 origin = GetProjectileOrigin();
            FireCommandProjectile(projectile, origin, GetDirectionToPlayer(origin), true);
        }

        public void PreviewAttackBullets(int totalBulletCount)
        {
            attackBulletTotal = Mathf.Max(0, totalBulletCount);
            attackBulletRemaining = attackBulletTotal;
            ShowCurrentAttackBullets();
        }

        public void ShowAttackRecovery(float remainingSeconds, float durationSeconds)
        {
            EnsureAttackTimingOutline();
            attackTimingOutline.Show(remainingSeconds, durationSeconds, attackBulletRemaining, attackBulletTotal);
        }

        public void ShowAttackBulletsOnly()
        {
            ShowCurrentAttackBullets();
        }

        public void ClearAttackBullets()
        {
            attackBulletTotal = 0;
            attackBulletRemaining = 0;
            attackTimingOutline?.Hide();
        }

        public float CommandStopAndFire(DronePilot.ProjectileSettings projectile, int bulletCount, float fireInterval, bool resumeIdle)
        {
            StopCommand();
            float duration = GetSequentialFireDuration(bulletCount, fireInterval);
            commandRoutine = StartCoroutine(RunStopAndFire(projectile, bulletCount, fireInterval, resumeIdle));
            return duration;
        }

        public float CommandOrbitFire(
            DronePilot.ProjectileSettings projectile,
            float orbitRadius,
            float orbitSeconds,
            float fireAngleStepDegrees,
            bool clockwise)
        {
            StopCommand();
            float duration = Mathf.Max(0.1f, orbitSeconds);
            commandRoutine = StartCoroutine(RunOrbitFire(projectile, orbitRadius, duration, fireAngleStepDegrees, clockwise));
            return duration;
        }

        public float CommandRadialBurst(
            DronePilot.ProjectileSettings projectile,
            int volleyCount,
            int directionCount,
            float volleyInterval,
            float spreadDegrees,
            bool resumeIdle)
        {
            StopCommand();
            float duration = GetSequentialFireDuration(volleyCount, volleyInterval);
            commandRoutine = StartCoroutine(RunRadialBurst(projectile, volleyCount, directionCount, volleyInterval, spreadDegrees, resumeIdle));
            return duration;
        }

        public float CommandChargeSideFire(
            DronePilot.ProjectileSettings projectile,
            float chargeSeconds,
            float chargeSpeed,
            float aimOffsetDegrees,
            float sideFireInterval,
            float sideFireAngleDegrees)
        {
            StopCommand();
            float duration = Mathf.Max(0.05f, chargeSeconds);
            commandRoutine = StartCoroutine(RunChargeSideFire(projectile, duration, chargeSpeed, aimOffsetDegrees, sideFireInterval, sideFireAngleDegrees));
            return duration;
        }

        public void CommandFormation(float angleOffsetDegrees, float radius, float speedMultiplier)
        {
            StopCommand();
            commandRoutine = StartCoroutine(RunFormation(angleOffsetDegrees, radius, speedMultiplier));
        }

        private IEnumerator RunStopAndFire(DronePilot.ProjectileSettings projectile, int bulletCount, float fireInterval, bool resumeIdle)
        {
            StopBody();
            int count = Mathf.Max(0, bulletCount);
            for (int i = 0; i < count; i++)
            {
                StopBody();
                FireOnceAtPlayer(projectile);
                if (i < count - 1)
                {
                    yield return new WaitForSeconds(Mathf.Max(0f, fireInterval));
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
            DronePilot.ProjectileSettings projectile,
            float orbitRadius,
            float orbitSeconds,
            float fireAngleStepDegrees,
            bool clockwise)
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
                if (player == null)
                {
                    break;
                }

                if (travelled >= nextFireAt)
                {
                    FireOnceAtPlayer(projectile);
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
            DronePilot.ProjectileSettings projectile,
            int volleyCount,
            int directionCount,
            float volleyInterval,
            float spreadDegrees,
            bool resumeIdle)
        {
            StopBody();
            int volleys = Mathf.Max(1, volleyCount);
            int directions = Mathf.Max(1, directionCount);
            for (int volley = 0; volley < volleys; volley++)
            {
                Vector3 origin = GetProjectileOrigin();
                Vector2 playerDirection = GetDirectionToPlayer(origin);
                float centerAngle = DirectionToAngle(playerDirection);
                float arc = spreadDegrees <= 0f ? 360f : Mathf.Min(360f, spreadDegrees);
                float step = directions <= 1 ? 0f : arc / (directions - 1);
                float start = directions <= 1 ? centerAngle : centerAngle - arc * 0.5f;
                bool firedAny = false;

                for (int i = 0; i < directions; i++)
                {
                    firedAny |= FireCommandProjectile(projectile, origin, AngleToDirection(start + step * i), i == 0, false) != null;
                }

                if (firedAny)
                {
                    ConsumeAttackBullet();
                }

                if (volley < volleys - 1)
                {
                    yield return new WaitForSeconds(Mathf.Max(0f, volleyInterval));
                }
            }

            FinishCommand(resumeIdle);
        }

        private IEnumerator RunChargeSideFire(
            DronePilot.ProjectileSettings projectile,
            float chargeSeconds,
            float chargeSpeed,
            float aimOffsetDegrees,
            float sideFireInterval,
            float sideFireAngleDegrees)
        {
            Vector2 direction = RotateDirection(GetDirectionToPlayer(transform.position), aimOffsetDegrees);
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
                SetVelocity(direction.normalized * Mathf.Max(0f, chargeSpeed));
                if (elapsed >= nextFireAt)
                {
                    Vector3 origin = GetProjectileOrigin();
                    bool firedAny = FireCommandProjectile(projectile, origin, RotateDirection(direction, sideAngle), true, false) != null;
                    firedAny |= FireCommandProjectile(projectile, origin, RotateDirection(direction, -sideAngle), false, false) != null;
                    if (firedAny)
                    {
                        ConsumeAttackBullet();
                    }

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
            while (owner != null && owner.Player != null)
            {
                Vector2 bossToPlayer = (Vector2)(owner.Player.position - owner.transform.position);
                Vector2 baseDirection = bossToPlayer.sqrMagnitude > 0.0001f ? bossToPlayer.normalized : Vector2.right;
                Vector2 targetDirection = RotateDirection(baseDirection, angleOffsetDegrees);
                Vector2 target = (Vector2)owner.Player.position + targetDirection * Mathf.Max(0.1f, radius);
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
            statusView.SetTargets(health, bullets);
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
            ClearAttackBullets();
            DestroyActiveProjectiles();
            StopCommand();
            QueueDestroyAfterDeath();
        }

        private EnemyProjectile FireCommandProjectile(
            DronePilot.ProjectileSettings projectile,
            Vector3 origin,
            Vector2 direction,
            bool playMuzzleFlash,
            bool consumeAttackBullet = true)
        {
            if (owner == null)
            {
                return null;
            }

            EnemyProjectile firedProjectile = owner.FireDroneProjectile(this, projectile, origin, direction, playMuzzleFlash);
            if (firedProjectile != null && consumeAttackBullet)
            {
                ConsumeAttackBullet();
            }

            return firedProjectile;
        }

        private void ConsumeAttackBullet()
        {
            if (attackBulletTotal <= 0)
            {
                return;
            }

            attackBulletRemaining = Mathf.Max(0, attackBulletRemaining - 1);
            ShowCurrentAttackBullets();
        }

        private void ShowCurrentAttackBullets()
        {
            if (attackBulletTotal <= 0)
            {
                attackTimingOutline?.Hide();
                return;
            }

            EnsureAttackTimingOutline();
            attackTimingOutline.ShowBullets(attackBulletRemaining, attackBulletTotal);
        }

        private void EnsureAttackTimingOutline()
        {
            if (attackTimingOutline == null)
            {
                attackTimingOutline = GetComponentInChildren<AttackTimingOutline>(true);
            }

            if (attackTimingOutline == null)
            {
                attackTimingOutline = gameObject.AddComponent<AttackTimingOutline>();
            }

            attackTimingOutline.SetTarget(bodyRoot != null ? bodyRoot : transform);
            attackTimingOutline.SetOutlineShape(AttackTimingOutline.OutlineShape.Square);
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

        private Transform GetPlayer()
        {
            if (owner != null && owner.Player != null)
            {
                return owner.Player;
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
    }
}
