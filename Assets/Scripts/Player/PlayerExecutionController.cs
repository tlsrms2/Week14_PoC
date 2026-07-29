using System.Collections;
using UnityEngine;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Enemy;

namespace Week14.Combat
{
    internal sealed class PlayerExecutionController
    {
        private const string FinalExecutionHeadName = "Head";
        private const string WallLayerName = "Wall";
        private const float TeleportColliderInset = 0.02f;
        private const float TeleportFallbackMinStep = 0.25f;
        private const float TeleportFallbackColliderHeightRatio = 0.5f;
        private const int TeleportFallbackMaxSteps = 8;

        private readonly PlayerCombatController.PlayerCombatContext context;
        private readonly PlayerCombatRig rig;
        private readonly PlayerAimController aimController;
        private readonly PlayerLockOnController lockOnController;
        private readonly PlayerExecutionPresentation presentation;
        private readonly CommonExecutionPresentationPlayer commonPresentation;
        private Coroutine executionRoutine;
        private ExecutionTarget hoveredExecutionTarget;
        private BossExecutionSequence activeBossExecutionSequence;
        private bool isExecuting;
        private bool isWaitingForVictoryPanel;

        internal PlayerExecutionController(
            PlayerCombatController.PlayerCombatContext context,
            PlayerCombatRig rig,
            PlayerAimController aimController,
            PlayerLockOnController lockOnController,
            PlayerExecutionPresentation presentation)
        {
            this.context = context;
            this.rig = rig;
            this.aimController = aimController;
            this.lockOnController = lockOnController;
            this.presentation = presentation;
            commonPresentation = new CommonExecutionPresentationPlayer(context, presentation);
        }

        internal ExecutionTarget HoveredExecutionTarget => hoveredExecutionTarget;
        internal bool IsExecuting => isExecuting;
        internal bool IsWaitingForVictoryPanel => isWaitingForVictoryPanel;

        internal bool TryBeginExecution()
        {
            ExecutionTarget executionTarget = FindHoveredExecutionTarget();
            if (executionTarget == null)
            {
                return false;
            }

            if (executionRoutine != null)
            {
                context.CoroutineHost.StopCoroutine(executionRoutine);
            }

            executionRoutine = context.CoroutineHost.StartCoroutine(ExecuteTarget(executionTarget));
            return true;
        }

        internal void UpdateHoveredExecutionTarget()
        {
            SetHoveredExecutionTarget(FindHoveredExecutionTarget());
        }

        internal void SetHoveredExecutionTarget(ExecutionTarget nextTarget)
        {
            hoveredExecutionTarget = nextTarget;
        }

        internal void FinishExecution(
            bool preserveCinematicFocus = false,
            bool preserveFinalLetterbox = false,
            bool keepPlayerHpHidden = false,
            bool preserveExecutionVisual = false)
        {
            activeBossExecutionSequence?.Cancel();
            activeBossExecutionSequence = null;
            commonPresentation.Stop();
            isExecuting = false;
            executionRoutine = null;
            if (!preserveExecutionVisual)
            {
                context.Visual?.EndExecutionVisual();
            }
            presentation.RestoreFinalExecutionPresentation(!preserveFinalLetterbox);
            if (!preserveCinematicFocus)
            {
                context.CameraFollow?.EndCinematicFocus();
            }

            lockOnController.ClearInvalidLockOnTarget();
            UpdateHoveredExecutionTarget();
            if (keepPlayerHpHidden)
            {
                presentation.KeepPlayerHpHiddenAfterFinalExecution();
            }
            else
            {
                presentation.RestorePlayerHpAfterExecution();
            }

            presentation.StartPendingExecutionBulletTimers();
            context.ExecutionImage?.Stop();
        }

        internal void RestorePlayerHpAfterExecution()
        {
            presentation.RestorePlayerHpAfterExecution();
        }

        internal void StopExecutionShotDim()
        {
            presentation.StopExecutionShotDim();
            presentation.RestoreFinalExecutionPresentation(true);
        }

