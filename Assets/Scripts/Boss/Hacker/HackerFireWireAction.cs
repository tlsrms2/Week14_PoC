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
        private const float WallCollisionSkin = 0.02f;
        private const float WallArrivalTolerance = 0.03f;
        private const float WallArrivalSettleSeconds = 0.08f;

        [Header("Wire")]
        [FormerlySerializedAs("launchOriginPath")]
        [SerializeField, BossGraphBossChildPath] private string firePointPath;
        [SerializeField, Min(0f)] private float windupSeconds = 0.25f;
        [SerializeField, Min(0.05f)] private float maxFlightSeconds = 2f;
        [SerializeField] private HackerFireWireTargetMode targetMode;
        [SerializeField, Min(0.05f)] private float grabSeconds = 0.65f;
        [SerializeField, Min(0.05f)] private float playerWallSearchRadius = 5f;
        [SerializeField, Min(0f)] private float minimumWallDistance = 2f;
        [SerializeField, Min(1)] private int fireCount = 1;
        [Tooltip("다음 와이어는 이전 와이어가 실제 생성된 시점으로부터 이 시간 이상 지난 뒤 발사됩니다.")]
        [SerializeField, Min(0f)] private float repeatIntervalSeconds = 0.15f;
        [SerializeField, Min(0f)] private float recoverySeconds = 0.2f;

        [Header("SFX")]
        [SerializeField, BossGraphSfxId] private string fireSfxId = HackerSfxIds.FireWire;
        [SerializeField, BossGraphSfxId] private string flightSfxId = HackerSfxIds.WireFlight;
        [SerializeField, BossGraphSfxId] private string grabSfxId = HackerSfxIds.Wire;

        [Header("Fire Effect")]
        [Tooltip("와이어를 발사할 때 한 번 생성할 이펙트 프리팹입니다. 프리팹의 오른쪽(+X)을 발사 방향으로 사용합니다.")]
        [SerializeField] private GameObject fireEffectPrefab;
        [Tooltip("Boss Graph Editor 하이어러키에서 이펙트 생성 위치를 드래그해 지정합니다.")]
        [SerializeField, BossGraphBossChildPath] private string fireEffectSpawnPointPath;
        [Tooltip("프리팹의 기본 방향을 보정할 Z축 회전값입니다.")]
        [SerializeField] private float fireEffectRotationOffsetDegrees;
        [SerializeField, Min(0.01f)] private float fireEffectScale = 1f;

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

                    context.PlaySfx(HackerSfxIds.Resolve(fireSfxId, HackerSfxIds.FireWire));

                    ShotIntervalTimer intervalTimer = null;
                    Coroutine intervalCoroutine = null;
                    if (shotIndex < count - 1)
                    {
                        intervalTimer = new ShotIntervalTimer(repeatIntervalSeconds);
                        intervalCoroutine = context.Boss.StartCoroutine(
                            CountDownShotInterval(context, intervalTimer));
                    }

                    try
                    {
                        HackerWireResolution? resolution = null;
                        Vector3 wallPosition = default;
                        Vector2 wallNormal = default;
                        Action<HackerWireResolution> handleResolution = result =>
                        {
                            resolution = result;
                            if (result == HackerWireResolution.WallAttached)
                            {
                                wallPosition = wire.Position;
                                wallNormal = wire.WallNormal;
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
                                yield return FlyBossToWall(context, wallPosition, wallNormal);
                            }
                            else if (resolution == HackerWireResolution.PlayerGrabbed)
                            {
                                context.PlaySfx(HackerSfxIds.Resolve(grabSfxId, HackerSfxIds.Wire));
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
                                if (resolution == HackerWireResolution.WallAttached)
                                {
                                    wire.RemoveImmediate();
                                }
                                else
                                {
                                    wire.BeginDissolve();
                                }
                            }
                        }

                        if (playerGrabbed)
                        {
                            break;
                        }

                        if (intervalTimer != null)
                        {
                            yield return WaitForShotInterval(context, intervalTimer);
                        }
                    }
                    finally
                    {
                        if (intervalCoroutine != null && context.Boss != null)
                        {
                            context.Boss.StopCoroutine(intervalCoroutine);
                        }
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
                + shotSeconds
                + Mathf.Max(shotSeconds, Mathf.Max(0f, repeatIntervalSeconds)) * Mathf.Max(0, count - 1)
                + Mathf.Max(0f, recoverySeconds);
            return true;
        }

        private static IEnumerator CountDownShotInterval(
            BossActionContext context,
            ShotIntervalTimer timer)
        {
            while (timer.RemainingSeconds > 0f)
            {
                if (!context.IsExecutionPaused)
                {
                    timer.RemainingSeconds -= EnemyTimeScale.DeltaTime;
                }

                yield return null;
            }
        }

        private static IEnumerator WaitForShotInterval(
            BossActionContext context,
            ShotIntervalTimer timer)
        {
            while (timer.RemainingSeconds > 0f)
            {
                if (context.IsExecutionPaused)
                {
                    context.Stop();
                }

                yield return null;
            }
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
            Vector2 fireDirection;
            if (targetMode == HackerFireWireTargetMode.Player)
            {
                fireDirection = context.GetDirectionToPlayer(origin);
                wire = HackerWire.CreateGrab(
                    hacker,
                    launchOrigin,
                    fireDirection,
                    wireSettings.FlightSpeed,
                    maxFlightSeconds,
                    grabSeconds,
                    wireSettings.PullSpeed,
                    wireSettings.PullStopDistance,
                    true,
                    true,
                    float.PositiveInfinity,
                    wireSettings.Width,
                    wireSettings.HitRadius,
                    wireSettings.Color);
            }
            else
            {
                if (!TryGetPlayerNearbyWallAimPoint(origin, out Vector2 wallAimPoint))
                {
                    return false;
                }

                fireDirection = wallAimPoint - (Vector2)origin;
                wire = HackerWire.CreatePersistentWallWire(
                    hacker,
                    launchOrigin,
                    fireDirection,
                    wireSettings.FlightSpeed,
                    maxFlightSeconds,
                    float.PositiveInfinity,
                    wireSettings.Width,
                    wireSettings.HitRadius,
                    wireSettings.Color);
            }

            if (wire == null)
            {
                return false;
            }

            HackerWireFireVfx.Play(
                fireEffectPrefab,
                context.GetBossChildTransform(fireEffectSpawnPointPath),
                fireDirection,
                fireEffectRotationOffsetDegrees,
                fireEffectScale);
            return true;
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
            Vector3 wallPosition,
            Vector2 wallNormal)
        {
            if (context?.Boss == null)
            {
                yield break;
            }

            Vector2 arrivalPosition = ResolveWallArrivalPosition(context.Boss, wallPosition, wallNormal);
            Vector2 bossPosition = context.Boss.Body != null
                ? context.Boss.Body.position
                : (Vector2)context.Boss.transform.position;
            if (Vector2.Distance(bossPosition, arrivalPosition) <= WallArrivalTolerance)
            {
                yield break;
            }

            context.PlaySfx(HackerSfxIds.Resolve(flightSfxId, HackerSfxIds.WireFlight));
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

                    Vector2 origin = context.Boss.Body != null
                        ? context.Boss.Body.position
                        : (Vector2)context.Boss.transform.position;
                    Vector2 toWall = arrivalPosition - origin;
                    if (toWall.magnitude <= WallArrivalTolerance)
                    {
                        yield break;
                    }

                    float progress = Mathf.Clamp01(elapsed / maxBossFlightSeconds);
                    float speedMultiplier = EvaluateSpeedCurve(bossFlightSpeedCurve, progress);
                    float speed = bossFlightSpeed * speedMultiplier;
                    float scaledSettleSeconds = Mathf.Max(
                        0.001f,
                        WallArrivalSettleSeconds * Mathf.Max(0.01f, EnemyTimeScale.Current));
                    speed = Mathf.Min(speed, toWall.magnitude / scaledSettleSeconds);
                    context.Boss.SetMovementVelocity(toWall.normalized * speed);
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

        private Vector2 ResolveWallArrivalPosition(
            BossAI boss,
            Vector2 wallPosition,
            Vector2 wallNormal)
        {
            Vector2 bossPosition = boss.Body != null
                ? boss.Body.position
                : (Vector2)boss.transform.position;
            Vector2 outwardDirection = wallNormal.sqrMagnitude > 0.0001f
                ? wallNormal.normalized
                : (bossPosition - wallPosition).normalized;
            if (outwardDirection.sqrMagnitude <= 0.0001f)
            {
                outwardDirection = Vector2.left;
            }

            float colliderClearance = ResolveBossColliderClearance(boss, bossPosition, -outwardDirection);
            float wallClearance = Mathf.Max(
                Mathf.Max(0f, wallArrivalDistance),
                colliderClearance + WallCollisionSkin);
            return wallPosition + outwardDirection * wallClearance;
        }

        private static float ResolveBossColliderClearance(
            BossAI boss,
            Vector2 bossPosition,
            Vector2 wallDirection)
        {
            Collider2D[] colliders = boss.GetComponentsInChildren<Collider2D>(true);
            float clearance = 0f;
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D collider = colliders[i];
                if (collider == null
                    || !collider.enabled
                    || collider.isTrigger
                    || !collider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                Vector2 centerOffset = (Vector2)bounds.center - bossPosition;
                Vector2 extents = bounds.extents;
                float projectedExtent = Mathf.Abs(wallDirection.x) * extents.x
                    + Mathf.Abs(wallDirection.y) * extents.y;
                float leadingDistance = Vector2.Dot(centerOffset, wallDirection) + projectedExtent;
                clearance = Mathf.Max(clearance, leadingDistance);
            }

            return clearance;
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

        private sealed class ShotIntervalTimer
        {
            internal ShotIntervalTimer(float seconds)
            {
                RemainingSeconds = Mathf.Max(0f, seconds);
            }

            internal float RemainingSeconds { get; set; }
        }
    }
}
