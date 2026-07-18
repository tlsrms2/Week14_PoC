using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;
using Week14.Enemy;

namespace Week14.Tutorial
{
    public enum TutorialTrainingEnemyMode
    {
        Passive,
        AttackTarget,
        ForcedHitPractice,
        ParryPractice,
        DodgePractice,
        Duel,
        SuppressionPractice
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health), typeof(BulletGauge), typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class TutorialTrainingEnemy : MonoBehaviour
    {
        private enum DuelPattern
        {
            InterceptableBurst3,
            InterceptableRadial,
            InterceptableBurst4,
            DodgeRadial
        }

        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

        [Header("Tutorial Enemy References")]
        [SerializeField] private CombatEffectData effectData;
        [SerializeField] private BossColorSettings colorSettings;

        [Header("Training Settings")]
        [SerializeField, Min(1)] private int maxBullets = 6;
        [SerializeField, Min(0f)] private float moveSpeed = 1.6f;
        [SerializeField, Min(0f)] private float stopDistance = 3f;
        [SerializeField, Min(0f)] private float practiceFireIntervalSeconds = 1.8f;
        [SerializeField, Min(0f)] private float duelFireIntervalSeconds = 1.3f;
        [SerializeField, Min(0)] private int contactDamage = 1;
        [SerializeField, Min(0f)] private float contactDamageCooldown = 1f;

        [Header("Duel Pattern")]
        [SerializeField, Min(1)] private int duelOpeningBurstShotCount = 3;
        [SerializeField, Min(1)] private int duelMainBurstShotCount = 4;
        [SerializeField, Min(0.01f)] private float duelSingleShotSpeedMultiplier = 1.5f;

        [Header("Projectile Settings")]
        [SerializeField] private BossProjectileSettings projectile = new();
        [SerializeField] private BossProjectileSettings dodgeProjectile = new();
        [SerializeField, Min(4)] private int dodgeProjectileCount = 12;
        [SerializeField, Min(0f)] private float dodgeFireDelaySeconds = 0.65f;
        [Tooltip("패턴 억제 연습 단계에서 발사할 미끼탄 프리팹입니다. ParryBullet_Basic(ParryBaitRewardProjectile)을 지정하세요.")]
        [SerializeField] private BossProjectileSettings suppressionBaitProjectile = new();
        [Tooltip("패링 성공 시 사방으로 뿌릴 보상탄 개수입니다. 프리팹 기본값을 덮어씁니다.")]
        [SerializeField, Min(1)] private int suppressionRewardBulletCount = 3;
        [Tooltip("보상탄이 배치될 원의 반지름입니다.")]
        [SerializeField, Min(0.01f)] private float suppressionRewardCircleRadius = 1.5f;
        [Tooltip("보상탄의 지속 시간(초)입니다.")]
        [SerializeField, Min(0.01f)] private float suppressionRewardLifetime = 2f;
        [Tooltip("패링 성공 시 그로기(무력화) 상태로 공격을 멈추는 시간(초)입니다.")]
        [SerializeField, Min(0f)] private float suppressionGroggySeconds = 2f;
        [Tooltip("패링 실패로 회피 탄막(스킬 회피 튜토리얼과 동일한 패링불가탄 패턴)을 쏜 뒤, 다음 미끼탄을 다시 발사하기까지의 대기 시간(초)입니다.")]
        [SerializeField, Min(0.1f)] private float suppressionRetryDelaySeconds = 1f;

        [Header("Hit Feedback")]
        [SerializeField] private Color hitFlashColor = Color.white;
        [SerializeField, Min(0f)] private float hitFlashSeconds = 0.08f;

        [Header("Scene References")]
        [FormerlySerializedAs("statusTarget")]
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Transform projectileOrigin;
        [SerializeField] private SpriteRenderer lockOnIndicator;

        [SerializeField, HideInInspector] private SpriteRenderer[] bodyRenderers;
        [SerializeField, HideInInspector] private Color lockOnIndicatorColor = Color.white;
        [SerializeField, HideInInspector] private Color bodyHitColor = new(1f, 0.35f, 0.25f, 1f);
        [SerializeField, HideInInspector] private float bodyHitColorSeconds = 0.08f;

        private Health health;
        private BulletGauge bullets;
        private SpriteRenderer configuredLockOnIndicator;
        private Collider2D[] interactionColliders;
        private bool[] interactionColliderBaseEnabled;
        private Color[] bodyRendererBaseColors;
        private MaterialPropertyBlock hitFlashPropertyBlock;
        private Transform target;
        private TutorialTrainingEnemyMode mode;
        private bool isActive;
        private bool playerInteractionEnabled = true;
        private float nextFireAt;
        private float nextContactDamageAt;
        private float bodyHitColorEndsAt;
        private float hitFlashEndsAt;
        private bool isBodyHitColorActive;
        private bool isHitFlashActive;
        private bool dodgeVolleyFired;
        private bool duelOpeningPatternCompleted;
        private int duelPatternIndex;
        private int duelBurstShotsRemaining;
        private Coroutine suppressionRoutine;
        private ParryBaitRewardProjectile activeSuppressionBait;
        private bool suppressionBaitResolved;
        private bool suppressionBaitParried;

        public Health Health => health;
        public BulletGauge Bullets => bullets;
        public bool IsPlayerTargetable => playerInteractionEnabled && isActive && health != null && !health.IsDead;
        public Color LockOnIndicatorColor => colorSettings != null ? colorSettings.LockOnIndicatorColor : lockOnIndicatorColor;
        public event Action<TutorialTrainingEnemy> Defeated;
        public event Action BaitSuppressed;

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            health.Died += HandleDied;
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Died -= HandleDied;
            }