        private ExecutionTarget FindHoveredExecutionTarget()
        {
            Vector2 executionCenter = rig.CombatCenterOrigin.position;
            ExecutionTarget bestTarget = null;
            float bestDistance = float.PositiveInfinity;

            ExecutionTarget[] executionTargets = UnityEngine.Object.FindObjectsByType<ExecutionTarget>(FindObjectsSortMode.None);
            for (int i = 0; i < executionTargets.Length; i++)
            {
                ChooseCloserExecutionTarget(executionTargets[i], executionCenter, ref bestTarget, ref bestDistance);
            }

            return bestTarget;
        }

        private void ChooseCloserExecutionTarget(
            ExecutionTarget target,
            Vector2 executionCenter,
            ref ExecutionTarget bestTarget,
            ref float bestDistance)
        {
            if (target == null || !target.CanExecute(context.PlayerTransform))
            {
                return;
            }

            float distance = Vector2.Distance(executionCenter, target.transform.position);
            if (distance >= bestDistance)
            {
                return;
            }

            bestTarget = target;
            bestDistance = distance;
        }

        private static BossExecutionSequence ResolveBossExecutionSequence(BossAI boss)
        {
            if (boss == null)
            {
                return null;
            }

            BossExecutionSequence[] attachedSequences =
                boss.GetComponents<BossExecutionSequence>();
            for (int i = 0; i < attachedSequences.Length; i++)
            {
                if (attachedSequences[i] != null
                    && attachedSequences[i].SupportsBoss(boss))
                {
                    return attachedSequences[i];
                }
            }

            BossExecutionSequence[] sceneSequences =
                UnityEngine.Object.FindObjectsByType<BossExecutionSequence>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < sceneSequences.Length; i++)
            {
                BossExecutionSequence sequence = sceneSequences[i];
                if (sequence != null
                    && sequence.gameObject.scene == boss.gameObject.scene
                    && sequence.SupportsBoss(boss))
                {
                    return sequence;
                }
            }

            if (boss is not MuscleBossAI)
            {
                return null;
            }

            BossExecutionStage[] stages =
                UnityEngine.Object.FindObjectsByType<BossExecutionStage>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
            for (int i = 0; i < stages.Length; i++)
            {
                BossExecutionStage stage = stages[i];
                if (stage != null && stage.gameObject.scene == boss.gameObject.scene)
                {
                    return stage.gameObject.AddComponent<MuscleExecutionSequence>();
                }
            }

            return null;
        }

