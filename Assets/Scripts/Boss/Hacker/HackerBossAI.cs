using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;
using Week14.UI;

namespace Week14.Enemy
{
    internal enum HackerFireWireResult
    {
        None,
        PlayerGrabbed,
        Missed
    }

    [System.Serializable]
    public sealed class HackerWireSettings
    {
        [SerializeField, Min(0.01f)] private float flightSpeed = 12f;
        [SerializeField, Min(0.01f)] private float width = 0.04f;
        [SerializeField, Min(0.01f)] private float hitRadius = 0.08f;
        [SerializeField, Min(0.01f)] private float pullSpeed = 16f;
        [SerializeField, Min(0f)] private float pullStopDistance = 0.75f;
        [SerializeField] private Color color = new(0.55f, 0.82f, 1f, 0.9f);

        public float FlightSpeed => flightSpeed;
        public float Width => width;
        public float HitRadius => hitRadius;
        public float PullSpeed => pullSpeed;
        public float PullStopDistance => pullStopDistance;
        public Color Color => color;
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Boss/Hacker Boss")]
    public class HackerBossAI : GraphBossAI
    {
        private const string TurretLayerName = "Turret";
        private const string IsWalkAnimationParameter = "isWalk";
        private const string TwoWeaponVisualName = "boss-05-2weapon";
        private const string OneWeaponVisualName = "boss-05-1weapon";
        private const string TwoWeaponStunEndStateName = "Anim-Hacker-2w-stun-end";

        protected override GameObject BossMuzzleFlashVfxPrefab => EffectData != null
            ? EffectData.HackerMuzzleFlashVfxPrefab
            : null;
        protected override bool RotatesBodyToPlayer => false;

        [Header("Wire Bullet Lifetime Penalty")]
        [SerializeField, Min(0f)] private float wireLifetimeReductionSeconds = 2.5f;
        [SerializeField, Min(0f)] private float wireMinimumRemainingSeconds = 1f;
        [SerializeField] private Color wireContactFlashColor = new(0.2f, 0.7f, 1f, 1f);
        [SerializeField, Min(0f)] private float wireContactFlashSeconds = 0.18f;
        [SerializeField, Range(0f, 1f)] private float wireBulletShakeIntensity = 0.4f;

        [Header("Wire Settings")]
        [SerializeField] private HackerWireSettings wireSettings = new();

        [Header("Parry Bait Projectile")]
        [Tooltip("Melee, Thrust, Dash Sweep이 공통으로 사용하는 ParryBaitRewardProjectile 설정입니다.")]
        [SerializeField] private BossProjectileSettings parryProjectileSettings = new();

        [Header("Hologram")]
        [SerializeField] private HackerHologramBoss hologramPrefab;
        [SerializeField, Min(1)] private int hologramStartPhaseNumber = 3;

        [Header("Facing")]
        [SerializeField] private Transform facingVisual;
        [SerializeField] private Transform facingHand;
        [SerializeField] private Transform facingParryingPoint;
        [SerializeField] private Transform facingMuzzlePoints;
        [SerializeField] private Transform facingEffectPrefabPoint;

        [Header("Animation")]
        [SerializeField, Min(0f)] private float walkVelocityThreshold = 0.01f;

        [Header("Phase Visuals")]
        [SerializeField] private GameObject twoWeaponVisual;
        [SerializeField] private GameObject oneWeaponVisual;
        [SerializeField, Min(0.1f)] private float phaseVisualSwitchFallbackSeconds = 1f;

        [Header("Attack Indicators")]
        [Tooltip("Melee, Thrust, Sweep 계열 공격의 범위 인디케이터를 표시합니다.")]
        [InspectorName("Melee / Thrust / Sweep 인디케이터 표시")]
        [SerializeField] private bool showAttackRangeIndicators = true;

        [Header("Editor")]
        [SerializeField] private bool drawApproachRangeGizmos = true;

