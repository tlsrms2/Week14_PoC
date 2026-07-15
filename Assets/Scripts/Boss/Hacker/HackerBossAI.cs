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

        protected override bool RotatesBodyToPlayer => false;

        [Header("Hacking")]
        [SerializeField, Min(1)] private int hackingMax = 5;
        [SerializeField, Min(0.1f)] private float parryDisableSeconds = 3f;

        [Header("Wire Settings")]
        [SerializeField] private HackerWireSettings wireSettings = new();

        [Header("Hologram")]
        [SerializeField] private HackerHologramBoss hologramPrefab;
        [SerializeField, Min(1)] private int hologramStartPhaseNumber = 3;

        [Header("Editor")]
        [SerializeField] private bool drawApproachRangeGizmos = true;

        private readonly Dictionary<HackerThrownWeaponType, HackerThrownWeapon> groundedWeapons = new();
        private Quaternion facingVisualBaseLocalRotation;
        private bool hasFacingVisualBaseLocalRotation;
        private bool isFacingLeft = true;
        private float lastSlamAt = float.NegativeInfinity;
        private bool gunWalkCounterParryArmed;
        private bool gunWalkCounterParryTriggered;
        private HackerFireWireResult lastFireWireResult;
        private HackerHologramBoss hologram;
        public override bool SuppressesBodyContactDamage => true;
        internal virtual HackerWireSettings WireSettings => wireSettings ??= new HackerWireSettings();

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
            TrySummonHologramForCurrentPhase();
        }

        private void LateUpdate()
        {
            if (IsExternalActionExecuting)
            {
                return;
            }

            UpdateFacingFromPlayer();
            OnIdleHackerLateUpdate();
        }

        protected override void OnBossDied()
        {
            ClearGroundedWeapons();
            DestroyHologram();
            base.OnBossDied();
        }

        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            base.OnBossPhaseChanged(phaseIndex, phaseNumber);
            TrySummonHologramForCurrentPhase();
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
            EndGunWalkCounterParry();
            ClearGroundedWeapons();
            DestroyHologram();
            base.OnDisable();
        }

        protected virtual bool IsExternalActionExecuting => false;

        protected virtual void OnIdleHackerLateUpdate() { }

        internal bool TryEnsureHologram()
        {
            if (hologram != null)
            {
                return false;
            }

            if (hologramPrefab == null || CurrentPhaseNumber < Mathf.Max(1, hologramStartPhaseNumber))
            {
                return false;
            }

            hologram = Instantiate(hologramPrefab, transform.position, transform.rotation);
            hologram.Initialize(this);
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

        internal void FaceHorizontalDirection(float horizontalDirection)
        {
            Transform facingVisual = BodyRoot;
            if (facingVisual == null || Mathf.Abs(horizontalDirection) <= 0.0001f)
            {
                return;
            }

            if (!hasFacingVisualBaseLocalRotation)
            {
                facingVisualBaseLocalRotation = facingVisual.localRotation;
                hasFacingVisualBaseLocalRotation = true;
            }

            isFacingLeft = horizontalDirection < 0f;
            facingVisual.localRotation = facingVisualBaseLocalRotation
                * Quaternion.Euler(0f, isFacingLeft ? 0f : 180f, 0f);
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
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, DetectionRange);

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
                    Destroy(weapon.gameObject);
                }
            }
        }

        private void TrySummonHologramForCurrentPhase()
        {
            if (CurrentPhaseNumber >= Mathf.Max(1, hologramStartPhaseNumber))
            {
                TryEnsureHologram();
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
    }
}
