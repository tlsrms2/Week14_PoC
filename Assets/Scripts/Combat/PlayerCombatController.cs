using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Bootstrap;
using Week14.Input;

namespace Week14.Combat
{
    public interface IAttackSource
    {
        Transform SourceTransform { get; }
        bool TryParry(PlayerCombatController player);
    }

    [RequireComponent(typeof(Health), typeof(HeatGauge))]
    public sealed class PlayerCombatController : MonoBehaviour
    {
        private const string BodyVisualName = "Visual";
        private const string LeftGunName = "LeftGun";
        private const string RightGunName = "RightGun";
        private const string FireOriginName = "FireOrigin";
        private const string MuzzleName = "Muzzle";
        private const string AttackRangeIndicatorName = "AttackRangeIndicator";
        private const string ParryRangeIndicatorName = "ParryRangeIndicator";
        private const int RangeDashCount = 48;

        public static PlayerCombatController Active { get; private set; }

        [SerializeField] private PlayerCombatConfig config;
        [SerializeField] private Transform bodyRoot;
        [SerializeField, FormerlySerializedAs("attackOrigin")] private Transform leftGunOrigin;
        [SerializeField] private Transform leftGunFireOrigin;
        [SerializeField] private Transform rightGunOrigin;
        [SerializeField] private Transform rightGunFireOrigin;
        [SerializeField] private LayerMask enemyMask = ~0;
        [SerializeField] private LayerMask parryMask = ~0;
        [SerializeField] private Rigidbody2D body;

        private Health health;
        private HeatGauge heat;
        private CameraFollow2D cameraFollow;
        private Health lockOnTarget;
        private Coroutine executionRoutine;
        private bool isExecuting;
        private float nextAttackTime;
        private float nextParryTime;
        private float leftGunAimLockedUntil;
        private Vector2 leftGunLockedDirection;
        private float rightGunAimLockedUntil;
        private Vector2 rightGunLockedDirection;
        private Transform rangeIndicatorRoot;
        private LineRenderer[] attackRangeDashes;
        private LineRenderer parryRangeLine;
        private static Material rangeIndicatorMaterial;

        public Health Health => health;
        public HeatGauge Heat => heat;
        public Health LockOnTarget => lockOnTarget;
        public bool IsExecuting => isExecuting;
        public PlayerCombatConfig Config => config;
        public bool CanMove => CanAct;
        private bool CanAct => !isExecuting && !health.IsDead && !heat.IsOverheated;

        private void Awake()
        {
            health = GetComponent<Health>();
            heat = GetComponent<HeatGauge>();

            if (body == null)
            {
                body = GetComponent<Rigidbody2D>();
            }

            ResolveRigReferences();

            if (cameraFollow == null && Camera.main != null)
            {
                cameraFollow = Camera.main.GetComponent<CameraFollow2D>();
            }
        }

        private void OnEnable()
        {
            Active = this;
        }

        private void OnDisable()
        {
            if (Active == this)
            {
                Active = null;
            }

            if (cameraFollow != null)
            {
                cameraFollow.SetFocusTarget(null);
            }
        }

        private void Start()
        {
            if (config == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatController)} requires {nameof(PlayerCombatConfig)}.", this);
                return;
            }

