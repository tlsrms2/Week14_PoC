using System.Collections;
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
        private const string BodyVisualName = "Visual";
        private const string LeftGunName = "LeftGun";
        private const string RightGunName = "RightGun";
        private const string FireOriginName = "FireOrigin";
        private const string MuzzleName = "Muzzle";
        private const string AttackRangeIndicatorName = "AttackRangeIndicator";
        private const string ParryRangeIndicatorName = "ParryRangeIndicator";
        private const string ProjectileLockOnIndicatorName = "ProjectileLockOnIndicator";
        private const int RangeDashCount = 48;
        private const int ExecutionDimSortingOrder = 65;
        private const float BaseGamepadLookTurnDegreesPerSecond = 540f;

        public static PlayerCombatController Active { get; private set; }

        [SerializeField] private PlayerCombatConfig config;
        [SerializeField] private Transform bodyRoot;
        [SerializeField] private Transform leftGunOrigin;
        [SerializeField] private Transform leftGunFireOrigin;
        [SerializeField] private Transform rightGunOrigin;
        [SerializeField] private Transform rightGunFireOrigin;
        [SerializeField] private GunRecoilMotion leftGunRecoil;
        [SerializeField] private GunRecoilMotion rightGunRecoil;
        [SerializeField] private LayerMask enemyMask = ~0;
        [SerializeField] private LayerMask parryMask = ~0;
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private AttackTimingOutline attackTimingOutline;

        private Health health;
        private BulletGauge bullets;
        private CameraFollow2D cameraFollow;
        private Health lockOnTarget;
        private ExecutionTarget hoveredExecutionTarget;
        private EnemyProjectile projectileLockOnTarget;
        private Coroutine executionRoutine;
        private bool isExecuting;
        private float nextParryReadyAt;
        private float leftGunAimLockedUntil;
        private Vector2 leftGunLockedDirection;
        private float rightGunAimLockedUntil;
        private Vector2 rightGunLockedDirection;
        private Transform rangeIndicatorRoot;
        private LineRenderer[] attackRangeDashes;
        private LineRenderer parryRangeLine;
        private LineRenderer projectileLockOnLine;
        private Transform executionFocusPoint;
        private SpriteRenderer executionDimRenderer;
        private Coroutine executionDimRoutine;
        private SpriteRenderer[] bodyRenderers;
        private SpriteRenderer[] bodyVisualRenderers;
        private Color[] bodyBaseColors;
        private float bodyHitColorEndsAt;
        private Vector2 smoothedGamepadLookDirection;
        private int smoothedGamepadLookFrame = -1;
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
        public bool CanMove => CanAct;
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
            SetProjectileLockOnIndicatorVisible(false);
            StopExecutionShotDim();
        }

        private void Start()
        {
            if (config == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatController)} requires {nameof(PlayerCombatConfig)}.", this);
                return;
            }

            bullets.Configure(config.MaxBullets, true);
            ResetParryCooldown();
        }

        public void SetConfig(PlayerCombatConfig nextConfig)
        {
            config = nextConfig;
            ResetParryCooldown();
        }

        private void Update()
        {
            if (health.IsDead)
            {
                StopBody();
                SetRangeIndicatorsVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetLockOnTarget(null);
                SetHoveredExecutionTarget(null);
                HideAttackTimingOutline();
                return;
            }

            if (isExecuting)
            {
                StopBody();
                SetRangeIndicatorsVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetHoveredExecutionTarget(null);
                HideAttackTimingOutline();
                return;
            }

            if (config == null)
            {
                SetRangeIndicatorsVisible(false);
                SetProjectileLockOnIndicatorVisible(false);
                SetHoveredExecutionTarget(null);
                HideAttackTimingOutline();
                return;
            }

            ClearInvalidLockOnTarget();
            UpdateLockOnTarget();
            UpdateHoveredExecutionTarget();
            RotateToAim();
            UpdateProjectileLockOnTarget();
            UpdateRangeIndicators();
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

        public bool ReceiveAttack(int bulletDamage)
        {
            return ReceiveAttack(bulletDamage, transform.position, Vector2.right);
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

            if (bullets == null || !bullets.TrySpend(config.LeftAttackBulletCost, BulletChangeSource.Attack))
            {
                return false;
            }

            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 direction = AimGunAndGetDirection(leftGunOrigin, fireOrigin, GetAimDirection(leftGunOrigin));
            LockLeftGunAim(direction);

            PlayerProjectile projectile = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                fireOrigin.position,
                direction,
                this,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                PlayerAttackBulletDamage,
                AttackEffectColor,
                true);

            if (projectile == null)
            {
                bullets.Restore(config.LeftAttackBulletCost, BulletChangeSource.Attack);
                return false;
            }

            ProjectileVfx.PlayMuzzleFlash(fireOrigin.position, direction, AttackEffectColor, 0.9f);
            leftGunRecoil?.Play(direction);
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
            Vector2 executionCenter = GetParryCenter();
            Collider2D[] hits = Physics2D.OverlapCircleAll(executionCenter, ParryRange, enemyMask);
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
            if (distance > ParryRange || distance >= bestDistance)
            {
                return;
            }

            bestTarget = target;
            bestDistance = distance;
        }

        private IEnumerator ExecuteTarget(ExecutionTarget executionTarget)
        {
            if (executionTarget == null || !executionTarget.BeginExecution(this))
            {
                FinishExecution();
                yield break;
            }

            isExecuting = true;
            StopBody();

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
            aimDirection = AimGunAndGetDirection(leftGunOrigin, leftFireOrigin, (Vector2)executionTarget.transform.position - (Vector2)leftGunOrigin.position);
            LockLeftGunAim(aimDirection);
            UpdateExecutionFocusPoint(transform.position, executionTarget.transform.position);
            leftGunRecoil?.ReturnToBase(config.ExecutionGunReturnSeconds);
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
                executionTarget.CompleteExecution(this, false);
                executionTarget.DestroyExecutedTarget();
            }

            FinishExecution();
        }

        private void FinishExecution()
        {
            isExecuting = false;
            executionRoutine = null;
            leftGunRecoil?.ReturnToBase(config != null ? config.ExecutionGunReturnSeconds : 0.045f);
            GetCameraFollow()?.EndCinematicFocus();
            ClearInvalidLockOnTarget();
            UpdateHoveredExecutionTarget();
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

        private void ResetParryCooldown()
        {
            nextParryReadyAt = 0f;
        }

        private bool CanUseParry()
        {
            return config != null && Time.time >= nextParryReadyAt;
        }

        private void StartParryCooldown()
        {
            nextParryReadyAt = Time.time + Mathf.Max(0f, config.ParryCooldownSeconds);
        }

        private bool TryParryProjectile()
        {
            if (config.ProjectilePrefab == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatConfig)} requires {nameof(PlayerCombatConfig.ProjectilePrefab)}.", this);
                return false;
            }

            if (!CanUseParry())
            {
                return false;
            }

            Transform rightFireOrigin = GetRightFireOrigin();
            Vector2 aimDirection = GetParryIndicatorDirection();
            EnemyProjectile target = projectileLockOnTarget != null && projectileLockOnTarget.CanBeIntercepted
                ? projectileLockOnTarget
                : FindClosestInterceptTarget(aimDirection);
            if (target == null)
            {
                return false;
            }

            Vector3 targetPosition = target.transform.position;
            rightFireOrigin = GetRightFireOrigin();
            Vector2 direction = AimGunAndGetDirection(rightGunOrigin, rightFireOrigin, (Vector2)(targetPosition - rightGunOrigin.position));
            PlayerProjectile parryShot = FireParryShot(rightFireOrigin, direction);
            if (parryShot != null)
            {
                StartParryCooldown();
                parryShot.SetForcedParryTarget(target);
            }

            return parryShot != null;
        }

        private PlayerProjectile FireParryShot(Transform fireOrigin, Vector2 aimDirection)
        {
            if (fireOrigin == null)
            {
                return null;
            }

            Vector2 direction = AimGunAndGetDirection(rightGunOrigin, fireOrigin, aimDirection);
            LockRightGunAim(direction);

            PlayerProjectile projectile = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                fireOrigin.position,
                direction,
                this,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                0,
                ParryEffectColor,
                false,
                true);
            if (projectile != null)
            {
                ProjectileVfx.PlayMuzzleFlash(fireOrigin.position, direction, ParryEffectColor, 1f);
                rightGunRecoil?.Play(direction);
            }

            return projectile;
        }

        private EnemyProjectile FindClosestInterceptTarget(Vector2 aimDirection)
        {
            Vector2 parryCenter = GetParryCenter();
            Collider2D[] hits = Physics2D.OverlapCircleAll(parryCenter, ParryRange, parryMask);
            EnemyProjectile bestTarget = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                EnemyProjectile source = hits[i].GetComponentInParent<EnemyProjectile>();
                if (source == null || !source.CanBeIntercepted)
                {
                    continue;
                }

                if (!IsInsideParryJudgementArea(source.transform.position, aimDirection))
                {
                    continue;
                }

                float distance = Vector2.Distance(parryCenter, source.transform.position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestTarget = source;
                bestDistance = distance;
            }

            return bestTarget;
        }

        private Vector2 GetParryCenter()
        {
            return bodyRoot != null ? bodyRoot.position : transform.position;
        }

        public bool TryGetParryIndicatorState(out Vector2 center, out Vector2 direction, out float radius, out float angleDegrees)
        {
            center = GetParryCenter();
            direction = GetParryIndicatorDirection();
            radius = config != null ? ParryRange : 0f;
            angleDegrees = config != null ? Mathf.Clamp(config.ParryAimAngleDegrees, 1f, 360f) : 0f;
            return config != null && radius > 0f;
        }

        private bool IsInsideParryJudgementArea(Vector2 targetPosition, Vector2 aimDirection)
        {
            Vector2 parryCenter = GetParryCenter();
            float radius = ParryRange;
            if (radius <= 0f || (targetPosition - parryCenter).sqrMagnitude > radius * radius)
            {
                return false;
            }

            float angleDegrees = Mathf.Clamp(config.ParryAimAngleDegrees, 1f, 360f);
            if (angleDegrees >= 359.5f)
            {
                return true;
            }

            Vector2 forward = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : Vector2.right;
            float halfAngle = angleDegrees * 0.5f;
            Vector2 lowerArcPoint = parryCenter + RotateDirection(forward, -halfAngle) * radius;
            Vector2 upperArcPoint = parryCenter + RotateDirection(forward, halfAngle) * radius;
            GetParryBodyEdgePoints(out Vector2 lowerBodyPoint, out Vector2 upperBodyPoint);

            Vector2 insideReference = parryCenter + forward * (radius * 0.5f);
            return IsSameSideOfLine(lowerBodyPoint, lowerArcPoint, insideReference, targetPosition)
                && IsSameSideOfLine(upperBodyPoint, upperArcPoint, insideReference, targetPosition);
        }

        private void GetParryBodyEdgePoints(out Vector2 lowerPoint, out Vector2 upperPoint)
        {
            Vector2 center = GetParryCenter();
            Vector2 up = bodyRoot != null ? (Vector2)bodyRoot.up : Vector2.up;
            if (up.sqrMagnitude <= 0.0001f)
            {
                up = Vector2.up;
            }
            up.Normalize();

            if (TryGetBodyVisualProjection(up, center, out float minOffset, out float maxOffset))
            {
                lowerPoint = center + up * minOffset;
                upperPoint = center + up * maxOffset;
                return;
            }

            lowerPoint = center;
            upperPoint = center;
        }

        private bool TryGetBodyVisualProjection(Vector2 axis, Vector2 center, out float minOffset, out float maxOffset)
        {
            minOffset = 0f;
            maxOffset = 0f;
            bool hasProjection = false;

            SpriteRenderer[] renderers = bodyVisualRenderers;
            if (renderers == null || renderers.Length == 0)
            {
                Transform targetRoot = bodyRoot != null ? bodyRoot : transform;
                renderers = targetRoot.GetComponentsInChildren<SpriteRenderer>(true);
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                Bounds bounds = renderer.sprite.bounds;
                Vector3 min = bounds.min;
                Vector3 max = bounds.max;
                IncludeBodyVisualProjection(renderer.transform.TransformPoint(new Vector3(min.x, min.y, 0f)), axis, center, ref minOffset, ref maxOffset, ref hasProjection);
                IncludeBodyVisualProjection(renderer.transform.TransformPoint(new Vector3(min.x, max.y, 0f)), axis, center, ref minOffset, ref maxOffset, ref hasProjection);
                IncludeBodyVisualProjection(renderer.transform.TransformPoint(new Vector3(max.x, min.y, 0f)), axis, center, ref minOffset, ref maxOffset, ref hasProjection);
                IncludeBodyVisualProjection(renderer.transform.TransformPoint(new Vector3(max.x, max.y, 0f)), axis, center, ref minOffset, ref maxOffset, ref hasProjection);
            }

            return hasProjection;
        }

        private static void IncludeBodyVisualProjection(
            Vector3 point,
            Vector2 axis,
            Vector2 center,
            ref float minOffset,
            ref float maxOffset,
            ref bool hasProjection)
        {
            float offset = Vector2.Dot((Vector2)point - center, axis);
            if (!hasProjection)
            {
                minOffset = offset;
                maxOffset = offset;
                hasProjection = true;
                return;
            }

            minOffset = Mathf.Min(minOffset, offset);
            maxOffset = Mathf.Max(maxOffset, offset);
        }

        private void UpdateLockOnTarget()
        {
            if (GameInput.LockOnDown)
            {
                SetLockOnTarget(FindNextLockOnTarget());
                return;
            }

            if (lockOnTarget == null)
            {
                SetLockOnTarget(FindNearestLockOnTarget());
            }
        }

        private Health FindNearestLockOnTarget()
        {
            Vector2 playerPosition = transform.position;
            Collider2D[] hits = Physics2D.OverlapCircleAll(playerPosition, GetLockOnAcquireDistance(), enemyMask);
            Health bestTarget = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                Health targetHealth = hits[i].GetComponentInParent<Health>();
                if (!IsValidLockOnTarget(targetHealth))
                {
                    continue;
                }

                float distance = Vector2.Distance(playerPosition, hits[i].bounds.center);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestTarget = targetHealth;
                bestDistance = distance;
            }

            if (bestTarget != null)
            {
                return bestTarget;
            }

            Health[] allTargets = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (int i = 0; i < allTargets.Length; i++)
            {
                Health targetHealth = allTargets[i];
                if (!IsValidLockOnTargetInRange(targetHealth))
                {
                    continue;
                }

                float distance = Vector2.Distance(playerPosition, targetHealth.transform.position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestTarget = targetHealth;
                bestDistance = distance;
            }

            return bestTarget;
        }

        private Health FindNextLockOnTarget()
        {
            Health[] allTargets = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
            Health firstTarget = null;
            Health nextTarget = null;
            int firstId = int.MaxValue;
            int nextId = int.MaxValue;
            int currentId = lockOnTarget != null ? lockOnTarget.GetInstanceID() : int.MinValue;

            for (int i = 0; i < allTargets.Length; i++)
            {
                Health targetHealth = allTargets[i];
                if (!IsValidLockOnTargetInRange(targetHealth))
                {
                    continue;
                }

                int targetId = targetHealth.GetInstanceID();
                if (targetId < firstId)
                {
                    firstId = targetId;
                    firstTarget = targetHealth;
                }

                if (targetHealth == lockOnTarget || targetId <= currentId || targetId >= nextId)
                {
                    continue;
                }

                nextId = targetId;
                nextTarget = targetHealth;
            }

            return nextTarget != null ? nextTarget : firstTarget;
        }

        private void ClearInvalidLockOnTarget()
        {
            if (lockOnTarget != null && !lockOnTarget.IsDead && !IsLockOnTooFar(lockOnTarget))
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

        private bool IsValidLockOnTargetInRange(Health targetHealth)
        {
            return IsValidLockOnTarget(targetHealth)
                && Vector2.Distance(transform.position, targetHealth.transform.position) <= GetLockOnAcquireDistance();
        }

        private float GetLockOnAcquireDistance()
        {
            return config != null ? config.LockOnBreakDistance : 0f;
        }

        private bool IsLockOnTooFar(Health targetHealth)
        {
            if (targetHealth == null || config == null || config.LockOnBreakDistance <= 0f)
            {
                return false;
            }

            return Vector2.Distance(transform.position, targetHealth.transform.position) > config.LockOnBreakDistance;
        }

        private void RotateToAim()
        {
            Transform bodyAimOrigin = bodyRoot != null ? bodyRoot : transform;
            Vector2 bodyDirection = GetAimDirection(bodyAimOrigin);
            RotateVisual(bodyRoot, bodyDirection);

            Vector2 leftDirection = lockOnTarget == null && Time.time <= leftGunAimLockedUntil
                ? leftGunLockedDirection
                : GetAimDirection(leftGunOrigin);
            RotateGun(leftGunOrigin, leftDirection);

            Vector2 rightDirection = lockOnTarget == null && Time.time <= rightGunAimLockedUntil
                ? rightGunLockedDirection
                : GetAimDirection(rightGunOrigin);
            RotateGun(rightGunOrigin, rightDirection);
        }

        private void LockLeftGunAim(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            leftGunLockedDirection = direction.normalized;
            leftGunAimLockedUntil = Time.time + GunAimHoldSeconds;
            RotateGun(leftGunOrigin, leftGunLockedDirection);
        }

        private void LockRightGunAim(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            rightGunLockedDirection = direction.normalized;
            rightGunAimLockedUntil = Time.time + GunAimHoldSeconds;
            RotateGun(rightGunOrigin, rightGunLockedDirection);
        }

        private void AimExecutionPose(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 normalized = direction.normalized;
            RotateVisual(bodyRoot, normalized);
            RotateGun(leftGunOrigin, normalized);
        }

        private void RotateGun(Transform gun, Vector2 direction)
        {
            if (gun == null || gun == bodyRoot || IsPhysicsRoot(gun))
            {
                return;
            }

            RotateRight(gun, direction);
        }

        private Vector2 AimGunAndGetDirection(Transform gun, Transform fireOrigin, Vector2 desiredDirection)
        {
            Vector2 normalized = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection.normalized : Vector2.right;
            RotateGun(gun, normalized);

            if (gun == null || gun == bodyRoot || IsPhysicsRoot(gun))
            {
                return normalized;
            }

            Transform forwardOrigin = fireOrigin != null ? fireOrigin : gun;
            Vector2 gunForward = forwardOrigin.right;
            return gunForward.sqrMagnitude > 0.0001f ? gunForward.normalized : normalized;
        }

        private void RotateVisual(Transform visual, Vector2 direction)
        {
            if (visual == null || IsPhysicsRoot(visual))
            {
                return;
            }

            RotateRight(visual, direction);
        }

        private static void RotateRight(Transform target, Vector2 direction)
        {
            if (target == null || direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            target.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void UpdateProjectileLockOnTarget()
        {
            projectileLockOnTarget = FindClosestInterceptTarget(GetParryIndicatorDirection());
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

        private void UpdateRangeIndicators()
        {
            EnsureRangeIndicators();
            SetAttackRangeDashesVisible(false);
            Vector2 indicatorDirection = GetParryIndicatorDirection();
            UpdateParryRangeIndicator(ParryRange, indicatorDirection);
            if (parryRangeLine != null)
            {
                parryRangeLine.enabled = true;
            }
        }

        private void EnsureRangeIndicators()
        {
            if (attackRangeDashes == null)
            {
                GameObject attackRoot = new GameObject(AttackRangeIndicatorName);
                attackRoot.transform.SetParent(transform, false);
                rangeIndicatorRoot = attackRoot.transform;
                attackRangeDashes = new LineRenderer[RangeDashCount];

                for (int i = 0; i < attackRangeDashes.Length; i++)
                {
                    GameObject dashObject = new GameObject($"Dash_{i:00}");
                    dashObject.transform.SetParent(attackRoot.transform, false);
                    Color failRangeColor = ParryEffectColor;
                    failRangeColor.a = 0.32f;
                    attackRangeDashes[i] = CreateRangeLine(dashObject, failRangeColor, 0.012f, 2);
                }
            }

            if (parryRangeLine == null)
            {
                GameObject parryObject = new GameObject(ParryRangeIndicatorName);
                parryObject.transform.SetParent(rangeIndicatorRoot != null ? rangeIndicatorRoot : transform, false);
                Color parryColor = GetParryRangeColor();
                parryRangeLine = CreateRangeLine(parryObject, parryColor, 0.03f, 32);
                parryRangeLine.sortingOrder = 5;
            }
        }

        private void UpdateAttackRangeIndicator(float radius, Vector2 direction)
        {
            if (attackRangeDashes == null)
            {
                return;
            }

            Vector3 center = GetRangeIndicatorCenter();
            float angleDegrees = Mathf.Clamp(config.ParryAimAngleDegrees, 1f, 360f);
            float centerAngle = Mathf.Atan2(direction.y, direction.x);
            float startRangeAngle = angleDegrees >= 359.5f ? 0f : centerAngle - angleDegrees * 0.5f * Mathf.Deg2Rad;
            float segmentAngle = angleDegrees * Mathf.Deg2Rad / attackRangeDashes.Length;
            float dashAngle = segmentAngle * 0.45f;
            for (int i = 0; i < attackRangeDashes.Length; i++)
            {
                LineRenderer dash = attackRangeDashes[i];
                if (dash == null)
                {
                    continue;
                }

                Color failRangeColor = ParryEffectColor;
                failRangeColor.a = 0.32f;
                dash.startColor = failRangeColor;
                dash.endColor = failRangeColor;
                float startAngle = startRangeAngle + segmentAngle * i;
                float endAngle = startAngle + dashAngle;
                float midAngle = (startAngle + endAngle) * 0.5f;
                dash.positionCount = 3;
                dash.SetPosition(0, center + CirclePoint(radius, startAngle));
                dash.SetPosition(1, center + CirclePoint(radius, midAngle));
                dash.SetPosition(2, center + CirclePoint(radius, endAngle));
            }
        }

        private void UpdateParryRangeIndicator(float radius, Vector2 direction)
        {
            if (parryRangeLine == null)
            {
                return;
            }

            Color parryColor = GetParryRangeColor();
            parryRangeLine.startColor = parryColor;
            parryRangeLine.endColor = parryColor;

            float angleDegrees = Mathf.Clamp(config.ParryAimAngleDegrees, 1f, 360f);
            bool fullCircle = angleDegrees >= 359.5f;
            int pointCount = fullCircle ? 73 : Mathf.Max(3, Mathf.CeilToInt(angleDegrees / 4f) + 1);
            float centerAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float startAngle = fullCircle ? 0f : centerAngle - angleDegrees * 0.5f;
            Vector3 center = GetRangeIndicatorCenter();

            parryRangeLine.loop = fullCircle;
            parryRangeLine.positionCount = pointCount;
            for (int i = 0; i < pointCount; i++)
            {
                float t = pointCount <= 1 ? 0f : (float)i / (pointCount - 1);
                float angle = (startAngle + angleDegrees * t) * Mathf.Deg2Rad;
                parryRangeLine.SetPosition(i, center + CirclePoint(radius, angle));
            }
        }

        private Color GetParryRangeColor()
        {
            Color color = ParryEffectColor;
            color.a = Mathf.Max(color.a, 0.85f);
            return color;
        }

        private Vector2 GetParryIndicatorDirection()
        {
            if (TryGetSmoothedGamepadLookDirection(out Vector2 lookDirection))
            {
                return lookDirection;
            }

            if (GameInput.IsGamepadMode)
            {
                return GetLastGamepadLookDirection();
            }

            Vector2 origin = GetParryCenter();
            Vector2 direction = GetMouseWorldPosition() - origin;
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            return bodyRoot != null ? (Vector2)bodyRoot.right : (Vector2)transform.right;
        }

        private void SetAttackRangeDashesVisible(bool visible)
        {
            if (attackRangeDashes == null)
            {
                return;
            }

            for (int i = 0; i < attackRangeDashes.Length; i++)
            {
                if (attackRangeDashes[i] != null)
                {
                    attackRangeDashes[i].enabled = visible;
                }
            }
        }

        private void SetRangeIndicatorsVisible(bool visible)
        {
            SetAttackRangeDashesVisible(visible);
            if (parryRangeLine != null)
            {
                parryRangeLine.enabled = visible;
            }
        }

        private static LineRenderer CreateRangeLine(GameObject owner, Color color, float width, int positionCount)
        {
            LineRenderer line = owner.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = positionCount;
            line.startWidth = width;
            line.endWidth = width;
            line.startColor = color;
            line.endColor = color;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.material = GetRangeIndicatorMaterial();
            line.sortingOrder = 3;
            return line;
        }

        private static Vector3 CirclePoint(float radius, float angleRadians)
        {
            return new Vector3(Mathf.Cos(angleRadians) * radius, Mathf.Sin(angleRadians) * radius, 0f);
        }

        private Vector3 GetRangeIndicatorCenter()
        {
            if (rangeIndicatorRoot != null)
            {
                rangeIndicatorRoot.localPosition = Vector3.zero;
                rangeIndicatorRoot.localRotation = Quaternion.identity;
                rangeIndicatorRoot.localScale = GetInverseScale(rangeIndicatorRoot.parent);
            }

            return Vector3.zero;
        }

        private static Vector3 GetInverseScale(Transform parent)
        {
            if (parent == null)
            {
                return Vector3.one;
            }

            Vector3 scale = parent.lossyScale;
            return new Vector3(
                Mathf.Abs(scale.x) > 0.0001f ? 1f / scale.x : 1f,
                Mathf.Abs(scale.y) > 0.0001f ? 1f / scale.y : 1f,
                Mathf.Abs(scale.z) > 0.0001f ? 1f / scale.z : 1f);
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
            if (bodyRoot == null || IsPhysicsRoot(bodyRoot))
            {
                bodyRoot = FindChildRecursive(transform, BodyVisualName);
            }

            if (leftGunOrigin == null || IsPhysicsRoot(leftGunOrigin))
            {
                leftGunOrigin = FindChildRecursive(transform, LeftGunName);
            }

            if (leftGunFireOrigin == null || IsPhysicsRoot(leftGunFireOrigin))
            {
                leftGunFireOrigin = FindFireOrigin(leftGunOrigin);
            }

            if (rightGunOrigin == null || IsPhysicsRoot(rightGunOrigin))
            {
                rightGunOrigin = FindChildRecursive(transform, RightGunName);
            }

            if (rightGunFireOrigin == null || IsPhysicsRoot(rightGunFireOrigin))
            {
                rightGunFireOrigin = FindFireOrigin(rightGunOrigin);
            }

            if (leftGunRecoil == null && leftGunOrigin != null)
            {
                leftGunRecoil = leftGunOrigin.GetComponentInChildren<GunRecoilMotion>();
            }

            if (rightGunRecoil == null && rightGunOrigin != null)
            {
                rightGunRecoil = rightGunOrigin.GetComponentInChildren<GunRecoilMotion>();
            }

            leftGunOrigin ??= transform;
            rightGunOrigin ??= leftGunOrigin;
            leftGunFireOrigin ??= leftGunOrigin;
            rightGunFireOrigin ??= rightGunOrigin;
        }

        private void CacheBodyRenderers()
        {
            Transform targetRoot = bodyRoot != null ? bodyRoot : transform;
            SpriteRenderer[] renderers = targetRoot.GetComponentsInChildren<SpriteRenderer>(true);
            bodyVisualRenderers = renderers;
            int count = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (ShouldTintBodyRenderer(renderers[i]))
                {
                    count++;
                }
            }

            bodyRenderers = new SpriteRenderer[count];
            bodyBaseColors = new Color[count];
            int index = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!ShouldTintBodyRenderer(renderers[i]))
                {
                    continue;
                }

                bodyRenderers[index] = renderers[i];
                bodyBaseColors[index] = renderers[i].color;
                index++;
            }
        }

        private bool ShouldTintBodyRenderer(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            Transform rendererTransform = renderer.transform;
            return !IsUnderTransform(rendererTransform, leftGunOrigin)
                && !IsUnderTransform(rendererTransform, rightGunOrigin)
                && !IsUnderTransform(rendererTransform, leftGunFireOrigin)
                && !IsUnderTransform(rendererTransform, rightGunFireOrigin);
        }

        private static bool IsUnderTransform(Transform target, Transform root)
        {
            return target != null && root != null && (target == root || target.IsChildOf(root));
        }

        private bool IsPhysicsRoot(Transform target)
        {
            return body != null && target == body.transform;
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

        private static Transform FindFireOrigin(Transform gun)
        {
            return FindChildRecursive(gun, FireOriginName) ?? FindChildRecursive(gun, MuzzleName) ?? gun;
        }

        private Transform GetLeftFireOrigin()
        {
            return leftGunFireOrigin != null ? leftGunFireOrigin : leftGunOrigin;
        }

        private Transform GetRightFireOrigin()
        {
            return rightGunFireOrigin != null ? rightGunFireOrigin : rightGunOrigin;
        }

        private Vector2 GetAimDirection(Transform origin)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                return origin != null ? (Vector2)origin.right : (Vector2)transform.right;
            }

            if (lockOnTarget == null && TryGetSmoothedGamepadLookDirection(out Vector2 lookDirection))
            {
                return lookDirection;
            }

            if (lockOnTarget == null && GameInput.IsGamepadMode)
            {
                return origin != null ? (Vector2)origin.right : (Vector2)transform.right;
            }

            Vector2 aimPoint = GetAimPoint();
            Vector2 direction = aimPoint - (Vector2)origin.position;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : (Vector2)origin.right;
        }

        private bool TryGetSmoothedGamepadLookDirection(out Vector2 direction)
        {
            direction = Vector2.zero;
            if (!GameInput.TryGetLookDirection(out Vector2 targetDirection))
            {
                return false;
            }

            if (smoothedGamepadLookFrame == Time.frameCount)
            {
                direction = smoothedGamepadLookDirection;
                return true;
            }

            if (smoothedGamepadLookDirection.sqrMagnitude <= 0.0001f)
            {
                smoothedGamepadLookDirection = GetNeutralGamepadLookDirection();
            }

            float maxRadiansDelta = BaseGamepadLookTurnDegreesPerSecond
                * GameInput.GamepadLookSensitivity
                * Mathf.Deg2Rad
                * Time.unscaledDeltaTime;
            Vector3 nextDirection = Vector3.RotateTowards(
                smoothedGamepadLookDirection,
                targetDirection,
                maxRadiansDelta,
                0f);
            smoothedGamepadLookDirection = ((Vector2)nextDirection).normalized;
            smoothedGamepadLookFrame = Time.frameCount;
            direction = smoothedGamepadLookDirection;
            return true;
        }

        private Vector2 GetLastGamepadLookDirection()
        {
            if (smoothedGamepadLookDirection.sqrMagnitude > 0.0001f)
            {
                return smoothedGamepadLookDirection.normalized;
            }

            return GetNeutralGamepadLookDirection();
        }

        private Vector2 GetNeutralGamepadLookDirection()
        {
            Vector2 direction = transform.right;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
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

        private static bool IsSameSideOfLine(Vector2 lineStart, Vector2 lineEnd, Vector2 reference, Vector2 point)
        {
            Vector2 line = lineEnd - lineStart;
            float referenceSide = Cross(line, reference - lineStart);
            float pointSide = Cross(line, point - lineStart);
            if (Mathf.Abs(referenceSide) <= 0.0001f)
            {
                return true;
            }

            return referenceSide * pointSide >= -0.0001f;
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }

        private static Vector2 RotateDirection(Vector2 direction, float angleDegrees)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return Vector2.right;
            }

            float radians = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians);
            float sin = Mathf.Sin(radians);
            Vector2 normalized = direction.normalized;
            return new Vector2(
                normalized.x * cos - normalized.y * sin,
                normalized.x * sin + normalized.y * cos);
        }

        private void StopBody()
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform leftOrigin = leftGunOrigin != null ? leftGunOrigin : transform;
            if (config == null)
            {
                return;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawLine(leftOrigin.position, leftOrigin.position + leftOrigin.right * ParryRange);
            Gizmos.color = Color.cyan;
            Vector3 parryCenter = bodyRoot != null ? bodyRoot.position : transform.position;
            Gizmos.DrawWireSphere(parryCenter, ParryRange);
            Gizmos.color = new Color(0.35f, 1f, 0.45f, 0.85f);
            DrawParryJudgementGizmo(parryCenter, GetGizmoParryDirection());
        }

        private Vector2 GetGizmoParryDirection()
        {
            Transform aimTransform = rightGunOrigin != null
                ? rightGunOrigin
                : bodyRoot != null
                    ? bodyRoot
                    : transform;
            Vector2 direction = aimTransform != null ? (Vector2)aimTransform.right : Vector2.right;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        }

        private void DrawParryJudgementGizmo(Vector3 center, Vector2 direction)
        {
            float radius = ParryRange;
            if (radius <= 0f)
            {
                return;
            }

            Vector2 normalized = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            float clampedAngle = Mathf.Clamp(config.ParryAimAngleDegrees, 1f, 360f);
            bool fullCircle = clampedAngle >= 359.5f;
            int segmentCount = fullCircle ? 72 : Mathf.Max(2, Mathf.CeilToInt(clampedAngle / 4f));
            float centerAngle = Mathf.Atan2(normalized.y, normalized.x) * Mathf.Rad2Deg;
            float startAngle = fullCircle ? 0f : centerAngle - clampedAngle * 0.5f;
            Vector3 firstPoint = center + CirclePoint(radius, startAngle * Mathf.Deg2Rad);
            Vector3 previousPoint = firstPoint;

            for (int i = 1; i <= segmentCount; i++)
            {
                float t = (float)i / segmentCount;
                float angle = (startAngle + clampedAngle * t) * Mathf.Deg2Rad;
                Vector3 currentPoint = center + CirclePoint(radius, angle);
                Gizmos.DrawLine(previousPoint, currentPoint);
                previousPoint = currentPoint;
            }

            if (!fullCircle)
            {
                GetParryBodyEdgePoints(out Vector2 lowerBodyPoint, out Vector2 upperBodyPoint);
                Gizmos.DrawLine(lowerBodyPoint, firstPoint);
                Gizmos.DrawLine(upperBodyPoint, previousPoint);
                Gizmos.DrawLine(lowerBodyPoint, upperBodyPoint);
            }
        }

        private int PlayerAttackBulletDamage => config.AttackBulletDamage;
        private float ParryRange => config.ParryRange;
        private float GunAimHoldSeconds => config.GunAimHoldSeconds;
        private Color AttackEffectColor => config.AttackEffectColor;
        private Color ParryEffectColor => config.ParryEffectColor;
    }
}
