using System;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum HackerWireResolution
    {
        PlayerGrabbed,
        WallAttached,
        Expired
    }

    internal sealed class HackerWire : MonoBehaviour
    {
        private const float DissolveSeconds = 0.2f;

        private enum WireState
        {
            Flying,
            AttachedToWall,
            AttachedToPlayer,
            Dissolving
        }

        private HackerBossAI owner;
        private Transform anchor;
        private Transform playerAnchor;
        private Vector2 endpoint;
        private Vector2 direction;
        private float flightSpeed;
        private float hitRadius;
        private float grabSeconds;
        private float pullSpeed;
        private float pullStopDistance;
        private int hackingPerHit;
        private float expiresAt;
        private float wallAttachedSeconds;
        private float attachedExpiresAt;
        private bool canGrabPlayer;
        private bool persistsAfterPlayerHit;
        private bool persistsAfterWallHit;
        private bool appliesHackingOnTouch;
        private bool dissolvesOnPlayerTouch;
        private bool wasTouchingPlayer;
        private PlayerCombatController forcedGrabTarget;
        private bool hasResolved;
        private WireState state;
        private LineRenderer line;
        private Material lineMaterial;
        private float dissolveStartedAt;

        private int wallLayer = -1;

        internal Vector3 Position => playerAnchor != null ? playerAnchor.position : endpoint;
        internal event Action<HackerWireResolution> Resolved;

        private void Awake()
        {
            wallLayer = LayerMask.NameToLayer("Wall");
        }

        internal static HackerWire CreateGrab(
            HackerBossAI owner,
            Transform anchor,
            Vector2 direction,
            float flightSpeed,
            float flightSeconds,
            float grabSeconds,
            int hackingPerHit,
            float pullSpeed,
            float pullStopDistance,
            bool nextPersistsAfterPlayerHit,
            bool nextPersistsAfterWallHit,
            float wallAttachedSeconds,
            float width,
            float hitRadius,
            Color color)
        {
            HackerWire wire = Create(
                owner,
                anchor,
                direction,
                flightSpeed,
                flightSeconds,
                wallAttachedSeconds,
                nextPersistsAfterPlayerHit,
                nextPersistsAfterWallHit,
                width,
                hitRadius,
                color);
            wire.canGrabPlayer = true;
            wire.grabSeconds = Mathf.Max(0.05f, grabSeconds);
            wire.hackingPerHit = Mathf.Max(1, hackingPerHit);
            wire.pullSpeed = Mathf.Max(0.01f, pullSpeed);
            wire.pullStopDistance = Mathf.Max(0f, pullStopDistance);
            return wire;
        }

        internal static HackerWire CreateGuaranteedGrab(
            HackerBossAI owner,
            Transform anchor,
            PlayerCombatController target,
            float flightSeconds,
            float grabSeconds,
            int hackingPerHit,
            float pullSpeed,
            float pullStopDistance,
            float width,
            float hitRadius,
            Color color)
        {
            Vector2 origin = anchor != null ? anchor.position : owner != null ? owner.transform.position : Vector2.zero;
            Vector2 targetPosition = target != null ? target.transform.position : origin;
            float speed = Vector2.Distance(origin, targetPosition) / Mathf.Max(0.01f, flightSeconds);
            HackerWire wire = CreateGrab(
                owner,
                anchor,
                targetPosition - origin,
                Mathf.Max(0.01f, speed),
                flightSeconds,
                grabSeconds,
                hackingPerHit,
                pullSpeed,
                pullStopDistance,
                true,
                false,
                0f,
                width,
                hitRadius,
                color);
            wire.forcedGrabTarget = target;
            wire.expiresAt = Time.time + Mathf.Max(0.05f, flightSeconds * 2f);
            return wire;
        }

        internal static HackerWire CreatePersistentWallWire(
            HackerBossAI owner,
            Transform anchor,
            Vector2 direction,
            float flightSpeed,
            float flightSeconds,
            float wallAttachedSeconds,
            float width,
            float hitRadius,
            Color color,
            int hackingPerTouch = 0,
            bool dissolveOnPlayerTouch = false)
        {
            HackerWire wire = Create(
                owner,
                anchor,
                direction,
                flightSpeed,
                flightSeconds,
                wallAttachedSeconds,
                false,
                true,
                width,
                hitRadius,
                color);
            wire.hackingPerHit = Mathf.Max(0, hackingPerTouch);
            wire.appliesHackingOnTouch = wire.hackingPerHit > 0;
            wire.dissolvesOnPlayerTouch = dissolveOnPlayerTouch;
            return wire;
        }

        private static HackerWire Create(
            HackerBossAI nextOwner,
            Transform nextAnchor,
            Vector2 nextDirection,
            float nextFlightSpeed,
            float flightSeconds,
            float wallAttachedSeconds,
            bool nextPersistsAfterPlayerHit,
            bool nextPersistsAfterWallHit,
            float width,
            float nextHitRadius,
            Color color)
        {
            GameObject wireObject = new("HackerWire");
            HackerWire wire = wireObject.AddComponent<HackerWire>();
            wire.owner = nextOwner;
            wire.anchor = nextAnchor != null ? nextAnchor : nextOwner != null ? nextOwner.transform : null;
            wire.endpoint = wire.anchor != null ? wire.anchor.position : wire.transform.position;
            wire.direction = nextDirection.sqrMagnitude > 0.0001f ? nextDirection.normalized : Vector2.right;
            wire.flightSpeed = Mathf.Max(0.01f, nextFlightSpeed);
            wire.hitRadius = Mathf.Max(0.01f, nextHitRadius);
            wire.expiresAt = Time.time + Mathf.Max(0.05f, flightSeconds);
            wire.wallAttachedSeconds = Mathf.Max(0.05f, wallAttachedSeconds);
            wire.persistsAfterPlayerHit = nextPersistsAfterPlayerHit;
            wire.persistsAfterWallHit = nextPersistsAfterWallHit;
            wire.CreateLine(color, width);
            wire.UpdateLine();
            return wire;
        }

        private void Update()
        {
            switch (state)
            {
                case WireState.Flying:
                    TickFlight();
                    break;
                case WireState.AttachedToWall:
                    if (Time.time >= attachedExpiresAt)
                    {
                        Expire();
                    }
                    break;
                case WireState.AttachedToPlayer:
                    if (Time.time >= attachedExpiresAt)
                    {
                        BeginDissolve();
                    }
                    break;
                case WireState.Dissolving:
                    if (Time.time >= dissolveStartedAt + DissolveSeconds)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    break;
            }

            TickPlayerWireContact();
            UpdateLine();
        }

        private void TickFlight()
        {
            if (Time.time >= expiresAt)
            {
                Expire();
                return;
            }

            float distance = flightSpeed * EnemyTimeScale.DeltaTime;
            if (distance <= 0f)
            {
                return;
            }

            if (forcedGrabTarget != null)
            {
                TickGuaranteedGrab(distance);
                return;
            }

            if (TryGetCollision(distance, out Vector2 collisionPosition, out PlayerCombatController player))
            {
                endpoint = collisionPosition;
                if (player != null)
                {
                    GrabPlayer(player);
                }
                else
                {
                    AttachToWall();
                }

                return;
            }

            endpoint += direction * distance;
        }

        private void TickGuaranteedGrab(float distance)
        {
            if (forcedGrabTarget == null)
            {
                Expire();
                return;
            }

            Vector2 targetPosition = forcedGrabTarget.transform.position;
            Vector2 toTarget = targetPosition - endpoint;
            if (toTarget.magnitude <= distance + hitRadius)
            {
                endpoint = targetPosition;
                GrabPlayer(forcedGrabTarget);
                return;
            }

            endpoint += toTarget.normalized * distance;
        }

        private bool TryGetCollision(float distance, out Vector2 collisionPosition, out PlayerCombatController hitPlayer)
        {
            collisionPosition = endpoint + direction * distance;
            hitPlayer = null;
            float nearestDistance = float.MaxValue;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(endpoint, hitRadius, direction, distance);
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];
                Collider2D collider = hit.collider;
                if (collider == null || hit.distance >= nearestDistance)
                {
                    continue;
                }

                PlayerCombatController hitPlayerCandidate = canGrabPlayer
                    ? collider.GetComponentInParent<PlayerCombatController>()
                    : null;
                bool isWall = wallLayer >= 0 && collider.gameObject.layer == wallLayer;
                if (hitPlayerCandidate == null && !isWall)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                collisionPosition = hit.point;
                hitPlayer = hitPlayerCandidate;
            }

            if (canGrabPlayer && TryGetPlayerSegmentHit(distance, out PlayerCombatController segmentPlayer, out float playerDistance)
                && playerDistance < nearestDistance)
            {
                nearestDistance = playerDistance;
                collisionPosition = endpoint + direction * playerDistance;
                hitPlayer = segmentPlayer;
            }

            return nearestDistance < float.MaxValue;
        }

        private bool TryGetPlayerSegmentHit(float distance, out PlayerCombatController player, out float hitDistance)
        {
            player = PlayerCombatController.Active;
            hitDistance = 0f;
            if (player == null)
            {
                return false;
            }

            Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(true);
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D collider = playerColliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                Vector2 center = bounds.center;
                float projectedDistance = Mathf.Clamp(Vector2.Dot(center - endpoint, direction), 0f, distance);
                Vector2 closestPoint = endpoint + direction * projectedDistance;
                float colliderRadius = Mathf.Max(0.01f, bounds.extents.magnitude);
                if ((center - closestPoint).sqrMagnitude > Mathf.Pow(hitRadius + colliderRadius, 2f)
                    || projectedDistance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = projectedDistance;
            }

            if (nearestDistance == float.MaxValue)
            {
                player = null;
                return false;
            }

            hitDistance = nearestDistance;
            return true;
        }

        private void GrabPlayer(PlayerCombatController player)
        {
            playerAnchor = player.transform;
            endpoint = playerAnchor.position;
            owner?.ApplyHacking(player, hackingPerHit);
            Transform pullTarget = owner != null ? owner.transform : anchor;
            HackerWireGrab.Apply(player, pullTarget, grabSeconds, pullSpeed, pullStopDistance);
            Resolve(HackerWireResolution.PlayerGrabbed);
            if (!persistsAfterPlayerHit)
            {
                BeginDissolve();
                return;
            }

            state = WireState.AttachedToPlayer;
            attachedExpiresAt = Time.time + grabSeconds;
        }

        private void TickPlayerWireContact()
        {
            if (state == WireState.Dissolving || !appliesHackingOnTouch || hackingPerHit <= 0 || owner == null)
            {
                wasTouchingPlayer = false;
                return;
            }

            PlayerCombatController player = PlayerCombatController.Active;
            bool isTouchingPlayer = player != null && IsTouchingWire(player);
            if (isTouchingPlayer && !wasTouchingPlayer)
            {
                owner.ApplyHacking(player, hackingPerHit);
                if (dissolvesOnPlayerTouch)
                {
                    BeginDissolve();
                }
            }

            wasTouchingPlayer = isTouchingPlayer;
        }

        private bool IsTouchingWire(PlayerCombatController player)
        {
            if (player == null)
            {
                return false;
            }

            Vector2 start = anchor != null ? anchor.position : transform.position;
            Vector2 end = Position;
            Vector2 segment = end - start;
            float segmentLengthSqr = segment.sqrMagnitude;
            if (segmentLengthSqr <= 0.0001f)
            {
                return false;
            }

            Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D collider = playerColliders[i];
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                Vector2 center = bounds.center;
                float progress = Mathf.Clamp01(Vector2.Dot(center - start, segment) / segmentLengthSqr);
                Vector2 closestPoint = start + segment * progress;
                float contactRadius = hitRadius + Mathf.Max(0.01f, bounds.extents.magnitude);
                if ((center - closestPoint).sqrMagnitude <= contactRadius * contactRadius)
                {
                    return true;
                }
            }

            return false;
        }

        private void AttachToWall()
        {
            Resolve(HackerWireResolution.WallAttached);
            if (!persistsAfterWallHit)
            {
                BeginDissolve();
                return;
            }

            state = WireState.AttachedToWall;
            attachedExpiresAt = Time.time + wallAttachedSeconds;
        }

        private void Expire()
        {
            Resolve(HackerWireResolution.Expired);
            BeginDissolve();
        }

        internal void BeginDissolve()
        {
            if (state == WireState.Dissolving)
            {
                return;
            }

            state = WireState.Dissolving;
            dissolveStartedAt = Time.time;
        }

        private void UpdateLine()
        {
            if (line == null)
            {
                return;
            }

            Vector3 start = anchor != null ? anchor.position : transform.position;
            Vector3 end = Position;
            if (state == WireState.Dissolving)
            {
                float progress = Mathf.Clamp01((Time.time - dissolveStartedAt) / DissolveSeconds);
                line.SetPosition(0, Vector3.Lerp(start, end, progress));
                line.SetPosition(1, end);
                return;
            }

            line.SetPosition(0, start);
            line.SetPosition(1, end);
        }

        private void CreateLine(Color color, float width)
        {
            line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = Mathf.Max(0.01f, width);
            line.endWidth = Mathf.Max(0.01f, width);
            line.startColor = color;
            line.endColor = color;
            line.sortingOrder = 20;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                lineMaterial = new Material(shader);
                line.material = lineMaterial;
            }
        }

        private void Resolve(HackerWireResolution resolution)
        {
            if (hasResolved)
            {
                return;
            }

            hasResolved = true;
            Resolved?.Invoke(resolution);
        }

        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Destroy(lineMaterial);
            }
        }
    }

    internal sealed class HackerWireGrab : MonoBehaviour
    {
        private float endsAt;
        private bool hasLock;
        private Transform pullTarget;
        private float pullSpeed;
        private float pullStopDistance;

        internal static void Apply(
            PlayerCombatController player,
            Transform pullTarget,
            float seconds,
            float pullSpeed,
            float pullStopDistance)
        {
            HackerWireGrab grab = player.GetComponent<HackerWireGrab>();
            if (grab == null)
            {
                grab = player.gameObject.AddComponent<HackerWireGrab>();
            }

            grab.endsAt = Mathf.Max(grab.endsAt, Time.time + Mathf.Max(0.05f, seconds));
            grab.pullTarget = pullTarget;
            grab.pullSpeed = Mathf.Max(0.01f, pullSpeed);
            grab.pullStopDistance = Mathf.Max(0f, pullStopDistance);
            if (!grab.hasLock)
            {
                grab.hasLock = true;
                player.PushExternalMovementLock();
            }
        }

        internal bool TryPull(Rigidbody2D body)
        {
            if (!hasLock || body == null || pullTarget == null || Time.time >= endsAt)
            {
                return false;
            }

            Vector2 targetPosition = pullTarget.position;
            Vector2 nextPosition = Vector2.MoveTowards(
                body.position,
                targetPosition,
                pullSpeed * EnemyTimeScale.Current * Time.fixedDeltaTime);
            if (Vector2.Distance(nextPosition, targetPosition) <= pullStopDistance)
            {
                nextPosition = Vector2.MoveTowards(targetPosition, body.position, pullStopDistance);
            }

            body.MovePosition(nextPosition);
            body.linearVelocity = Vector2.zero;
            return true;
        }

        private void Update()
        {
            if (Time.time >= endsAt)
            {
                Release();
                Destroy(this);
            }
        }

        private void OnDestroy()
        {
            Release();
        }

        private void Release()
        {
            if (!hasLock)
            {
                return;
            }

            PlayerCombatController player = GetComponent<PlayerCombatController>();
            player?.PopExternalMovementLock();
            hasLock = false;
            pullTarget = null;
        }
    }
}
