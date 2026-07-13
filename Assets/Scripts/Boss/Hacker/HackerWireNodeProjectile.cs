using System.Collections.Generic;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public sealed class HackerWireNodeProjectile : EnemyProjectile
    {
        private static readonly List<HackerWireNodeProjectile> AttachedNodes = new();

        private bool isAttached;
        private int wallLayer = -1;
        private HackerBossAI hackingOwner;
        private int hackingPerHit = 1;
        private float attachedLifetimeSeconds;

        public bool IsAttached => isAttached;
        internal HackerBossAI HackingOwner => hackingOwner ?? OwnerBoss as HackerBossAI;
        internal int HackingPerHit => hackingPerHit;

        protected override void OnProjectileAwake()
        {
            wallLayer = LayerMask.NameToLayer("Wall");
            ConfigureInterceptable(false);
        }

        protected override void OnProjectileInitialized()
        {
            isAttached = false;
            hackingOwner = OwnerBoss as HackerBossAI;
            hackingPerHit = 1;
            attachedLifetimeSeconds = 0f;
            ConfigurePlayerCollisionIgnored(true);
            ConfigureInterceptable(false);
        }

        protected override bool CanHitPlayer(PlayerCombatController player)
        {
            return false;
        }

        protected override bool TryDestroyIfCrossedWall()
        {
            return false;
        }

        public override bool TryDestroyByInterceptShot(out bool parried)
        {
            parried = false;
            return false;
        }

        internal void ConfigureWireHacking(HackerBossAI nextOwner, int nextHackingPerHit)
        {
            hackingOwner = nextOwner ?? OwnerBoss as HackerBossAI;
            hackingPerHit = Mathf.Max(1, nextHackingPerHit);
        }

        internal void ConfigureAttachedLifetime(float seconds)
        {
            attachedLifetimeSeconds = Mathf.Max(0f, seconds);
        }

        protected override void OnTriggerEnter2D(Collider2D other)
        {
            if (!isAttached && IsWallCollider(other))
            {
                AttachToWall();
            }
        }

        protected override void OnProjectileDestroying(EnemyProjectileDestroyReason reason, Vector3 position)
        {
            UnregisterNode(this);
        }

        protected override void OnProjectileReturnedToPool()
        {
            UnregisterNode(this);
            base.OnProjectileReturnedToPool();
        }

        private void AttachToWall()
        {
            isAttached = true;
            ConfigureExternalMotionDriven(true);
            if (attachedLifetimeSeconds > 0f)
            {
                OverrideProjectileLifetime(attachedLifetimeSeconds);
            }
            else
            {
                ConfigurePersistentLifetime();
            }
            if (ProjectileBody != null)
            {
                ProjectileBody.linearVelocity = Vector2.zero;
            }

            RegisterNode(this);
        }

        private static void RegisterNode(HackerWireNodeProjectile node)
        {
            RemoveInvalidNodes();
            for (int i = 0; i < AttachedNodes.Count; i++)
            {
                HackerWireNodeProjectile other = AttachedNodes[i];
                if (other != null && other != node)
                {
                    HackerWireNodeLinkVisual.Create(other, node);
                }
            }

            AttachedNodes.Add(node);
        }

        private static void UnregisterNode(HackerWireNodeProjectile node)
        {
            AttachedNodes.Remove(node);
        }

        private static void RemoveInvalidNodes()
        {
            for (int i = AttachedNodes.Count - 1; i >= 0; i--)
            {
                if (AttachedNodes[i] == null || !AttachedNodes[i].isAttached)
                {
                    AttachedNodes.RemoveAt(i);
                }
            }
        }

        private bool IsWallCollider(Collider2D collider)
        {
            return collider != null && wallLayer >= 0 && collider.gameObject.layer == wallLayer;
        }
    }

    internal sealed class HackerWireNodeLinkVisual : MonoBehaviour
    {
        private HackerWireNodeProjectile first;
        private HackerWireNodeProjectile second;
        private HackerBossAI hackingOwner;
        private int hackingPerHit;
        private float hitRadius;
        private bool playerWasTouching;
        private LineRenderer line;

        internal static void Create(HackerWireNodeProjectile first, HackerWireNodeProjectile second)
        {
            if (first == null || second == null)
            {
                return;
            }

            GameObject linkObject = new("HackerWireNodeLink");
            HackerWireNodeLinkVisual link = linkObject.AddComponent<HackerWireNodeLinkVisual>();
            link.first = first;
            link.second = second;
            link.hackingOwner = second.HackingOwner ?? first.HackingOwner;
            link.hackingPerHit = Mathf.Max(1, second.HackingPerHit);
            link.hitRadius = link.hackingOwner != null
                ? Mathf.Max(0.01f, link.hackingOwner.WireSettings.HitRadius)
                : 0.08f;
            link.CreateLine();
        }

        private void Update()
        {
            if (first == null || second == null)
            {
                Destroy(gameObject);
                return;
            }

            PlayerCombatController player = PlayerCombatController.Active;
            bool isTouching = IsPlayerTouchingWire(player);
            if (isTouching && !playerWasTouching && hackingOwner != null)
            {
                hackingOwner.ApplyHacking(player, hackingPerHit);
            }

            playerWasTouching = isTouching;
        }

        private void LateUpdate()
        {
            if (first == null || second == null)
            {
                Destroy(gameObject);
                return;
            }

            line.SetPosition(0, first.transform.position);
            line.SetPosition(1, second.transform.position);
        }

        private bool IsPlayerTouchingWire(PlayerCombatController player)
        {
            if (player == null)
            {
                return false;
            }

            Vector2 start = first.transform.position;
            Vector2 end = second.transform.position;
            Vector2 closestPoint = GetClosestPointOnSegment(player.transform.position, start, end);
            Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D playerCollider = playerColliders[i];
                if (playerCollider == null || !playerCollider.enabled || !playerCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 colliderPoint = playerCollider.ClosestPoint(closestPoint);
                if (Vector2.Distance(colliderPoint, closestPoint) <= hitRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private static Vector2 GetClosestPointOnSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= 0.0001f)
            {
                return start;
            }

            float progress = Mathf.Clamp01(Vector2.Dot(point - start, segment) / lengthSquared);
            return start + segment * progress;
        }

        private void CreateLine()
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = 0.035f;
            line.endWidth = 0.035f;
            line.startColor = new Color(0.35f, 0.8f, 1f, 0.85f);
            line.endColor = new Color(0.35f, 0.8f, 1f, 0.85f);
            line.sortingOrder = 18;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.material = new Material(shader);
            }
        }
    }
}