        private IEnumerator ExecuteTarget(ExecutionTarget executionTarget)
        {
            PlayerCombatConfig config = context.Config;
            isExecuting = true;
            context.Owner.BeginExecutionBodyColor();
            if (config == null || executionTarget == null || !executionTarget.BeginExecution(context.Owner))
            {
                FinishExecution();
                yield break;
            }

            rig.StopBody();
            presentation.HidePlayerHpForExecution();
            context.HideBaseballBatDisplay();
            BossAI executionBoss = executionTarget.GetComponentInParent<BossAI>();
            bool isFinalBossExecution = executionBoss != null && executionBoss.CurrentLives <= 1;
            commonPresentation.Begin(
                !isFinalBossExecution ? config.CommonExecutionPresentationProfile : null,
                executionBoss);
            if (isFinalBossExecution)
            {
                executionBoss.FreezeCombatTimer();
            }

            BossExecutionSequence bossExecutionSequence =
                isFinalBossExecution
                    ? ResolveBossExecutionSequence(executionBoss)
                    : null;
            bool useBossExecutionSequence = bossExecutionSequence != null
                && bossExecutionSequence.CanPlay
                && bossExecutionSequence.Prepare(context.Owner, executionBoss);
            activeBossExecutionSequence =
                useBossExecutionSequence ? bossExecutionSequence : null;
            float finalBlackoutTimeMultiplier = useBossExecutionSequence
                ? Mathf.Max(
                    0f,
                    bossExecutionSequence.FinalBlackoutTimeMultiplier)
                : 1f;
            float flourishSeconds = useBossExecutionSequence
                ? bossExecutionSequence.ExpectedDurationSeconds
                : Mathf.Max(0f, config.ExecutionFlourishDelaySeconds)
                    + Mathf.Max(0, config.ExecutionFlourishShotCount)
                    * Mathf.Max(0.01f, config.ExecutionFlourishShotInterval);
            float holsteringSeconds = Mathf.Max(0.01f, config.ExecutionFlourishShotInterval);
            float finalPresentationSeconds = isFinalBossExecution
                ? config.FinalExecutionLetterboxEnterSeconds
                    + (config.FinalExecutionBlackoutFadeInSeconds
                        + config.FinalExecutionOutlineFlashSeconds
                        + config.FinalExecutionBlackoutHoldSeconds
                        + config.FinalExecutionBlackoutFadeOutSeconds)
                    * finalBlackoutTimeMultiplier
                : 0f;
            Health targetHealth = executionTarget.GetComponent<Health>();
            if (targetHealth != null)
            {
                lockOnController.SetLockOnTarget(targetHealth);
            }

            Coroutine letterboxRoutine = null;
            bool teleported = false;
            if (executionBoss != null)
            {
                letterboxRoutine = context.CoroutineHost.StartCoroutine(
                    presentation.ShowFinalExecutionLetterbox());
                teleported = useBossExecutionSequence
                    || TeleportBesideBoss(executionBoss, config.ExecutionTeleportDistance);
            }

            Vector2 targetPosition = executionTarget.transform.position;
            Vector2 playerPosition = context.Body != null
                ? context.Body.position
                : (Vector2)context.PlayerTransform.position;
            executionBoss?.FaceTowards(playerPosition);
            Vector2 standDirection = playerPosition - targetPosition;
            if (standDirection.sqrMagnitude <= 0.0001f)
            {
                standDirection = -Vector2.right;
            }
            else
            {
                standDirection.Normalize();
            }

            presentation.UpdateExecutionFocusPoint(playerPosition, targetPosition);
            CameraFollow2D activeCamera = context.CameraFollow;
            Transform initialFocusTarget = useBossExecutionSequence
                ? bossExecutionSequence.InitialCameraFocus
                : presentation.ExecutionFocusPoint != null
                    ? presentation.ExecutionFocusPoint
                    : executionTarget.transform;
            if (useBossExecutionSequence)
            {
                activeCamera?.BeginCinematicFocus(
                    initialFocusTarget,
                    bossExecutionSequence.WideCameraFocusWeight,
                    bossExecutionSequence.WideCameraZoomMultiplier,
                    bossExecutionSequence.WideCameraBlendSmoothTime);
            }
            else
            {
                activeCamera?.BeginCinematicFocus(
                    initialFocusTarget,
                    teleported ? 1f : config.ExecutionCameraFocusWeight,
                    config.ExecutionCameraZoomMultiplier);
            }

            if (teleported && !useBossExecutionSequence)
            {
                activeCamera?.SnapToCinematicFocus();
            }

            if (letterboxRoutine != null)
            {
                yield return letterboxRoutine;
                if (executionTarget == null)
                {
                    FinishExecution();
                    yield break;
                }
            }

            commonPresentation.Trigger(CommonExecutionCuePoint.AfterLetterbox);
            SoundManager.PlaySfx("Execute");
            if (!useBossExecutionSequence)
            {
                context.ExecutionImage?.Play(
                    flourishSeconds
                    + config.ExecutionAimSeconds
                    + config.ExecutionShotDelaySeconds
                    + holsteringSeconds * 2f
                    + finalPresentationSeconds
                    + config.ExecutionKillDelaySeconds);
            }

            activeCamera?.PlayImpact(standDirection, 0.08f, 0.14f, 0.12f);

            Transform rightFireOrigin;
            Vector2 aimDirection;
            if (useBossExecutionSequence)
            {
                yield return bossExecutionSequence.PlayPrelude();
                if (executionTarget == null)
                {
                    FinishExecution();
                    yield break;
                }

                targetPosition = executionTarget.transform.position;
                playerPosition = context.Body != null
                    ? context.Body.position
                    : (Vector2)context.PlayerTransform.position;
                presentation.UpdateExecutionFocusPoint(playerPosition, targetPosition);
                activeCamera?.BeginCinematicFocus(
                    presentation.ExecutionFocusPoint != null
                        ? presentation.ExecutionFocusPoint
                        : executionTarget.transform,
                    config.ExecutionCameraFocusWeight,
                    config.ExecutionCameraZoomMultiplier);

                rightFireOrigin = rig.GetRightFireOrigin();
                aimDirection = targetPosition - (Vector2)rightFireOrigin.position;
                aimController.AimExecutionPose(aimDirection);
                context.Visual?.BeginExecutionAimVisual(aimDirection);
            }
            else
            {
                rightFireOrigin = rig.GetRightFireOrigin();
                aimDirection = targetPosition - (Vector2)rightFireOrigin.position;
                aimController.AimExecutionPose(aimDirection);
                context.Visual?.BeginExecutionAimVisual(aimDirection);
                commonPresentation.Trigger(CommonExecutionCuePoint.FlourishStart);
                yield return new WaitForSeconds(config.ExecutionFlourishDelaySeconds);
                yield return RunExecutionFlourish(executionTarget);

                yield return new WaitForSeconds(config.ExecutionAimSeconds);
                if (executionTarget == null)
                {
                    FinishExecution();
                    yield break;
                }
            }

            Transform finalShotTarget = ResolveFinalExecutionShotTarget(
                executionTarget,
                executionBoss,
                isFinalBossExecution);
            rightFireOrigin = rig.GetRightFireOrigin();
            Vector3 finalImpactPosition = finalShotTarget != null
                ? finalShotTarget.position
                : executionTarget.transform.position;
            aimDirection = (Vector2)finalImpactPosition - (Vector2)rightFireOrigin.position;
            aimController.AimExecutionPose(aimDirection);

            commonPresentation.Trigger(CommonExecutionCuePoint.BeforePowerShot);
            yield return new WaitForSeconds(config.ExecutionShotDelaySeconds);
            if (executionTarget == null)
            {
                FinishExecution();
                yield break;
            }

            rightFireOrigin = rig.GetRightFireOrigin();
            finalImpactPosition = finalShotTarget != null
                ? finalShotTarget.position
                : executionTarget.transform.position;
            aimDirection = aimController.AimGunAndGetDirection(
                rightFireOrigin,
                (Vector2)finalImpactPosition - (Vector2)rightFireOrigin.position);
            presentation.UpdateExecutionFocusPoint(context.PlayerTransform.position, executionTarget.transform.position);
            if (useBossExecutionSequence)
            {
                context.Visual?.BeginExecutionAimVisual(aimDirection);
            }
            else if (context.Visual != null)
            {
                yield return context.Visual.PlayExecutionRightArmHolstering(holsteringSeconds, true);
            }
            else
            {
                yield return new WaitForSeconds(holsteringSeconds);
            }

            if (isFinalBossExecution)
            {
                activeBossExecutionSequence
                    ?.OnFinalShotPreparationStarted(executionBoss);
                yield return presentation.BeginFinalExecutionBlackout(
                    executionBoss,
                    finalBlackoutTimeMultiplier);
                activeBossExecutionSequence?.OnFinalBlackoutStarted(
                    executionBoss);
            }

            SoundManager.PlaySfx("PlayerPowerShot");
            commonPresentation.PrepareFinalShot(
                rightFireOrigin.position,
                finalImpactPosition);
            bool beganFinalShotSlowMotionBeforeImpact =
                isFinalBossExecution
                && useBossExecutionSequence
                && bossExecutionSequence
                    .BeginFinalShotSlowMotionBeforeImpact;
            if (beganFinalShotSlowMotionBeforeImpact)
            {
                presentation.BeginFinalExecutionShotSlowMotion();
            }

            commonPresentation.Trigger(CommonExecutionCuePoint.PowerShot);
            activeBossExecutionSequence?.OnFinalShotImpact(executionBoss);
            if (!isFinalBossExecution)
            {
                presentation.PlayExecutionShotDim();
            }

            executionTarget.GetComponentInParent<BossAI>()?.PlayExecutionBarDrain();

            Vector3 shotLineEnd = finalImpactPosition;
            if (isFinalBossExecution)
            {
                shotLineEnd += (Vector3)(aimDirection.normalized * config.ExecutionRange);
            }

            GameObject[] finalShotLines = null;
            if (isFinalBossExecution)
            {
                finalShotLines = PlayFinalExecutionParryShotVfx(
                    rightFireOrigin,
                    finalImpactPosition,
                    shotLineEnd,
                    config,
                    useBossExecutionSequence
                        && bossExecutionSequence.UseParryColorForFinalShotLine
                            ? config.ParryEffectColor
                            : Color.white,
                    presentation.FinalExecutionSortingLayerId,
                    presentation.FinalExecutionBossFrontSortingOrder,
                    presentation.FinalExecutionBossBackSortingOrder,
                    useBossExecutionSequence
                        ? bossExecutionSequence.FinalShotLineWidthMultiplier
                        : 1f);
                if (!beganFinalShotSlowMotionBeforeImpact)
                {
                    presentation.BeginFinalExecutionShotSlowMotion();
                }
            }
            else
            {
                PlayExecutionParryShotVfx(
                    rightFireOrigin,
                    shotLineEnd,
                    config);
            }
            if (isFinalBossExecution)
            {
                ExecutionVfx.PlayImpact(
                    finalImpactPosition,
                    aimDirection,
                    Color.white,
                    config.ExecutionImpactParticleCount,
                    config.ExecutionImpactParticleSeconds,
                    66,
                    presentation.FinalExecutionSortingLayerId);
            }

            activeCamera?.PlayImpact(aimDirection, 0.12f, 0.14f, 0.08f);

            if (context.Visual != null && !isFinalBossExecution)
            {
                yield return context.Visual.PlayExecutionRightArmHolstering(holsteringSeconds, false);
            }
            else if (!isFinalBossExecution)
            {
                yield return new WaitForSeconds(holsteringSeconds);
            }

            if (isFinalBossExecution)
            {
                yield return presentation.PlayFinalExecutionImpactAndRelease(
                    finalShotLines,
                    finalBlackoutTimeMultiplier,
                    () => activeBossExecutionSequence
                        ?.OnFinalBlackoutFadeOutStarted(
                            executionBoss,
                            config.FinalExecutionBlackoutFadeOutSeconds
                                * finalBlackoutTimeMultiplier));
                activeBossExecutionSequence?.OnFinalBlackoutEnded(
                    executionBoss);
            }

            commonPresentation.Trigger(CommonExecutionCuePoint.AfterImpact);
            yield return new WaitForSeconds(config.ExecutionKillDelaySeconds);
            if (executionTarget == null)
            {
                FinishExecution();
                yield break;
            }

            Vector3 impactPosition = executionTarget.transform.position;
            float playerHpRecoverySeconds = isFinalBossExecution
                ? 0f
                : presentation.ShowPlayerHpForExecutionRecovery();
            executionTarget.RecoverExecutorBullets(context.Owner);
            if (!isFinalBossExecution)
            {
                ExecutionVfx.PlayImpact(
                    impactPosition,
                    aimDirection,
                    config.ExecutionImpactColor,
                    config.ExecutionImpactParticleCount,
                    config.ExecutionImpactParticleSeconds);
            }

            context.CameraFollow?.PlayImpact(aimDirection, 0.18f, 0.18f, 0.1f);
            lockOnController.SetLockOnTarget(null);
            SetHoveredExecutionTarget(null);

            commonPresentation.Trigger(CommonExecutionCuePoint.SequenceEnd);
            yield return new WaitForSeconds(Mathf.Max(config.ExecutionFinishSeconds, playerHpRecoverySeconds));

            bool executionFinished = false;
            if (executionTarget != null)
            {
                BossAI boss = executionBoss != null
                    ? executionBoss
                    : executionTarget.GetComponentInParent<BossAI>();
                if (boss != null)
                {
                    if (boss.TryConsumeLife())
                    {
                        executionTarget.CompleteExecutionWithoutKill();
                    }
                    else
                    {
                        SetWaitingForVictoryPanel(true);
                        FinishExecution(
                            isFinalBossExecution,
                            isFinalBossExecution,
                            isFinalBossExecution,
                            isFinalBossExecution);
                        executionFinished = true;
                        try
                        {
                            CameraFollow2D finalDeathCamera = activeCamera;
                            if (isFinalBossExecution)
                            {
                                finalDeathCamera = presentation.BeginFinalDeathCameraFocus(boss);
                                // 레터박스가 죽는 애니메이션이 다 끝난 뒤에 걷히면, 플레이어 입장에서는
                                // "컷신이 끝난 순간 이미 보스가 죽어있는" 것처럼 보인다. 처형 연출이
                                // 끝났다는 시각적 신호(레터박스 해제)가 먼저 오고, 그 다음에 Die
                                // 애니메이션이 재생을 시작해야 자연스럽다.
                                yield return presentation.HideFinalExecutionLetterbox();
                            }
                            else
                            {
                                yield return presentation.WaitBeforeFinalDeathFocus();
                                finalDeathCamera = presentation.BeginFinalDeathCameraFocus(boss);
                            }

                            bool playFinalDeathExplosions = config.ShouldPlayFinalDeathExplosionsInScene(
                                boss.gameObject.scene.name);
                            yield return boss.PlayFinalDeathSequence(playFinalDeathExplosions);
                            bossExecutionSequence?.OnFinalDeathSequenceComplete(boss);
                            if (isFinalBossExecution)
                            {
                                activeCamera?.EndCinematicFocus();
                            }

                            float resultPanelDelaySeconds = Mathf.Max(
                                0f,
                                config.FinalExecutionResultPanelDelaySeconds);
                            if (resultPanelDelaySeconds > 0f)
                            {
                                yield return new WaitForSeconds(resultPanelDelaySeconds);
                            }

                            if (executionTarget != null)
                            {
                                executionTarget.CompleteExecution(context.Owner, false);
                                // 결과 패널 뒤에서 보스 사망 애니메이션의 마지막 프레임을 유지한다.
                            }

                            if (!isFinalBossExecution)
                            {
                                finalDeathCamera?.EndCinematicFocus();
                            }
                        }
                        finally
                        {
                            if (isFinalBossExecution)
                            {
                                presentation.RestoreFinalExecutionPresentation(true);
                                activeCamera?.EndCinematicFocus();
                            }

                            SetWaitingForVictoryPanel(false);
                        }
                    }
                }
                else
                {
                    executionTarget.CompleteExecution(context.Owner, false);
                    executionTarget.DestroyExecutedTarget();
                }
            }

            if (!executionFinished)
            {
                yield return presentation.HideFinalExecutionLetterbox();
                FinishExecution();
            }
        }