        private readonly Dictionary<HackerThrownWeaponType, HackerThrownWeapon> groundedWeapons = new();
        private Quaternion facingVisualBaseLocalRotation;
        private Quaternion facingHandBaseLocalRotation;
        private Quaternion facingParryingPointBaseLocalRotation;
        private Quaternion facingMuzzlePointsBaseLocalRotation;
        private Quaternion facingEffectPrefabPointBaseLocalRotation;
        private bool facingTargetsResolved;
        private bool hasFacingBaseLocalRotations;
        private bool isFacingLeft = true;
        private bool hasAppliedWalkState;
        private bool lastIsWalking;
        private float lastSlamAt = float.NegativeInfinity;
        private bool gunWalkCounterParryArmed;
        private bool gunWalkCounterParryTriggered;
        private bool isHologramSummonUnlocked;
        private HackerFireWireResult lastFireWireResult;
        private HackerHologramBoss hologram;
        private Animator twoWeaponAnimator;
        private Animator oneWeaponAnimator;
        private Coroutine phaseVisualSwitchRoutine;
        private bool isOneWeaponVisualActive;
        protected virtual bool UsesHackerPresentationUpdates => true;
        public override bool SuppressesBodyContactDamage => true;
        internal virtual HackerWireSettings WireSettings => wireSettings ??= new HackerWireSettings();
        internal virtual BossProjectileSettings ParryProjectileSettings => parryProjectileSettings ??= new BossProjectileSettings();
        internal virtual bool ShowsAttackRangeIndicators => showAttackRangeIndicators;

        internal bool IsFacingLeft => isFacingLeft;
        internal HackerFireWireResult LastFireWireResult => lastFireWireResult;

        internal void SetLastFireWireResult(HackerFireWireResult result)
        {
            lastFireWireResult = result;
        }

        internal bool IsConsecutiveSlam(float chainSeconds)
        {
            bool consecutive = Time.time - lastSlamAt <= Mathf.Max(0f, chainSeconds);
            lastSlamAt = Time.time;
            return consecutive;
        }

        internal virtual void ApplyWireLifetimePenalty(PlayerCombatController player)
        {
            if (player == null)
            {
                return;
            }

            PlayerHP.ShortenCurrentBulletLifetimes(
                player.Bullets,
                Mathf.Max(0f, wireLifetimeReductionSeconds),
                Mathf.Max(0f, wireMinimumRemainingSeconds));
            PlayerHP.PlayBulletShake(player.Bullets, wireBulletShakeIntensity);
            player.FlashBodyColor(wireContactFlashColor, wireContactFlashSeconds);
        }

        internal void BeginGunWalkCounterParry()
        {
            gunWalkCounterParryArmed = true;
            gunWalkCounterParryTriggered = false;
        }

        internal bool IsGunWalkCounterParryTriggered => gunWalkCounterParryTriggered;

        internal void EndGunWalkCounterParry()
        {
            gunWalkCounterParryArmed = false;
            gunWalkCounterParryTriggered = false;
        }

        internal void RegisterGroundedWeapon(HackerThrownWeapon weapon)
        {
            if (weapon == null)
            {
                return;
            }

            if (groundedWeapons.TryGetValue(weapon.WeaponType, out HackerThrownWeapon previous)
                && previous != null
                && previous != weapon)
            {
                Destroy(previous.gameObject);
            }

            groundedWeapons[weapon.WeaponType] = weapon;
        }

        public bool TryGetGroundedWeapon(HackerThrownWeaponType weaponType, out HackerThrownWeapon weapon)
        {
            if (groundedWeapons.TryGetValue(weaponType, out weapon) && weapon != null)
            {
                return true;
            }

            groundedWeapons.Remove(weaponType);
            weapon = null;
            return false;
        }

        internal void UnregisterGroundedWeapon(HackerThrownWeapon weapon)
        {
            if (weapon != null && groundedWeapons.TryGetValue(weapon.WeaponType, out HackerThrownWeapon current) && current == weapon)
            {
                groundedWeapons.Remove(weapon.WeaponType);
            }
        }

        protected override void Start()
        {
            base.Start();
            ResolvePhaseVisuals();
            ApplyPhaseVisual(CurrentPhaseNumber >= 2, true);
            IgnorePlayerPhysicsCollisions();
            IgnoreTurretLayerCollisions();
            UpdateFacingFromPlayer();
        }

        private void LateUpdate()
        {
            if (UsesHackerPresentationUpdates)
            {
                UpdateWalkState();
                UpdateFacingFromPlayer();
            }

            OnIdleHackerLateUpdate();
        }

        protected override void OnBossDied()
        {
            ApplyWalkState(false, true);
            HackerWireNodeProjectile.ClearAttachedNodes(this);
            ClearGroundedWeapons();
            DestroyHologram();
            base.OnBossDied();
        }

        protected override void OnHpEmptyBegan()
        {
            PlayGroggyStunVisual();
            base.OnHpEmptyBegan();
            DestroyActiveProjectiles();
            ClearRuntimeCombatEffects();
            ClearSpawnedWeapons();
        }

        protected override void OnHpEmptyRecovered()
        {
            PlayGroggyEndStunVisual();
            if (CurrentPhaseNumber == 2 && !isOneWeaponVisualActive)
            {
                BeginOneWeaponVisualSwitch();
            }

            base.OnHpEmptyRecovered();
        }

        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            base.OnBossPhaseChanged(phaseIndex, phaseNumber);
            HackerWireNodeProjectile.ClearAttachedNodes(this);

