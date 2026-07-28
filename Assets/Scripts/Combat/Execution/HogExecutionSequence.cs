using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Enemy;

namespace Week14.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Combat/Hog Execution Sequence")]
    public sealed class HogExecutionSequence : BossExecutionSequence
    {
        [Header("References")]
        [SerializeField] private BossExecutionStage stage;
        [SerializeField] private ParticleSystem aimChargeVfx;
        [SerializeField] private ExecutionChargeGatherVfx aimChargeGatherVfx;

        [Header("Boss Graph Pattern")]
        [SerializeField, Tooltip("패턴 목록을 가져올 MainScene의 Hog 보스입니다.")]
        private HogBossAI patternSourceBoss;
        [SerializeField, Tooltip("Hog Boss Graph 에디터에서 만든 처형용 Pattern ID입니다.")]
        private string executionPatternId;
        [SerializeField, Tooltip("연출 중 그래프 패턴 탄환이 벽에 막히지 않게 합니다.")]
        private bool ignorePatternProjectileWalls = true;

        [Header("Approach And Parry")]
        [SerializeField, Min(0.1f)] private float approachDistance = 2f;
        [SerializeField, Min(0.1f)] private float approachGateTimeoutSeconds = 6f;
        [SerializeField, Min(0.05f)] private float corridorHalfWidth = 0.65f;
        [SerializeField, Min(0.1f)] private float playerClearanceDistance = 1.15f;
        [SerializeField, Range(0f, 1f)] private float projectileSlowMultiplier = 0.08f;
        [SerializeField, Min(0.1f)] private float projectileSlowSafetySeconds = 6f;
        [SerializeField, Min(0f)] private float preParryHoldSeconds = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float preParryCameraZoomMultiplier = 0.38f;
        [SerializeField, Min(0.01f)] private float preParryCameraBlendSmoothTime = 0.12f;
        [SerializeField, Min(0.1f)] private float executionImageSeconds = 3.5f;
        [SerializeField, BossGraphSfxId] private string preParrySfxId;
        [SerializeField, Range(0f, 1f)] private float parryCameraFocusWeight = 1f;
        [SerializeField, Range(0.1f, 1f)] private float parryCameraZoomMultiplier = 0.42f;
        [SerializeField, Min(0.01f)] private float parryCameraBlendSmoothTime = 0.08f;

        [Header("Sequence Timing")]
        [SerializeField, Min(0f)] private float patternSignalTimeSeconds = 0.15f;
        [SerializeField, Min(0f)] private float projectileGateSignalTimeSeconds = 1f / 3f;
        [SerializeField] private float[] parrySignalTimesSeconds =
        {
            0.8f,
            0.92f,
            1.04f,
            1.16f,
            1.16f,
            1.28f,
            1.4f,
            1.52f
        };
        [SerializeField, Min(0f)] private float rollSignalTimeSeconds = 119f / 60f;
        [SerializeField, Min(0f)] private float aimChargeSignalTimeSeconds = 2.5f;
        [SerializeField, Min(0f)] private float finalShotSignalTimeSeconds = 4f;

        [Header("Roll And Camera")]
        [SerializeField, Min(0.05f)] private float rollSeconds = 0.55f;
        [SerializeField, Range(0f, 1f)] private float wideCameraFocusWeight = 1f;
        [SerializeField, Range(0.1f, 1.5f)] private float wideCameraZoomMultiplier = 0.72f;
        [SerializeField, Min(0.01f)] private float wideCameraBlendSmoothTime = 0.22f;
        [SerializeField, BossGraphSfxId] private string aimChargeSfxId;

        private readonly List<EnemyProjectile> spawnedProjectiles = new();
        private readonly Queue<EnemyProjectile> corridorProjectiles = new();
        private PlayerCombatController player;
        private HogBossAI hog;
        private CameraFollow2D cameraFollow;
        private Coroutine rollRoutine;
        private bool projectileClearanceEnabled;
        private bool runtimePrepared;
        private double sequenceClockSeconds;

        public override bool CanPlay
        {
            get
            {
                ResolveReferences();
                return stage != null
                    && stage.HasRequiredAnchors
                    && !string.IsNullOrWhiteSpace(executionPatternId)
                    && (patternSourceBoss == null
                        || patternSourceBoss.HasConfiguredGraphPattern(executionPatternId));
            }
        }

        public override Transform InitialCameraFocus =>
            stage != null && stage.WideCameraFocus != null
                ? stage.WideCameraFocus
                : hog != null ? hog.transform : transform;

        public override float WideCameraFocusWeight => wideCameraFocusWeight;
        public override float WideCameraZoomMultiplier => wideCameraZoomMultiplier;
        public override float WideCameraBlendSmoothTime => wideCameraBlendSmoothTime;
        public override float ExpectedDurationSeconds =>
            Mathf.Max(0f, finalShotSignalTimeSeconds)
            + Mathf.Max(0f, approachGateTimeoutSeconds)
            + Mathf.Max(0f, preParryHoldSeconds);

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            CleanupRuntime(true);
        }

        public override bool SupportsBoss(BossAI boss)
        {
            return boss is HogBossAI;
        }

        internal override bool Prepare(
            PlayerCombatController nextPlayer,
            BossAI nextBoss)
        {
            if (nextBoss is not HogBossAI nextHog)
            {
                return false;
            }

            ResolveReferences();
            CleanupRuntime(false);
            if (nextPlayer == null || nextHog == null)
            {
                return false;
            }

            player = nextPlayer;
            hog = nextHog;
            patternSourceBoss ??= nextHog;
            if (!CanPlay || !hog.HasConfiguredGraphPattern(executionPatternId))
            {
                player = null;
                hog = null;
                return false;
            }

            cameraFollow = nextPlayer.CameraFollow;
            runtimePrepared = stage.PlaceActors(player, hog);
            projectileClearanceEnabled = true;
            sequenceClockSeconds = 0f;
            FaceActorsTowardEachOther();
            player.Visual?.BeginExecutionAimVisual(
                (Vector2)hog.transform.position - (Vector2)player.transform.position);
            return runtimePrepared;
        }

        internal override IEnumerator PlayPrelude()
        {
            if (!runtimePrepared)
            {
                yield break;
            }

            try
            {
                yield return WaitUntilSequenceTime(patternSignalTimeSeconds);
                if (!runtimePrepared)
                {
                    yield break;
                }

                RunSelectedPattern();
                yield return WaitUntilSequenceTime(projectileGateSignalTimeSeconds);
                if (!runtimePrepared)
                {
                    yield break;
                }

                yield return WaitForProjectileApproach();
                int executionParryCount =
                    parrySignalTimesSeconds != null ? parrySignalTimesSeconds.Length : 0;
                for (int i = 0; runtimePrepared && i < executionParryCount; i++)
                {
                    yield return WaitUntilSequenceTime(parrySignalTimesSeconds[i]);
                    FocusAndParryNextProjectile();
                }

                yield return WaitUntilSequenceTime(rollSignalTimeSeconds);
                if (!runtimePrepared)
                {
                    yield break;
                }

                BeginRollPresentation();
                yield return WaitUntilSequenceTime(aimChargeSignalTimeSeconds);
                StartAimCharge();
                yield return WaitUntilSequenceTime(finalShotSignalTimeSeconds);
                yield return null;
                while (runtimePrepared && rollRoutine != null)
                {
                    yield return null;
                }
            }
            finally
            {
                CleanupRuntime(false);
            }
        }

        internal override void Cancel()
        {
            CleanupRuntime(true);
        }

        // Hog Boss Graph에서 선택한 처형 패턴을 한 번 실행합니다.
        private void RunSelectedPattern()
        {
            if (!runtimePrepared || player == null || hog == null)
            {
                return;
            }

            if (!hog.TryRunCinematicPatternOnce(
                    executionPatternId,
                    ConfigureSpawnedPatternProjectile))
            {
                Debug.LogWarning(
                    $"{nameof(HogExecutionSequence)}: 처형용 Boss Graph 패턴 " +
                    $"'{executionPatternId}' 실행에 실패했습니다.",
                    this);
            }
        }

        // 플레이어와 Hog 사이 통로에서 가장 가까운 탄환을 패링합니다.
        private void FocusAndParryNextProjectile()
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
                    parryCameraZoomMultiplier,
                    parryCameraBlendSmoothTime);
            }

            player.TryParryProjectileForCinematic(projectile);
        }

        // 탄환의 감속과 충돌 안전 처리를 해제한 뒤 구르기 카메라로 전환합니다.
        private void BeginRollPresentation()
        {
            if (!runtimePrepared || player == null || stage == null || rollRoutine != null)
            {
                return;
            }

            RestoreEnemyTimeScale();
            ReleaseProjectileClearance();
            if (stage.WideCameraFocus != null)
            {
                cameraFollow?.BeginCinematicFocus(
                    stage.WideCameraFocus,
                    wideCameraFocusWeight,
                    wideCameraZoomMultiplier,
                    wideCameraBlendSmoothTime);
            }

            rollRoutine = StartCoroutine(RollPlayerToBoss());
        }

        // 구르기 이후 Hog를 조준하고 충전 연출을 시작합니다.
        private void StartAimCharge()
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

            player.Visual?.BeginExecutionAimVisual(aimDirection);
            player.Visual?.SetBodyAimDirection(aimDirection);
            if (aimChargeVfx != null)
            {
                aimChargeVfx.Play(true);
            }

            aimChargeGatherVfx?.Play(player.RightFireOrigin);

            if (!string.IsNullOrWhiteSpace(aimChargeSfxId))
            {
                SoundManager.PlaySfx(aimChargeSfxId);
            }
        }

        private void ConfigureSpawnedPatternProjectile(EnemyProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.ConfigureExecutionPauseIgnored(true);
            projectile.ConfigurePlayerCollisionIgnored(true);
            if (projectileClearanceEnabled)
            {
                projectile.ConfigureCinematicPlayerClearance(
                    player != null ? player.transform : null,
                    playerClearanceDistance);
            }
            else
            {
                projectile.ReleaseCinematicPlayerClearance();
            }
            projectile.ConfigureIgnoresWalls(ignorePatternProjectileWalls);
            projectile.ConfigureInterceptable(true);
            spawnedProjectiles.Add(projectile);
        }

        private void ReleaseProjectileClearance()
        {
            projectileClearanceEnabled = false;
            for (int i = 0; i < spawnedProjectiles.Count; i++)
            {
                EnemyProjectile projectile = spawnedProjectiles[i];
                if (projectile != null)
                {
                    projectile.ReleaseCinematicPlayerClearance();
                }
            }
        }

        private void FaceActorsTowardEachOther()
        {
            if (player == null || hog == null)
            {
                return;
            }

            Vector2 playerToHog =
                (Vector2)hog.transform.position - (Vector2)player.transform.position;
            if (playerToHog.sqrMagnitude <= 0.0001f)
            {
                playerToHog = Vector2.left;
            }

            player.Visual?.SetBodyAimDirection(playerToHog);
            player.Visual?.SetLeftArmAimDirection(playerToHog);
            hog.FaceTowards(player.transform.position);
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

            if (!runtimePrepared)
            {
                yield break;
            }

            BuildCorridorProjectileQueue();
            EnemyTimeScale.SetTemporary(projectileSlowMultiplier, projectileSlowSafetySeconds);
            if (player != null)
            {
                cameraFollow?.BeginCinematicFocus(
                    player.transform,
                    1f,
                    preParryCameraZoomMultiplier,
                    preParryCameraBlendSmoothTime);
                player.PlayExecutionImageForCinematic(executionImageSeconds);
            }

            if (!string.IsNullOrWhiteSpace(preParrySfxId))
            {
                SoundManager.PlaySfx(preParrySfxId);
            }

            if (preParryHoldSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(preParryHoldSeconds);
            }
        }

        private IEnumerator RollPlayerToBoss()
        {
            if (!runtimePrepared || player == null || stage == null)
            {
                yield break;
            }

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            Vector2 start = body != null ? body.position : (Vector2)player.transform.position;
            Vector2 end = stage.PlayerRollEnd.position;
            float duration = Mathf.Max(0.05f, rollSeconds);
            player.Visual?.PlayCinematicRoll(
                duration,
                end - start);

            for (float elapsed = 0f;
                 runtimePrepared && player != null && elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - progress) * (1f - progress);
                SetPlayerPosition(body, Vector2.LerpUnclamped(start, end, eased));
                yield return null;
            }

            if (runtimePrepared && player != null)
            {
                SetPlayerPosition(body, end);
            }

            rollRoutine = null;
        }

        private IEnumerator WaitUntilSequenceTime(double targetSeconds)
        {
            while (runtimePrepared && sequenceClockSeconds < targetSeconds)
            {
                yield return null;
                sequenceClockSeconds += Time.unscaledDeltaTime;
            }
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
                || rollRoutine != null
                || spawnedProjectiles.Count > 0;
            runtimePrepared = false;
            projectileClearanceEnabled = false;
            sequenceClockSeconds = 0d;

            if (rollRoutine != null)
            {
                StopCoroutine(rollRoutine);
                rollRoutine = null;
            }

            hog?.StopCinematicPattern();

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
                aimChargeVfx.Stop(
                    true,
                    endCameraFocus
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
            }

            aimChargeGatherVfx?.Stop(endCameraFocus);

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
            stage ??= GetComponent<BossExecutionStage>();
            aimChargeGatherVfx ??= GetComponent<ExecutionChargeGatherVfx>();
            if (aimChargeGatherVfx == null && Application.isPlaying)
            {
                aimChargeGatherVfx = gameObject.AddComponent<ExecutionChargeGatherVfx>();
            }

            patternSourceBoss ??=
                UnityEngine.Object.FindFirstObjectByType<HogBossAI>(FindObjectsInactive.Include);
        }
    }
}
