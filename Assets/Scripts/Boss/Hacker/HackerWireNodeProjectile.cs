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
        private HackerBossAI wireOwner;

        public bool IsAttached => isAttached;
        internal HackerBossAI WireOwner => wireOwner ?? OwnerBoss as HackerBossAI;

        protected override void OnProjectileAwake()
        {
            wallLayer = LayerMask.NameToLayer("Wall");
            ConfigureInterceptable(true);
        }

        protected override void OnProjectileInitialized()
        {
            isAttached = false;
            wireOwner = OwnerBoss as HackerBossAI;
            ConfigurePlayerCollisionIgnored(true);
            ConfigureInterceptable(true);
        }

        protected override bool CanHitPlayer(PlayerCombatController player)
        {
            return false;
        }

        protected override bool TryDestroyIfCrossedWall()
        {
            return false;
        }

        internal void ConfigureWireOwner(HackerBossAI nextOwner)
        {
            wireOwner = nextOwner ?? OwnerBoss as HackerBossAI;
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
            isAttached = false;
            UnregisterNode(this);
        }

        protected override void OnProjectileReturnedToPool()
        {
            isAttached = false;
            UnregisterNode(this);
            base.OnProjectileReturnedToPool();
        }

        private void AttachToWall()
        {
            isAttached = true;
            CancelInterceptReservation();
            ConfigureInterceptable(false);
            ConfigureExternalMotionDriven(true);
            ConfigurePersistentLifetime();
            OwnerBoss?.UnregisterActiveProjectile(this);
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
                if (other != null
                    && other != node
                    && other.WireOwner == node.WireOwner)
                {
                    HackerWireNodeLinkVisual.Create(other, node);
                }
            }

            AttachedNodes.Add(node);
        }

        internal static void ClearAttachedNodes(HackerBossAI owner)
        {
            RemoveInvalidNodes();
            for (int i = AttachedNodes.Count - 1; i >= 0; i--)
            {
                HackerWireNodeProjectile node = AttachedNodes[i];
                if (node != null && node.WireOwner == owner)
                {
                    node.DestroyFromOwner();
                }
            }

            RemoveInvalidNodes();
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
        private const float DissolveSeconds = 0.2f;

        private HackerWireNodeProjectile first;
        private HackerWireNodeProjectile second;
        private HackerBossAI wireOwner;
        private float hitRadius;
        private bool playerWasTouching;
        private LineRenderer line;
        private Vector3 firstPosition;
        private Vector3 secondPosition;
        private float dissolveStartedAt = -1f;

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
            link.wireOwner = second.WireOwner ?? first.WireOwner;
            link.hitRadius = link.wireOwner != null
                ? Mathf.Max(0.01f, link.wireOwner.WireSettings.HitRadius)
                : 0.08f;
            link.firstPosition = first.transform.position;
            link.secondPosition = second.transform.position;
            link.CreateLine();
        }

        private void Update()
        {
            if (dissolveStartedAt >= 0f)
            {
                if (Time.time >= dissolveStartedAt + DissolveSeconds)
                {
                    Destroy(gameObject);
                }
                return;
            }

            if (first == null
                || second == null
                || !first.IsAttached
                || !second.IsAttached)
            {
                dissolveStartedAt = Time.time;
                return;
            }

            firstPosition = first.transform.position;
            secondPosition = second.transform.position;

            PlayerCombatController player = PlayerCombatController.Active;
            bool isTouching = IsPlayerTouchingWire(player);
            if (isTouching && !playerWasTouching && wireOwner != null)
            {
                wireOwner.ApplyWireLifetimePenalty(player);
            }

            playerWasTouching = isTouching;
        }

        private void LateUpdate()
        {
            if (line == null)
            {
                return;
            }

            if (dissolveStartedAt >= 0f)
            {
                float progress = Mathf.Clamp01((Time.time - dissolveStartedAt) / DissolveSeconds);
                line.SetPosition(0, Vector3.Lerp(firstPosition, secondPosition, progress));
                line.SetPosition(1, secondPosition);
                return;
            }

            line.SetPosition(0, firstPosition);
            line.SetPosition(1, secondPosition);
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
            BossSorting.Apply(line);
            line.sortingOrder = 18;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                line.material = new Material(shader);
            }
        }
    }
}