            int hologramStartPhase = Mathf.Max(1, hologramStartPhaseNumber);
            if (phaseNumber < hologramStartPhase)
            {
                isHologramSummonUnlocked = false;
                return;
            }

            if (phaseNumber == hologramStartPhase)
            {
                // 홀로그램 리플레이 노드가 먼저 실행되어도 3페이즈 전에는 생성하지 않는다.
                isHologramSummonUnlocked = true;
                TryEnsureHologram(playSummonEntrance: true);
            }
        }

        internal void ClearPatternSpawnedWeapons()
        {
            ClearSpawnedWeapons();
        }

        protected override bool TryHandlePlayerHitBeforeDamage(
            int bulletDamage,
            bool strongHit,
            Vector3 hitPosition,
            Vector2 hitDirection,
            Color hitColor)
        {
            if (!gunWalkCounterParryArmed || gunWalkCounterParryTriggered)
            {
                return false;
            }

            gunWalkCounterParryTriggered = true;
            return true;
        }

        protected override void OnDisable()
        {
            if (phaseVisualSwitchRoutine != null)
            {
                StopCoroutine(phaseVisualSwitchRoutine);
                phaseVisualSwitchRoutine = null;
            }

            ApplyWalkState(false, true);
            EndGunWalkCounterParry();
            ClearGroundedWeapons();
            DestroyHologram();
            base.OnDisable();
        }

        protected virtual void OnIdleHackerLateUpdate() { }

        private void BeginOneWeaponVisualSwitch()
        {
            ResolvePhaseVisuals();
            if (twoWeaponVisual == null || oneWeaponVisual == null || twoWeaponAnimator == null)
            {
                ApplyPhaseVisual(true, true);
                return;
            }

            if (phaseVisualSwitchRoutine != null)
            {
                StopCoroutine(phaseVisualSwitchRoutine);
            }

            phaseVisualSwitchRoutine = StartCoroutine(SwitchToOneWeaponAfterStunEnd());
        }

        private IEnumerator SwitchToOneWeaponAfterStunEnd()
        {
            int stunEndStateHash = Animator.StringToHash(TwoWeaponStunEndStateName);
            float timeoutSeconds = ResolveTwoWeaponStunEndTimeout();
            float elapsed = 0f;
            bool enteredStunEnd = false;

            yield return null;
            while (elapsed < timeoutSeconds && twoWeaponAnimator != null && twoWeaponAnimator.isActiveAndEnabled)
            {
                bool isTransitioning = twoWeaponAnimator.IsInTransition(0);
                AnimatorStateInfo currentState = twoWeaponAnimator.GetCurrentAnimatorStateInfo(0);
                bool isCurrentStunEnd = currentState.shortNameHash == stunEndStateHash;
                bool isNextStunEnd = isTransitioning
                    && twoWeaponAnimator.GetNextAnimatorStateInfo(0).shortNameHash == stunEndStateHash;

                if (!enteredStunEnd)
                {
                    enteredStunEnd = isCurrentStunEnd || isNextStunEnd;
                }
                else if (!isCurrentStunEnd && !isNextStunEnd)
                {
                    break;
                }
                else if (isCurrentStunEnd && currentState.normalizedTime >= 1f && !isTransitioning)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }

            ApplyPhaseVisual(true, true);
            phaseVisualSwitchRoutine = null;
        }

        private float ResolveTwoWeaponStunEndTimeout()
        {
            float fallback = Mathf.Max(0.1f, phaseVisualSwitchFallbackSeconds);
            RuntimeAnimatorController controller = twoWeaponAnimator != null
                ? twoWeaponAnimator.runtimeAnimatorController
                : null;
            if (controller == null)
            {
                return fallback;
            }

            AnimationClip[] clips = controller.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null && clip.name == TwoWeaponStunEndStateName)
                {
                    return Mathf.Max(fallback, clip.length + 0.25f);
                }
            }