            StopSuppressionRoutine();
            SetLockOnIndicatorVisible(false);
            StopBody();
        }

        private void Start()
        {
            bullets.Configure(maxBullets, true, BulletChangeSource.CombatStart);
        }

        private void Update()
        {
            UpdateBodyHitColor();
            UpdateHitFlash();

            if (!CanAct())
            {
                StopBody();
                return;
            }

            if (ShouldFire())
            {
                TryFire();
            }
        }

        private void FixedUpdate()
        {
            if (!CanAct())
            {
                StopBody();
                return;
            }

            if (!ShouldMove())
            {
                StopBody();
                Face((Vector2)target.position - (Vector2)transform.position);
                return;
            }

            MoveNearTarget();
        }

        private void LateUpdate()
        {
            UpdateLockOnIndicator();
        }

        public void Activate(Transform nextTarget, TutorialTrainingEnemyMode nextMode)
        {
            EnsureReferences();
            StopSuppressionRoutine();
            target = nextTarget;
            mode = nextMode;
            playerInteractionEnabled = true;
            gameObject.SetActive(true);
            ApplyPlayerInteractionState();
            health.Revive();
            bullets.Configure(maxBullets, true, BulletChangeSource.CombatStart);
            isActive = true;
            dodgeVolleyFired = false;
            ResetDuelPattern();
            nextFireAt = Time.time + GetInitialFireDelaySeconds();
            nextContactDamageAt = 0f;

            if (mode == TutorialTrainingEnemyMode.SuppressionPractice)
            {
                StartSuppressionRoutine();
            }
        }

        public void Deactivate()
        {
            isActive = false;
            StopSuppressionRoutine();
            SetPlayerInteractionEnabled(false);
            SetLockOnIndicatorVisible(false);
            StopBody();
        }

        public void SetPlayerInteractionEnabled(bool enabled)
        {
            playerInteractionEnabled = enabled;
            EnsureReferences();
            ApplyPlayerInteractionState();

            if (!enabled)
            {
                SetLockOnIndicatorVisible(false);
            }
        }

        public bool ReceivePlayerHit(int bulletDamage, Vector3 hitPosition, Vector2 hitDirection)
        {
            EnsureReferences();
            if (!IsPlayerTargetable)
            {
                return false;
            }

            if (bullets != null)
            {
                if (bullets.IsEmpty)
                {
                    health.Kill();
                }
                else
                {
                    bullets.TrySpend(bulletDamage, BulletChangeSource.Hit);
                }
            }
            else
            {
                health.TakeDamage(bulletDamage);
            }

            FlashBodyHitColor();
            PlayEnemyHitVfx(hitPosition, hitDirection);
            BossAI.PlayEnemyHitCameraImpactForSequence(hitDirection, 0.08f, 0.12f, 0.05f);
            return true;
        }

        private void EnsureReferences()
        {
            health ??= GetComponent<Health>();
            bullets ??= GetComponent<BulletGauge>();
            body ??= GetComponent<Rigidbody2D>();
            bodyRoot ??= transform;
            projectileOrigin ??= bodyRoot != null ? bodyRoot : transform;
            lockOnIndicator ??= FindIndicator("LockOnIndicator");
            CacheBodyRenderers();
            CacheInteractionColliders();
            ConfigureLockOnIndicator();
            ApplyPlayerInteractionState();

            if (body != null)
            {
                body.gravityScale = 0f;
                body.freezeRotation = true;
                body.interpolation = RigidbodyInterpolation2D.Interpolate;
            }
        }

        private SpriteRenderer FindIndicator(string indicatorName)
        {
            Transform directChild = transform.Find(indicatorName);
            if (directChild != null && directChild.TryGetComponent(out SpriteRenderer directRenderer))
            {
                return directRenderer;
            }

            SpriteRenderer[] candidates = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < candidates.Length; i++)
            {
                if (candidates[i] != null && candidates[i].name == indicatorName)
                {
                    return candidates[i];
                }
            }

            return null;
        }

        private void CacheBodyRenderers()
        {
            if (bodyRenderers == null || bodyRenderers.Length == 0)
            {
                Transform rendererRoot = bodyRoot != null ? bodyRoot : transform;
                bodyRenderers = rendererRoot.GetComponentsInChildren<SpriteRenderer>(true);
            }

            if (bodyRenderers == null)
            {
                bodyRendererBaseColors = Array.Empty<Color>();
                return;
            }

            if (bodyRendererBaseColors != null && bodyRendererBaseColors.Length == bodyRenderers.Length)
            {
                return;
            }

            bodyRendererBaseColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                bodyRendererBaseColors[i] = bodyRenderers[i] != null ? bodyRenderers[i].color : Color.white;
            }
        }

        private void CacheInteractionColliders()
        {
            if (interactionColliders != null && interactionColliders.Length > 0)
            {
                return;
            }

            interactionColliders = GetComponentsInChildren<Collider2D>(true);
            if (interactionColliders == null)
            {
                interactionColliderBaseEnabled = Array.Empty<bool>();
                return;
            }

            interactionColliderBaseEnabled = new bool[interactionColliders.Length];
            for (int i = 0; i < interactionColliders.Length; i++)
            {
                interactionColliderBaseEnabled[i] = interactionColliders[i] != null && interactionColliders[i].enabled;
            }
        }

        private void ApplyPlayerInteractionState()
        {
            if (interactionColliders == null || interactionColliderBaseEnabled == null)
            {
                return;
            }

            for (int i = 0; i < interactionColliders.Length; i++)
            {
                Collider2D targetCollider = interactionColliders[i];
                if (targetCollider == null)
                {
                    continue;
                }

                bool baseEnabled = i < interactionColliderBaseEnabled.Length && interactionColliderBaseEnabled[i];
                targetCollider.enabled = playerInteractionEnabled && baseEnabled;
            }
        }

        private bool CanAct()
        {
            return isActive
                && playerInteractionEnabled
                && target != null
                && health != null
                && !health.IsDead;
        }

        private void MoveNearTarget()
        {
            Vector2 offset = (Vector2)target.position - (Vector2)transform.position;
            float safeStopDistance = Mathf.Max(0.1f, stopDistance);
            if (offset.sqrMagnitude <= safeStopDistance * safeStopDistance)
            {
                StopBody();
                Face(offset);
                return;
            }

            Vector2 direction = offset.normalized;
            body.linearVelocity = GroundMovementConstraint.ClampVelocity(body, direction * moveSpeed);
            Face(direction);
        }

        private void TryFire()
        {
            if (Time.time < nextFireAt)
            {
                return;
            }

            if (mode == TutorialTrainingEnemyMode.Duel)
            {
                TryFireDuelPattern();
                return;
            }

            BossProjectileSettings settings = ResolveProjectileSettings();
            if (settings == null || settings.Prefab == null)
            {
                return;
            }

            if (mode == TutorialTrainingEnemyMode.ForcedHitPractice)
            {
                FireRadialVolley(settings, true);
                nextFireAt = Time.time + Mathf.Max(0.1f, GetFireIntervalSeconds());
                return;
            }

            if (mode == TutorialTrainingEnemyMode.DodgePractice)
            {
                FireRadialVolley(settings, false);
                dodgeVolleyFired = true;
                nextFireAt = float.PositiveInfinity;
                return;
            }

            FireAtTarget(settings);
            nextFireAt = Time.time + Mathf.Max(0.1f, GetFireIntervalSeconds());
        }

        private void TryFireDuelPattern()
        {
            DuelPattern pattern = GetCurrentDuelPattern();

            if (pattern == DuelPattern.DodgeRadial)
            {
                StartDuelSuppressionBait();
                return;
            }

            BossProjectileSettings settings = ResolveDuelPatternProjectileSettings(pattern);
            if (settings == null || settings.Prefab == null)
            {
                return;
            }

            if (pattern == DuelPattern.InterceptableBurst3 || pattern == DuelPattern.InterceptableBurst4)
            {
                FireDuelBurstShot(settings, GetDuelBurstShotCount(pattern));
            }
            else
            {
                FireRadialVolley(settings, pattern == DuelPattern.InterceptableRadial);
                AdvanceDuelPattern();
            }

            nextFireAt = Time.time + GetDuelPatternIntervalSeconds();
        }

        private void StartDuelSuppressionBait()
        {
            nextFireAt = float.PositiveInfinity;
            StopSuppressionRoutine();
            suppressionRoutine = StartCoroutine(RunDuelSuppressionBait());
        }

        private IEnumerator RunDuelSuppressionBait()
        {
            FireSuppressionBait();

            if (activeSuppressionBait != null)
            {
                suppressionBaitResolved = false;
                while (!suppressionBaitResolved)
                {
                    yield return null;
                }

                if (suppressionBaitParried)
                {
                    yield return new WaitForSeconds(Mathf.Max(0f, suppressionGroggySeconds));
                }
                else
                {
                    FireSuppressionDodgeVolley();
                }
            }
            else
            {
                // 미끼탄 프리팹이 설정되지 않은 경우 기존처럼 회피 탄막만 발사한다.
                FireSuppressionDodgeVolley();
            }

            AdvanceDuelPattern();
            suppressionRoutine = null;
            nextFireAt = Time.time + GetDuelPatternIntervalSeconds();
        }

        private void FireDuelBurstShot(BossProjectileSettings settings, int shotCount)
        {
            if (duelBurstShotsRemaining <= 0)
            {
                duelBurstShotsRemaining = Mathf.Max(1, shotCount);
            }

            FireAtTarget(settings);
            duelBurstShotsRemaining--;

            if (duelBurstShotsRemaining <= 0)
            {
                AdvanceDuelPattern();
            }
        }

        private void FireAtTarget(BossProjectileSettings settings)
        {
            Vector3 origin = projectileOrigin != null ? projectileOrigin.position : transform.position;
            Vector2 direction = (Vector2)target.position - (Vector2)origin;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.left;
            }

            SpawnProjectile(settings, origin, direction.normalized, true);
        }

        private void StartSuppressionRoutine()
        {
            StopSuppressionRoutine();
            suppressionRoutine = StartCoroutine(RunSuppressionLoop());
        }

        private void StopSuppressionRoutine()
        {
            if (suppressionRoutine != null)
            {
                StopCoroutine(suppressionRoutine);
                suppressionRoutine = null;
            }

            ClearActiveSuppressionBait();
        }

        private void ClearActiveSuppressionBait()
        {
            if (activeSuppressionBait == null)
            {
                return;
            }

            activeSuppressionBait.Destroyed -= HandleSuppressionBaitDestroyed;
            activeSuppressionBait = null;
        }

        private IEnumerator RunSuppressionLoop()
        {
            yield return new WaitForSeconds(Mathf.Max(0f, dodgeFireDelaySeconds));

            while (isActive && mode == TutorialTrainingEnemyMode.SuppressionPractice)
            {
                if (!CanAct())
                {
                    yield return null;
                    continue;
                }

                FireSuppressionBait();
                if (activeSuppressionBait == null)
                {
                    yield return null;
                    continue;
                }

                suppressionBaitResolved = false;
                while (!suppressionBaitResolved)
                {
                    yield return null;
                }

                if (suppressionBaitParried)
                {
                    yield return new WaitForSeconds(Mathf.Max(0f, suppressionGroggySeconds));
                }
                else
                {
                    FireSuppressionDodgeVolley();
                    yield return new WaitForSeconds(Mathf.Max(0.1f, suppressionRetryDelaySeconds));
                }
            }

            suppressionRoutine = null;
        }

        private void FireSuppressionBait()
        {
            if (suppressionBaitProjectile == null || suppressionBaitProjectile.Prefab == null)
            {
                return;
            }

            Vector3 origin = projectileOrigin != null ? projectileOrigin.position : transform.position;
            Vector2 direction = (Vector2)target.position - (Vector2)origin;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.left;
            }

            EnemyProjectile fired = SpawnProjectile(suppressionBaitProjectile, origin, direction.normalized, true);
            if (fired is not ParryBaitRewardProjectile bait)
            {
                return;
            }

            activeSuppressionBait = bait;
            bait.Destroyed += HandleSuppressionBaitDestroyed;
        }

        private void FireSuppressionDodgeVolley()
        {
            BossProjectileSettings settings = dodgeProjectile != null && dodgeProjectile.Prefab != null
                ? dodgeProjectile
                : projectile;
            if (settings == null || settings.Prefab == null)
            {
                return;
            }

            FireRadialVolley(settings, false);
        }

        private void HandleSuppressionBaitDestroyed(
            EnemyProjectile firedProjectile,
            EnemyProjectileDestroyReason reason,
            Vector3 _)
        {
            if (activeSuppressionBait == null || !ReferenceEquals(firedProjectile, activeSuppressionBait))
            {
                return;
            }

            activeSuppressionBait.Destroyed -= HandleSuppressionBaitDestroyed;
            activeSuppressionBait = null;
            suppressionBaitParried = reason == EnemyProjectileDestroyReason.Intercepted;
            suppressionBaitResolved = true;

            if (suppressionBaitParried)
            {
                BaitSuppressed?.Invoke();
            }
        }

        private void FireRadialVolley(BossProjectileSettings settings, bool interceptable)
        {
            int count = Mathf.Max(4, dodgeProjectileCount);
            Vector3 origin = projectileOrigin != null ? projectileOrigin.position : transform.position;
            float step = 360f / count;
            float startAngle = GetAngleToTarget(origin);

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + step * i;
                Vector2 direction = Quaternion.Euler(0f, 0f, angle) * Vector2.right;
                SpawnProjectile(settings, origin, direction, interceptable);
            }
        }

        private EnemyProjectile SpawnProjectile(
            BossProjectileSettings settings,
            Vector3 origin,
            Vector2 direction,
            bool interceptable)
        {
            if (settings == null || settings.Prefab == null)
            {
                return null;
            }

            EnemyProjectile fired = EnemyProjectile.Spawn(
                settings.Prefab,
                bullets,
                origin,
                direction.normalized,
                settings.BulletDamage,
                settings.ChargeSeconds,
                settings.Speed,
                settings.Lifetime,
                settings.Radius,
                Color.clear,
                settings.TrailSeconds,
                settings.TrailWidthMultiplier,
                false,
                0f,
                0f);

            if (fired == null)
            {
                return null;
            }

            fired.ConfigureChargeMotion(
                settings.ChargeDriftSpeed,
                settings.AimAtPlayerWhileCharging,
                settings.AimAtPlayerOnLaunch);
            fired.ConfigureInterceptable(interceptable);

            if (fired is ParryBaitRewardProjectile bait)
            {
                bait.ConfigureRewardOverrides(
                    suppressionRewardBulletCount,
                    suppressionRewardCircleRadius,
                    suppressionRewardLifetime);
            }

            return fired;
        }

        private BossProjectileSettings ResolveProjectileSettings()
        {
            if (mode == TutorialTrainingEnemyMode.DodgePractice
                && dodgeProjectile != null
                && dodgeProjectile.Prefab != null)
            {
                return dodgeProjectile;
            }

            return projectile;
        }

        private BossProjectileSettings ResolveDuelPatternProjectileSettings(DuelPattern pattern)
        {
            if (pattern == DuelPattern.DodgeRadial && dodgeProjectile != null && dodgeProjectile.Prefab != null)
            {
                return dodgeProjectile;
            }

            return projectile;
        }

        private void ResetDuelPattern()
        {
            duelOpeningPatternCompleted = false;
            duelPatternIndex = 0;
            duelBurstShotsRemaining = 0;
        }

        private DuelPattern GetCurrentDuelPattern()
        {
            if (!duelOpeningPatternCompleted)
            {
                return duelPatternIndex switch
                {
                    0 => DuelPattern.InterceptableBurst3,
                    1 => DuelPattern.InterceptableRadial,
                    2 => DuelPattern.InterceptableBurst4,
                    3 => DuelPattern.DodgeRadial,
                    _ => DuelPattern.InterceptableBurst3
                };
            }

            return duelPatternIndex switch
            {
                0 => DuelPattern.InterceptableBurst3,
                1 => DuelPattern.InterceptableRadial,
                2 => DuelPattern.InterceptableBurst4,
                3 => DuelPattern.InterceptableRadial,
                _ => DuelPattern.InterceptableBurst3
            };
        }

        private int GetDuelBurstShotCount(DuelPattern pattern)
        {
            return pattern == DuelPattern.InterceptableBurst4
                ? Mathf.Max(1, duelMainBurstShotCount)
                : Mathf.Max(1, duelOpeningBurstShotCount);
        }

        private void AdvanceDuelPattern()
        {
            duelBurstShotsRemaining = 0;
            duelPatternIndex++;

            if (!duelOpeningPatternCompleted && duelPatternIndex >= 4)
            {
                duelOpeningPatternCompleted = true;
                duelPatternIndex = 0;
                return;
            }

            if (duelOpeningPatternCompleted && duelPatternIndex >= 4)
            {
                duelPatternIndex = 0;
            }
        }

        private float GetAngleToTarget(Vector3 origin)
        {
            Vector2 direction = target != null
                ? (Vector2)target.position - (Vector2)origin
                : Vector2.right;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        }

        private float GetInitialFireDelaySeconds()
        {
            if (mode == TutorialTrainingEnemyMode.ForcedHitPractice
                || mode == TutorialTrainingEnemyMode.DodgePractice)
            {
                return Mathf.Max(0f, dodgeFireDelaySeconds);
            }

            if (mode == TutorialTrainingEnemyMode.Duel)
            {
                return Mathf.Max(0.35f, GetDuelPatternIntervalSeconds() * 0.5f);
            }

            if (ShouldFire())
            {
                return Mathf.Max(0.35f, GetFireIntervalSeconds() * 0.5f);
            }

            return float.PositiveInfinity;
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            Vector2 hitPosition = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;
            TryDealContactDamage(collision.collider, hitPosition);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            Vector2 hitPosition = collision.contactCount > 0
                ? collision.GetContact(0).point
                : transform.position;
            TryDealContactDamage(collision.collider, hitPosition);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDealContactDamage(other, other != null ? other.ClosestPoint(transform.position) : transform.position);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDealContactDamage(other, other != null ? other.ClosestPoint(transform.position) : transform.position);
        }

        private void TryDealContactDamage(Collider2D other, Vector2 hitPosition)
        {
            if (!CanAct()
                || mode != TutorialTrainingEnemyMode.Duel
                || contactDamage <= 0
                || Time.time < nextContactDamageAt
                || other == null)
            {
                return;
            }

            PlayerCombatController player = other.GetComponentInParent<PlayerCombatController>();
            if (player == null || player.Health == null || player.Health.IsDead)
            {
                return;
            }

            Vector2 hitDirection = (Vector2)player.transform.position - hitPosition;
            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = Vector2.right;
            }

            if (player.ReceiveAttack(contactDamage, hitPosition, hitDirection.normalized))
            {
                nextContactDamageAt = Time.time + Mathf.Max(0f, contactDamageCooldown);
            }
        }

        private void Face(Vector2 direction)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                Transform faceRoot = bodyRoot != null ? bodyRoot : transform;
                faceRoot.right = direction.normalized;
            }
        }

        private void StopBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        private void ConfigureLockOnIndicator()
        {
            if (lockOnIndicator == null)
            {
                return;
            }

            if (configuredLockOnIndicator == lockOnIndicator)
            {
                return;
            }

            configuredLockOnIndicator = lockOnIndicator;
            lockOnIndicator.enabled = false;
        }

        private void UpdateLockOnIndicator()
        {
            if (lockOnIndicator == null)
            {
                return;
            }

            bool visible = IsLockOnTarget();
            SetLockOnIndicatorVisible(visible);
            if (visible)
            {
                ApplyLockOnIndicatorTransform();
            }
        }

        private void ApplyLockOnIndicatorTransform()
        {
            Transform indicatorTransform = lockOnIndicator.transform;
            Transform center = bodyRoot != null ? bodyRoot : transform;
            indicatorTransform.position = center.position;
            indicatorTransform.rotation = Quaternion.identity;
        }

        private bool IsLockOnTarget()
        {
            PlayerCombatController player = PlayerCombatController.Active;
            if (!IsPlayerTargetable || player == null || player.IsExecuting || player.LockOnTarget == null)
            {
                return false;
            }

            if (player.LockOnTarget == health)
            {
                return true;
            }

            TutorialTrainingEnemy targetEnemy = player.LockOnTarget.GetComponent<TutorialTrainingEnemy>()
                ?? player.LockOnTarget.GetComponentInParent<TutorialTrainingEnemy>();
            return targetEnemy == this;
        }

        private void SetLockOnIndicatorVisible(bool visible)
        {
            if (lockOnIndicator == null)
            {
                return;
            }

            if (visible && !lockOnIndicator.gameObject.activeSelf)
            {
                lockOnIndicator.gameObject.SetActive(true);
            }

            lockOnIndicator.enabled = visible;
        }

        private void FlashBodyHitColor()
        {
            isBodyHitColorActive = true;
            bodyHitColorEndsAt = Time.time + BodyHitColorSeconds;
            isHitFlashActive = true;
            hitFlashEndsAt = Time.time + Mathf.Max(0f, hitFlashSeconds);
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

        private void UpdateHitFlash()
        {
            if (!isHitFlashActive || Time.time < hitFlashEndsAt)
            {
                return;
            }

            isHitFlashActive = false;
            ApplyBodyStateColor();
        }

        private void ApplyBodyStateColor()
        {
            if (bodyRenderers == null)
            {
                return;
            }

            hitFlashPropertyBlock ??= new MaterialPropertyBlock();
            float flashAmount = isHitFlashActive ? 1f : 0f;

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                SpriteRenderer renderer = bodyRenderers[i];
                if (renderer == null || renderer == lockOnIndicator)
                {
                    continue;
                }

                Color baseColor = bodyRendererBaseColors != null && i < bodyRendererBaseColors.Length
                    ? bodyRendererBaseColors[i]
                    : Color.white;
                renderer.color = isBodyHitColorActive ? BodyHitColor : baseColor;

                renderer.GetPropertyBlock(hitFlashPropertyBlock);
                hitFlashPropertyBlock.SetColor(FlashColorId, hitFlashColor);
                hitFlashPropertyBlock.SetFloat(FlashAmountId, flashAmount);
                renderer.SetPropertyBlock(hitFlashPropertyBlock);
            }
        }

        private void PlayEnemyHitVfx(Vector3 hitPosition, Vector2 hitDirection)
        {
            ProjectileVfx.PlayPrefab(
                effectData?.EnemyHitVfxPrefab,
                hitPosition,
                hitDirection,
                transform,
                followRotation: false);
        }

        private Color BodyHitColor => effectData != null ? effectData.EnemyBodyHitColor : bodyHitColor;
        private float BodyHitColorSeconds => effectData != null ? effectData.BodyHitColorSeconds : Mathf.Max(0f, bodyHitColorSeconds);

        private void HandleDied(Health _)
        {
            if (isActive && mode != TutorialTrainingEnemyMode.Duel)
            {
                health.Revive();
                bullets.Configure(maxBullets, true, BulletChangeSource.CombatStart);
                return;
            }

            isActive = false;
            StopBody();
            Defeated?.Invoke(this);
        }

        private bool ShouldFire()
        {
            return mode == TutorialTrainingEnemyMode.ParryPractice
                || mode == TutorialTrainingEnemyMode.ForcedHitPractice
                || mode == TutorialTrainingEnemyMode.DodgePractice && !dodgeVolleyFired
                || mode == TutorialTrainingEnemyMode.Duel;
        }

        private bool ShouldMove()
        {
            return mode == TutorialTrainingEnemyMode.Duel && moveSpeed > 0f;
        }

        private float GetFireIntervalSeconds()
        {
            return mode == TutorialTrainingEnemyMode.Duel
                ? duelFireIntervalSeconds
                : practiceFireIntervalSeconds;
        }

        private float GetDuelPatternIntervalSeconds()
        {
            float speedMultiplier = Mathf.Max(0.01f, duelSingleShotSpeedMultiplier);
            return Mathf.Max(0.1f, duelFireIntervalSeconds / speedMultiplier);
        }
    }
}
