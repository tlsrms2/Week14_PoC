using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Combat;

namespace Week14.Enemy
{
    public enum HackerFireWireTargetMode
    {
        Player,
        PlayerNearbyWall
    }

    [Serializable]
    public sealed class HackerFireWireAction : BossAction, IBossActionDurationProvider
    {
        private const string WireShotAnimationTrigger = "WireShot";
        private const string GrabAnimationTrigger = "Grab";
        private const string IsWireShotActiveAnimationParameter = "IsWireShotActive";
        private const string IsWireGrabbingAnimationParameter = "IsWireGrabbing";

        [Header("Wire")]
        [FormerlySerializedAs("launchOriginPath")]
        [SerializeField, BossGraphBossChildPath] private string firePointPath;
        [SerializeField, Min(0f)] private float windupSeconds = 0.25f;
        [SerializeField, Min(0.05f)] private float maxFlightSeconds = 2f;
        [SerializeField] private HackerFireWireTargetMode targetMode;
        [SerializeField, Min(0.05f)] private float grabSeconds = 0.65f;
        [SerializeField, Min(1)] private int hackingPerHit = 1;
        [SerializeField, Min(0.05f)] private float playerWallSearchRadius = 5f;
        [SerializeField, Min(0f)] private float minimumWallDistance = 2f;
        [SerializeField, Min(1)] private int fireCount = 1;
        [SerializeField, Min(0f)] private float repeatIntervalSeconds = 0.15f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.2f;

        [Header("Wall Flight")]
        [SerializeField, Min(0.01f)] private float bossFlightSpeed = 14f;
        [SerializeField, Min(0.05f)] private float maxBossFlightSeconds = 1.2f;
        [SerializeField, Min(0.01f)] private float wallArrivalDistance = 0.45f;
        [SerializeField] private AnimationCurve bossFlightSpeedCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0.7f);