            return fallback;
        }

        private void ResolvePhaseVisuals()
        {
            if (twoWeaponVisual == null)
            {
                Transform found = FindDescendant(TwoWeaponVisualName);
                twoWeaponVisual = found != null ? found.gameObject : null;
            }

            if (oneWeaponVisual == null)
            {
                Transform found = FindDescendant(OneWeaponVisualName);
                oneWeaponVisual = found != null ? found.gameObject : null;
            }

            if (twoWeaponAnimator == null && twoWeaponVisual != null)
            {
                twoWeaponAnimator = twoWeaponVisual.GetComponent<Animator>();
            }

            if (oneWeaponAnimator == null && oneWeaponVisual != null)
            {
                oneWeaponAnimator = oneWeaponVisual.GetComponent<Animator>();
            }
        }

        private void ApplyPhaseVisual(bool useOneWeapon, bool resetAnimator)
        {
            ResolvePhaseVisuals();
            GameObject activeVisual = useOneWeapon ? oneWeaponVisual : twoWeaponVisual;
            GameObject inactiveVisual = useOneWeapon ? twoWeaponVisual : oneWeaponVisual;
            Animator activeAnimator = useOneWeapon ? oneWeaponAnimator : twoWeaponAnimator;

            if (activeVisual != null)
            {
                activeVisual.SetActive(true);
            }

            if (resetAnimator && activeAnimator != null)
            {
                activeAnimator.Rebind();
                activeAnimator.Update(0f);
            }

            if (inactiveVisual != null)
            {
                inactiveVisual.SetActive(false);
            }

            SetPatternGroggyAnimator(activeAnimator);
            isOneWeaponVisualActive = useOneWeapon;
            hasAppliedWalkState = false;
        }

        protected override bool CanStartGraphPattern()
        {
            return (hologram == null || !hologram.IsPlayingSummonEntrance)
                && base.CanStartGraphPattern();
        }

        internal bool TryEnsureHologram(bool playSummonEntrance = false)
        {
            if (hologram != null)
            {
                return false;
            }

            if (hologramPrefab == null || !isHologramSummonUnlocked)
            {
                return false;
            }

            hologram = Instantiate(hologramPrefab, transform.position, transform.rotation);
            hologram.Initialize(this);
            if (playSummonEntrance)
            {
                hologram.PlaySummonEntrance();
            }
            return true;
        }

        internal bool TryGetHologram(out HackerHologramBoss result)
        {
            result = hologram;
            return result != null;
        }

        private void IgnorePlayerPhysicsCollisions()
        {
            if (Player == null)
            {
                return;
            }

            Collider2D[] bossColliders = GetComponentsInChildren<Collider2D>(true);
            Collider2D[] playerColliders = Player.GetComponentsInChildren<Collider2D>(true);
            for (int bossIndex = 0; bossIndex < bossColliders.Length; bossIndex++)
            {
                Collider2D bossCollider = bossColliders[bossIndex];
                if (bossCollider == null)
                {
                    continue;
                }

                for (int playerIndex = 0; playerIndex < playerColliders.Length; playerIndex++)
                {
                    Collider2D playerCollider = playerColliders[playerIndex];
                    if (playerCollider != null)
                    {
                        Physics2D.IgnoreCollision(bossCollider, playerCollider, true);
                    }
                }
            }
        }

        private void UpdateFacingFromPlayer()
        {
            if (Player == null || BodyRoot == null)
            {
                return;
            }

            if (GraphContext?.IsFacingLocked == true)
            {
                return;
            }

            float horizontalOffset = Player.position.x - transform.position.x;
            if (Mathf.Abs(horizontalOffset) <= 0.0001f)
            {
                return;
            }

            FaceHorizontalDirection(horizontalOffset);
        }

        private void UpdateWalkState()
        {
            float threshold = Mathf.Max(0f, walkVelocityThreshold);
            bool isWalking = Body != null
                && Body.linearVelocity.sqrMagnitude > threshold * threshold;
            ApplyWalkState(isWalking, false);
        }

        private void ApplyWalkState(bool isWalking, bool force)
        {
            if (!force && hasAppliedWalkState && lastIsWalking == isWalking)
            {
                return;
            }

            GraphContext?.SetAnimationBool(IsWalkAnimationParameter, isWalking);
            lastIsWalking = isWalking;
            hasAppliedWalkState = true;
        }

        internal void FaceHorizontalDirection(float horizontalDirection)
        {
            if (GraphContext?.IsFacingLocked == true
                || Mathf.Abs(horizontalDirection) <= 0.0001f)
            {
                return;
            }

            ResolveFacingTargets();
            if (!hasFacingBaseLocalRotations)
            {
                facingVisualBaseLocalRotation = facingVisual != null ? facingVisual.localRotation : Quaternion.identity;
                facingHandBaseLocalRotation = facingHand != null ? facingHand.localRotation : Quaternion.identity;
                facingParryingPointBaseLocalRotation = facingParryingPoint != null
                    ? facingParryingPoint.localRotation
                    : Quaternion.identity;
                facingMuzzlePointsBaseLocalRotation = facingMuzzlePoints != null
                    ? facingMuzzlePoints.localRotation
                    : Quaternion.identity;
                facingEffectPrefabPointBaseLocalRotation = facingEffectPrefabPoint != null
                    ? facingEffectPrefabPoint.localRotation
                    : Quaternion.identity;
                hasFacingBaseLocalRotations = true;
            }

            isFacingLeft = horizontalDirection < 0f;
            Quaternion facingRotation = Quaternion.Euler(0f, isFacingLeft ? 0f : 180f, 0f);
            ApplyFacingRotation(facingVisual, facingVisualBaseLocalRotation, facingRotation);
            ApplyFacingRotation(facingHand, facingHandBaseLocalRotation, facingRotation);
            ApplyFacingRotation(facingParryingPoint, facingParryingPointBaseLocalRotation, facingRotation);
            ApplyFacingRotation(facingMuzzlePoints, facingMuzzlePointsBaseLocalRotation, facingRotation);
            ApplyFacingRotation(facingEffectPrefabPoint, facingEffectPrefabPointBaseLocalRotation, facingRotation);
        }

        private void ResolveFacingTargets()
        {
            if (facingTargetsResolved)
            {
                return;
            }

            facingVisual ??= FindDescendant("Boss-Hacker Visual");
            facingHand ??= FindDescendant("Hand");
            facingParryingPoint ??= FindDescendant("ParryingPoint");
            facingMuzzlePoints ??= FindDescendant("MuzzlePoints");
            facingEffectPrefabPoint ??= FindDescendant("EffectPrefabPoint");
            facingTargetsResolved = true;
        }

        private Transform FindDescendant(string targetName)
        {
            Transform[] descendants = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                if (descendants[i] != null && descendants[i].name == targetName)
                {
                    return descendants[i];
                }
            }

            return null;
        }

        private static void ApplyFacingRotation(Transform target, Quaternion baseRotation, Quaternion facingRotation)
        {
            if (target != null)
            {
                target.localRotation = baseRotation * facingRotation;
            }
        }

        private void IgnoreTurretLayerCollisions()
        {
            int turretLayer = LayerMask.NameToLayer(TurretLayerName);
            if (turretLayer < 0)
            {
                return;
            }

            Collider2D[] bossColliders = GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < bossColliders.Length; i++)
            {
                Collider2D bossCollider = bossColliders[i];
                if (bossCollider != null)
                {
                    Physics2D.IgnoreLayerCollision(bossCollider.gameObject.layer, turretLayer, true);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (drawApproachRangeGizmos)
            {
                HackerActionRangeGizmo.Draw(transform.position, BossGraph);
            }
        }

        private void ClearGroundedWeapons()
        {
            List<HackerThrownWeapon> weapons = new(groundedWeapons.Values);
            groundedWeapons.Clear();
            foreach (HackerThrownWeapon weapon in weapons)
            {
                if (weapon != null)
                {
                    weapon.DespawnAndRestoreEquippedWeapon();
                }
            }
        }

        private void ClearSpawnedWeapons()
        {
            ClearGroundedWeapons();

            HackerThrownWeapon[] weapons = UnityEngine.Object.FindObjectsByType<HackerThrownWeapon>(FindObjectsSortMode.None);
            for (int i = 0; i < weapons.Length; i++)
            {
                HackerThrownWeapon weapon = weapons[i];
                if (weapon != null && weapon.IsOwnedBy(this))
                {
                    weapon.DespawnAndRestoreEquippedWeapon();
                }
            }
        }

        private void DestroyHologram()
        {
            if (hologram != null)
            {
                Destroy(hologram.gameObject);
                hologram = null;
            }
        }

        private static void ClearRuntimeCombatEffects()
        {
            DestroyRuntimeObjects<HackerAttackRangeIndicator>();
            DestroyRuntimeObjects<HackerSnipingChargeIndicator>();
            DestroyRuntimeObjects<HackerDashedAimIndicator>();
            DestroyRuntimeObjects<HackerSpiderWebHazard>();
            DestroyRuntimeObjects<HackerSpiderWebCellIndicator>();
            DestroyRuntimeObjects<HackerSpiderWebCellExplosionVisual>();
            DestroyRuntimeObjects<HackerWire>();
        }

        private static void DestroyRuntimeObjects<T>() where T : Component
        {
            T[] runtimeObjects = UnityEngine.Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            for (int i = 0; i < runtimeObjects.Length; i++)
            {
                if (runtimeObjects[i] != null)
                {
                    UnityEngine.Object.Destroy(runtimeObjects[i].gameObject);
                }
            }
        }
    }
}