        private void SetWaitingForVictoryPanel(bool waiting)
        {
            isWaitingForVictoryPanel = waiting;
            if (waiting)
            {
                rig.StopBody();
            }
        }

        private bool TeleportBesideBoss(BossAI boss, float distance)
        {
            Transform playerTransform = context.PlayerTransform;
            Rigidbody2D body = context.Body;
            if (boss == null || playerTransform == null)
            {
                return false;
            }

            Vector2 originalPosition = body != null
                ? body.position
                : (Vector2)playerTransform.position;
            Vector2 bossPosition = boss.transform.position;
            float horizontalDistance = Mathf.Max(0f, distance);
            Vector2 leftPosition = bossPosition + Vector2.left * horizontalDistance;
            Vector2 rightPosition = bossPosition + Vector2.right * horizontalDistance;
            bool preferLeft = (leftPosition - originalPosition).sqrMagnitude
                <= (rightPosition - originalPosition).sqrMagnitude;
            Vector2 preferredPosition = preferLeft ? leftPosition : rightPosition;
            Vector2 fallbackPosition = preferLeft ? rightPosition : leftPosition;
            Collider2D[] playerColliders = body != null
                ? body.GetComponentsInChildren<Collider2D>(true)
                : playerTransform.GetComponentsInChildren<Collider2D>(true);

            if (!TryResolveTeleportDestination(
                    originalPosition,
                    preferredPosition,
                    fallbackPosition,
                    playerColliders,
                    out Vector2 destination))
            {
                return false;
            }

            rig.StopBody();
            if (body != null)
            {
                body.position = destination;
            }
            else
            {
                Vector3 worldPosition = playerTransform.position;
                worldPosition.x = destination.x;
                worldPosition.y = destination.y;
                playerTransform.position = worldPosition;
            }

            return true;
        }

