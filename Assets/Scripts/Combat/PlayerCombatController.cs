using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Bootstrap;
using Week14.Enemy;
using Week14.Input;
using Week14.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Combat
{
    [RequireComponent(typeof(Health), typeof(BulletGauge))]
    public sealed class PlayerCombatController : MonoBehaviour
    {
        private const string BodyVisualName = "VisualRoot";
        private const string CombatCenterName = "Center Pivot";
        private const string ProjectileLockOnIndicatorName = "ProjectileLockOnIndicator";
        private const int ExecutionDimSortingOrder = 65;
        private const float LockOnAcquireViewportPadding = 0f;
        private const float LockOnReleaseViewportPadding = 0.08f;

        public static PlayerCombatController Active { get; private set; }
        public static bool IsExecutionCinematicActive => Active != null && Active.IsExecuting;

        [SerializeField] private PlayerCombatConfig config;
        [SerializeField] private PlayerVisualRig visual;
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform combatCenter;
        [SerializeField] private Transform leftGunOrigin;
        [SerializeField] private Transform leftGunFireOrigin;
        [SerializeField] private GunRecoilMotion leftGunRecoil;
        [SerializeField] private LayerMask enemyMask = ~0;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private AttackTimingOutline attackTimingOutline;
        [SerializeField] private ExecutionImageEffect executionImage;
        [SerializeField, Tooltip("마우스 위치를 따라다닐 패링 조준선 SpriteRenderer입니다. 씬/프리팹에 직접 만든 오브젝트를 연결합니다.")]
        private SpriteRenderer mouseParryReticleRenderer;
        [SerializeField] private MouseParryReticle mouseParryReticle;

        private Health health;
        private BulletGauge bullets;
        private CameraFollow2D cameraFollow;
        private Health lockOnTarget;
        private ExecutionTarget hoveredExecutionTarget;
        private EnemyProjectile projectileLockOnTarget;
        private Coroutine executionRoutine;
        private bool isExecuting;
        private float leftGunAimLockedUntil;
        private Vector2 leftGunLockedDirection;
        private LineRenderer projectileLockOnLine;
        private Transform executionFocusPoint;
        private SpriteRenderer executionDimRenderer;
        private Coroutine executionDimRoutine;
        private SpriteRenderer[] bodyRenderers;
        private Color[] bodyBaseColors;
        private float bodyHitColorEndsAt;
        private float nextEnemyBodyContactDamageAt;
        private float enemyBodyContactStaggerEndsAt;
#if ENABLE_INPUT_SYSTEM
        private PlayerInput playerInput;
#endif
        private static Material rangeIndicatorMaterial;
        private static Sprite executionDimSprite;

        public Health Health => health;
        public BulletGauge Bullets => bullets;
        public Health LockOnTarget => lockOnTarget;
        public ExecutionTarget HoveredExecutionTarget => hoveredExecutionTarget;
        public bool IsExecuting => isExecuting;
        public PlayerCombatConfig Config => config;
        public bool CanMove => CanAct && !IsBodyContactStaggered;
        public bool IsBodyContactStaggered => Time.time < enemyBodyContactStaggerEndsAt;
        private bool CanAct => !GameModalState.BlocksGameplayInput && !isExecuting && !health.IsDead;

        private void Awake()
        {
            health = GetComponent<Health>();
            bullets = GetComponent<BulletGauge>();

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

#if ENABLE_INPUT_SYSTEM
            BindPlayerInput();
#endif
            ResolveRigReferences();
            ResolveMouseParryReticleReference();
            CacheBodyRenderers();

            if (cameraFollow == null && Camera.main != null)
            {
                cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
            }
        }

#if ENABLE_INPUT_SYSTEM
        private void BindPlayerInput()
        {
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }

            if (playerInput == null)
            {
                playerInput = GetComponentInParent<PlayerInput>();
            }

            if (playerInput == null)
            {
                playerInput = GetComponentInChildren<PlayerInput>();
            }

            GameInput.Bind(playerInput);
        }
#endif

        private void OnEnable()
        {
            Active = this;
#if ENABLE_INPUT_SYSTEM
            BindPlayerInput();
#endif
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            GameInput.Unbind(playerInput);
#endif

            if (Active == this)
            {
                Active = null;
            }

            CameraFollow2D activeCamera = GetCameraFollow();
            if (activeCamera != null)
            {
                activeCamera.EndCinematicFocus();
                activeCamera.SetFocusTarget(null);
            }

            HideAttackTimingOutline();
            SetMouseParryReticleVisible(false);
            SetProjectileLockOnIndicatorVisible(false);
            StopExecutionShotDim();
            executionImage?.Stop();
        }

        private void Start()
        {
            if (config == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatController)} requires {nameof(PlayerCombatConfig)}.", this);
                return;
            }

            bullets.Configure(config.MaxBullets, true);
        }

        public void SetConfig(PlayerCombatConfig nextConfig)
        {
            config = nextConfig;
        }

        private void Update()
        {
            UpdateCursorPresentation();

            if (health.IsDead)
            {
                StopBody();
                SetMouseParryReticleVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetLockOnTarget(null);
                SetHoveredExecutionTarget(null);
                HideAttackTimingOutline();
                return;
            }

            if (isExecuting)
            {
                StopBody();
                SetMouseParryReticleVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetHoveredExecutionTarget(null);
                HideAttackTimingOutline();
                return;
            }

            if (config == null)
            {
                SetMouseParryReticleVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetHoveredExecutionTarget(null);
                HideAttackTimingOutline();
                return;
            }

            ClearInvalidLockOnTarget();
            UpdateLockOnTarget();
            UpdateHoveredExecutionTarget();
            RotateToAim();
            UpdateMouseParryReticle();
            UpdateProjectileLockOnTarget();
            UpdateMouseParryReticleThreat();
            UpdateProjectileLockOnIndicator();
            UpdateBodyColor();

            if (GameInput.LeftAttackDown && CanAct)
            {
                if (!TryBeginExecution())
                {
                    TryShootEnemy();
                }
            }

            if (GameInput.RightAttackDown && CanAct)
            {
                TryParryProjectile();
            }

            UpdateAttackTimingOutline();
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            Vector2 hitPosition = collision.contactCount > 0 ? collision.GetContact(0).point : collision.collider.ClosestPoint(transform.position);
            TryReceiveEnemyBodyContact(collision.collider, hitPosition);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            if (collision == null || collision.collider == null)
            {
                return;
            }

            Vector2 hitPosition = collision.contactCount > 0 ? collision.GetContact(0).point : collision.collider.ClosestPoint(transform.position);
            TryReceiveEnemyBodyContact(collision.collider, hitPosition);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryReceiveEnemyBodyContact(other, other != null ? other.ClosestPoint(transform.position) : transform.position);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryReceiveEnemyBodyContact(other, other != null ? other.ClosestPoint(transform.position) : transform.position);
        }

        public bool ReceiveAttack(int bulletDamage)
        {
            return ReceiveAttack(bulletDamage, transform.position, Vector2.right);
        }

        private void TryReceiveEnemyBodyContact(Collider2D other, Vector2 hitPosition)
        {
            if (other == null || config == null || Time.time < nextEnemyBodyContactDamageAt)
            {
                return;
            }

            if (!IsEnemyBodyContact(other))
            {
                return;
            }

            Vector2 hitDirection = (Vector2)transform.position - hitPosition;
            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = (Vector2)transform.position - (Vector2)other.transform.position;
            }

            if (hitDirection.sqrMagnitude <= 0.0001f)
            {
                hitDirection = Vector2.right;
            }

            if (ReceiveAttack(config.EnemyBodyContactBulletDamage, hitPosition, hitDirection.normalized))
            {
                ApplyEnemyBodyContactKnockback(hitDirection.normalized);
                nextEnemyBodyContactDamageAt = Time.time + config.EnemyBodyContactCooldownSeconds;
            }
        }

        private void ApplyEnemyBodyContactKnockback(Vector2 direction)
        {
            if (body == null || config == null)
            {
                return;
            }

            float staggerSeconds = Mathf.Max(0f, config.EnemyBodyContactStaggerSeconds);
            enemyBodyContactStaggerEndsAt = Mathf.Max(enemyBodyContactStaggerEndsAt, Time.time + staggerSeconds);
            body.linearVelocity = direction * Mathf.Max(0f, config.EnemyBodyContactKnockbackSpeed);
        }

        private bool IsEnemyBodyContact(Collider2D other)
        {
            if (other.GetComponentInParent<EnemyProjectile>() != null)
            {
                return false;
            }

            Drone drone = other.GetComponentInParent<Drone>();
            if (drone != null)
            {
                return !drone.SuppressesBodyContactDamage;
            }

            return other.GetComponentInParent<EnemyAI>() != null
                || other.GetComponentInParent<BossAI>() != null;
        }

        public bool ReceiveAttack(int bulletDamage, Vector3 hitPosition, Vector2 hitDirection)
        {
            if (isExecuting || health.IsDead || config == null)
            {
                return false;
            }

            if (bullets == null || bullets.IsEmpty)
            {
                health.Kill();
            }
            else
            {
                bullets.TrySpend(Mathf.Clamp(bulletDamage, 1, bullets.CurrentBullets), BulletChangeSource.Hit);
            }
            FlashBodyHitColor();
            ProjectileVfx.PlayPlayerAttackImpact(
                hitPosition,
                hitDirection,
                config.EnemyProjectileColor,
                config.PlayerHitSparkCount,
                config.PlayerHitBackSparkCount,
                config.PlayerHitFlameCount,
                config.PlayerHitEffectScale);
            GetCameraFollow()?.PlayImpact(hitDirection, 0.16f, 0.18f, 0.1f);
            return true;
        }

        private void FlashBodyHitColor()
        {
            if (config == null)
            {
                return;
            }

            bodyHitColorEndsAt = Time.time + config.BodyHitColorSeconds;
            UpdateBodyColor(true);
        }

        private void UpdateBodyColor(bool force = false)
        {
            if (bodyRenderers == null || bodyRenderers.Length == 0 || config == null)
            {
                return;
            }

            Color? overrideColor = null;
            if (Time.time < bodyHitColorEndsAt)
            {
                overrideColor = config.PlayerBodyHitColor;
            }
            else if (bullets != null && bullets.IsEmpty)
            {
                overrideColor = config.PlayerBodyBulletEmptyColor;
            }

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] == null)
                {
                    continue;
                }

                Color targetColor = overrideColor ?? GetBodyBaseColor(i);
                targetColor.a = bodyRenderers[i].color.a;
                if (force || bodyRenderers[i].color != targetColor)
                {
                    bodyRenderers[i].color = targetColor;
                }
            }
        }

        private Color GetBodyBaseColor(int index)
        {
            return bodyBaseColors != null && index >= 0 && index < bodyBaseColors.Length
                ? bodyBaseColors[index]
                : Color.white;
        }

        public void PlayParryImpact(Vector3 position)
        {
            PlayParryImpact(position, Vector2.right);
        }

        public void PlayParryImpact(Vector3 position, Vector2 direction)
        {
            if (config == null)
            {
                return;
            }

            bullets.Restore(config.ParryBulletRecovery, BulletChangeSource.Parry);
            ProjectileVfx.PlayParry(
                position,
                direction,
                config.ParrySparkColor,
                config.ParryRingColor,
                config.ParryRingGlitterColor,
                config.ParrySparkCount,
                config.ParryRingGlitterCount,
                config.ParrySparkSeconds,
                config.ParryRingSeconds,
                config.ParryRingGlitterSeconds,
                config.ParryFlameCount,
                config.ParryEffectScale);
            GetCameraFollow()?.PlayImpact(direction, 0.32f, 0.24f, 0.22f);
        }

        private bool TryShootEnemy()
        {
            if (config.ProjectilePrefab == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatConfig)} requires {nameof(PlayerCombatConfig.ProjectilePrefab)}.", this);
                return false;
            }

            int dynamicDamage = CalculateAttackBulletDamage();

            if (bullets == null || !bullets.TrySpend(config.LeftAttackBulletCost, BulletChangeSource.Attack))
            {
                return false;
            }

            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 direction = AimGunAndGetDirection(leftGunOrigin, GetAimDirection(leftGunOrigin));
            LockLeftGunAim(direction);

            PlayerProjectile projectile = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                fireOrigin.position,
                direction,
                this,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                dynamicDamage,
                AttackEffectColor,
                true);

            if (projectile == null)
            {
                bullets.Restore(config.LeftAttackBulletCost, BulletChangeSource.Attack);
                return false;
            }

            ProjectileVfx.PlayMuzzleFlash(fireOrigin.position, direction, AttackEffectColor, 0.9f);
            leftGunRecoil?.Play(direction);
            visual?.PlayShot();
            return true;
        }

        private void UpdateAttackTimingOutline()
        {
            HideAttackTimingOutline();
        }

        private void HideAttackTimingOutline()
        {
            if (attackTimingOutline != null)
            {
                attackTimingOutline.Hide();
            }
        }

        private void EnsureAttackTimingOutline()
        {
            if (attackTimingOutline == null)
            {
                attackTimingOutline = GetComponent<AttackTimingOutline>();
            }

            if (attackTimingOutline == null)
            {
                attackTimingOutline = gameObject.AddComponent<AttackTimingOutline>();
            }

            attackTimingOutline.SetTarget(bodyRoot);
        }

        private bool TryBeginExecution()
        {
            ExecutionTarget executionTarget = FindHoveredExecutionTarget();
            if (executionTarget == null)
            {
                return false;
            }

            if (executionRoutine != null)
            {
                StopCoroutine(executionRoutine);
            }

            executionRoutine = StartCoroutine(ExecuteTarget(executionTarget));
            return true;
        }

        private void UpdateHoveredExecutionTarget()
        {
            SetHoveredExecutionTarget(FindHoveredExecutionTarget());
        }

        private ExecutionTarget FindHoveredExecutionTarget()
        {
            Vector2 executionCenter = CombatCenterOrigin.position;
            float executionRange = config != null ? config.ExecutionRange : 0f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(executionCenter, executionRange, enemyMask);
            ExecutionTarget bestTarget = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                ExecutionTarget target = hits[i].GetComponentInParent<ExecutionTarget>();
                ChooseCloserExecutionTarget(target, executionCenter, ref bestTarget, ref bestDistance);
            }

            ExecutionTarget[] executionTargets = Object.FindObjectsByType<ExecutionTarget>(FindObjectsSortMode.None);
            for (int i = 0; i < executionTargets.Length; i++)
            {
                ChooseCloserExecutionTarget(executionTargets[i], executionCenter, ref bestTarget, ref bestDistance);
            }

            return bestTarget;
        }

        private void ChooseCloserExecutionTarget(
            ExecutionTarget target,
            Vector2 executionCenter,
            ref ExecutionTarget bestTarget,
            ref float bestDistance)
        {
            if (target == null || !target.CanExecute(transform))
            {
                return;
            }

            float distance = Vector2.Distance(executionCenter, target.transform.position);
            float executionRange = config != null ? config.ExecutionRange : 0f;
            if (distance > executionRange || distance >= bestDistance)
            {
                return;
            }

            bestTarget = target;
            bestDistance = distance;
        }

        private IEnumerator ExecuteTarget(ExecutionTarget executionTarget)
        {
            isExecuting = true;
            if (executionTarget == null || !executionTarget.BeginExecution(this))
            {
                FinishExecution();
                yield break;
            }

            StopBody();
            float flourishSeconds = Mathf.Max(0f, config.ExecutionFlourishDelaySeconds)
                + Mathf.Max(0, config.ExecutionFlourishShotCount) * Mathf.Max(0.01f, config.ExecutionFlourishShotInterval);
            executionImage?.Play(flourishSeconds + config.ExecutionAimSeconds + config.ExecutionShotDelaySeconds + config.ExecutionKillDelaySeconds);

            Health targetHealth = executionTarget.GetComponent<Health>();
            if (targetHealth != null)
            {
                SetLockOnTarget(targetHealth);
            }

            Vector2 targetPosition = executionTarget.transform.position;
            Vector2 playerPosition = transform.position;
            Vector2 standDirection = playerPosition - targetPosition;
            if (standDirection.sqrMagnitude <= 0.0001f)
            {
                standDirection = -Vector2.right;
            }
            else
            {
                standDirection.Normalize();
            }

            UpdateExecutionFocusPoint(transform.position, executionTarget.transform.position);
            CameraFollow2D activeCamera = GetCameraFollow();
            activeCamera?.BeginCinematicFocus(
                executionFocusPoint != null ? executionFocusPoint : executionTarget.transform,
                config.ExecutionCameraFocusWeight,
                config.ExecutionCameraZoomMultiplier);
            activeCamera?.PlayImpact(standDirection, 0.08f, 0.14f, 0.12f);

            Transform leftFireOrigin = GetLeftFireOrigin();
            Vector2 aimDirection = targetPosition - (Vector2)leftGunOrigin.position;
            AimExecutionPose(aimDirection);
            yield return new WaitForSeconds(config.ExecutionFlourishDelaySeconds);
            yield return RunExecutionFlourish(executionTarget, aimDirection);
            leftGunRecoil?.PlayKick(aimDirection, config.ExecutionGunKickSeconds);

            yield return new WaitForSeconds(config.ExecutionAimSeconds);
            if (executionTarget == null)
            {
                FinishExecution();
                yield break;
            }

            leftFireOrigin = GetLeftFireOrigin();
            aimDirection = (Vector2)executionTarget.transform.position - (Vector2)leftGunOrigin.position;
            AimExecutionPose(aimDirection);

            yield return new WaitForSeconds(config.ExecutionShotDelaySeconds);
            if (executionTarget == null)
            {
                FinishExecution();
                yield break;
            }

            leftFireOrigin = GetLeftFireOrigin();
            aimDirection = AimGunAndGetDirection(leftGunOrigin, (Vector2)executionTarget.transform.position - (Vector2)leftGunOrigin.position);
            LockLeftGunAim(aimDirection);
            UpdateExecutionFocusPoint(transform.position, executionTarget.transform.position);
            leftGunRecoil?.ReturnToBase(config.ExecutionGunReturnSeconds);
            visual?.PlayShot();
            PlayExecutionShotDim();

            PlayerProjectile executionShot = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                leftFireOrigin.position,
                aimDirection,
                this,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                0,
                config.ExecutionShotColor,
                false);
            if (executionShot != null)
            {
                Color muzzleFlashColor = Color.Lerp(config.ExecutionShotColor, Color.white, 0.65f);
                muzzleFlashColor.a = 1f;
                ProjectileVfx.PlayMuzzleFlash(leftFireOrigin.position, aimDirection, muzzleFlashColor, 1.55f);
                activeCamera?.PlayImpact(aimDirection, 0.12f, 0.14f, 0.08f);
            }

            yield return new WaitForSeconds(config.ExecutionKillDelaySeconds);
            if (executionTarget == null)
            {
                FinishExecution();
                yield break;
            }

            Vector3 impactPosition = executionTarget.transform.position;
            executionTarget.RecoverExecutorBullets(this);
            ExecutionVfx.PlayImpact(
                impactPosition,
                aimDirection,
                config.ExecutionImpactColor,
                config.ExecutionImpactParticleCount,
                config.ExecutionImpactParticleSeconds);
            ExecutionVfx.PlayAbsorb(
                impactPosition,
                transform,
                config.ExecutionAbsorbColor,
                config.ExecutionAbsorbParticleCount,
                config.ExecutionImpactParticleSeconds);
            GetCameraFollow()?.PlayImpact(aimDirection, 0.18f, 0.18f, 0.1f);
            SetLockOnTarget(null);
            SetHoveredExecutionTarget(null);

            yield return new WaitForSeconds(config.ExecutionFinishSeconds);

            if (executionTarget != null)
            {
                BossAI boss = executionTarget.GetComponentInParent<BossAI>();
                if (boss != null)
                {
                    EnemyProjectile.DestroyAllActive();
                    if (boss.TryConsumeLife())
                    {
                        executionTarget.CompleteExecutionWithoutKill();
                    }
                    else
                    {
                        executionTarget.CompleteExecution(this, false);
                        executionTarget.DestroyExecutedTarget();
                    }
                }
                else
                {
                    executionTarget.CompleteExecution(this, false);
                    executionTarget.DestroyExecutedTarget();
                }
            }

            FinishExecution();
        }

        private IEnumerator RunExecutionFlourish(ExecutionTarget executionTarget, Vector2 aimDirection)
        {
            int shotCount = Mathf.Max(0, config.ExecutionFlourishShotCount);
            float interval = Mathf.Max(0.01f, config.ExecutionFlourishShotInterval);
            for (int i = 0; i < shotCount; i++)
            {
                visual?.PlayShot();
                leftGunRecoil?.Play(aimDirection);
                FireExecutionFlourishShot(executionTarget, aimDirection);
                yield return new WaitForSeconds(interval);
            }
        }

        private void FireExecutionFlourishShot(ExecutionTarget executionTarget, Vector2 aimDirection)
        {
            Transform fireOrigin = GetLeftFireOrigin();
            if (fireOrigin == null)
            {
                return;
            }

            PlayerProjectile flourishShot = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                fireOrigin.position,
                aimDirection,
                this,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                0,
                config.ExecutionShotColor,
                false);
            if (flourishShot != null)
            {
                Color muzzleFlashColor = Color.Lerp(config.ExecutionShotColor, Color.white, 0.65f);
                muzzleFlashColor.a = 1f;
                ProjectileVfx.PlayMuzzleFlash(fireOrigin.position, aimDirection, muzzleFlashColor, 1.55f);
            }

            if (executionTarget != null)
            {
                executionTarget.PlayHitReaction(executionTarget.transform.position, aimDirection, config.ExecutionShotColor);
            }
        }

        private void FinishExecution()
        {
            isExecuting = false;
            executionRoutine = null;
            leftGunRecoil?.ReturnToBase(config != null ? config.ExecutionGunReturnSeconds : 0.045f);
            GetCameraFollow()?.EndCinematicFocus();
            ClearInvalidLockOnTarget();
            UpdateHoveredExecutionTarget();
            executionImage?.Stop();
        }

        private void UpdateExecutionFocusPoint(Vector3 playerPosition, Vector3 targetPosition)
        {
            if (executionFocusPoint == null)
            {
                GameObject focusObject = new GameObject("ExecutionCameraFocus")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                executionFocusPoint = focusObject.transform;
            }

            Vector3 focusPosition = (playerPosition + targetPosition) * 0.5f;
            focusPosition.z = playerPosition.z;
            executionFocusPoint.position = focusPosition;
        }

        private void PlayExecutionShotDim()
        {
            if (config == null || config.ExecutionShotDimSeconds <= 0f || config.ExecutionShotDimAlpha <= 0f)
            {
                return;
            }

            Camera targetCamera = Camera.main;
            if (targetCamera == null)
            {
                return;
            }

            if (executionDimRoutine != null)
            {
                StopCoroutine(executionDimRoutine);
            }

            executionDimRoutine = StartCoroutine(PlayExecutionShotDimRoutine(targetCamera));
        }

        private IEnumerator PlayExecutionShotDimRoutine(Camera targetCamera)
        {
            SpriteRenderer renderer = EnsureExecutionDimRenderer(targetCamera);
            if (renderer == null)
            {
                yield break;
            }

            float duration = Mathf.Max(0.01f, config.ExecutionShotDimSeconds);
            float maxAlpha = Mathf.Clamp01(config.ExecutionShotDimAlpha);
            renderer.enabled = true;

            float elapsed = 0f;
            while (elapsed < duration && targetCamera != null)
            {
                elapsed += Time.deltaTime;
                UpdateExecutionDimTransform(targetCamera, renderer.transform);
                float t = Mathf.Clamp01(elapsed / duration);
                renderer.color = new Color(0f, 0f, 0f, maxAlpha * (1f - t));
                yield return null;
            }

            renderer.enabled = false;
            executionDimRoutine = null;
        }

        private SpriteRenderer EnsureExecutionDimRenderer(Camera targetCamera)
        {
            if (targetCamera == null)
            {
                return null;
            }

            if (executionDimRenderer == null)
            {
                GameObject dimObject = new GameObject("ExecutionShotDim")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                executionDimRenderer = dimObject.AddComponent<SpriteRenderer>();
                executionDimRenderer.sprite = GetExecutionDimSprite();
                executionDimRenderer.sortingOrder = ExecutionDimSortingOrder;
                executionDimRenderer.enabled = false;
            }

            executionDimRenderer.transform.SetParent(targetCamera.transform, false);
            UpdateExecutionDimTransform(targetCamera, executionDimRenderer.transform);
            return executionDimRenderer;
        }

        private static void UpdateExecutionDimTransform(Camera targetCamera, Transform dimTransform)
        {
            if (targetCamera == null || dimTransform == null)
            {
                return;
            }

            float height = targetCamera.orthographic ? targetCamera.orthographicSize * 2f : 50f;
            float width = height * targetCamera.aspect;
            dimTransform.localPosition = new Vector3(0f, 0f, targetCamera.nearClipPlane + 0.05f);
            dimTransform.localRotation = Quaternion.identity;
            dimTransform.localScale = new Vector3(width, height, 1f);
        }

        private static Sprite GetExecutionDimSprite()
        {
            if (executionDimSprite != null)
            {
                return executionDimSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            executionDimSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            return executionDimSprite;
        }

        private void StopExecutionShotDim()
        {
            if (executionDimRoutine != null)
            {
                StopCoroutine(executionDimRoutine);
                executionDimRoutine = null;
            }

            if (executionDimRenderer != null)
            {
                executionDimRenderer.enabled = false;
            }
        }

        private bool TryParryProjectile()
        {
            if (config.ProjectilePrefab == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatConfig)} requires {nameof(PlayerCombatConfig.ProjectilePrefab)}.", this);
                return false;
            }

            EnemyProjectile target = projectileLockOnTarget != null
                    && projectileLockOnTarget.CanBeIntercepted
                    && IsProjectileInMouseParryRange(projectileLockOnTarget)
                ? projectileLockOnTarget
                : FindClosestInterceptTarget();
            if (target == null)
            {
                return false;
            }

            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 firePosition = fireOrigin != null ? fireOrigin.position : transform.position;
            Vector2 direction = (Vector2)target.transform.position - firePosition;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = target.IncomingDirection.sqrMagnitude > 0.0001f
                    ? -target.IncomingDirection
                    : Vector2.right;
            }

            if (!target.TryReserveIntercept())
            {
                return false;
            }

            PlayerProjectile parryShot = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                firePosition,
                direction.normalized,
                this,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                0,
                ParryEffectColor,
                false,
                true);
            if (parryShot == null)
            {
                target.CancelInterceptReservation();
                return false;
            }

            parryShot.SetForcedParryTarget(target);
            ProjectileVfx.PlayMuzzleFlash(firePosition, direction.normalized, ParryEffectColor, 1f);
            visual?.PlayIntercept();
            return true;
        }

        private int CalculateAttackBulletDamage()
        {
            if (bullets == null || config == null)
            {
                return config != null ? config.AttackBulletDamage : 1;
            }

            return config.GetAttackDamageForRemainingBullets(bullets.CurrentBullets);
        }

        private EnemyProjectile FindClosestInterceptTarget()
        {
            Vector2 cursorPosition = GetParryCursorWorldPosition();
            EnemyProjectile bestTarget = null;
            float bestDistance = float.PositiveInfinity;
            IReadOnlyList<EnemyProjectile> activeProjectiles = EnemyProjectile.ActiveProjectiles;

            for (int i = 0; i < activeProjectiles.Count; i++)
            {
                EnemyProjectile source = activeProjectiles[i];
                if (source == null || !source.CanBeIntercepted)
                {
                    continue;
                }

                if (!IsProjectileInMouseParryRange(source))
                {
                    continue;
                }

                Vector2 sourcePosition = source.transform.position;
                float distance = Vector2.Distance(cursorPosition, sourcePosition);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestTarget = source;
                bestDistance = distance;
            }

            return bestTarget;
        }

        private bool IsProjectileInMouseParryRange(EnemyProjectile projectile)
        {
            if (projectile == null || !projectile.CanBeIntercepted)
            {
                return false;
            }

            return IsPointInsideMouseParryDiamond(projectile.transform.position);
        }

        private Transform CombatCenterOrigin => combatCenter != null ? combatCenter : (bodyRoot != null ? bodyRoot : transform);

        private void UpdateLockOnTarget()
        {
            SetLockOnTarget(FindNearestLockOnTarget());
        }

        private Health FindNearestLockOnTarget()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return null;
            }

            Vector2 mousePosition = GetMouseWorldPosition();
            Health bestTarget = null;
            float bestDistance = float.PositiveInfinity;

            Health[] allTargets = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < allTargets.Length; i++)
            {
                Health targetHealth = allTargets[i];
                ChooseCloserLockOnTarget(targetHealth, camera, mousePosition, ref bestTarget, ref bestDistance);
            }

            return bestTarget;
        }

        private void ChooseCloserLockOnTarget(
            Health targetHealth,
            Camera camera,
            Vector2 mousePosition,
            ref Health bestTarget,
            ref float bestDistance)
        {
            if (!IsValidLockOnTargetInCamera(targetHealth, camera)
                || !CanKeepLockOnTarget(targetHealth, camera))
            {
                return;
            }

            Vector2 targetPoint = GetLockOnMouseComparePoint(targetHealth, mousePosition);
            float distance = Vector2.Distance(mousePosition, targetPoint);
            if (distance >= bestDistance)
            {
                return;
            }

            bestTarget = targetHealth;
            bestDistance = distance;
        }

        private static Vector2 GetLockOnMouseComparePoint(
            Health targetHealth,
            Vector2 mousePosition)
        {
            Collider2D[] colliders = targetHealth != null ? targetHealth.GetComponentsInChildren<Collider2D>() : null;
            if (colliders == null || colliders.Length == 0)
            {
                return targetHealth != null ? targetHealth.transform.position : mousePosition;
            }

            Vector2 bestPoint = targetHealth.transform.position;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 point = collider.ClosestPoint(mousePosition);
                float distance = Vector2.Distance(mousePosition, point);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestPoint = point;
                bestDistance = distance;
            }

            return bestPoint;
        }

        private void ClearInvalidLockOnTarget()
        {
            if (lockOnTarget != null
                && !lockOnTarget.IsDead
                && CanKeepLockOnTarget(lockOnTarget, Camera.main))
            {
                return;
            }

            SetLockOnTarget(null);
        }

        private void SetLockOnTarget(Health nextTarget)
        {
            if (lockOnTarget == nextTarget)
            {
                return;
            }

            lockOnTarget = nextTarget;
            CameraFollow2D activeCamera = GetCameraFollow();
            if (activeCamera != null)
            {
                activeCamera.SetFocusTarget(lockOnTarget != null ? lockOnTarget.transform : null);
            }
        }

        private CameraFollow2D GetCameraFollow()
        {
            if (cameraFollow == null && Camera.main != null)
            {
                cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
            }

            return cameraFollow;
        }

        private bool IsValidLockOnTarget(Health targetHealth)
        {
            return targetHealth != null
                && targetHealth != health
                && !targetHealth.IsDead
                && (targetHealth.GetComponent<Week14.Enemy.EnemyAI>() != null
                    || targetHealth.GetComponentInParent<Week14.Enemy.EnemyAI>() != null
                    || targetHealth.GetComponent<Week14.Enemy.BossAI>() != null
                    || targetHealth.GetComponentInParent<Week14.Enemy.BossAI>() != null
                    || targetHealth.GetComponent<Week14.Enemy.Drone>() != null
                    || targetHealth.GetComponentInParent<Week14.Enemy.Drone>() != null);
        }

        private bool IsValidLockOnTargetInCamera(Health targetHealth, Camera camera)
        {
            return IsValidLockOnTargetInCamera(targetHealth, camera, LockOnAcquireViewportPadding);
        }

        private bool IsValidLockOnTargetInCamera(Health targetHealth, Camera camera, float viewportPadding)
        {
            return IsValidLockOnTarget(targetHealth)
                && IsTargetVisibleInCamera(targetHealth, camera, viewportPadding);
        }

        private bool CanKeepLockOnTarget(Health targetHealth, Camera camera)
        {
            return IsValidLockOnTarget(targetHealth)
                && CanCameraContainPair(camera, transform.position, targetHealth.transform.position, LockOnReleaseViewportPadding);
        }

        private static bool CanCameraContainPair(Camera camera, Vector3 firstPosition, Vector3 secondPosition, float viewportPadding)
        {
            if (camera == null || !camera.orthographic)
            {
                return false;
            }

            float padding = Mathf.Clamp01(viewportPadding);
            float availableHalfHeight = camera.orthographicSize * (1f - padding);
            float availableHalfWidth = availableHalfHeight * camera.aspect;
            Vector2 delta = secondPosition - firstPosition;
            return Mathf.Abs(delta.x) * 0.5f <= availableHalfWidth
                && Mathf.Abs(delta.y) * 0.5f <= availableHalfHeight;
        }

        private static bool IsTargetVisibleInCamera(Health targetHealth, Camera camera, float viewportPadding)
        {
            if (targetHealth == null || camera == null)
            {
                return false;
            }

            if (IsWorldPointInCamera(camera, targetHealth.transform.position, viewportPadding))
            {
                return true;
            }

            Collider2D[] colliders = targetHealth.GetComponentsInChildren<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (IsBoundsVisibleInCamera(camera, collider.bounds, viewportPadding))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsBoundsVisibleInCamera(Camera camera, Bounds bounds, float viewportPadding)
        {
            return IsWorldPointInCamera(camera, bounds.center, viewportPadding)
                || IsWorldPointInCamera(camera, new Vector3(bounds.min.x, bounds.min.y, bounds.center.z), viewportPadding)
                || IsWorldPointInCamera(camera, new Vector3(bounds.min.x, bounds.max.y, bounds.center.z), viewportPadding)
                || IsWorldPointInCamera(camera, new Vector3(bounds.max.x, bounds.min.y, bounds.center.z), viewportPadding)
                || IsWorldPointInCamera(camera, new Vector3(bounds.max.x, bounds.max.y, bounds.center.z), viewportPadding);
        }

        private static bool IsWorldPointInCamera(Camera camera, Vector3 worldPoint, float viewportPadding)
        {
            Vector3 viewportPoint = camera.WorldToViewportPoint(worldPoint);
            float padding = Mathf.Max(0f, viewportPadding);
            return viewportPoint.z >= camera.nearClipPlane
                && viewportPoint.z <= camera.farClipPlane
                && viewportPoint.x >= -padding
                && viewportPoint.x <= 1f + padding
                && viewportPoint.y >= -padding
                && viewportPoint.y <= 1f + padding;
        }

        private void RotateToAim()
        {
            Vector2 bodyDirection = GetAimDirection(CombatCenterOrigin);
            visual?.SetBodyAimDirection(bodyDirection);

            Vector2 leftDirection = lockOnTarget == null && Time.time <= leftGunAimLockedUntil
                ? leftGunLockedDirection
                : GetAimDirection(leftGunOrigin);
            visual?.SetLeftArmAimDirection(leftDirection);
        }

        private void LockLeftGunAim(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            leftGunLockedDirection = direction.normalized;
            leftGunAimLockedUntil = Time.time + GunAimHoldSeconds;
            visual?.SetLeftArmAimDirection(leftGunLockedDirection);
        }

        private void AimExecutionPose(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 normalized = direction.normalized;
            visual?.SetBodyAimDirection(normalized);
            visual?.SetLeftArmAimDirection(normalized);
        }

        private Vector2 AimGunAndGetDirection(Transform gun, Vector2 desiredDirection)
        {
            Vector2 normalized = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection.normalized : Vector2.right;
            if (gun == leftGunOrigin)
            {
                visual?.SetLeftArmAimDirection(normalized);
            }

            return normalized;
        }

        private void UpdateProjectileLockOnTarget()
        {
            projectileLockOnTarget = FindClosestInterceptTarget();
        }

        private void UpdateMouseParryReticle()
        {
            if (!CanShowMouseParryReticle())
            {
                SetMouseParryReticleVisible(false);
                return;
            }

            if (mouseParryReticleRenderer == null)
            {
                return;
            }

            mouseParryReticleRenderer.enabled = true;
            ResolveMouseParryReticleReference();
            mouseParryReticle?.SetVisible(true);

            Vector2 cursorPosition = GetParryCursorWorldPosition();
            Transform reticleTransform = mouseParryReticleRenderer.transform;
            reticleTransform.position = new Vector3(cursorPosition.x, cursorPosition.y, reticleTransform.position.z);
        }

        private void UpdateMouseParryReticleThreat()
        {
            if (mouseParryReticle != null)
            {
                mouseParryReticle.SetThreatened(projectileLockOnTarget != null && IsProjectileInMouseParryRange(projectileLockOnTarget));
            }
        }

        private bool CanShowMouseParryReticle()
        {
            return config != null
                && !GameModalState.BlocksGameplayInput
                && !isExecuting
                && health != null
                && !health.IsDead;
        }

        private bool IsPointInsideMouseParryDiamond(Vector2 worldPoint)
        {
            if (mouseParryReticleRenderer == null || mouseParryReticleRenderer.sprite == null)
            {
                return false;
            }

            Bounds spriteBounds = mouseParryReticleRenderer.sprite.bounds;
            Vector2 center = spriteBounds.center;
            Vector2 halfSize = spriteBounds.extents;
            if (halfSize.x <= 0.0001f || halfSize.y <= 0.0001f)
            {
                return false;
            }

            Vector2 local = mouseParryReticleRenderer.transform.InverseTransformPoint(worldPoint) - (Vector3)center;
            return Mathf.Abs(local.x) / halfSize.x + Mathf.Abs(local.y) / halfSize.y <= 1f;
        }

        private bool TryGetMouseParryDiamondCorners(out Vector3 top, out Vector3 right, out Vector3 bottom, out Vector3 left)
        {
            top = right = bottom = left = Vector3.zero;
            if (mouseParryReticleRenderer == null || mouseParryReticleRenderer.sprite == null)
            {
                return false;
            }

            Bounds spriteBounds = mouseParryReticleRenderer.sprite.bounds;
            Vector3 center = spriteBounds.center;
            Vector3 extents = spriteBounds.extents;
            if (extents.x <= 0.0001f || extents.y <= 0.0001f)
            {
                return false;
            }

            Transform reticleTransform = mouseParryReticleRenderer.transform;
            top = reticleTransform.TransformPoint(center + Vector3.up * extents.y);
            right = reticleTransform.TransformPoint(center + Vector3.right * extents.x);
            bottom = reticleTransform.TransformPoint(center + Vector3.down * extents.y);
            left = reticleTransform.TransformPoint(center + Vector3.left * extents.x);
            return true;
        }

        private void UpdateCursorPresentation()
        {
            bool gameplayMouseActive = config != null
                && !GameModalState.BlocksGameplayInput
                && !isExecuting
                && health != null
                && !health.IsDead;

            Cursor.visible = !gameplayMouseActive;
            Cursor.lockState = CursorLockMode.None;
        }

        private void SetMouseParryReticleVisible(bool visible)
        {
            ResolveMouseParryReticleReference();
            if (mouseParryReticleRenderer != null)
            {
                mouseParryReticleRenderer.enabled = visible;
            }

            if (mouseParryReticle != null)
            {
                mouseParryReticle.SetVisible(visible);
                if (!visible)
                {
                    mouseParryReticle.SetThreatened(false);
                }
            }
        }

        private void ResolveMouseParryReticleReference()
        {
            if (mouseParryReticle != null || mouseParryReticleRenderer == null)
            {
                return;
            }

            mouseParryReticle = mouseParryReticleRenderer.GetComponent<MouseParryReticle>();
            if (mouseParryReticle == null)
            {
                mouseParryReticle = mouseParryReticleRenderer.GetComponentInParent<MouseParryReticle>();
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryGetMouseParryDiamondCorners(out Vector3 top, out Vector3 right, out Vector3 bottom, out Vector3 left))
            {
                return;
            }

            Gizmos.color = new Color(1f, 0.48f, 0f, 0.95f);
            Gizmos.DrawLine(top, right);
            Gizmos.DrawLine(right, bottom);
            Gizmos.DrawLine(bottom, left);
            Gizmos.DrawLine(left, top);
            Gizmos.DrawLine(top, bottom);
            Gizmos.DrawLine(left, right);
        }

        private void UpdateProjectileLockOnIndicator()
        {
            if (projectileLockOnTarget == null || !projectileLockOnTarget.CanBeIntercepted)
            {
                SetProjectileLockOnIndicatorVisible(false);
                return;
            }

            EnsureProjectileLockOnIndicator();
            if (projectileLockOnLine == null)
            {
                return;
            }

            Color color = ParryEffectColor;
            color.a = Mathf.Max(color.a, projectileLockOnTarget.IsCharging ? 0.95f : 0.72f);
            projectileLockOnLine.enabled = true;
            projectileLockOnLine.startColor = color;
            projectileLockOnLine.endColor = color;
            projectileLockOnLine.startWidth = projectileLockOnTarget.IsCharging ? 0.035f : 0.026f;
            projectileLockOnLine.endWidth = projectileLockOnLine.startWidth;

            Vector3 center = projectileLockOnTarget.transform.position;
            float radius = projectileLockOnTarget.LockOnRadius;
            projectileLockOnLine.SetPosition(0, center + Vector3.up * radius);
            projectileLockOnLine.SetPosition(1, center + Vector3.right * radius);
            projectileLockOnLine.SetPosition(2, center + Vector3.down * radius);
            projectileLockOnLine.SetPosition(3, center + Vector3.left * radius);
        }

        private void EnsureProjectileLockOnIndicator()
        {
            if (projectileLockOnLine != null)
            {
                return;
            }

            GameObject indicatorObject = new GameObject(ProjectileLockOnIndicatorName);
            indicatorObject.transform.SetParent(transform, false);
            projectileLockOnLine = indicatorObject.AddComponent<LineRenderer>();
            projectileLockOnLine.useWorldSpace = true;
            projectileLockOnLine.loop = true;
            projectileLockOnLine.positionCount = 4;
            projectileLockOnLine.numCornerVertices = 2;
            projectileLockOnLine.numCapVertices = 2;
            projectileLockOnLine.material = GetRangeIndicatorMaterial();
            projectileLockOnLine.sortingOrder = 45;
            projectileLockOnLine.enabled = false;
        }

        private void SetProjectileLockOnIndicatorVisible(bool visible)
        {
            if (projectileLockOnLine != null)
            {
                projectileLockOnLine.enabled = visible;
            }

            if (!visible)
            {
                projectileLockOnTarget = null;
            }
        }

        private void SetHoveredExecutionTarget(ExecutionTarget nextTarget)
        {
            hoveredExecutionTarget = nextTarget;
        }

        private Vector2 GetParryCursorWorldPosition()
        {
            return GetMouseWorldPosition();
        }

        private static Material GetRangeIndicatorMaterial()
        {
            if (rangeIndicatorMaterial != null)
            {
                return rangeIndicatorMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            rangeIndicatorMaterial = shader != null ? new Material(shader) : null;
            return rangeIndicatorMaterial;
        }

        private void ResolveRigReferences()
        {
            if (bodyRoot == null)
            {
                bodyRoot = FindChildRecursive(transform, BodyVisualName);
            }

            if (combatCenter == null)
            {
                combatCenter = FindChildRecursive(transform, CombatCenterName);
            }

            if (leftGunRecoil == null && leftGunOrigin != null)
            {
                leftGunRecoil = leftGunOrigin.GetComponentInChildren<GunRecoilMotion>();
            }

            leftGunOrigin ??= transform;
            leftGunFireOrigin ??= leftGunOrigin;
        }

        private void CacheBodyRenderers()
        {
            Transform targetRoot = bodyRoot != null ? bodyRoot : transform;
            bodyRenderers = targetRoot.GetComponentsInChildren<SpriteRenderer>(true);
            bodyBaseColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                bodyBaseColors[i] = bodyRenderers[i].color;
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null)
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform nested = FindChildRecursive(child, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private Transform GetLeftFireOrigin()
        {
            return leftGunFireOrigin != null ? leftGunFireOrigin : leftGunOrigin;
        }

        private Vector2 GetAimDirection(Transform origin)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return origin != null ? (Vector2)origin.right : (Vector2)transform.right;
            }

            Vector2 aimPoint = GetAimPoint();
            Vector2 direction = aimPoint - (Vector2)origin.position;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : (Vector2)origin.right;
        }

        private Vector2 GetAimPoint()
        {
            if (lockOnTarget != null && !lockOnTarget.IsDead)
            {
                return lockOnTarget.transform.position;
            }

            return GetMouseWorldPosition();
        }

        private static Vector2 GetMouseWorldPosition()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return Vector2.zero;
            }

            return camera.ScreenToWorldPoint(GameInput.MouseScreenPosition);
        }

        private void StopBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private float GunAimHoldSeconds => config.GunAimHoldSeconds;
        private Color AttackEffectColor => config.AttackEffectColor;
        private Color ParryEffectColor => config.ParryEffectColor;
    }
}
