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
    [AddComponentMenu("Week14/Combat/Hacker Execution Sequence")]
    public sealed class HackerExecutionSequence : BossExecutionSequence
    {
        [Header("References")]
        [SerializeField] private BossExecutionStage stage;
        [SerializeField] private HackerBossAI patternSourceBoss;
        [SerializeField] private ParticleSystem aimChargeVfx;
        [SerializeField] private ExecutionChargeGatherVfx aimChargeGatherVfx;
        [SerializeField] private HackerExecutionParticleGatherVfx particleGatherVfx;

        [Header("Boss Graph Pattern")]
        [SerializeField] private string executionPatternId = "Execution";
        [SerializeField] private bool ignoreCombatProjectileWalls = true;

        [Header("Prelude Tempo")]
        [SerializeField, Range(0.1f, 1f)] private float preludeTempoMultiplier = 0.78f;
        [SerializeField, Range(0.1f, 1f)] private float preludeAnimatorMultiplier = 0.82f;
        [SerializeField, Min(1f)] private float preludeTempoSafetySeconds = 30f;

        [Header("Sniping Shots")]
        [SerializeField, Min(0.1f)] private float shotApproachTimeoutSeconds = 4f;
        [SerializeField, Range(0.1f, 0.9f)] private float firstShotParryTravelRatio = 0.52f;
        [SerializeField, Range(0.1f, 1.5f)] private float firstShotCameraZoomMultiplier = 0.42f;
        [SerializeField, Min(0.01f)] private float firstShotCameraBlendSmoothTime = 0.07f;
        [SerializeField, Min(0f)] private float firstShotCameraHoldSeconds = 0.2f;
        [SerializeField, Range(0.01f, 1f)] private float firstShotParrySlowMultiplier = 0.08f;
        [SerializeField, Min(0.01f)] private float firstShotParrySlowSeconds = 0.5f;
        [FormerlySerializedAs("secondHitCameraZoomMultiplier")]
        [SerializeField, Range(0.1f, 1.5f)] private float secondShotCameraZoomMultiplier = 0.46f;
        [FormerlySerializedAs("secondHitCameraBlendSmoothTime")]
        [SerializeField, Min(0.01f)] private float secondShotCameraBlendSmoothTime = 0.07f;
        [SerializeField, Range(0.1f, 0.9f)] private float secondShotParryTravelRatio = 0.5f;
        [FormerlySerializedAs("secondShotKnockbackSeconds")]
        [SerializeField, Min(0f)] private float secondShotParryHoldSeconds = 0.18f;

        [Header("Sweep Roll")]
        [FormerlySerializedAs("sweepRollDelayAfterHitSeconds")]
        [SerializeField, Min(0.1f)] private float sweepRollWaitTimeoutSeconds = 3f;
        [SerializeField, Min(0f)] private float sweepRollStartOffsetSeconds;
        [SerializeField, Min(0.05f)] private float sweepRollSeconds = 0.45f;
        [SerializeField, Min(0.1f)] private float rollBossClearanceDistance = 2.35f;
        [SerializeField, Min(0.1f)] private float rollDistance = 2.55f;
        [SerializeField, Range(0.1f, 1.5f)] private float rollCameraZoomMultiplier = 0.64f;
        [SerializeField, Min(0.01f)] private float rollCameraBlendSmoothTime = 0.07f;
        [SerializeField, Min(0f)] private float rollCameraShakeAmplitude = 0.11f;
        [SerializeField, Min(0.01f)] private float rollCameraShakeSeconds = 0.16f;

        [Header("Slam Barrage")]
        [SerializeField, Min(1)] private int slamProjectileCount = 14;
        [SerializeField, Min(0.1f)] private float slamBarrageWaitTimeoutSeconds = 8f;
        [SerializeField, Min(0f)] private float slamPostFireDelaySeconds = 0.3f;
        [SerializeField, Range(0.01f, 1f)] private float slamSlowMultiplier = 0.06f;
        [SerializeField, Range(0f, 1f)] private float bossAnimationSlowMultiplier = 0.25f;
        [SerializeField, Min(0.1f)] private float slamSlowSafetySeconds = 10f;
        [SerializeField, Min(0.1f)] private float executionImageSeconds = 2.4f;
        [SerializeField, BossGraphSfxId] private string executionImageSfxId;

        [Header("Final Parry Camera")]
        [SerializeField, Range(0f, 1f)] private float parryBossCameraFocusWeight = 0.85f;
        [SerializeField, Range(0.1f, 1.5f)] private float parryBossCameraZoomMultiplier = 0.62f;
        [SerializeField, Min(0.01f)] private float parryBossCameraBlendSmoothTime = 0.12f;
        [SerializeField, Min(0f)] private float parryBossFocusHoldSeconds = 0.35f;

        [Header("Aim Charge")]
        [SerializeField, Min(0.1f)] private float aimChargeSeconds = 3f;
        [SerializeField, Range(0.1f, 1.5f)] private float chargeCameraZoomMultiplier = 0.704f;
        [SerializeField, Min(0.01f)] private float chargeCameraBlendSmoothTime = 1.3f;
        [SerializeField, Min(0f)] private float chargeCameraShakeAmplitude = 0.025f;
        [SerializeField, Min(0.05f)] private float chargeCameraShakeInterval = 0.16f;
        [SerializeField, Min(0.05f)] private float chargeCameraShakeSeconds = 0.12f;

        [Header("Wide Camera")]
        [SerializeField, Range(0f, 1f)] private float wideCameraFocusWeight = 1f;
        [SerializeField, Range(0.1f, 1.5f)] private float wideCameraZoomMultiplier = 1.08f;
        [SerializeField, Min(0.01f)] private float wideCameraBlendSmoothTime = 0.2f;
        [SerializeField, Range(0.1f, 1.5f)] private float shotReleaseZoomMultiplier = 0.96f;
        [SerializeField, Min(0.01f)] private float shotReleaseBlendSmoothTime = 0.08f;

        [Header("Final Shot")]
        [SerializeField, Min(0.1f)] private float finalShotLineWidthMultiplier = 2.5f;

        [Header("Fallback Stage")]
        [SerializeField] private Vector2 fallbackPlayerPosition = Vector2.zero;
        [SerializeField] private Vector2 fallbackBossPosition = new(6.2f, 0f);
        [SerializeField] private Vector2 fallbackPlayerRollEndPosition = new(3.3f, 0f);

        private readonly List<EnemyProjectile> spawnedProjectiles = new();
        private readonly List<EnemyProjectile> combatProjectiles = new();
        private readonly List<float> combatProjectileParryDistances = new();
        private readonly List<EnemyProjectile> finalBarrageProjectiles = new();
        private readonly Dictionary<Animator, float> animatorSpeeds = new();
        private PlayerCombatController player;
        private HackerBossAI hacker;
        private CameraFollow2D cameraFollow;
        private Rigidbody2D playerBody;
        private bool runtimePrepared;
        private bool preludeTempoActive;
        private bool finalSlowActive;
        private float shotParrySlowEndsAt;
        private bool patternStoppedForFinalShot;
        private GameObject runtimeAnchorRoot;
        private Transform runtimeWideCameraFocus;
        private Transform runtimeProjectileFocusProxy;
        private Transform runtimeRollEnd;

        public override bool CanPlay
        {
            get
            {
                ResolveReferences();
                return (stage == null || stage.HasRequiredAnchors)
                    && !string.IsNullOrWhiteSpace(executionPatternId)
                    && (patternSourceBoss == null
                        || patternSourceBoss.HasConfiguredGraphPattern(
                            executionPatternId));
            }
        }

        public override Transform InitialCameraFocus =>
            stage != null && stage.WideCameraFocus != null
                ? stage.WideCameraFocus
                : runtimeWideCameraFocus != null
                    ? runtimeWideCameraFocus
                    : hacker != null
                        ? hacker.transform
                        : transform;

        public override float WideCameraFocusWeight => wideCameraFocusWeight;
        public override float WideCameraZoomMultiplier => wideCameraZoomMultiplier;
        public override float WideCameraBlendSmoothTime => wideCameraBlendSmoothTime;
        public override bool UseParryColorForFinalShotLine => false;
        public override bool BeginFinalShotSlowMotionBeforeImpact => true;
        public override float FinalShotLineWidthMultiplier =>
            finalShotLineWidthMultiplier;
        public override float ExpectedDurationSeconds =>
            shotApproachTimeoutSeconds * 2f
            + firstShotCameraHoldSeconds
            + secondShotParryHoldSeconds
            + sweepRollWaitTimeoutSeconds
            + sweepRollStartOffsetSeconds
            + sweepRollSeconds
            + slamBarrageWaitTimeoutSeconds
            + slamPostFireDelaySeconds
            + aimChargeSeconds;
        public override string FinalChargeSfxId =>
            GameplaySfxIds.ExecuteChargeHacker;
        public override bool PlayFinalChargeSfxOnChargeStart => true;
        public override float FinalChargeSfxDelayAfterChargeStartSeconds =>
            0.7f;

        private void Awake()
        {
            ResolveReferences();
        }

        private void LateUpdate()
        {
            if (!runtimePrepared)
            {
                return;
            }

            AimPlayerAtHacker();
            MaintainPreludeTempo();
            MaintainFinalSlow();
        }

        private void OnDisable()
        {
            CleanupRuntime(true);
        }

        public override bool SupportsBoss(BossAI boss)
        {
            return boss is HackerBossAI;
        }

        internal override bool Prepare(
            PlayerCombatController nextPlayer,
            BossAI nextBoss)
        {
            ResolveReferences();
            CleanupRuntime(false);
            if (nextPlayer == null
                || nextBoss is not HackerBossAI nextHacker)
            {
                return false;
            }

            player = nextPlayer;
            hacker = nextHacker;
            patternSourceBoss ??= nextHacker;
            if (!CanPlay
                || !hacker.HasConfiguredGraphPattern(executionPatternId))
            {
                player = null;
                hacker = null;
                return false;
            }

            playerBody = player.GetComponent<Rigidbody2D>();
            cameraFollow = player.CameraFollow;
            patternStoppedForFinalShot = false;
            runtimePrepared = PlaceActors();
            if (!runtimePrepared)
            {
                CleanupRuntime(false);
                return false;
            }

            hacker.FaceTowards(player.transform.position);
            hacker.BeginExecutionMeleeReleaseHold();
            AimPlayerAtHacker();
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
                hacker.PrepareExecutionCinematicAnimation();
                BeginPreludeTempo();
                BeginWideCamera();
                if (!hacker.TryRunCinematicPatternOnce(
                        executionPatternId,
                        ConfigureSpawnedPatternProjectile))
                {
                    yield break;
                }

                yield return WaitForCombatProjectile(0);
                yield return WaitForShotParryPoint(
                    GetCombatProjectile(0),
                    GetCombatProjectileParryDistance(0),
                    firstShotCameraZoomMultiplier,
                    firstShotCameraBlendSmoothTime);
                yield return ParryFirstShot();

                yield return WaitForCombatProjectile(1);
                yield return WaitForShotParryPoint(
                    GetCombatProjectile(1),
                    GetCombatProjectileParryDistance(1),
                    secondShotCameraZoomMultiplier,
                    secondShotCameraBlendSmoothTime);
                yield return ParrySecondShot();

                BeginWideCamera();
                yield return WaitForSweepRollMoment();
                yield return RollPlayerNearBoss();
                BeginWideCamera();
                yield return WaitForFinalBarrage();
                if (!runtimePrepared)
                {
                    yield break;
                }

                if (slamPostFireDelaySeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(
                        slamPostFireDelaySeconds);
                }

                BeginFinalParryPresentation();
                ParryFinalBarrageAtOnce();
                BeginAimCharge();
                yield return PlayAimChargeCamera();
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
            hacker?.SetExecutionBlackoutVisual(true);
        }

        internal override void OnFinalShotPreparationStarted(
            BossAI boss)
        {
            ReleaseCameraForFinalShot();
            RestoreBossAnimatorSpeeds();
            hacker?.SetExecutionBlackoutVisual(true);
            hacker?.ReleaseExecutionMeleeAttack();
        }

        internal override void OnFinalShotImpact(BossAI boss)
        {
            StopPatternAndClearThreats();
            StopChargeVfx(false);
        }

        internal override void OnFinalBlackoutEnded(BossAI boss)
        {
            hacker?.SetExecutionBlackoutVisual(false);
            StopPatternAndClearThreats();
        }

        internal override void OnFinalDeathSequenceComplete(BossAI boss)
        {
            CleanupRuntime(
                endCameraFocus: false,
                playGroggyVisual: false,
                stopBossPattern: false);
        }

        internal override void Cancel()
        {
            CleanupRuntime(true);
        }

        private IEnumerator WaitForCombatProjectile(int index)
        {
            float remaining = Mathf.Max(
                0.1f,
                shotApproachTimeoutSeconds);
            while (runtimePrepared
                && combatProjectiles.Count <= index
                && remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (combatProjectiles.Count <= index)
            {
                Debug.LogWarning(
                    $"{nameof(HackerExecutionSequence)}: " +
                    $"{index + 1}번째 저격탄 생성 대기 시간이 초과됐습니다.",
                    this);
            }
        }

        private IEnumerator WaitForShotParryPoint(
            EnemyProjectile projectile,
            float parryDistance,
            float cameraZoomMultiplier,
            float cameraBlendSmoothTime)
        {
            if (!IsUsableProjectile(projectile) || player == null)
            {
                yield break;
            }

            float remainingDistance = Mathf.Max(
                0.1f,
                parryDistance);
            float remainingTime = Mathf.Max(
                0.1f,
                shotApproachTimeoutSeconds);
            while (runtimePrepared
                && IsUsableProjectile(projectile)
                && player != null
                && Vector2.Distance(
                    projectile.transform.position,
                    player.transform.position) > remainingDistance
                && remainingTime > 0f)
            {
                remainingTime -= Time.unscaledDeltaTime;
                yield return null;
            }

            Transform focusProxy = GetProjectileFocusProxy();
            if (focusProxy != null
                && IsUsableProjectile(projectile))
            {
                focusProxy.position = projectile.transform.position;
                cameraFollow?.BeginCinematicFocus(
                    focusProxy,
                    1f,
                    cameraZoomMultiplier,
                    cameraBlendSmoothTime);
            }
            else
            {
                BeginPlayerCamera(
                    cameraZoomMultiplier,
                    cameraBlendSmoothTime);
            }
        }

        private IEnumerator ParryFirstShot()
        {
            yield return ParryShot(
                GetCombatProjectile(0),
                firstShotCameraHoldSeconds);
        }

        private IEnumerator ParrySecondShot()
        {
            yield return ParryShot(
                GetCombatProjectile(1),
                secondShotParryHoldSeconds);
        }

        private IEnumerator ParryShot(
            EnemyProjectile projectile,
            float cameraHoldSeconds)
        {
            if (!IsUsableProjectile(projectile) || player == null)
            {
                yield break;
            }

            float slowSeconds = Mathf.Max(
                firstShotParrySlowSeconds,
                cameraHoldSeconds + 0.1f);
            shotParrySlowEndsAt =
                Time.unscaledTime + slowSeconds;
            EnemyTimeScale.SetTemporary(
                firstShotParrySlowMultiplier,
                slowSeconds);

            if (cameraHoldSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    cameraHoldSeconds);
            }

            if (!IsUsableProjectile(projectile) || player == null)
            {
                yield break;
            }

            projectile.ReleaseCinematicPlayerClearance();
            projectile.ConfigureInterceptable(true);
            AimPlayerAt(projectile.transform.position);
            Vector3 impactPosition = projectile.transform.position;
            Vector2 parryDirection =
                impactPosition
                - (player.RightFireOrigin != null
                    ? player.RightFireOrigin.position
                    : player.transform.position);
            if (parryDirection.sqrMagnitude <= 0.0001f)
            {
                parryDirection = Vector2.right;
            }

            parryDirection.Normalize();
            cameraFollow?.PlayImpact(
                (Vector2)projectile.transform.position
                    - (Vector2)player.transform.position,
                0.12f,
                0.16f,
                0f);
            SoundManager.PlaySfx("PlayerShot");
            bool parried = player.TryParryProjectileForCinematic(
                projectile,
                false);
            if (!parried && IsUsableProjectile(projectile))
            {
                projectile.DestroyFromOwner();
            }

            player.PlayParryImpact(
                impactPosition,
                parryDirection,
                false);
            SoundManager.PlaySfx("Parry2");
        }

        private IEnumerator RollPlayerNearBoss()
        {
            if (player == null)
            {
                yield break;
            }

            Vector2 start = GetPlayerPosition();
            Vector2 end;
            if (hacker != null)
            {
                Vector2 toBoss =
                    (Vector2)hacker.transform.position - start;
                if (toBoss.sqrMagnitude <= 0.0001f)
                {
                    toBoss = Vector2.right;
                }

                float maximumTravel = Mathf.Max(
                    0f,
                    toBoss.magnitude
                        - Mathf.Max(0.1f, rollBossClearanceDistance));
                float travelDistance = Mathf.Min(
                    Mathf.Max(0.1f, rollDistance),
                    maximumTravel);
                end = start + toBoss.normalized * travelDistance;
            }
            else
            {
                Transform rollEnd = stage != null
                    ? stage.PlayerRollEnd
                    : runtimeRollEnd;
                if (rollEnd == null)
                {
                    yield break;
                }

                end = rollEnd.position;
            }

            Vector2 direction = end - start;
            float duration = Mathf.Max(0.05f, sweepRollSeconds);
            BeginPlayerCamera(
                rollCameraZoomMultiplier,
                rollCameraBlendSmoothTime);
            cameraFollow?.PlayImpact(
                direction.sqrMagnitude > 0.0001f
                    ? direction.normalized
                    : Vector2.right,
                rollCameraShakeAmplitude,
                rollCameraShakeSeconds,
                0f);
            player.Visual?.SetBodyAimDirection(direction);
            player.Visual?.PlayCinematicRoll(
                duration,
                direction);
            for (float elapsed = 0f;
                 runtimePrepared && elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = Mathf.SmoothStep(0f, 1f, progress);
                SetActorPosition(
                    player.transform,
                    playerBody,
                    Vector2.LerpUnclamped(start, end, eased));
                yield return null;
            }

            if (runtimePrepared && player != null)
            {
                SetActorPosition(player.transform, playerBody, end);
                AimPlayerAtHacker();
            }
        }

        private IEnumerator WaitForSweepRollMoment()
        {
            float remaining = Mathf.Max(
                0.1f,
                sweepRollWaitTimeoutSeconds);
            while (runtimePrepared
                && hacker != null
                && !hacker.IsExecutionWeaponSweepActive
                && remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (runtimePrepared
                && hacker != null
                && hacker.IsExecutionWeaponSweepActive
                && sweepRollStartOffsetSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(
                    sweepRollStartOffsetSeconds);
            }
        }

        private IEnumerator WaitForFinalBarrage()
        {
            float remaining = Mathf.Max(
                0.1f,
                slamBarrageWaitTimeoutSeconds);
            int requiredCount = Mathf.Max(1, slamProjectileCount);
            while (runtimePrepared
                && finalBarrageProjectiles.Count < requiredCount
                && remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }

            if (finalBarrageProjectiles.Count < requiredCount)
            {
                Debug.LogWarning(
                    $"{nameof(HackerExecutionSequence)}: " +
                    $"Slam 탄막을 {requiredCount}개 기다렸지만 " +
                    $"{finalBarrageProjectiles.Count}개만 감지했습니다.",
                    this);
            }
        }

        private void BeginFinalParryPresentation()
        {
            preludeTempoActive = false;
            finalSlowActive = true;
            EnemyTimeScale.SetTemporary(
                slamSlowMultiplier,
                slamSlowSafetySeconds);
            ApplyBossAnimatorSlow();
            player?.PlayExecutionImageForCinematic(
                executionImageSeconds);
            if (!string.IsNullOrWhiteSpace(executionImageSfxId))
            {
                SoundManager.PlaySfx(executionImageSfxId);
            }

            cameraFollow?.BeginCinematicFocus(
                hacker != null ? hacker.transform : null,
                parryBossCameraFocusWeight,
                parryBossCameraZoomMultiplier,
                parryBossCameraBlendSmoothTime);
        }

        private void ParryFinalBarrageAtOnce()
        {
            if (player == null)
            {
                return;
            }

            Color parryColor = player.Config != null
                ? player.Config.ParryEffectColor
                : new Color(0.2f, 0.65f, 1f, 1f);
            Vector3 averagePosition = Vector3.zero;
            int parriedCount = 0;
            for (int i = 0; i < finalBarrageProjectiles.Count; i++)
            {
                EnemyProjectile projectile =
                    finalBarrageProjectiles[i];
                if (!IsUsableProjectile(projectile))
                {
                    continue;
                }

                Vector3 position = projectile.transform.position;
                particleGatherVfx?.CaptureBurst(
                    position,
                    parryColor);
                if (player.TryParryProjectileForCinematic(
                        projectile,
                        false))
                {
                    averagePosition += position;
                    parriedCount++;
                }
            }

            if (parriedCount > 0)
            {
                averagePosition /= parriedCount;
                Vector2 direction = hacker != null
                    ? (Vector2)hacker.transform.position
                        - (Vector2)player.transform.position
                    : Vector2.right;
                player.PlayParryImpact(
                    averagePosition,
                    direction,
                    false);
                SoundManager.PlaySfx("Parry2");
            }
        }

        private void BeginAimCharge()
        {
            if (player == null)
            {
                return;
            }

            NotifyFinalChargeStarted(Mathf.Max(0.1f, aimChargeSeconds));
            AimPlayerAtHacker();
            if (aimChargeVfx != null)
            {
                aimChargeVfx.Play(true);
            }

            Color parryColor = player.Config != null
                ? player.Config.ParryEffectColor
                : new Color(0.2f, 0.65f, 1f, 1f);
            aimChargeGatherVfx?.SetMuzzleCircleVisible(false);
            aimChargeGatherVfx?.Play(player.RightFireOrigin);
            particleGatherVfx?.BeginGather(
                player.RightFireOrigin,
                aimChargeSeconds,
                parryColor);
        }

        private IEnumerator PlayAimChargeCamera()
        {
            float duration = Mathf.Max(0.1f, aimChargeSeconds);
            float focusAt = Mathf.Min(
                duration,
                Mathf.Max(0f, parryBossFocusHoldSeconds));
            float nextShakeAt = focusAt;
            bool playerFocusStarted = false;
            for (float elapsed = 0f;
                 runtimePrepared && elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                if (!playerFocusStarted && elapsed >= focusAt)
                {
                    playerFocusStarted = true;
                    cameraFollow?.BeginCinematicFocus(
                        player != null ? player.transform : null,
                        1f,
                        chargeCameraZoomMultiplier,
                        chargeCameraBlendSmoothTime);
                }

                if (playerFocusStarted
                    && chargeCameraShakeAmplitude > 0f
                    && elapsed >= nextShakeAt)
                {
                    Vector2 direction = Random.insideUnitCircle;
                    cameraFollow?.PlayImpact(
                        direction,
                        chargeCameraShakeAmplitude,
                        chargeCameraShakeSeconds,
                        0f);
                    nextShakeAt += Mathf.Max(
                        0.05f,
                        chargeCameraShakeInterval);
                }

                yield return null;
            }
        }

        private void ReleaseCameraForFinalShot()
        {
            Transform focus = stage != null
                && stage.WideCameraFocus != null
                    ? stage.WideCameraFocus
                    : runtimeWideCameraFocus;
            cameraFollow?.BeginCinematicFocus(
                focus != null ? focus : player?.transform,
                wideCameraFocusWeight,
                shotReleaseZoomMultiplier,
                shotReleaseBlendSmoothTime);
        }

        private void BeginWideCamera()
        {
            Transform focus = stage != null
                && stage.WideCameraFocus != null
                    ? stage.WideCameraFocus
                    : runtimeWideCameraFocus;
            if (focus == runtimeWideCameraFocus
                && player != null
                && hacker != null)
            {
                runtimeWideCameraFocus.position =
                    (player.transform.position
                        + hacker.transform.position)
                    * 0.5f;
            }

            cameraFollow?.BeginCinematicFocus(
                focus != null ? focus : player?.transform,
                wideCameraFocusWeight,
                wideCameraZoomMultiplier,
                wideCameraBlendSmoothTime);
        }

        private void BeginPlayerCamera(
            float zoomMultiplier,
            float blendSmoothTime)
        {
            cameraFollow?.BeginCinematicFocus(
                player != null ? player.transform : null,
                1f,
                zoomMultiplier,
                blendSmoothTime);
        }

        private Transform GetProjectileFocusProxy()
        {
            return stage != null
                && stage.ProjectileFocusProxy != null
                    ? stage.ProjectileFocusProxy
                    : runtimeProjectileFocusProxy;
        }

        private void ConfigureSpawnedPatternProjectile(
            EnemyProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            spawnedProjectiles.Add(projectile);
            projectile.ConfigureExecutionPauseIgnored(true);
            projectile.ConfigurePlayerCollisionIgnored(true);
            if (projectile is HackerWireNodeProjectile)
            {
                return;
            }

            projectile.ConfigureIgnoresWalls(
                ignoreCombatProjectileWalls);
            projectile.ConfigureInterceptable(true);
            if (combatProjectiles.Count < 2)
            {
                int shotIndex = combatProjectiles.Count;
                combatProjectiles.Add(projectile);
                projectile.ConfigurePersistentLifetime();
                if (player != null)
                {
                    float travelRatio = shotIndex == 0
                        ? firstShotParryTravelRatio
                        : secondShotParryTravelRatio;
                    float spawnDistance = Vector2.Distance(
                        projectile.transform.position,
                        player.transform.position);
                    float parryDistance = Mathf.Max(
                        0.1f,
                        spawnDistance
                            * (1f - Mathf.Clamp01(travelRatio)));
                    combatProjectileParryDistances.Add(
                        parryDistance);
                    projectile.ConfigureCinematicPlayerClearance(
                        player.transform,
                        parryDistance);
                }
                else
                {
                    combatProjectileParryDistances.Add(0.1f);
                }
            }
            else
            {
                finalBarrageProjectiles.Add(projectile);
            }
        }

        private EnemyProjectile GetCombatProjectile(int index)
        {
            return index >= 0 && index < combatProjectiles.Count
                ? combatProjectiles[index]
                : null;
        }

        private float GetCombatProjectileParryDistance(int index)
        {
            return index >= 0
                && index < combatProjectileParryDistances.Count
                    ? combatProjectileParryDistances[index]
                    : 0.1f;
        }

        private void AimPlayerAtHacker()
        {
            if (player == null || hacker == null)
            {
                return;
            }

            AimPlayerAt(hacker.transform.position);
            hacker.FaceTowards(player.transform.position);
        }

        private void AimPlayerAt(Vector3 worldPosition)
        {
            if (player == null)
            {
                return;
            }

            Vector2 direction =
                (Vector2)worldPosition
                - (Vector2)player.transform.position;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                direction = Vector2.right;
            }

            player.Visual?.BeginExecutionAimVisual(direction);
            player.Visual?.UpdateExecutionAimVisual(direction);
            player.Visual?.SetBodyAimDirection(direction);
            player.Visual?.SetLeftArmAimDirection(direction);
        }

        private void MaintainFinalSlow()
        {
            if (!finalSlowActive)
            {
                return;
            }

            if (!Mathf.Approximately(
                    EnemyTimeScale.Current,
                    slamSlowMultiplier))
            {
                EnemyTimeScale.SetTemporary(
                    slamSlowMultiplier,
                    slamSlowSafetySeconds);
            }
        }

        private void ApplyBossAnimatorSlow()
        {
            ApplyBossAnimatorMultiplier(
                bossAnimationSlowMultiplier);
        }

        private void ApplyBossAnimatorMultiplier(
            float speedMultiplier)
        {
            RestoreBossAnimatorSpeeds();
            if (hacker == null)
            {
                return;
            }

            Animator[] animators =
                hacker.GetComponentsInChildren<Animator>(true);
            float multiplier = Mathf.Clamp01(
                speedMultiplier);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null)
                {
                    continue;
                }

                animatorSpeeds[animator] = animator.speed;
                animator.speed *= multiplier;
            }
        }

        private void RestoreBossAnimatorSpeeds()
        {
            foreach (KeyValuePair<Animator, float> entry
                     in animatorSpeeds)
            {
                if (entry.Key != null)
                {
                    entry.Key.speed = entry.Value;
                }
            }

            animatorSpeeds.Clear();
        }

        private void BeginPreludeTempo()
        {
            preludeTempoActive = true;
            shotParrySlowEndsAt =
                float.NegativeInfinity;
            EnemyTimeScale.SetTemporary(
                preludeTempoMultiplier,
                preludeTempoSafetySeconds);
            ApplyBossAnimatorMultiplier(
                preludeAnimatorMultiplier);
        }

        private void MaintainPreludeTempo()
        {
            if (!preludeTempoActive || finalSlowActive)
            {
                return;
            }

            float multiplier = Time.unscaledTime
                    < shotParrySlowEndsAt
                ? firstShotParrySlowMultiplier
                : preludeTempoMultiplier;
            if (!Mathf.Approximately(
                    EnemyTimeScale.Current,
                    multiplier))
            {
                EnemyTimeScale.SetTemporary(
                    multiplier,
                    preludeTempoSafetySeconds);
            }
        }

        private void StopPatternAndClearThreats()
        {
            preludeTempoActive = false;
            finalSlowActive = false;
            RestoreEnemyTimeScale();
            RestoreBossAnimatorSpeeds();
            if (!patternStoppedForFinalShot)
            {
                patternStoppedForFinalShot = true;
                hacker?.EndExecutionCinematicPattern();
            }

            DestroySpawnedProjectiles();
        }

        private void DestroySpawnedProjectiles()
        {
            for (int i = 0; i < spawnedProjectiles.Count; i++)
            {
                EnemyProjectile projectile = spawnedProjectiles[i];
                if (IsUsableProjectile(projectile))
                {
                    projectile.DestroyFromOwner();
                }
            }

            spawnedProjectiles.Clear();
            combatProjectiles.Clear();
            combatProjectileParryDistances.Clear();
            finalBarrageProjectiles.Clear();
        }

        private void CleanupRuntime(
            bool endCameraFocus,
            bool playGroggyVisual = true,
            bool stopBossPattern = true)
        {
            bool hadRuntime = runtimePrepared
                || player != null
                || hacker != null
                || spawnedProjectiles.Count > 0;
            runtimePrepared = false;
            preludeTempoActive = false;
            finalSlowActive = false;
            if (stopBossPattern && hacker != null)
            {
                hacker.EndExecutionCinematicPattern(
                    playGroggyVisual);
            }

            DestroySpawnedProjectiles();
            RestoreBossAnimatorSpeeds();
            if (hadRuntime)
            {
                RestoreEnemyTimeScale();
            }

            StopChargeVfx(endCameraFocus);
            if (endCameraFocus)
            {
                cameraFollow?.EndCinematicFocus();
            }

            DestroyRuntimeAnchors();
            patternStoppedForFinalShot = false;
            player = null;
            hacker = null;
            playerBody = null;
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
            particleGatherVfx?.Clear();
        }

        private void ResolveReferences()
        {
            stage ??= GetComponent<BossExecutionStage>();
            aimChargeGatherVfx ??=
                GetComponent<ExecutionChargeGatherVfx>();
            particleGatherVfx ??=
                GetComponent<HackerExecutionParticleGatherVfx>();
            if (Application.isPlaying)
            {
                aimChargeGatherVfx ??=
                    gameObject.AddComponent<ExecutionChargeGatherVfx>();
                particleGatherVfx ??=
                    gameObject.AddComponent<HackerExecutionParticleGatherVfx>();
            }

            if (patternSourceBoss == null)
            {
                patternSourceBoss =
                    Object.FindFirstObjectByType<HackerBossAI>(
                        FindObjectsInactive.Include);
            }
        }

        private bool PlaceActors()
        {
            if (player == null || hacker == null)
            {
                return false;
            }

            if (stage != null)
            {
                return stage.PlaceActors(player, hacker);
            }

            SetActorPosition(
                player.transform,
                playerBody,
                fallbackPlayerPosition);
            SetActorPosition(
                hacker.transform,
                hacker.Body,
                fallbackBossPosition);
            EnsureRuntimeAnchors();
            Physics2D.SyncTransforms();
            return true;
        }

        private void EnsureRuntimeAnchors()
        {
            if (runtimeAnchorRoot != null)
            {
                return;
            }

            runtimeAnchorRoot =
                new GameObject("HackerExecutionRuntimeAnchors")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            runtimeWideCameraFocus =
                CreateRuntimeAnchor("WideCameraFocus");
            runtimeProjectileFocusProxy =
                CreateRuntimeAnchor("ProjectileFocusProxy");
            runtimeRollEnd =
                CreateRuntimeAnchor("PlayerRollEnd");
            runtimeWideCameraFocus.position =
                (fallbackPlayerPosition + fallbackBossPosition) * 0.5f;
            runtimeProjectileFocusProxy.position =
                fallbackPlayerPosition;
            runtimeRollEnd.position =
                fallbackPlayerRollEndPosition;
        }

        private Transform CreateRuntimeAnchor(string anchorName)
        {
            GameObject anchorObject = new(anchorName)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            anchorObject.transform.SetParent(
                runtimeAnchorRoot.transform,
                false);
            return anchorObject.transform;
        }

        private void DestroyRuntimeAnchors()
        {
            if (runtimeAnchorRoot != null)
            {
                Destroy(runtimeAnchorRoot);
            }

            runtimeAnchorRoot = null;
            runtimeWideCameraFocus = null;
            runtimeProjectileFocusProxy = null;
            runtimeRollEnd = null;
        }

        private Vector2 GetPlayerPosition()
        {
            return playerBody != null
                ? playerBody.position
                : player != null
                    ? player.transform.position
                    : Vector2.zero;
        }

        private static bool IsUsableProjectile(
            EnemyProjectile projectile)
        {
            return projectile != null
                && projectile.gameObject.activeInHierarchy;
        }

        private static void RestoreEnemyTimeScale()
        {
            EnemyTimeScale.SetTemporary(1f, 0f);
        }

        private static void SetActorPosition(
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