        private static bool TryResolveTeleportDestination(
            Vector2 originalPosition,
            Vector2 preferredPosition,
            Vector2 fallbackPosition,
            Collider2D[] playerColliders,
            out Vector2 destination)
        {
            if (IsSafeTeleportPosition(originalPosition, preferredPosition, playerColliders))
            {
                destination = preferredPosition;
                return true;
            }

            if (TryFindSafeTeleportPositionBelow(
                    originalPosition,
                    preferredPosition,
                    playerColliders,
                    out destination))
            {
                return true;
            }

            if (IsSafeTeleportPosition(originalPosition, fallbackPosition, playerColliders))
            {
                destination = fallbackPosition;
                return true;
            }

            return TryFindSafeTeleportPositionBelow(
                originalPosition,
                fallbackPosition,
                playerColliders,
                out destination);
        }

        private static bool TryFindSafeTeleportPositionBelow(
            Vector2 originalPosition,
            Vector2 blockedPosition,
            Collider2D[] playerColliders,
            out Vector2 destination)
        {
            float stepDistance = GetTeleportFallbackStep(playerColliders);
            for (int step = 1; step <= TeleportFallbackMaxSteps; step++)
            {
                Vector2 candidate = blockedPosition + Vector2.down * (stepDistance * step);
                if (IsSafeTeleportPosition(originalPosition, candidate, playerColliders))
                {
                    destination = candidate;
                    return true;
                }
            }

            destination = default;
            return false;
        }

