using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Enemy;

namespace Week14.Combat
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Week14/Combat/Assassin Execution Sequence")]
    public sealed class AssassinExecutionSequence : BossExecutionSequence
    {
        [Header("References")]
        [SerializeField] private BossExecutionStage stage;
        [SerializeField] private AssassinBossAI patternSourceBoss;
        [SerializeField] private ParticleSystem aimChargeVfx;
        [SerializeField] private ExecutionChargeGatherVfx aimChargeGatherVfx;

        [Header("Boss Graph Pattern")]
        [SerializeField] private string executionPatternId = "Execution";
        [SerializeField, Min(0.02f)]
        private float projectileBatchSeparationSeconds = 0.18f;
        [SerializeField] private bool ignorePatternProjectileWalls = true;

        [Header("Whip Parry")]
        [SerializeField, Min(1)] private int parryCountPerWhip = 3;
        [SerializeField, Min(0.1f)] private float parryApproachDistance = 2.4f;
        [SerializeField, Min(0.1f)] private float parryApproachTimeoutSeconds = 1.2f;
        [SerializeField, Min(0f)] private float parryIntervalSeconds = 0.075f;
        [SerializeField, Range(0f, 1f)] private float parryCameraFocusWeight = 0.2f;
        [SerializeField, Range(0.1f, 1f)] private float parryCameraZoomMultiplier = 0.81f;
        [SerializeField, Min(0.01f)] private float parryCameraBlendSmoothTime = 0.08f;

        [Header("Clone Barrage")]
        [SerializeField, Min(1)] private int barrageProjectilesBeforeReveal = 12;
        [SerializeField, Min(0.1f)] private float barrageWaitTimeoutSeconds = 5f;
        [SerializeField, Min(0f)] private float barragePostFireDelaySeconds = 0.4f;
        [SerializeField, Range(0.01f, 1f)] private float barrageSlowMultiplier = 0.08f;
        [SerializeField, Min(0.1f)] private float barrageSlowSafetySeconds = 10f;
        [SerializeField, Min(0f)] private float executionImageLeadSeconds = 0.35f;
        [SerializeField, Min(0.1f)] private float executionImageSeconds = 3.5f;

        [Header("Aim Charge")]
        [SerializeField, Min(0f)] private float aimChargeSeconds = 1.5f;

        [Header("Camera")]
        [SerializeField, Range(0f, 1f)] private float wideCameraFocusWeight = 1f;
        [SerializeField, Range(0.1f, 1.5f)] private float wideCameraZoomMultiplier = 0.82f;
        [SerializeField, Min(0.01f)] private float wideCameraBlendSmoothTime = 0.2f;
        [FormerlySerializedAs("revealCameraZoomMultiplier")]
        [SerializeField, Range(0.1f, 1f)] private float aimChargeCameraZoomMultiplier = 0.46f;
        [FormerlySerializedAs("revealCameraBlendSmoothTime")]
        [SerializeField, Min(0.01f)] private float aimChargeCameraBlendSmoothTime = 1.1f;

        [Header("Fallback Stage")]
        [SerializeField] private Vector2 fallbackPlayerPosition = Vector2.zero;
        [SerializeField] private Vector2 fallbackBossPosition = new(0f, 4.2f);
        [FormerlySerializedAs("finalShotBossExtraDistance")]
        [SerializeField, Min(0.1f)] private float finalShotBossLeftDistance = 3.1f;

        private readonly List<EnemyProjectile> spawnedProjectiles = new();
        private readonly List<List<EnemyProjectile>> projectileBatches = new();
        private PlayerCombatController player;
        private AssassinBossAI assassin;
        private CameraFollow2D cameraFollow;
        private bool runtimePrepared;
        private bool barrageSlowActive;
        private float lastProjectileSpawnTime = float.NegativeInfinity;
        private GameObject runtimeCameraAnchorRoot;
        private Transform runtimeWideCameraFocus;
        private Transform runtimeProjectileFocusProxy;

        public override bool CanPlay
        {
            get
            {
                ResolveReferences();
                return (stage == null || stage.HasRequiredAnchors)
                    && !string.IsNullOrWhiteSpace(executionPatternId)
                    && (patternSourceBoss == null
                        || patternSourceBoss.HasConfiguredStealthGraphPattern(
                            executionPatternId));
            }
        }

        public override Transform InitialCameraFocus =>
            stage != null && stage.WideCameraFocus != null
                ? stage.WideCameraFocus
                : runtimeWideCameraFocus != null
                    ? runtimeWideCameraFocus
                    : assassin != null ? assassin.transform : transform;

        public override float WideCameraFocusWeight => wideCameraFocusWeight;
        public override float WideCameraZoomMultiplier => wideCameraZoomMultiplier;
        public override float WideCameraBlendSmoothTime => wideCameraBlendSmoothTime;

        public override float ExpectedDurationSeconds =>
            parryApproachTimeoutSeconds * 2f
            + parryIntervalSeconds * parryCountPerWhip * 2f
            + barrageWaitTimeoutSeconds
            + barragePostFireDelaySeconds
            + executionImageLeadSeconds
            + aimChargeSeconds;

        private void Awake()
        {
            ResolveReferences();
        }

        private void LateUpdate()
        {
            if (!runtimePrepared || player == null || assassin == null)
            {
                return;
            }

            AimPlayerAtAssassin();
            MaintainBarrageSlow();
        }

        private void OnDisable()
        {
            CleanupRuntime(true);
        }

        public override bool SupportsBoss(BossAI boss)
        {
            return boss is AssassinBossAI;
        }

        internal override bool Prepare(
            PlayerCombatController nextPlayer,
            BossAI nextBoss)
        {
            ResolveReferences();
            CleanupRuntime(false);

            if (nextPlayer == null
                || nextBoss is not AssassinBossAI nextAssassin)
            {
                return false;
            }

            player = nextPlayer;
            assassin = nextAssassin;
            patternSourceBoss ??= nextAssassin;
            if (!CanPlay
                || !assassin.HasConfiguredStealthGraphPattern(
                    executionPatternId))
            {
                player = null;
                assassin = null;
                return false;
            }

            cameraFollow = player.CameraFollow;
            runtimePrepared = PlaceActors();
            if (!runtimePrepared
                || !assassin.BeginExecutionStealthPattern(
                    executionPatternId))
            {
                CleanupRuntime(false);
                return false;
            }

            assassin.FaceTowards(player.transform.position);
            AimPlayerAtAssassin();
            return true;
        }

        internal override IEnumerator PlayPrelude()
        {
            if (!runtimePrepared)
            {
                yield break;
            }

            bool completed = false;
            try
            {
                if (!assassin.TryRunCinematicPatternOnce(
                        executionPatternId,
                        ConfigureSpawnedPatternProjectile))
                {
                    yield break;
                }

                yield return WaitForProjectileBatch(0);
                if (!HasProjectileBatch(0))
                {
                    yield break;
                }

                yield return ParryClosestProjectiles(projectileBatches[0]);

                yield return WaitForProjectileBatch(1);
                if (!HasProjectileBatch(1))
                {
                    yield break;
                }

                yield return ParryClosestProjectiles(projectileBatches[1]);
                ReturnToWideCamera();
                assassin.SetExecutionStealthHidden(true);

                yield return WaitForBarrageReady();
                if (!runtimePrepared)
                {
                    yield break;
                }

                if (barragePostFireDelaySeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(
                        barragePostFireDelaySeconds);
                }

                if (!runtimePrepared)
                {
                    yield break;
                }

                PlaceAssassinLeftForFinalShot();
                BeginExecutionImagePresentation();
                if (executionImageLeadSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(
                        executionImageLeadSeconds);
                }

                BeginAimCharge();
                if (aimChargeSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(
                        aimChargeSeconds);
                }

                completed = true;
            }
            finally
            {
                if (!completed)
                {
                    CleanupRuntime(false);
                }
            }
        }

        internal override void OnFinalBlackoutStarted(BossAI boss)
        {
            RevealAssassinAndClearThreats();
        }

        internal override void OnFinalShotImpact(BossAI boss)
        {
            RevealAssassinAndClearThreats();
            StopChargeVfx(false);
        }

        internal override void OnFinalBlackoutEnded(BossAI boss)
        {
            RevealAssassinAndClearThreats();
        }

        internal override void OnFinalDeathSequenceComplete(BossAI boss)
        {
            CleanupRuntime(true);
        }

        internal override void Cancel()
        {
            CleanupRuntime(true);
        }

        private IEnumerator WaitForProjectileBatch(int batchIndex)
        {
            float remaining = Mathf.Max(
                0.1f,
                barrageWaitTimeoutSeconds);
            while (runtimePrepared
                && !HasProjectileBatch(batchIndex)
                && remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (!HasProjectileBatch(batchIndex))
            {
                Debug.LogWarning(
                    $"{nameof(AssassinExecutionSequence)}: " +
                    $"{batchIndex + 1}번째 휩뿌리기 탄막을 감지하지 못했습니다.",
                    this);
            }
        }

        private bool HasProjectileBatch(int batchIndex)
        {
            return batchIndex >= 0
                && batchIndex < projectileBatches.Count
                && projectileBatches[batchIndex].Count > 0;
        }

        private IEnumerator ParryClosestProjectiles(
            List<EnemyProjectile> batch)
        {
            if (batch == null || batch.Count == 0 || player == null)
            {
                yield break;
            }

            float remaining = Mathf.Max(
                0.1f,
                parryApproachTimeoutSeconds);
            while (runtimePrepared
                && GetClosestProjectileDistance(batch)
                    > parryApproachDistance
                && remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            List<EnemyProjectile> candidates = new(batch);
            candidates.RemoveAll(projectile => !IsUsableProjectile(projectile));
            Vector2 playerPosition = player.transform.position;
            candidates.Sort(
                (left, right) =>
                    Vector2.SqrMagnitude(
                        (Vector2)left.transform.position - playerPosition)
                    .CompareTo(
                        Vector2.SqrMagnitude(
                            (Vector2)right.transform.position
                            - playerPosition)));

            int count = Mathf.Min(
                Mathf.Max(1, parryCountPerWhip),
                candidates.Count);
            for (int i = 0; i < count && runtimePrepared; i++)
            {
                EnemyProjectile projectile = candidates[i];
                if (!IsUsableProjectile(projectile))
                {
                    continue;
                }

                FocusAndAimAtProjectile(projectile);
                SoundManager.PlaySfx(SoundEvent.Player_NormalShot);
                player.TryParryProjectileForCinematic(projectile);
                cameraFollow?.PlayImpact(
                    Vector2.up,
                    0.055f,
                    0.09f,
                    0.07f);

                if (parryIntervalSeconds > 0f && i < count - 1)
                {
                    yield return new WaitForSecondsRealtime(
                        parryIntervalSeconds);
                }
            }

            AimPlayerAtAssassin();
        }

        private float GetClosestProjectileDistance(
            List<EnemyProjectile> projectiles)
        {
            if (player == null || projectiles == null)
            {
                return 0f;
            }

            float closestSqrDistance = float.PositiveInfinity;
            Vector2 playerPosition = player.transform.position;
            for (int i = 0; i < projectiles.Count; i++)
            {
                EnemyProjectile projectile = projectiles[i];
                if (!IsUsableProjectile(projectile))
                {
                    continue;
                }

                float sqrDistance = Vector2.SqrMagnitude(
                    (Vector2)projectile.transform.position
                    - playerPosition);
                closestSqrDistance = Mathf.Min(
                    closestSqrDistance,
                    sqrDistance);
            }

            return float.IsPositiveInfinity(closestSqrDistance)
                ? 0f
                : Mathf.Sqrt(closestSqrDistance);
        }

        private IEnumerator WaitForBarrageReady()
        {
            float remaining = Mathf.Max(
                0.1f,
                barrageWaitTimeoutSeconds);
            while (runtimePrepared
                && GetProjectileBatchCount(2)
                    < Mathf.Max(1, barrageProjectilesBeforeReveal)
                && remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (GetProjectileBatchCount(2)
                < Mathf.Max(1, barrageProjectilesBeforeReveal))
            {
                Debug.LogWarning(
                    $"{nameof(AssassinExecutionSequence)}: " +
                    "분신 탄막 대기 시간이 초과됐습니다.",
                    this);
            }
        }

        private int GetProjectileBatchCount(int batchIndex)
        {
            return HasProjectileBatch(batchIndex)
                ? projectileBatches[batchIndex].Count
                : 0;
        }

        private void BeginExecutionImagePresentation()
        {
            barrageSlowActive = true;
            EnemyTimeScale.SetTemporary(
                barrageSlowMultiplier,
                barrageSlowSafetySeconds);
            player?.PlayExecutionImageForCinematic(
                executionImageSeconds);

            SoundManager.PlaySfx(SoundEvent.Assassin_ExecutionImage);
        }

        private void BeginAimCharge()
        {
            if (player == null || assassin == null)
            {
                return;
            }

            AimPlayerAtAssassin();
            cameraFollow?.BeginCinematicFocus(
                player.transform,
                1f,
                aimChargeCameraZoomMultiplier,
                aimChargeCameraBlendSmoothTime);
            if (aimChargeVfx != null)
            {
                aimChargeVfx.Play(true);
            }

            aimChargeGatherVfx?.Play(player.RightFireOrigin);
            SoundManager.PlaySfx(SoundEvent.Execution_AssassinAimCharge);
        }

        private void ConfigureSpawnedPatternProjectile(
            EnemyProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.ConfigureExecutionPauseIgnored(true);
            projectile.ConfigureIgnoresWalls(
                ignorePatternProjectileWalls);
            spawnedProjectiles.Add(projectile);

            float now = Time.unscaledTime;
            if (projectileBatches.Count == 0
                || now - lastProjectileSpawnTime
                    > projectileBatchSeparationSeconds)
            {
                projectileBatches.Add(new List<EnemyProjectile>());
            }

            projectileBatches[^1].Add(projectile);
            lastProjectileSpawnTime = now;
        }

        private void FocusAndAimAtProjectile(
            EnemyProjectile projectile)
        {
            if (projectile == null || player == null)
            {
                return;
            }

            Transform focusProxy = GetProjectileFocusProxy();
            if (focusProxy != null)
            {
                focusProxy.position = projectile.transform.position;
                cameraFollow?.BeginCinematicFocus(
                    focusProxy,
                    parryCameraFocusWeight,
                    parryCameraZoomMultiplier,
                    parryCameraBlendSmoothTime);
            }

            Vector2 direction =
                (Vector2)projectile.transform.position
                - (Vector2)player.transform.position;
            player.Visual?.UpdateExecutionAimVisual(direction);
            player.Visual?.SetBodyAimDirection(direction);
        }

        private void ReturnToWideCamera()
        {
            Transform focusTarget =
                stage != null && stage.WideCameraFocus != null
                    ? stage.WideCameraFocus
                    : runtimeWideCameraFocus;
            cameraFollow?.BeginCinematicFocus(
                focusTarget != null
                    ? focusTarget
                    : assassin != null
                        ? assassin.transform
                        : transform,
                wideCameraFocusWeight,
                wideCameraZoomMultiplier,
                wideCameraBlendSmoothTime);
        }

        private void AimPlayerAtAssassin()
        {
            if (player == null || assassin == null)
            {
                return;
            }

            Vector2 direction =
                (Vector2)assassin.transform.position
                - (Vector2)player.transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.up;
            }

            player.Visual?.BeginExecutionAimVisual(direction);
            player.Visual?.UpdateExecutionAimVisual(direction);
            player.Visual?.SetBodyAimDirection(direction);
            player.Visual?.SetLeftArmAimDirection(direction);
        }

        private void PlaceAssassinLeftForFinalShot()
        {
            if (player == null
                || assassin == null)
            {
                return;
            }

            Vector2 playerPosition = player.transform.position;
            MoveActor(
                assassin.transform,
                assassin.Body,
                playerPosition
                    + Vector2.left
                    * Mathf.Max(0.1f, finalShotBossLeftDistance));
            Physics2D.SyncTransforms();
        }

        private void MaintainBarrageSlow()
        {
            if (!barrageSlowActive
                || Mathf.Approximately(
                    EnemyTimeScale.Current,
                    barrageSlowMultiplier))
            {
                return;
            }

            EnemyTimeScale.SetTemporary(
                barrageSlowMultiplier,
                barrageSlowSafetySeconds);
        }

        private void RevealAssassinAndClearThreats()
        {
            barrageSlowActive = false;
            RestoreEnemyTimeScale();
            if (assassin != null)
            {
                assassin.EndExecutionStealthPatternAndReveal();
                assassin.ClearActiveClones();
            }

            DestroySpawnedProjectiles();
        }

        private void DestroySpawnedProjectiles()
        {
            for (int i = 0; i < spawnedProjectiles.Count; i++)
            {
                EnemyProjectile projectile = spawnedProjectiles[i];
                if (projectile != null
                    && projectile.gameObject.activeInHierarchy)
                {
                    projectile.DestroyFromOwner();
                }
            }

            spawnedProjectiles.Clear();
            projectileBatches.Clear();
        }

        private static bool IsUsableProjectile(
            EnemyProjectile projectile)
        {
            return projectile != null
                && projectile.gameObject.activeInHierarchy;
        }

        private void CleanupRuntime(bool endCameraFocus)
        {
            bool hadRuntime = runtimePrepared
                || player != null
                || assassin != null
                || spawnedProjectiles.Count > 0;
            runtimePrepared = false;
            barrageSlowActive = false;

            if (assassin != null)
            {
                assassin.EndExecutionStealthPatternAndReveal();
                assassin.ClearActiveClones();
            }

            DestroySpawnedProjectiles();
            if (hadRuntime)
            {
                RestoreEnemyTimeScale();
            }

            StopChargeVfx(endCameraFocus);
            if (endCameraFocus
                && cameraFollow != null)
            {
                cameraFollow.EndCinematicFocusIfTarget(
                    GetProjectileFocusProxy());
            }

            DestroyRuntimeCameraAnchors();

            lastProjectileSpawnTime = float.NegativeInfinity;
            player = null;
            assassin = null;
            cameraFollow = null;
        }

        private void StopChargeVfx(bool clear)
        {
            if (aimChargeVfx != null)
            {
                aimChargeVfx.Stop(
                    true,
                    clear
                        ? ParticleSystemStopBehavior.StopEmittingAndClear
                        : ParticleSystemStopBehavior.StopEmitting);
            }

            aimChargeGatherVfx?.Stop(clear);
        }

        private static void RestoreEnemyTimeScale()
        {
            EnemyTimeScale.SetTemporary(1f, 0f);
        }

        private void ResolveReferences()
        {
            stage ??= GetComponent<BossExecutionStage>();
            aimChargeGatherVfx ??=
                GetComponent<ExecutionChargeGatherVfx>();
            if (aimChargeGatherVfx == null && Application.isPlaying)
            {
                aimChargeGatherVfx =
                    gameObject.AddComponent<ExecutionChargeGatherVfx>();
            }

            if (patternSourceBoss == null)
            {
                AssassinBossAI[] bosses =
                    Object.FindObjectsByType<AssassinBossAI>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                if (bosses.Length > 0)
                {
                    patternSourceBoss = bosses[0];
                }
            }
        }

        private bool PlaceActors()
        {
            if (player == null || assassin == null)
            {
                return false;
            }

            if (stage != null)
            {
                return stage.PlaceActors(player, assassin);
            }

            MoveActor(
                player.transform,
                player.GetComponent<Rigidbody2D>(),
                fallbackPlayerPosition);
            MoveActor(
                assassin.transform,
                assassin.Body,
                fallbackBossPosition);
            EnsureRuntimeCameraAnchors();
            Physics2D.SyncTransforms();
            return true;
        }

        private void EnsureRuntimeCameraAnchors()
        {
            if (runtimeCameraAnchorRoot != null)
            {
                return;
            }

            runtimeCameraAnchorRoot =
                new GameObject("AssassinExecutionRuntimeAnchors")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            runtimeWideCameraFocus =
                CreateRuntimeAnchor("WideCameraFocus");
            runtimeProjectileFocusProxy =
                CreateRuntimeAnchor("ProjectileFocusProxy");
            runtimeWideCameraFocus.position =
                (fallbackPlayerPosition + fallbackBossPosition) * 0.5f;
            runtimeProjectileFocusProxy.position =
                fallbackPlayerPosition;
        }

        private Transform CreateRuntimeAnchor(string anchorName)
        {
            GameObject anchorObject = new(anchorName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            anchorObject.transform.SetParent(
                runtimeCameraAnchorRoot.transform,
                false);
            return anchorObject.transform;
        }

        private Transform GetProjectileFocusProxy()
        {
            return stage != null
                ? stage.ProjectileFocusProxy
                : runtimeProjectileFocusProxy;
        }

        private void DestroyRuntimeCameraAnchors()
        {
            if (runtimeCameraAnchorRoot != null)
            {
                Destroy(runtimeCameraAnchorRoot);
            }

            runtimeCameraAnchorRoot = null;
            runtimeWideCameraFocus = null;
            runtimeProjectileFocusProxy = null;
        }

        private static void MoveActor(
            Transform actor,
            Rigidbody2D body,
            Vector2 destination)
        {
            if (actor == null)
            {
                return;
            }

            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.position = destination;
            }

            Vector3 position = actor.position;
            position.x = destination.x;
            position.y = destination.y;
            actor.position = position;
        }
    }
}