        [Header("Boss Flight Projectiles")]
        [SerializeField, BossGraphProjectileName] private string flightProjectileName = "Default";
        [SerializeField, HideInInspector] private BossProjectileSettings flightProjectile = new();
        [SerializeField, Min(0f)] private float flightProjectileInterval = 0.3f;
        [SerializeField, Min(0)] private int maxFlightProjectileCount = 4;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is not HackerBossAI hacker)
            {
                yield break;
            }

            hacker.SetLastFireWireResult(HackerFireWireResult.Missed);
            Vector2 facingDirection = context.GetDirectionToPlayer(hacker.transform.position);
            hacker.FaceHorizontalDirection(facingDirection.x);
            using IDisposable facingLock = context.AcquireFacingLock();
            bool playerGrabbed = false;
            try
            {
                context.SetAnimationBool(IsWireShotActiveAnimationParameter, true);
                context.RestartAnimationTrigger(WireShotAnimationTrigger);
                yield return HackerMeleeAttackAction.Wait(context, windupSeconds);

                int count = Mathf.Max(1, fireCount);
                for (int shotIndex = 0; shotIndex < count; shotIndex++)
                {
                    if (shotIndex > 0)
                    {
                        context.SetAnimationBool(IsWireShotActiveAnimationParameter, true);
                        context.RestartAnimationTrigger(WireShotAnimationTrigger);
                    }

                    if (!TryCreateWire(context, hacker, out HackerWire wire))
                    {
                        context.SetAnimationBool(IsWireShotActiveAnimationParameter, false);
                        break;
                    }

                    HackerWireResolution? resolution = null;
                    Vector3 wallPosition = default;
                    Action<HackerWireResolution> handleResolution = result =>
                    {
                        resolution = result;
                        if (result == HackerWireResolution.WallAttached)
                        {
                            wallPosition = wire.Position;
                        }
                    };
                    wire.Resolved += handleResolution;

                    try
                    {
                        float elapsed = 0f;
                        while (elapsed < maxFlightSeconds && !resolution.HasValue)
                        {
                            if (context.IsExecutionPaused)
                            {
                                context.Stop();
                                yield return null;
                                continue;
                            }

                            elapsed += EnemyTimeScale.DeltaTime;
                            yield return null;
                        }

                        if (resolution == HackerWireResolution.WallAttached)
                        {
                            yield return FlyBossToWall(context, wallPosition);
                        }
                        else if (resolution == HackerWireResolution.PlayerGrabbed)
                        {
                            playerGrabbed = true;
                            context.SetAnimationBool(IsWireShotActiveAnimationParameter, false);
                            context.SetAnimationBool(IsWireGrabbingAnimationParameter, true);
                            context.RestartAnimationTrigger(GrabAnimationTrigger);
                            yield return HackerMeleeAttackAction.Wait(context, grabSeconds);
                        }
                    }
                    finally
                    {
                        context.SetAnimationBool(IsWireShotActiveAnimationParameter, false);
                        context.SetAnimationBool(IsWireGrabbingAnimationParameter, false);
                        if (wire != null)
                        {
                            wire.Resolved -= handleResolution;
                            wire.BeginDissolve();
                        }
                    }

                    if (playerGrabbed)
                    {
                        break;
                    }

                    if (shotIndex < count - 1)
                    {
                        yield return HackerMeleeAttackAction.Wait(context, repeatIntervalSeconds);
                    }
                }

                yield return HackerMeleeAttackAction.Wait(context, recoverySeconds);
                hacker.SetLastFireWireResult(playerGrabbed
                    ? HackerFireWireResult.PlayerGrabbed
                    : HackerFireWireResult.Missed);
            }
            finally
            {
                context.SetAnimationBool(IsWireShotActiveAnimationParameter, false);
                context.SetAnimationBool(IsWireGrabbingAnimationParameter, false);
            }
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            int count = Mathf.Max(1, fireCount);
            float shotSeconds = Mathf.Max(0.05f, maxFlightSeconds)
                + Mathf.Max(Mathf.Max(0.05f, maxBossFlightSeconds), Mathf.Max(0.05f, grabSeconds));
            seconds = Mathf.Max(0f, windupSeconds)
                + shotSeconds * count
                + Mathf.Max(0f, repeatIntervalSeconds) * Mathf.Max(0, count - 1)
                + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private bool TryCreateWire(BossActionContext context, HackerBossAI hacker, out HackerWire wire)
        {
            wire = null;
            Transform launchOrigin = context.GetBossChildTransform(firePointPath);
            if (launchOrigin == null)
            {
                Debug.LogWarning($"{nameof(HackerFireWireAction)}: 설정된 발사 Point '{firePointPath}'를 찾을 수 없습니다.", hacker);
                return false;
            }

            Vector3 origin = launchOrigin.position;
            HackerWireSettings wireSettings = hacker.WireSettings;
            if (targetMode == HackerFireWireTargetMode.Player)
            {
                wire = HackerWire.CreateGrab(
                    hacker,
                    launchOrigin,
                    context.GetDirectionToPlayer(origin),
                    wireSettings.FlightSpeed,
                    maxFlightSeconds,
                    grabSeconds,
                    hackingPerHit,
                    wireSettings.PullSpeed,
                    wireSettings.PullStopDistance,
                    true,
                    true,
                    float.PositiveInfinity,
                    wireSettings.Width,
                    wireSettings.HitRadius,
                    wireSettings.Color);
                return wire != null;
            }

            if (!TryGetPlayerNearbyWallAimPoint(origin, out Vector2 wallAimPoint))
            {
                return false;
            }

            wire = HackerWire.CreatePersistentWallWire(
                hacker,
                launchOrigin,
                wallAimPoint - (Vector2)origin,
                wireSettings.FlightSpeed,
                maxFlightSeconds,
                float.PositiveInfinity,
                wireSettings.Width,
                wireSettings.HitRadius,
                wireSettings.Color);
            return wire != null;
        }

        private bool TryGetPlayerNearbyWallAimPoint(Vector2 origin, out Vector2 wallAimPoint)
        {
            wallAimPoint = origin;
            PlayerCombatController player = PlayerCombatController.Active;
            int wallLayer = LayerMask.NameToLayer("Wall");
            if (player == null || wallLayer < 0)
            {
                return false;
            }

            Vector2 playerPosition = player.transform.position;
            Collider2D[] wallColliders = Physics2D.OverlapCircleAll(
                playerPosition,
                playerWallSearchRadius,
                1 << wallLayer);
            float bestDistanceSqr = float.PositiveInfinity;
            for (int i = 0; i < wallColliders.Length; i++)
            {
                Collider2D wallCollider = wallColliders[i];
                if (wallCollider == null || !wallCollider.enabled || !wallCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Vector2 candidatePoint = wallCollider.ClosestPoint(playerPosition);
                Vector2 toCandidate = candidatePoint - origin;
                float candidateDistance = toCandidate.magnitude;
                if (candidateDistance <= 0.0001f || candidateDistance < minimumWallDistance)
                {
                    continue;
                }

                RaycastHit2D hit = Physics2D.Raycast(
                    origin,
                    toCandidate / candidateDistance,
                    candidateDistance + 0.05f,
                    1 << wallLayer);
                if (hit.collider == null || hit.collider.gameObject.layer != wallLayer)
                {
                    continue;
                }

                if (hit.distance < minimumWallDistance)
                {
                    continue;
                }

                float playerDistanceSqr = ((Vector2)hit.point - playerPosition).sqrMagnitude;
                if (playerDistanceSqr >= bestDistanceSqr)
                {
                    continue;
                }

                bestDistanceSqr = playerDistanceSqr;
                wallAimPoint = hit.point;
            }

            return bestDistanceSqr < float.PositiveInfinity;
        }

        private IEnumerator FlyBossToWall(
            BossActionContext context,
            Vector3 wallPosition)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            context.SetFacingLocked(true);
            context.SetDashing(true);
            float elapsed = 0f;
            float nextProjectileAt = 0f;
            int firedProjectileCount = 0;
            try
            {
                while (elapsed < maxBossFlightSeconds)
                {
                    if (context.IsExecutionPaused)
                    {
                        context.Stop();
                        yield return null;
                        continue;
                    }

                    Vector2 origin = context.Boss.transform.position;
                    Vector2 toWall = (Vector2)wallPosition - origin;
                    if (toWall.magnitude <= wallArrivalDistance)
                    {
                        yield break;
                    }

                    float progress = Mathf.Clamp01(elapsed / maxBossFlightSeconds);
                    float speedMultiplier = EvaluateSpeedCurve(bossFlightSpeedCurve, progress);
                    context.Boss.SetMovementVelocity(toWall.normalized * (bossFlightSpeed * speedMultiplier));
                    if (firedProjectileCount < maxFlightProjectileCount
                        && elapsed >= nextProjectileAt)
                    {
                        FireFlightProjectile(context, origin);
                        firedProjectileCount++;
                        nextProjectileAt += Mathf.Max(0.01f, flightProjectileInterval);
                    }

                    elapsed += EnemyTimeScale.DeltaTime;
                    yield return null;
                }
            }
            finally
            {
                context.SetDashing(false);
                context.SetFacingLocked(false);
                context.Stop();
            }
        }

        private void FireFlightProjectile(BossActionContext context, Vector3 origin)
        {
            BossProjectileSettings settings = context.ResolveGraphProjectileSettings(flightProjectileName) ?? flightProjectile;
            if (settings == null || settings.Prefab == null)
            {
                return;
            }

            context.FireProjectile(
                settings,
                origin,
                context.GetDirectionToPlayer(origin),
                0f,
                projectileName: flightProjectileName);
        }

        private static float EvaluateSpeedCurve(AnimationCurve curve, float progress)
        {
            return curve != null && curve.length > 0
                ? Mathf.Max(0f, curve.Evaluate(progress))
                : 1f;
        }
    }
}