        private static float GetTeleportFallbackStep(Collider2D[] playerColliders)
        {
            float maxColliderHeight = 0f;
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D playerCollider = playerColliders[i];
                if (IsUsableTeleportCollider(playerCollider))
                {
                    maxColliderHeight = Mathf.Max(maxColliderHeight, playerCollider.bounds.size.y);
                }
            }

            return Mathf.Max(
                TeleportFallbackMinStep,
                maxColliderHeight * TeleportFallbackColliderHeightRatio);
        }

        private static bool IsSafeTeleportPosition(
            Vector2 originalPosition,
            Vector2 candidatePosition,
            Collider2D[] playerColliders)
        {
            return IsWallFreeTeleportPosition(originalPosition, candidatePosition, playerColliders)
                && GroundMovementConstraint.IsColliderFootprintGrounded(
                    originalPosition,
                    candidatePosition,
                    playerColliders,
                    TeleportColliderInset);
        }

        private static bool IsWallFreeTeleportPosition(
            Vector2 originalPosition,
            Vector2 candidatePosition,
            Collider2D[] playerColliders)
        {
            int wallLayer = LayerMask.NameToLayer(WallLayerName);
            if (wallLayer < 0)
            {
                return true;
            }

            int wallMask = 1 << wallLayer;
            Vector2 positionDelta = candidatePosition - originalPosition;
            bool checkedCollider = false;
            for (int i = 0; i < playerColliders.Length; i++)
            {
                Collider2D playerCollider = playerColliders[i];
                if (!IsUsableTeleportCollider(playerCollider))
                {
                    continue;
                }

                checkedCollider = true;
                Bounds bounds = playerCollider.bounds;
                Vector2 probeSize = new(
                    Mathf.Max(0.01f, bounds.size.x - TeleportColliderInset),
                    Mathf.Max(0.01f, bounds.size.y - TeleportColliderInset));
                if (Physics2D.OverlapBox((Vector2)bounds.center + positionDelta, probeSize, 0f, wallMask) != null)
                {
                    return false;
                }
            }

            return checkedCollider
                || Physics2D.OverlapCircle(candidatePosition, 0.01f, wallMask) == null;
        }

        private static bool IsUsableTeleportCollider(Collider2D collider)
        {
            return collider != null
                && collider.enabled
                && !collider.isTrigger
                && collider.gameObject.activeInHierarchy;
        }

        private IEnumerator RunExecutionFlourish(ExecutionTarget executionTarget)
        {
            PlayerCombatConfig config = context.Config;
            int shotCount = Mathf.Max(0, config.ExecutionFlourishShotCount);
            float interval = Mathf.Max(0.01f, config.ExecutionFlourishShotInterval);
            for (int i = 0; i < shotCount; i++)
            {
                Transform fireOrigin = rig.GetRightFireOrigin();
                Vector2 aimDirection = executionTarget != null
                    ? (Vector2)executionTarget.transform.position - (Vector2)fireOrigin.position
                    : Vector2.right;
                aimDirection = aimDirection.sqrMagnitude > 0.0001f
                    ? aimDirection.normalized
                    : Vector2.right;

                if (context.Visual != null)
                {
                    yield return context.Visual.PlayExecutionRightArmHolstering(interval, true);
                }
                else
                {
                    yield return new WaitForSeconds(interval);
                }

                SoundManager.PlaySfx("PlayerShot");
                FireExecutionFlourishShot(executionTarget, aimDirection);
                commonPresentation.Trigger(CommonExecutionCuePoint.FlourishShot, i);
            }
        }

        private void FireExecutionFlourishShot(ExecutionTarget executionTarget, Vector2 aimDirection)
        {
            PlayerCombatConfig config = context.Config;
            Transform fireOrigin = rig.GetRightFireOrigin();
            if (fireOrigin == null || config == null)
            {
                return;
            }

            Vector3 impactPosition = executionTarget != null
                ? executionTarget.transform.position
                : fireOrigin.position + (Vector3)(aimDirection * config.ExecutionRange);
            PlayExecutionParryShotVfx(fireOrigin, impactPosition, config);

            if (executionTarget != null)
            {
                executionTarget.PlayHitReaction(executionTarget.transform.position, aimDirection, config.ExecutionShotColor);
            }
        }

        private static void PlayExecutionParryShotVfx(
            Transform fireOrigin,
            Vector3 impactPosition,
            PlayerCombatConfig config)
        {
            Vector3 firePosition = fireOrigin.position;
            ProjectileVfx.PlayShotLine(firePosition, impactPosition, config.ParryEffectColor, 0.08f, 0.06f);
        }

        private static GameObject[] PlayFinalExecutionParryShotVfx(
            Transform fireOrigin,
            Vector3 impactPosition,
            Vector3 lineEndPosition,
            PlayerCombatConfig config,
            Color lineColor,
            int sortingLayerId,
            int frontSortingOrder,
            int backSortingOrder,
            float widthMultiplier)
        {
            Vector3 firePosition = fireOrigin.position;
            float lineWidth = 0.06f * Mathf.Max(0.1f, widthMultiplier);
            GameObject frontLine = ProjectileVfx.PlayShotLine(
                firePosition,
                impactPosition,
                lineColor,
                config.FinalExecutionShotLineSeconds,
                lineWidth,
                frontSortingOrder,
                sortingLayerId,
                false);
            GameObject backLine = ProjectileVfx.PlayShotLine(
                impactPosition,
                lineEndPosition,
                lineColor,
                config.FinalExecutionShotLineSeconds,
                lineWidth,
                backSortingOrder,
                sortingLayerId,
                false);
            return new[] { frontLine, backLine };
        }

        private static Transform ResolveFinalExecutionShotTarget(
            ExecutionTarget executionTarget,
            BossAI executionBoss,
            bool isFinalBossExecution)
        {
            if (!isFinalBossExecution || executionBoss == null)
            {
                return executionTarget != null ? executionTarget.transform : null;
            }

            Transform[] bossTransforms = executionBoss.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < bossTransforms.Length; i++)
            {
                if (bossTransforms[i] != null && bossTransforms[i].name == FinalExecutionHeadName)
                {
                    return bossTransforms[i];
                }
            }

            return executionTarget != null ? executionTarget.transform : null;
        }

    }
}
