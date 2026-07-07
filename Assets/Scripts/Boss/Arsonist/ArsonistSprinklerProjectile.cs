using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Boss/Arsonist/Sprinkler Projectile")]
    public sealed class ArsonistSprinklerProjectile : MonoBehaviour
    {
        [SerializeField] private string sprinklerId;
        [SerializeField, Min(0.05f)] private float parryRadius = 0.45f;
        [SerializeField, Min(0.05f)] private float waterRadius = 1.2f;
        [SerializeField, Min(0.05f)] private float waterDuration = 0.85f;
        [SerializeField] private Color waterColor = new(0.3f, 0.75f, 1f, 0.62f);
        [SerializeField] private bool activeOnCombatStart;

        private ArsonistBossAI owner;
        private ArsonistSprinklerParryTarget activeTarget;
        private bool functionalActive;
        private bool used;

        public string SprinklerId => sprinklerId;
        public bool IsFunctionalActive => functionalActive && !used;
        public bool IsUsed => used;

        internal void ResetForCombat(ArsonistBossAI nextOwner)
        {
            owner = nextOwner != null ? nextOwner : owner;
            used = false;
            SetFunctionalActive(false);
            if (activeOnCombatStart)
            {
                SetFunctionalActive(true);
            }
        }

        internal void SetFunctionalActive(ArsonistBossAI nextOwner, bool active)
        {
            owner = nextOwner != null ? nextOwner : owner;
            SetFunctionalActive(active);
        }

        internal void NotifyTargetDestroyed(ArsonistSprinklerParryTarget target)
        {
            if (activeTarget == target)
            {
                activeTarget = null;
            }
        }

        internal void HandleParried(Vector3 parryPosition)
        {
            if (used)
            {
                return;
            }

            used = true;
            functionalActive = false;
            activeTarget = null;
            ResolveOwner()?.CreateWaterArea(parryPosition, waterRadius, waterDuration, waterColor);
        }

        private void SetFunctionalActive(bool active)
        {
            if (!active)
            {
                functionalActive = false;
                DestroyActiveTarget();
                return;
            }

            if (used)
            {
                return;
            }

            functionalActive = true;
            EnsureParryTarget();
        }

        private void EnsureParryTarget()
        {
            if (activeTarget != null)
            {
                activeTarget.transform.position = transform.position;
                return;
            }

            GameObject targetObject = new($"{name}_ParryTarget");
            targetObject.transform.SetParent(transform, false);
            targetObject.transform.position = transform.position;

            Rigidbody2D body = targetObject.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            CircleCollider2D collider = targetObject.AddComponent<CircleCollider2D>();
            collider.isTrigger = true;
            collider.radius = Mathf.Max(0.05f, parryRadius);

            activeTarget = targetObject.AddComponent<ArsonistSprinklerParryTarget>();
            activeTarget.Initialize(this, parryRadius, waterColor);
        }

        private void DestroyActiveTarget()
        {
            if (activeTarget == null)
            {
                return;
            }

            ArsonistSprinklerParryTarget target = activeTarget;
            activeTarget = null;
            target.DetachSprinkler();
            target.DestroyFromOwner();
        }

        private ArsonistBossAI ResolveOwner()
        {
            if (owner != null)
            {
                return owner;
            }

            owner = GetComponentInParent<ArsonistBossAI>();
            return owner;
        }

        private void LateUpdate()
        {
            if (activeTarget != null)
            {
                activeTarget.transform.position = transform.position;
            }
        }

        private void OnDisable()
        {
            DestroyActiveTarget();
        }
    }

    internal sealed class ArsonistSprinklerParryTarget : EnemyProjectile
    {
        private ArsonistSprinklerProjectile sprinkler;

        protected override bool UsesProjectileVisibility => false;
        protected override bool ShowsPathIndicator => false;

        internal void Initialize(ArsonistSprinklerProjectile nextSprinkler, float radius, Color color)
        {
            sprinkler = nextSprinkler;
            InitializeStationaryParryTarget(radius, color);
            ConfigurePathIndicatorSuppressed(true);
        }

        internal void DetachSprinkler()
        {
            sprinkler = null;
        }

        protected override bool CanHitPlayer(PlayerCombatController player)
        {
            return false;
        }

        protected override void OnProjectileDestroying(EnemyProjectileDestroyReason reason, Vector3 position)
        {
            if (reason == EnemyProjectileDestroyReason.Intercepted)
            {
                sprinkler?.HandleParried(position);
            }
        }

        protected override void OnDestroy()
        {
            sprinkler?.NotifyTargetDestroyed(this);
            base.OnDestroy();
        }
    }
}
