using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
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
    public sealed class HackerBossAI : GraphBossAI
    {
        private const string TurretLayerName = "Turret";

        protected override bool RotatesBodyToPlayer => false;

        [Header("Hacking")]
        [SerializeField, Min(1)] private int hackingMax = 5;
        [SerializeField, Min(0.1f)] private float parryDisableSeconds = 3f;

        [Header("Wire Settings")]
        [SerializeField] private HackerWireSettings wireSettings = new();

        [Header("Editor")]
        [SerializeField] private bool drawApproachRangeGizmos = true;

        private readonly Dictionary<HackerThrownWeaponType, HackerThrownWeapon> groundedWeapons = new();
        private Quaternion facingVisualBaseLocalRotation;
        private bool hasFacingVisualBaseLocalRotation;
        private bool isFacingLeft = true;
        private int facingLockedNodeExecutionVersion = -1;
        private float lastSlamAt = float.NegativeInfinity;
        private bool gunWalkCounterParryArmed;
        private bool gunWalkCounterParryTriggered;
        public override bool SuppressesBodyContactDamage => true;
        internal HackerWireSettings WireSettings => wireSettings ??= new HackerWireSettings();

        internal bool IsFacingLeft => isFacingLeft;

        internal bool IsConsecutiveSlam(float chainSeconds)
        {
            bool consecutive = Time.time - lastSlamAt <= Mathf.Max(0f, chainSeconds);
            lastSlamAt = Time.time;
            return consecutive;
        }

        internal void ApplyHacking(PlayerCombatController player, int hackingPerHit)
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
            if (GraphContext?.IsNodeActionExecuting == true)
            {
                if (facingLockedNodeExecutionVersion != GraphContext.NodeExecutionVersion)
                {
                    UpdateFacingFromPlayer();
                    facingLockedNodeExecutionVersion = GraphContext.NodeExecutionVersion;
                }

                return;
            }

            facingLockedNodeExecutionVersion = -1;
            UpdateFacingFromPlayer();
        }

        protected override void OnBossDied()
        {
            ClearGroundedWeapons();
            base.OnBossDied();
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
            base.OnDisable();
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
            Transform facingVisual = BodyRoot;
            if (Player == null || facingVisual == null)
            {
                return;
            }

            float horizontalOffset = Player.position.x - transform.position.x;
            if (Mathf.Abs(horizontalOffset) <= 0.0001f)
            {
                return;
            }

            if (!hasFacingVisualBaseLocalRotation)
            {
                facingVisualBaseLocalRotation = facingVisual.localRotation;
                hasFacingVisualBaseLocalRotation = true;
            }

            isFacingLeft = horizontalOffset < 0f;
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
    }
}
