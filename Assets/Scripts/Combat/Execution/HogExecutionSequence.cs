using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Enemy;

namespace Week14.Combat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayableDirector))]
    [AddComponentMenu("Week14/Combat/Hog Execution Sequence")]
    public sealed class HogExecutionSequence : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BossExecutionStage stage;
        [SerializeField] private PlayableDirector playableDirector;
        [SerializeField] private ParticleSystem aimChargeVfx;

        [Header("Projectile")]
        [SerializeField, BossGraphProjectileName] private string projectileName = "Default";
        [SerializeField] private BossProjectileSettings fallbackProjectile = new();
        [SerializeField, Min(1)] private int bulletsPerVolley = 16;
        [SerializeField, Min(0f)] private float spawnRadius = 0.2f;
        [SerializeField] private float volleyAngleOffsetDegrees = 7.5f;
        [SerializeField] private bool ignoreWalls = true;
        [SerializeField, BossGraphSfxId] private string volleySfxId;

        [Header("Approach And Parry")]
        [SerializeField, Min(0.1f)] private float approachDistance = 2f;
        [SerializeField, Min(0.1f)] private float approachGateTimeoutSeconds = 6f;
        [SerializeField, Min(0.05f)] private float corridorHalfWidth = 0.65f;
        [SerializeField, Range(0f, 1f)] private float projectileSlowMultiplier = 0.08f;
        [SerializeField, Min(0.1f)] private float projectileSlowSafetySeconds = 6f;
        [SerializeField, Range(0f, 1f)] private float parryCameraFocusWeight = 1f;
        [SerializeField, Range(0.1f, 1f)] private float parryCameraZoomMultiplier = 0.42f;
        [SerializeField] private bool snapCameraOnEachParry = true;

        [Header("Roll And Camera")]
        [SerializeField, Min(0.05f)] private float rollSeconds = 0.55f;
        [SerializeField, Range(0f, 1f)] private float wideCameraFocusWeight = 1f;
        [SerializeField, Range(0.1f, 1.5f)] private float wideCameraZoomMultiplier = 0.72f;
        [SerializeField, BossGraphSfxId] private string aimChargeSfxId;

        private readonly List<EnemyProjectile> spawnedProjectiles = new();
        private readonly Queue<EnemyProjectile> corridorProjectiles = new();
        private PlayerCombatController player;
        private HogBossAI hog;
        private CameraFollow2D cameraFollow;
        private Coroutine approachGateRoutine;
        private Coroutine rollRoutine;
        private int volleyIndex;
        private bool runtimePrepared;
        private bool readyForFinalShot;

        public bool CanPlay
        {
            get
            {
                ResolveReferences();
                return stage != null
                    && stage.HasRequiredAnchors
                    && playableDirector != null
                    && playableDirector.playableAsset != null;
            }
        }

        public Transform InitialCameraFocus =>
            stage != null && stage.WideCameraFocus != null
                ? stage.WideCameraFocus
                : hog != null ? hog.transform : transform;

        public float WideCameraFocusWeight => wideCameraFocusWeight;
        public float WideCameraZoomMultiplier => wideCameraZoomMultiplier;
        public float ExpectedDurationSeconds =>
            playableDirector != null && playableDirector.playableAsset != null
                ? Mathf.Max(0f, (float)playableDirector.duration)
                    + Mathf.Max(0f, approachGateTimeoutSeconds)
                : 0f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            CleanupRuntime(true);
        }

        internal bool Prepare(PlayerCombatController nextPlayer, HogBossAI nextHog)
        {
            ResolveReferences();
            CleanupRuntime(false);
            if (!CanPlay || nextPlayer == null || nextHog == null)
            {
                return false;
            }

            player = nextPlayer;
            hog = nextHog;
            cameraFollow = nextPlayer.CameraFollow;
            runtimePrepared = stage.PlaceActors(player, hog);
            volleyIndex = 0;
            readyForFinalShot = false;
            return runtimePrepared;
        }

        internal IEnumerator PlayPrelude()
        {
            if (!runtimePrepared || playableDirector == null)
            {
                yield break;
            }

            playableDirector.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            playableDirector.extrapolationMode = DirectorWrapMode.None;
            playableDirector.time = 0d;
            playableDirector.Evaluate();
            playableDirector.Play();

            try
            {
                double duration = playableDirector.duration;
                while (runtimePrepared && !readyForFinalShot)
                {
                    bool reachedEnd = duration <= 0d
                        || playableDirector.time >= duration - 0.001d;
                    if (reachedEnd && playableDirector.state != PlayState.Playing)
                    {
                        break;
                    }

                    yield return null;
                }

                while (rollRoutine != null)
                {
                    yield return null;
                }
            }
            finally
            {
                CleanupRuntime(false);
            }
        }

        internal void Cancel()
        {
            CleanupRuntime(true);
        }

        // Timeline Signal: 방사형 탄막 한 묶음을 발사합니다.
        public void SpawnNextVolley()
        {
            if (!runtimePrepared || player == null || hog == null)
            {
                return;
            }

            BossProjectileSettings settings =
                hog.ResolveGraphProjectileSettingsForActions(projectileName) ?? fallbackProjectile;
            if (settings == null || settings.Prefab == null)
            {
                Debug.LogWarning(
                    $"{nameof(HogExecutionSequence)}: '{projectileName}' 투사체 설정이 없습니다.",
                    this);
                return;
            }

            Vector2 centerDirection = (Vector2)player.transform.position - (Vector2)hog.transform.position;
            if (centerDirection.sqrMagnitude <= 0.0001f)
            {
                centerDirection = Vector2.left;
            }

            float baseAngle = Mathf.Atan2(centerDirection.y, centerDirection.x) * Mathf.Rad2Deg
                + volleyIndex * volleyAngleOffsetDegrees;
            float angleStep = 360f / Mathf.Max(1, bulletsPerVolley);
            Vector3 center = hog.BodyRoot != null ? hog.BodyRoot.position : hog.transform.position;
            for (int i = 0; i < bulletsPerVolley; i++)
            {
                Vector2 direction = BossActionContext.AngleToDirection(baseAngle + angleStep * i);
                Vector3 origin = center + (Vector3)(direction * spawnRadius);
                EnemyProjectile projectile = BossProjectileEmitter.Fire(
                    SpawnCinematicProjectile,
                    settings,
                    origin,
                    direction);
                if (projectile != null)
                {
                    spawnedProjectiles.Add(projectile);
                }
            }

            volleyIndex++;
            if (!string.IsNullOrWhiteSpace(volleySfxId))
            {
                SoundManager.PlaySfx(volleySfxId);
            }
        }

        // Timeline Signal: 탄환이 가까워질 때까지 Timeline을 멈췄다가 슬로우와 함께 재개합니다.
        public void WaitUntilProjectilesAreClose()
        {
            if (!runtimePrepared || playableDirector == null || approachGateRoutine != null)
            {
                return;
            }

            playableDirector.Pause();
            approachGateRoutine = StartCoroutine(WaitForProjectileApproach());
        }

        // Timeline Signal: 플레이어와 Hog 사이 통로의 다음 탄환을 패링합니다.
        public void ParryNext()
        {
            if (!runtimePrepared || player == null)
            {
                return;
            }

            EnemyProjectile projectile = DequeueNextCorridorProjectile();
            if (projectile == null)
            {
                return;
            }

            Vector3 impactPosition = projectile.transform.position;
            Transform focusProxy = stage != null ? stage.ProjectileFocusProxy : null;
            if (focusProxy != null)
            {
                focusProxy.position = impactPosition;
                cameraFollow?.BeginCinematicFocus(
                    focusProxy,
                    parryCameraFocusWeight,
                    parryCameraZoomMultiplier);
                if (snapCameraOnEachParry)
                {
                    cameraFollow?.SnapToCinematicFocus();
                }
            }

            player.TryParryProjectileForCinematic(projectile);
        }

        // Timeline Signal: 뚫린 통로를 따라 Hog 앞으로 구릅니다.
        public void BeginRoll()
        {
            if (!runtimePrepared || player == null || stage == null || rollRoutine != null)
            {
                return;
            }

            RestoreEnemyTimeScale();
            rollRoutine = StartCoroutine(RollPlayerToBoss());
        }

        // Timeline Signal: 구르기 이후 Hog를 조준하고 충전 연출을 시작합니다.
        public void BeginAimCharge()
        {
            if (!runtimePrepared || player == null || hog == null)
            {
                return;
            }

            Vector2 aimDirection = (Vector2)hog.transform.position - (Vector2)player.transform.position;
            if (aimDirection.sqrMagnitude <= 0.0001f)
            {
                aimDirection = Vector2.right;
            }

            player.Visual?.BeginExecutionVisual(aimDirection);
            player.Visual?.SetBodyAimDirection(aimDirection);
            player.Visual?.SetLeftArmAimDirection(aimDirection);
            if (aimChargeVfx != null)
            {
                aimChargeVfx.Play(true);
            }

            if (!string.IsNullOrWhiteSpace(aimChargeSfxId))
            {
                SoundManager.PlaySfx(aimChargeSfxId);
            }
        }

        // Timeline Signal: 여기부터 기존 최종 처형의 암전 발사 구간으로 넘어갑니다.
        public void ReadyFinalShot()
        {
            if (!runtimePrepared)
            {
                return;
            }

            readyForFinalShot = true;
            playableDirector?.Pause();
        }

        private EnemyProjectile SpawnCinematicProjectile(
            EnemyProjectile prefab,
            Vector3 position,
            Vector2 direction,
            int projectileBulletDamage,
            float chargeSeconds,
            float speed,
            float lifetime,
            float radius,
            Color color,
            float trailSeconds,
            float trailWidth,
            bool homingEnabled,
            float homingSeconds,
            float homingTurnDegrees,
            Vector3? _,
            float __)
        {
            EnemyProjectile projectile = EnemyProjectile.Spawn(
                prefab,
                hog != null ? hog.HpGauge : null,
                position,
                direction,
                0,
                chargeSeconds,
                speed,
                lifetime,
                radius,
                color,
                trailSeconds,
                trailWidth,
                homingEnabled,
                homingSeconds,
                homingTurnDegrees);
            if (projectile == null)
            {
                return null;
            }

            projectile.ConfigureExecutionPauseIgnored(true);
            projectile.ConfigurePlayerCollisionIgnored(true);
            projectile.ConfigureIgnoresWalls(ignoreWalls);
            projectile.ConfigureInterceptable(true);
            return projectile;
        }

        private IEnumerator WaitForProjectileApproach()
        {
            float remaining = approachGateTimeoutSeconds;
            while (runtimePrepared && remaining > 0f)
            {
                if (TryFindClosestCorridorProjectile(out EnemyProjectile closest, out float distance)
                    && closest != null
                    && distance <= approachDistance)
                {
                    break;
                }

                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            BuildCorridorProjectileQueue();
            EnemyTimeScale.SetTemporary(projectileSlowMultiplier, projectileSlowSafetySeconds);
            approachGateRoutine = null;
            if (runtimePrepared && playableDirector != null)
            {
                playableDirector.Play();
            }
        }

        private IEnumerator RollPlayerToBoss()
        {
            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Vector2 start = body != null ? body.position : (Vector2)player.transform.position;
            Vector2 end = stage.PlayerRollEnd.position;
            float duration = Mathf.Max(0.05f, rollSeconds);
            player.Visual?.PlayRoll(duration);

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - progress) * (1f - progress);
                SetPlayerPosition(body, Vector2.LerpUnclamped(start, end, eased));
                yield return null;
            }

            SetPlayerPosition(body, end);
            rollRoutine = null;
        }

        private void SetPlayerPosition(Rigidbody2D body, Vector2 position)
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = position;
                return;
            }

            Vector3 worldPosition = player.transform.position;
            worldPosition.x = position.x;
            worldPosition.y = position.y;
            player.transform.position = worldPosition;
        }

        private EnemyProjectile DequeueNextCorridorProjectile()
        {
            while (corridorProjectiles.Count > 0)
            {
                EnemyProjectile projectile = corridorProjectiles.Dequeue();
                if (IsUsableProjectile(projectile))
                {
                    return projectile;
                }
            }

            BuildCorridorProjectileQueue();
            while (corridorProjectiles.Count > 0)
            {
                EnemyProjectile projectile = corridorProjectiles.Dequeue();
                if (IsUsableProjectile(projectile))
                {
                    return projectile;
                }
            }

            return null;
        }

        private void BuildCorridorProjectileQueue()
        {
            corridorProjectiles.Clear();
            if (player == null || hog == null)
            {
                return;
            }

            Vector2 playerPosition = player.transform.position;
            Vector2 hogPosition = hog.transform.position;
            List<EnemyProjectile> candidates = new();
            for (int i = 0; i < spawnedProjectiles.Count; i++)
            {
                EnemyProjectile projectile = spawnedProjectiles[i];
                if (!IsUsableProjectile(projectile))
                {
                    continue;
                }

                float corridorDistance = DistanceToSegment(
                    projectile.transform.position,
                    playerPosition,
                    hogPosition);
                if (corridorDistance <= corridorHalfWidth)
                {
                    candidates.Add(projectile);
                }
            }

            candidates.Sort((left, right) =>
            {
                float leftDistance = ((Vector2)left.transform.position - playerPosition).sqrMagnitude;
                float rightDistance = ((Vector2)right.transform.position - playerPosition).sqrMagnitude;
                return leftDistance.CompareTo(rightDistance);
            });

            for (int i = 0; i < candidates.Count; i++)
            {
                corridorProjectiles.Enqueue(candidates[i]);
            }
        }

        private bool TryFindClosestCorridorProjectile(
            out EnemyProjectile closest,
            out float closestDistance)
        {
            closest = null;
            closestDistance = float.PositiveInfinity;
            if (player == null || hog == null)
            {
                return false;
            }

            Vector2 playerPosition = player.transform.position;
            Vector2 hogPosition = hog.transform.position;
            for (int i = 0; i < spawnedProjectiles.Count; i++)
            {
                EnemyProjectile projectile = spawnedProjectiles[i];
                if (!IsUsableProjectile(projectile))
                {
                    continue;
                }

                Vector2 projectilePosition = projectile.transform.position;
                if (DistanceToSegment(projectilePosition, playerPosition, hogPosition) > corridorHalfWidth)
                {
                    continue;
                }

                float distance = Vector2.Distance(playerPosition, projectilePosition);
                if (distance < closestDistance)
                {
                    closest = projectile;
                    closestDistance = distance;
                }
            }

            return closest != null;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 start, Vector2 end)
        {
            Vector2 segment = end - start;
            float sqrLength = segment.sqrMagnitude;
            if (sqrLength <= 0.0001f)
            {
                return Vector2.Distance(point, start);
            }

            float progress = Mathf.Clamp01(Vector2.Dot(point - start, segment) / sqrLength);
            Vector2 closest = start + segment * progress;
            return Vector2.Distance(point, closest);
        }

        private static bool IsUsableProjectile(EnemyProjectile projectile)
        {
            return projectile != null
                && projectile.gameObject.activeInHierarchy
                && projectile.CanBeIntercepted;
        }

        private void CleanupRuntime(bool endCameraFocus)
        {
            bool hadActiveRuntime = runtimePrepared
                || player != null
                || approachGateRoutine != null
                || rollRoutine != null
                || spawnedProjectiles.Count > 0;
            runtimePrepared = false;
            readyForFinalShot = false;

            if (approachGateRoutine != null)
            {
                StopCoroutine(approachGateRoutine);
                approachGateRoutine = null;
            }

            if (rollRoutine != null)
            {
                StopCoroutine(rollRoutine);
                rollRoutine = null;
            }

            if (playableDirector != null && hadActiveRuntime)
            {
                playableDirector.Stop();
            }

            if (hadActiveRuntime)
            {
                RestoreEnemyTimeScale();
            }
            for (int i = 0; i < spawnedProjectiles.Count; i++)
            {
                EnemyProjectile projectile = spawnedProjectiles[i];
                if (projectile != null && projectile.gameObject.activeInHierarchy)
                {
                    projectile.DestroyFromOwner();
                }
            }

            spawnedProjectiles.Clear();
            corridorProjectiles.Clear();
            if (aimChargeVfx != null)
            {
                aimChargeVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (endCameraFocus && stage != null && cameraFollow != null)
            {
                cameraFollow.EndCinematicFocusIfTarget(stage.ProjectileFocusProxy);
            }

            player = null;
            hog = null;
            cameraFollow = null;
        }

        private static void RestoreEnemyTimeScale()
        {
            EnemyTimeScale.SetTemporary(1f, 0f);
        }

        private void ResolveReferences()
        {
            playableDirector ??= GetComponent<PlayableDirector>();
            stage ??= GetComponent<BossExecutionStage>();
        }
    }
}