            health.SetMaxDurability(config.MaxDurability, true);
            heat.Configure(
                config.MaxHeat,
                config.HeatCoolingPerSecond,
                config.OverheatSeconds,
                config.HeatAfterOverheatRatio,
                true);
        }

        public void SetConfig(PlayerCombatConfig nextConfig)
        {
            config = nextConfig;
        }

        private void Update()
        {
            if (health.IsDead)
            {
                StopBody();
                SetRangeIndicatorsVisible(false);
                SetLockOnTarget(null);
                return;
            }

            if (isExecuting)
            {
                StopBody();
                SetRangeIndicatorsVisible(false);
                return;
            }

            if (config == null)
            {
                SetRangeIndicatorsVisible(false);
                return;
            }

            UpdateLockOnInput();
            ClearInvalidLockOnTarget();
            RotateToAim();
            UpdateRangeIndicators();

            if (GameInput.GetMouseButtonDown(0) && CanAct)
            {
                if (!TryBeginExecution())
                {
                    TryShootEnemy();
                }
            }

            if (GameInput.GetMouseButtonDown(1) && CanAct)
            {
                TryParryProjectile();
            }
        }

        public bool ReceiveAttack(float damage)
        {
            if (health.IsDead || config == null)
            {
                return false;
            }

            health.TakeDamage(damage);
            heat.AddHeat(HitHeatToPlayer);
            return true;
        }

        public void SuppressHeatCooling(float seconds)
        {
            heat?.SuppressCooling(seconds);
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
                config.ParryRingGlitterSeconds);
        }

        private bool TryShootEnemy()
        {
            if (config.ProjectilePrefab == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatConfig)} requires {nameof(PlayerCombatConfig.ProjectilePrefab)}.", this);
                return false;
            }

            if (Time.time < nextAttackTime)
            {
                return false;
            }

            nextAttackTime = Time.time + PlayerAttackCooldown;

            Transform fireOrigin = GetLeftFireOrigin();
            Vector2 direction = AimGunAndGetDirection(leftGunOrigin, fireOrigin, GetAimDirection(leftGunOrigin));
            LockLeftGunAim(direction);
            Debug.DrawRay(fireOrigin.position, direction * PlayerAttackRange, AttackEffectColor, CombatEffectSeconds);
            PlayerProjectile projectile = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                fireOrigin.position,
                direction,
                this,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                PlayerAttackRange,
                PlayerAttackDamage,
                PlayerAttackHeat,
                AttackEffectColor,
                true);
            if (projectile == null)
            {
                return false;
            }

            SuppressHeatCooling(config.ActionHeatCoolingSuppressSeconds);
            return true;
        }

        private bool TryBeginExecution()
        {
            ExecutionTarget executionTarget = FindExecutionTarget();
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

        private ExecutionTarget FindExecutionTarget()
        {
            if (lockOnTarget != null)
            {
                ExecutionTarget lockedExecutionTarget = lockOnTarget.GetComponent<ExecutionTarget>();
                if (lockedExecutionTarget != null && lockedExecutionTarget.CanExecute(transform))
                {
                    return lockedExecutionTarget;
                }
            }

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, config.ExecutionRange, enemyMask);
            ExecutionTarget bestTarget = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                ExecutionTarget target = hits[i].GetComponentInParent<ExecutionTarget>();
                ChooseCloserExecutionTarget(target, ref bestTarget, ref bestDistance);
            }

            if (bestTarget != null)
            {
                return bestTarget;
            }

            ExecutionTarget[] executionTargets = Object.FindObjectsByType<ExecutionTarget>(FindObjectsSortMode.None);
            for (int i = 0; i < executionTargets.Length; i++)
            {
                ChooseCloserExecutionTarget(executionTargets[i], ref bestTarget, ref bestDistance);
            }

            return bestTarget;
        }

        private void ChooseCloserExecutionTarget(ExecutionTarget target, ref ExecutionTarget bestTarget, ref float bestDistance)
        {
            if (target == null || !target.CanExecute(transform))
            {
                return;
            }

            float distance = Vector2.Distance(transform.position, target.transform.position);
            if (distance >= bestDistance)
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
            Vector2 standDirection = ((Vector2)transform.position - targetPosition);
            if (standDirection.sqrMagnitude <= 0.0001f)
            {
                standDirection = -Vector2.right;
            }

            standDirection.Normalize();
            Vector2 executionPosition = targetPosition + standDirection * config.ExecutionStandOffDistance;
            if (body != null)
            {
                body.position = executionPosition;
            }

            transform.position = executionPosition;

            Transform leftFireOrigin = GetLeftFireOrigin();
            Vector2 aimDirection = targetPosition - (Vector2)leftGunOrigin.position;
            AimExecutionPose(aimDirection);

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
            float shotDistance = Mathf.Max(0.01f, Vector2.Distance(leftFireOrigin.position, executionTarget.transform.position));
            aimDirection = AimGunAndGetDirection(leftGunOrigin, leftFireOrigin, (Vector2)executionTarget.transform.position - (Vector2)leftGunOrigin.position);
            LockLeftGunAim(aimDirection);
            PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                leftFireOrigin.position,
                aimDirection,
                this,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                shotDistance,
                0f,
                0f,
                config.ExecutionShotColor,
                false);

            yield return new WaitForSeconds(config.ExecutionKillDelaySeconds);
            if (executionTarget == null)
            {
                FinishExecution();
                yield break;
            }

            Vector3 impactPosition = executionTarget.transform.position;
            executionTarget.RecoverExecutorHeat(this);
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
            SetLockOnTarget(null);

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
            ClearInvalidLockOnTarget();
        }

        private bool TryParryProjectile()
        {
            if (config.ProjectilePrefab == null)
            {
                Debug.LogWarning($"{nameof(PlayerCombatConfig)} requires {nameof(PlayerCombatConfig.ProjectilePrefab)}.", this);
                return false;
            }

            if (Time.time < nextParryTime)
            {
                return false;
            }

            nextParryTime = Time.time + ParryShotCooldown;

            Transform rightFireOrigin = GetRightFireOrigin();
            Vector2 aimDirection = GetAimDirection(rightGunOrigin);
            IAttackSource target = FindClosestParryTarget(aimDirection);
            if (target?.SourceTransform == null)
            {
                return false;
            }

            Vector3 targetPosition = target.SourceTransform.position;
            rightFireOrigin = GetRightFireOrigin();
            float parryShotDistance = Mathf.Min(PlayerAttackRange, Vector2.Distance(rightFireOrigin.position, targetPosition));
            Vector2 direction = AimGunAndGetDirection(rightGunOrigin, rightFireOrigin, (Vector2)(targetPosition - rightGunOrigin.position));
            bool parried = target.TryParry(this);
            if (!parried)
            {
                return false;
            }

            SuppressHeatCooling(config.ActionHeatCoolingSuppressSeconds);
            LockRightGunAim(direction);

            PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                rightFireOrigin.position,
                direction,
                this,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                parryShotDistance,
                0f,
                0f,
                ParryEffectColor,
                false);

            return true;
        }

        private IAttackSource FindClosestParryTarget(Vector2 aimDirection)
        {
            Vector2 parryCenter = GetParryCenter();
            Collider2D[] hits = Physics2D.OverlapCircleAll(parryCenter, PlayerAttackRange, parryMask);
            IAttackSource bestTarget = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                MonoBehaviour[] behaviours = hits[i].GetComponentsInParent<MonoBehaviour>();
                for (int j = 0; j < behaviours.Length; j++)
                {
                    if (behaviours[j] is not IAttackSource source || source.SourceTransform == null)
                    {
                        continue;
                    }

                    Vector2 toSource = (Vector2)(source.SourceTransform.position - rightGunOrigin.position);
                    if (!IsInsideAimAngle(toSource, aimDirection, config.ParryAimAngleDegrees))
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(parryCenter, source.SourceTransform.position);
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestTarget = source;
                    bestDistance = distance;
                }
            }

            return bestTarget;
        }

        private Vector2 GetParryCenter()
        {
            return bodyRoot != null ? bodyRoot.position : transform.position;
        }

        private void UpdateLockOnInput()
        {
            if (!GameInput.GetKeyDown(KeyCode.Q))
            {
                return;
            }

            SetLockOnTarget(FindLockOnTarget());
        }

        private Health FindLockOnTarget()
        {
            Vector2 mouseWorld = GetMouseWorldPosition();
            Collider2D[] hits = Physics2D.OverlapCircleAll(mouseWorld, config.LockOnSearchRadius, enemyMask);
            Health bestTarget = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                Health targetHealth = hits[i].GetComponentInParent<Health>();
                if (!IsValidLockOnTarget(targetHealth))
                {
                    continue;
                }

                float distance = Vector2.Distance(mouseWorld, hits[i].bounds.center);
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
                if (!IsValidLockOnTarget(targetHealth))
                {
                    continue;
                }

                float distance = Vector2.Distance(mouseWorld, targetHealth.transform.position);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestTarget = targetHealth;
                bestDistance = distance;
            }

            return bestTarget;
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
            if (nextTarget != null && lockOnTarget == nextTarget)
            {
                return;
            }

            lockOnTarget = nextTarget;
            if (cameraFollow != null)
            {
                cameraFollow.SetFocusTarget(lockOnTarget != null ? lockOnTarget.transform : null);
            }
        }

        private bool IsValidLockOnTarget(Health targetHealth)
        {
            return targetHealth != null
                && targetHealth != health
                && !targetHealth.IsDead
                && targetHealth.GetComponent<Week14.Enemy.EnemyAI>() != null;
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

        private void UpdateRangeIndicators()
        {
            EnsureRangeIndicators();
            SetRangeIndicatorsVisible(true);
            UpdateAttackRangeIndicator(PlayerAttackRange);
            UpdateParryRangeIndicator(PlayerAttackRange, GetParryIndicatorDirection());
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
                    attackRangeDashes[i] = CreateRangeLine(dashObject, new Color(0.65f, 0.65f, 0.65f, 0.42f), 0.025f, 2);
                }
            }

            if (parryRangeLine == null)
            {
                GameObject parryObject = new GameObject(ParryRangeIndicatorName);
                parryObject.transform.SetParent(rangeIndicatorRoot != null ? rangeIndicatorRoot : transform, false);
                Color parryColor = ParryEffectColor;
                parryColor.a = Mathf.Max(parryColor.a, 0.85f);
                parryRangeLine = CreateRangeLine(parryObject, parryColor, 0.04f, 32);
                parryRangeLine.sortingOrder = 4;
            }
        }

        private void UpdateAttackRangeIndicator(float radius)
        {
            if (attackRangeDashes == null)
            {
                return;
            }

            Vector3 center = GetRangeIndicatorCenter();
            float segmentAngle = Mathf.PI * 2f / attackRangeDashes.Length;
            float dashAngle = segmentAngle * 0.45f;
            for (int i = 0; i < attackRangeDashes.Length; i++)
            {
                LineRenderer dash = attackRangeDashes[i];
                if (dash == null)
                {
                    continue;
                }

                float startAngle = segmentAngle * i;
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

            Color parryColor = ParryEffectColor;
            parryColor.a = Mathf.Max(parryColor.a, 0.85f);
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

        private Vector2 GetParryIndicatorDirection()
        {
            if (lockOnTarget == null && Time.time <= rightGunAimLockedUntil && rightGunLockedDirection.sqrMagnitude > 0.0001f)
            {
                return rightGunLockedDirection;
            }

            return GetAimDirection(rightGunOrigin);
        }

        private void SetRangeIndicatorsVisible(bool visible)
        {
            if (attackRangeDashes != null)
            {
                for (int i = 0; i < attackRangeDashes.Length; i++)
                {
                    if (attackRangeDashes[i] != null)
                    {
                        attackRangeDashes[i].enabled = visible;
                    }
                }
            }

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

            leftGunOrigin ??= transform;
            rightGunOrigin ??= leftGunOrigin;
            leftGunFireOrigin ??= leftGunOrigin;
            rightGunFireOrigin ??= rightGunOrigin;
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

        private static bool IsInsideAimAngle(Vector2 toTarget, Vector2 forward, float angleDegrees)
        {
            if (toTarget.sqrMagnitude <= 0.0001f || angleDegrees >= 360f)
            {
                return true;
            }

            return Vector2.Angle(forward, toTarget) <= angleDegrees * 0.5f;
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
            Gizmos.DrawLine(leftOrigin.position, leftOrigin.position + leftOrigin.right * PlayerAttackRange);
            Gizmos.color = Color.cyan;
            Vector3 parryCenter = bodyRoot != null ? bodyRoot.position : transform.position;
            Gizmos.DrawWireSphere(parryCenter, PlayerAttackRange);
        }

        private float PlayerAttackDamage => config.AttackDamage;
        private float PlayerAttackHeat => config.AttackHeat;
        private float PlayerAttackRange => config.AttackRange;
        private float PlayerAttackCooldown => config.AttackCooldown;
        private float CombatEffectSeconds => config.CombatEffectSeconds;
        private float GunAimHoldSeconds => config.GunAimHoldSeconds;
        private Color AttackEffectColor => config.AttackEffectColor;
        private float ParryShotCooldown => config.ParryShotCooldown;
        private Color ParryEffectColor => config.ParryEffectColor;
        private float HitHeatToPlayer => config.HitHeatToPlayer;
    }
}
