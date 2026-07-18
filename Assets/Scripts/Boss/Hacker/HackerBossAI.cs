using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

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

        protected override GameObject BossMuzzleFlashVfxPrefab => EffectData != null
            ? EffectData.HackerMuzzleFlashVfxPrefab
            : null;
        protected override bool RotatesBodyToPlayer => false;

        [Header("Hacking")]
        [SerializeField, Min(1)] private int hackingMax = 5;
        [SerializeField, Min(0.1f)] private float parryDisableSeconds = 3f;

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
        public override bool SuppressesBodyContactDamage => true;
        internal virtual HackerWireSettings WireSettings => wireSettings ??= new HackerWireSettings();
        internal virtual BossProjectileSettings ParryProjectileSettings => parryProjectileSettings ??= new BossProjectileSettings();

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

        internal virtual void ApplyHacking(PlayerCombatController player, int hackingPerHit)
        {
            HackerPlayerHackStatus.Apply(
                player,
                Mathf.Max(1, hackingPerHit),
                Mathf.Max(1, hackingMax),
                Mathf.Max(0.1f, parryDisableSeconds));
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
            IgnorePlayerPhysicsCollisions();
            IgnoreTurretLayerCollisions();
            UpdateFacingFromPlayer();
        }

        private void LateUpdate()
        {
            UpdateWalkState();
            if (IsExternalActionExecuting)
            {
                return;
            }

            UpdateFacingFromPlayer();
            OnIdleHackerLateUpdate();
        }

        protected override void OnBossDied()
        {
            ApplyWalkState(false, true);
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
            base.OnHpEmptyRecovered();
        }

        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            base.OnBossPhaseChanged(phaseIndex, phaseNumber);
            if (this is not HackerHologramBoss)
            {
                HackerPlayerHackStatus.Clear(PlayerCombatController.Active);
            }

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
            ApplyWalkState(false, true);
            EndGunWalkCounterParry();
            ClearGroundedWeapons();
            DestroyHologram();
            base.OnDisable();
        }

        protected virtual bool IsExternalActionExecuting => false;

        protected virtual void OnIdleHackerLateUpdate() { }

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
            if (Mathf.Abs(horizontalDirection) <= 0.0001f)
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
            DestroyRuntimeObjects<HackerWireNodeLinkVisual>();
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
