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
    [AddComponentMenu("Week14/Combat/Muscle Execution Sequence")]
    public sealed class MuscleExecutionSequence : BossExecutionSequence
    {
        [Header("References")]
        [SerializeField] private BossExecutionStage stage;
        [SerializeField] private ParticleSystem aimChargeVfx;
        [SerializeField] private ExecutionChargeGatherVfx aimChargeGatherVfx;

        [Header("Boss Graph Pattern")]
        [SerializeField] private MuscleBossAI patternSourceBoss;
        [SerializeField] private string executionPatternId = "Execution";
        [SerializeField] private bool ignorePatternProjectileWalls = true;

        [Header("Initial Placement")]
        [SerializeField] private Vector2 initialBossDirection = Vector2.one;
        [SerializeField, Min(0.1f)] private float initialBossDistance = 3f;

        [Header("Player Dodges")]
        [SerializeField] private Vector2 firstDodgeOffset = new(2f, 0f);
        [SerializeField] private Vector2 secondDodgeOffset = new(0f, 2.2f);
        [SerializeField, Min(0.05f)] private float firstDodgeSeconds = 0.28f;
        [SerializeField, Min(0.05f)] private float secondDodgeSeconds = 0.28f;
        [FormerlySerializedAs("thirdDodgeStartDelaySeconds")]
        [SerializeField, Min(0f)] private float thirdWalkStartDelaySeconds = 0.1f;
        [FormerlySerializedAs("thirdDodgeSeconds")]
        [Tooltip("대각선 걷기 시간입니다. 이동 거리는 바뀌지 않고 속도만 달라집니다.")]
        [SerializeField, Min(0.05f)] private float thirdDiagonalWalkSeconds = 0.4f;
        [FormerlySerializedAs("thirdDodgeLeftDistance")]
        [Tooltip("대각선 걷기의 왼쪽 이동 거리입니다.")]
        [SerializeField, Min(0.1f)] private float thirdDiagonalWalkLeftDistance = 2.4f;
        [FormerlySerializedAs("thirdDodgeBelowBoss")]
        [SerializeField, Min(0f)] private float thirdDiagonalWalkBelowBoss;
        [SerializeField, Min(0.1f)] private float thirdWalkLeftDistance = 2.8f;
        [SerializeField, Min(0.05f)] private float thirdWalkSeconds = 0.9f;
        [SerializeField, Min(0.1f)] private float dashStateTimeoutSeconds = 3f;

        [Header("Final Approach")]
        [Tooltip("세 번째 Dash가 실제로 시작된 뒤 슬로우와 Execution Image가 시작될 때까지의 시간입니다.")]
        [SerializeField, Min(0f)] private float finalDashSlowStartDelaySeconds = 0.15f;
        [SerializeField, Min(0.1f)] private float bossClearanceDistance = 1.25f;
        [SerializeField, Min(0.1f)] private float projectileClearanceDistance = 1.1f;
        [SerializeField, Range(0f, 1f)] private float projectileSlowMultiplier = 0.02f;
        [SerializeField, Range(0f, 1f)] private float bossAnimationSlowMultiplier = 0.5f;
        [SerializeField, Min(0.1f)] private float projectileSlowSafetySeconds = 8f;
        [SerializeField, Min(0f)] private float preParryHoldSeconds = 0.55f;
        [SerializeField, Range(0.1f, 1f)] private float preParryCameraZoomMultiplier = 0.38f;
        [SerializeField, Min(0.01f)] private float preParryCameraBlendSmoothTime = 0.12f;
        [SerializeField, Min(0.1f)] private float executionImageSeconds = 3.5f;
        [SerializeField, BossGraphSfxId] private string preParrySfxId;

        [Header("Parry")]
        [SerializeField, Min(1)] private int parryProjectileCount = 6;
        [SerializeField, Min(0f)] private float parryIntervalSeconds = 0.075f;
        [SerializeField, Range(0f, 1f)] private float parryCameraFocusWeight = 1f;
        [SerializeField, Range(0.1f, 1f)] private float parryCameraZoomMultiplier = 0.42f;
        [SerializeField, Min(0.01f)] private float parryCameraBlendSmoothTime = 0.08f;

        [Header("Aim Charge")]
        [SerializeField, Min(0f)] private float aimChargeSeconds = 1.5f;
        [SerializeField] private string finalDashAnimationBoolName = "isCharge";

        [Header("Wide Camera")]
        [SerializeField, Range(0f, 1f)] private float wideCameraFocusWeight = 1f;
        [SerializeField, Range(0.1f, 1.5f)] private float wideCameraZoomMultiplier = 0.72f;
        [SerializeField, Min(0.01f)] private float wideCameraBlendSmoothTime = 0.2f;

        private readonly List<EnemyProjectile> spawnedProjectiles = new();
        private readonly Queue<EnemyProjectile> parryProjectiles = new();
        private readonly Dictionary<Animator, float> bossAnimatorSpeeds = new();
        private PlayerCombatController player;
        private MuscleBossAI muscle;
        private MuscleBossAI heldFinalDashAnimationBoss;
        private Rigidbody2D playerBody;
        private CameraFollow2D cameraFollow;
        private Coroutine playerMoveRoutine;
        private bool runtimePrepared;
        private bool playerWalking;
        private bool finalBossClearanceEnabled;
        private bool finalBossClearanceReached;
        private bool finalApproachSlowActive;
        private bool finalShotImpacted;
        private Vector2 finalDashDirection;

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
                : muscle != null ? muscle.transform : transform;

        public override float WideCameraFocusWeight => wideCameraFocusWeight;
        public override float WideCameraZoomMultiplier => wideCameraZoomMultiplier;
        public override float WideCameraBlendSmoothTime => wideCameraBlendSmoothTime;

        public override float ExpectedDurationSeconds =>
            dashStateTimeoutSeconds * 3f
            + finalDashSlowStartDelaySeconds
            + preParryHoldSeconds
            + thirdWalkSeconds
            + parryIntervalSeconds * Mathf.Max(1, parryProjectileCount)
            + aimChargeSeconds;

        private void Awake()
        {
            ResolveReferences();
        }

        private void LateUpdate()
        {
            if (!runtimePrepared || player == null || muscle == null)
            {
                return;
            }

            UpdateWideCameraFocus();
            AimPlayerAtBoss();
            MaintainFinalApproachSlow();
            ApplyFinalBossClearance();
        }

        private void OnDisable()
        {
            CleanupRuntime(true);
            RestoreBossAnimatorSpeeds();
        }

        public override bool SupportsBoss(BossAI boss)
        {
            return boss is MuscleBossAI;
        }

        internal override bool Prepare(
            PlayerCombatController nextPlayer,
            BossAI nextBoss)
        {
            if (heldFinalDashAnimationBoss != null)
            {
                SetFinalDashAnimationHeld(
                    heldFinalDashAnimationBoss,
                    false);
            }

            RestoreBossAnimatorSpeeds();
            finalShotImpacted = false;
            ResolveReferences();
            CleanupRuntime(false);
            if (nextPlayer == null || nextBoss is not MuscleBossAI nextMuscle)
            {
                return false;
            }

            player = nextPlayer;
            muscle = nextMuscle;
            patternSourceBoss ??= nextMuscle;
            if (!CanPlay || !muscle.HasConfiguredGraphPattern(executionPatternId))
            {
                player = null;
                muscle = null;
                return false;
            }

            cameraFollow = nextPlayer.CameraFollow;
            playerBody = nextPlayer.GetComponent<Rigidbody2D>();
            runtimePrepared = stage.PlaceActors(player, muscle);
            if (!runtimePrepared)
            {
                CleanupRuntime(false);
                return false;
            }

            PlaceBossAtExecutionStart();
            UpdateWideCameraFocus();
            AimPlayerAtBoss();
            muscle.FaceTowards(GetActorPosition(player.transform, playerBody));
            return true;
        }

        internal override IEnumerator PlayPrelude()
        {
            if (!runtimePrepared)
            {
                yield break;
            }

            bool preludeCompleted = false;
            try
            {
                if (!RunSelectedPattern())
                {
                    yield break;
                }

                yield return WaitForDashState(true);
                if (!IsCurrentDashState(true, "첫 번째 Boss Dash"))
                {
                    yield break;
                }

                StartPlayerMove(
                    GetActorPosition(player.transform, playerBody) + firstDodgeOffset,
                    firstDodgeSeconds);
                yield return WaitForDashState(false);
                yield return WaitForPlayerMove();

                yield return WaitForDashState(true);
                if (!IsCurrentDashState(true, "두 번째 Boss Dash"))
                {
                    yield break;
                }

                StartPlayerMove(
                    GetActorPosition(player.transform, playerBody) + secondDodgeOffset,
                    secondDodgeSeconds);
                yield return WaitForDashState(false);
                yield return WaitForPlayerMove();

                if (thirdWalkStartDelaySeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(thirdWalkStartDelaySeconds);
                }

                if (!runtimePrepared || player == null || muscle == null)
                {
                    yield break;
                }

                Vector2 currentPlayerPosition =
                    GetActorPosition(player.transform, playerBody);
                Vector2 thirdDiagonalWalkTarget = new(
                    currentPlayerPosition.x - thirdDiagonalWalkLeftDistance,
                    GetActorPosition(muscle.transform, muscle.Body).y
                        - thirdDiagonalWalkBelowBoss);
                Vector2 thirdWalkTarget =
                    thirdDiagonalWalkTarget
                    + Vector2.left * thirdWalkLeftDistance;
                StartPlayerWalkPath(
                    thirdDiagonalWalkTarget,
                    thirdDiagonalWalkSeconds,
                    thirdWalkTarget,
                    thirdWalkSeconds);
                yield return WaitForPlayerMove();

                yield return WaitForDashState(true);
                if (!IsCurrentDashState(true, "세 번째 Boss Dash"))
                {
                    yield break;
                }

                finalDashDirection =
                    (GetActorPosition(player.transform, playerBody)
                        - GetActorPosition(muscle.transform, muscle.Body))
                    .normalized;
                if (finalDashDirection.sqrMagnitude <= 0.0001f)
                {
                    finalDashDirection = Vector2.right;
                }

                finalBossClearanceEnabled = true;
                yield return WaitForFinalSlowStart();
                if (!runtimePrepared)
                {
                    yield break;
                }

                BeginParryPresentation();
                if (preParryHoldSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(preParryHoldSeconds);
                }

                yield return ParryApproachingProjectiles();
                SetFinalDashAnimationHeld(muscle, true);

                BeginAimCharge();
                if (aimChargeSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(aimChargeSeconds);
                }

                preludeCompleted = true;
            }
            finally
            {
                if (!preludeCompleted)
                {
                    CleanupRuntime(false);
                }
            }
        }

        internal override void Cancel()
        {
            CleanupRuntime(true);
            if (!finalShotImpacted)
            {
                RestoreBossAnimatorSpeeds();
            }
        }

        internal override void OnFinalShotImpact(BossAI boss)
        {
            finalShotImpacted = true;
            MuscleBossAI target = boss as MuscleBossAI
                ?? heldFinalDashAnimationBoss
                ?? muscle;
            CleanupRuntime(false);
            SetFinalDashAnimationHeld(target, false);
            target?.ReplayStunVisualForExecution();
        }

        internal override void OnFinalDeathSequenceComplete(BossAI boss)
        {
            RestoreBossAnimatorSpeeds();
            finalShotImpacted = false;
        }

        private bool RunSelectedPattern()
        {
            if (!runtimePrepared || muscle == null)
            {
                return false;
            }

            bool started = muscle.TryRunCinematicPatternOnce(
                executionPatternId,
                ConfigureSpawnedPatternProjectile);
            if (!started)
            {
                Debug.LogWarning(
                    $"{nameof(MuscleExecutionSequence)}: 처형용 Boss Graph 패턴 " +
                    $"'{executionPatternId}' 실행에 실패했습니다.",
                    this);
            }

            return started;
        }

        private IEnumerator WaitForDashState(bool expected)
        {
            float remaining = Mathf.Max(0.1f, dashStateTimeoutSeconds);
            while (runtimePrepared
                && muscle != null
                && muscle.IsDashing != expected
                && remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private bool IsCurrentDashState(bool expected, string phaseName)
        {
            bool matched = runtimePrepared
                && muscle != null
                && muscle.IsDashing == expected;
            if (!matched)
            {
                Debug.LogWarning(
                    $"{nameof(MuscleExecutionSequence)}: {phaseName} 상태 감지 시간이 초과됐습니다.",
                    this);
            }

            return matched;
        }

        private void StartPlayerMove(
            Vector2 target,
            float seconds,
            bool playRoll = true)
        {
            if (playerMoveRoutine != null)
            {
                StopCoroutine(playerMoveRoutine);
            }

            playerMoveRoutine =
                StartCoroutine(MovePlayerTo(target, seconds, playRoll));
        }

        private void StartPlayerWalkPath(
            Vector2 firstTarget,
            float firstSeconds,
            Vector2 secondTarget,
            float secondSeconds)
        {
            if (playerMoveRoutine != null)
            {
                StopCoroutine(playerMoveRoutine);
            }

            playerMoveRoutine = StartCoroutine(
                WalkPlayerPath(
                    firstTarget,
                    firstSeconds,
                    secondTarget,
                    secondSeconds));
        }

        private IEnumerator WaitForPlayerMove()
        {
            while (runtimePrepared && playerMoveRoutine != null)
            {
                yield return null;
            }
        }

        private IEnumerator MovePlayerTo(
            Vector2 target,
            float seconds,
            bool playRoll)
        {
            if (player == null)
            {
                playerMoveRoutine = null;
                yield break;
            }

            Rigidbody2D body = playerBody;
            Vector2 start = body != null
                ? body.position
                : (Vector2)player.transform.position;
            float duration = Mathf.Max(0.05f, seconds);
            if (playRoll)
            {
                player.Visual?.PlayCinematicRoll(
                    duration,
                    target - start);
            }
            else
            {
                playerWalking = true;
                player.Visual?.BeginCinematicMovement(Vector2.left);
            }

            yield return MovePlayerSegment(target, duration, body);

            if (!playRoll)
            {
                EndPlayerWalking();
            }

            playerMoveRoutine = null;
        }

        private IEnumerator WalkPlayerPath(
            Vector2 firstTarget,
            float firstSeconds,
            Vector2 secondTarget,
            float secondSeconds)
        {
            if (player == null)
            {
                playerMoveRoutine = null;
                yield break;
            }

            playerWalking = true;
            player.Visual?.BeginCinematicMovement(Vector2.left);
            yield return MovePlayerSegment(
                firstTarget,
                Mathf.Max(0.05f, firstSeconds),
                playerBody);
            yield return MovePlayerSegment(
                secondTarget,
                Mathf.Max(0.05f, secondSeconds),
                playerBody);
            EndPlayerWalking();
            playerMoveRoutine = null;
        }

        private IEnumerator MovePlayerSegment(
            Vector2 target,
            float duration,
            Rigidbody2D body)
        {
            Vector2 start = GetActorPosition(player.transform, body);
            for (float elapsed = 0f;
                 runtimePrepared && player != null && elapsed < duration;
                 elapsed += Time.unscaledDeltaTime)
            {
                float progress = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - (1f - progress) * (1f - progress);
                SetActorPosition(
                    player.transform,
                    body,
                    Vector2.LerpUnclamped(start, target, eased));
                AimPlayerAtBoss();
                yield return null;
            }

            if (runtimePrepared && player != null)
            {
                SetActorPosition(player.transform, body, target);
                AimPlayerAtBoss();
            }
        }

        private void EndPlayerWalking()
        {
            playerWalking = false;
            if (player == null)
            {
                return;
            }

            Vector2 facingDirection = muscle != null
                ? GetActorPosition(muscle.transform, muscle.Body)
                    - GetActorPosition(player.transform, playerBody)
                : Vector2.right;
            player.Visual?.EndCinematicMovement(facingDirection);
        }

        private IEnumerator WaitForFinalSlowStart()
        {
            float remaining = Mathf.Max(
                0f,
                finalDashSlowStartDelaySeconds);
            while (runtimePrepared && remaining > 0f)
            {
                remaining -= Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void BeginParryPresentation()
        {
            BuildParryQueue();
            finalApproachSlowActive = true;
            EnemyTimeScale.SetTemporary(
                projectileSlowMultiplier,
                projectileSlowSafetySeconds);
            ApplyBossAnimatorSlow();

            cameraFollow?.BeginCinematicFocus(
                player != null ? player.transform : null,
                1f,
                preParryCameraZoomMultiplier,
                preParryCameraBlendSmoothTime);
            player?.PlayExecutionImageForCinematic(executionImageSeconds);
            if (!string.IsNullOrWhiteSpace(preParrySfxId))
            {
                SoundManager.PlaySfx(preParrySfxId);
            }
        }

        private void MaintainFinalApproachSlow()
        {
            if (!finalApproachSlowActive
                || Mathf.Approximately(
                    EnemyTimeScale.Current,
                    projectileSlowMultiplier))
            {
                return;
            }

            EnemyTimeScale.SetTemporary(
                projectileSlowMultiplier,
                projectileSlowSafetySeconds);
        }

        private IEnumerator ParryApproachingProjectiles()
        {
            while (runtimePrepared)
            {
                EnemyProjectile projectile = DequeueNextProjectile();
                if (projectile == null)
                {
                    yield break;
                }

                Transform focusProxy = stage != null ? stage.ProjectileFocusProxy : null;
                if (focusProxy != null)
                {
                    focusProxy.position = projectile.transform.position;
                    cameraFollow?.BeginCinematicFocus(
                        focusProxy,
                        parryCameraFocusWeight,
                        parryCameraZoomMultiplier,
                        parryCameraBlendSmoothTime);
                }

                player?.TryParryProjectileForCinematic(projectile);
                if (parryIntervalSeconds > 0f)
                {
                    yield return new WaitForSecondsRealtime(parryIntervalSeconds);
                }
                else
                {
                    yield return null;
                }
            }
        }

        private void BeginAimCharge()
        {
            if (player == null || muscle == null)
            {
                return;
            }

            UpdateWideCameraFocus();
            if (stage != null && stage.WideCameraFocus != null)
            {
                cameraFollow?.BeginCinematicFocus(
                    stage.WideCameraFocus,
                    wideCameraFocusWeight,
                    wideCameraZoomMultiplier,
                    wideCameraBlendSmoothTime);
            }

            Vector2 aimDirection =
                GetActorPosition(muscle.transform, muscle.Body)
                - GetActorPosition(player.transform, playerBody);
            player.Visual?.UpdateExecutionAimVisual(aimDirection);
            player.Visual?.SetBodyAimDirection(aimDirection);
            if (aimChargeVfx != null)
            {
                aimChargeVfx.Play(true);
            }

            aimChargeGatherVfx?.Play(player.RightFireOrigin);
            NotifyFinalChargeStarted(aimChargeSeconds);
        }

        private void ConfigureSpawnedPatternProjectile(EnemyProjectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            projectile.ConfigureExecutionPauseIgnored(true);
            projectile.ConfigurePlayerCollisionIgnored(true);
            projectile.ConfigureCinematicPlayerClearance(
                player != null ? player.transform : null,
                projectileClearanceDistance);
            projectile.ConfigureIgnoresWalls(ignorePatternProjectileWalls);
            projectile.ConfigureInterceptable(true);
            spawnedProjectiles.Add(projectile);
        }

        private void BuildParryQueue()
        {
            parryProjectiles.Clear();
            if (player == null)
            {
                return;
            }

            Vector2 playerPosition =
                GetActorPosition(player.transform, playerBody);
            List<EnemyProjectile> candidates = new();
            for (int i = 0; i < spawnedProjectiles.Count; i++)
            {
                EnemyProjectile projectile = spawnedProjectiles[i];
                if (IsUsableProjectile(projectile))
                {
                    candidates.Add(projectile);
                }
            }

            candidates.Sort((left, right) =>
            {
                float leftDistance =
                    ((Vector2)left.transform.position - playerPosition).sqrMagnitude;
                float rightDistance =
                    ((Vector2)right.transform.position - playerPosition).sqrMagnitude;
                return leftDistance.CompareTo(rightDistance);
            });
            int queueCount = Mathf.Min(
                candidates.Count,
                Mathf.Max(1, parryProjectileCount));
            for (int i = 0; i < queueCount; i++)
            {
                parryProjectiles.Enqueue(candidates[i]);
            }
        }

        private EnemyProjectile DequeueNextProjectile()
        {
            while (parryProjectiles.Count > 0)
            {
                EnemyProjectile projectile = parryProjectiles.Dequeue();
                if (IsUsableProjectile(projectile))
                {
                    return projectile;
                }
            }

            return null;
        }

        private void ApplyFinalBossClearance()
        {
            if (!finalBossClearanceEnabled
                || player == null
                || muscle == null
                || finalDashDirection.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 playerPosition =
                GetActorPosition(player.transform, playerBody);
            Vector2 safeBossPosition =
                playerPosition - finalDashDirection * bossClearanceDistance;
            Vector2 bossPosition = muscle.Body != null
                ? muscle.Body.position
                : GetActorPosition(muscle.transform, null);
            bool crossedSafePosition =
                Vector2.Dot(bossPosition - safeBossPosition, finalDashDirection) >= 0f;
            if (!finalBossClearanceReached && !crossedSafePosition)
            {
                return;
            }

            finalBossClearanceReached = true;
            SetActorPosition(
                muscle.transform,
                muscle.Body,
                safeBossPosition);
        }

        private void PlaceBossAtExecutionStart()
        {
            if (player == null || muscle == null)
            {
                return;
            }

            Vector2 direction = initialBossDirection.sqrMagnitude > 0.0001f
                ? initialBossDirection.normalized
                : new Vector2(1f, 1f).normalized;
            Vector2 bossPosition =
                GetActorPosition(player.transform, playerBody)
                + direction * initialBossDistance;
            SetActorPosition(muscle.transform, muscle.Body, bossPosition);
        }

        private void AimPlayerAtBoss()
        {
            if (player == null || muscle == null)
            {
                return;
            }

            Vector2 aimDirection =
                GetActorPosition(muscle.transform, muscle.Body)
                - GetActorPosition(player.transform, playerBody);
            player.Visual?.UpdateExecutionAimVisual(aimDirection);
            player.Visual?.SetBodyAimDirection(aimDirection);
        }

        private void UpdateWideCameraFocus()
        {
            if (stage == null
                || stage.WideCameraFocus == null
                || player == null
                || muscle == null)
            {
                return;
            }

            stage.WideCameraFocus.position = Vector2.Lerp(
                GetActorPosition(player.transform, playerBody),
                GetActorPosition(muscle.transform, muscle.Body),
                0.5f);
        }

        private static bool IsUsableProjectile(EnemyProjectile projectile)
        {
            return projectile != null
                && projectile.gameObject.activeInHierarchy
                && projectile.CanBeIntercepted;
        }

        private static void SetActorPosition(
            Transform actor,
            Rigidbody2D body,
            Vector2 position)
        {
            if (body != null)
            {
                body.linearVelocity = Vector2.zero;
                body.position = position;
            }

            if (actor == null)
            {
                return;
            }

            Vector3 worldPosition = actor.position;
            worldPosition.x = position.x;
            worldPosition.y = position.y;
            actor.position = worldPosition;
        }

        private static Vector2 GetActorPosition(
            Transform actor,
            Rigidbody2D body)
        {
            return body != null
                ? body.position
                : actor != null ? (Vector2)actor.position : Vector2.zero;
        }

        private void SetFinalDashAnimationHeld(
            MuscleBossAI target,
            bool held)
        {
            if (target == null
                || string.IsNullOrWhiteSpace(finalDashAnimationBoolName))
            {
                if (!held)
                {
                    heldFinalDashAnimationBoss = null;
                }

                return;
            }

            int parameterHash =
                Animator.StringToHash(finalDashAnimationBoolName);
            Animator[] animators = target.BodyRoot != null
                ? target.BodyRoot.GetComponentsInChildren<Animator>(true)
                : target.GetComponentsInChildren<Animator>(true);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator != null
                    && HasAnimatorBool(animator, parameterHash))
                {
                    animator.SetBool(parameterHash, held);
                }
            }

            heldFinalDashAnimationBoss = held ? target : null;
        }

        private static bool HasAnimatorBool(
            Animator animator,
            int parameterHash)
        {
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == parameterHash
                    && parameters[i].type
                        == AnimatorControllerParameterType.Bool)
                {
                    return true;
                }
            }

            return false;
        }

        private void ApplyBossAnimatorSlow()
        {
            RestoreBossAnimatorSpeeds();
            if (muscle == null)
            {
                return;
            }

            Animator[] animators = muscle.BodyRoot != null
                ? muscle.BodyRoot.GetComponentsInChildren<Animator>(true)
                : muscle.GetComponentsInChildren<Animator>(true);
            float multiplier = Mathf.Clamp01(bossAnimationSlowMultiplier);
            for (int i = 0; i < animators.Length; i++)
            {
                Animator animator = animators[i];
                if (animator == null || bossAnimatorSpeeds.ContainsKey(animator))
                {
                    continue;
                }

                float originalSpeed = animator.speed;
                bossAnimatorSpeeds.Add(animator, originalSpeed);
                animator.speed = originalSpeed * multiplier;
            }
        }

        private void RestoreBossAnimatorSpeeds()
        {
            foreach (KeyValuePair<Animator, float> entry in bossAnimatorSpeeds)
            {
                if (entry.Key != null)
                {
                    entry.Key.speed = entry.Value;
                }
            }

            bossAnimatorSpeeds.Clear();
        }

        private void CleanupRuntime(bool endCameraFocus)
        {
            if (endCameraFocus && heldFinalDashAnimationBoss != null)
            {
                SetFinalDashAnimationHeld(
                    heldFinalDashAnimationBoss,
                    false);
            }

            bool hadActiveRuntime = runtimePrepared
                || player != null
                || playerMoveRoutine != null
                || spawnedProjectiles.Count > 0;
            runtimePrepared = false;
            finalBossClearanceEnabled = false;
            finalBossClearanceReached = false;
            finalApproachSlowActive = false;
            finalDashDirection = Vector2.zero;

            if (playerMoveRoutine != null)
            {
                StopCoroutine(playerMoveRoutine);
                playerMoveRoutine = null;
            }

            if (playerWalking && player != null)
            {
                EndPlayerWalking();
            }

            playerWalking = false;
            muscle?.StopCinematicPattern();
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
            parryProjectiles.Clear();
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
            muscle = null;
            playerBody = null;
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

            if (patternSourceBoss == null)
            {
                MuscleBossAI[] bosses =
                    UnityEngine.Object.FindObjectsByType<MuscleBossAI>(
                        FindObjectsInactive.Include,
                        FindObjectsSortMode.None);
                for (int i = 0; i < bosses.Length; i++)
                {
                    if (bosses[i] != null
                        && bosses[i].gameObject.scene == gameObject.scene)
                    {
                        patternSourceBoss = bosses[i];
                        break;
                    }
                }
            }
        }
    }
}
