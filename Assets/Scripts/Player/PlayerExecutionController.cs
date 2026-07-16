using System.Collections;
using UnityEngine;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Enemy;

namespace Week14.Combat
{
    internal sealed class PlayerExecutionController
    {
        private readonly PlayerCombatController.PlayerCombatContext context;
        private readonly PlayerCombatRig rig;
        private readonly PlayerAimController aimController;
        private readonly PlayerLockOnController lockOnController;
        private readonly PlayerExecutionPresentation presentation;
        private Coroutine executionRoutine;
        private ExecutionTarget hoveredExecutionTarget;
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

        internal void FinishExecution()
        {
            isExecuting = false;
            executionRoutine = null;
            context.Visual?.EndExecutionVisual();
            context.CameraFollow?.EndCinematicFocus();
            lockOnController.ClearInvalidLockOnTarget();
            UpdateHoveredExecutionTarget();
            presentation.RestorePlayerHpAfterExecution();
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
        }

        private ExecutionTarget FindHoveredExecutionTarget()
        {
            Vector2 executionCenter = rig.CombatCenterOrigin.position;
            float executionRange = context.Config != null ? context.Config.ExecutionRange : 0f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(executionCenter, executionRange, context.EnemyMask);
            ExecutionTarget bestTarget = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hits.Length; i++)
            {
                ExecutionTarget target = hits[i].GetComponentInParent<ExecutionTarget>();
                ChooseCloserExecutionTarget(target, executionCenter, ref bestTarget, ref bestDistance);
            }

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
            float executionRange = context.Config != null ? context.Config.ExecutionRange : 0f;
            if (distance > executionRange || distance >= bestDistance)
            {
                return;
            }

            bestTarget = target;
            bestDistance = distance;
        }

        private IEnumerator ExecuteTarget(ExecutionTarget executionTarget)
        {
            PlayerCombatConfig config = context.Config;
            isExecuting = true;
            if (config == null || executionTarget == null || !executionTarget.BeginExecution(context.Owner))
            {
                FinishExecution();
                yield break;
            }

            rig.StopBody();
            presentation.HidePlayerHpForExecution();
            SoundManager.PlaySfx("Execute");
            float flourishSeconds = Mathf.Max(0f, config.ExecutionFlourishDelaySeconds)
                + Mathf.Max(0, config.ExecutionFlourishShotCount) * Mathf.Max(0.01f, config.ExecutionFlourishShotInterval);
            float holsteringSeconds = Mathf.Max(0.01f, config.ExecutionFlourishShotInterval);
            context.ExecutionImage?.Play(
                flourishSeconds
                + config.ExecutionAimSeconds
                + config.ExecutionShotDelaySeconds
                + holsteringSeconds * 2f
                + config.ExecutionKillDelaySeconds);

            Health targetHealth = executionTarget.GetComponent<Health>();
            if (targetHealth != null)
            {
                lockOnController.SetLockOnTarget(targetHealth);
            }

            Vector2 targetPosition = executionTarget.transform.position;
            Vector2 playerPosition = context.PlayerTransform.position;
            Vector2 standDirection = playerPosition - targetPosition;
            if (standDirection.sqrMagnitude <= 0.0001f)
            {
                standDirection = -Vector2.right;
            }
            else
            {
                standDirection.Normalize();
            }

            presentation.UpdateExecutionFocusPoint(context.PlayerTransform.position, executionTarget.transform.position);
            CameraFollow2D activeCamera = context.CameraFollow;
            activeCamera?.BeginCinematicFocus(
                presentation.ExecutionFocusPoint != null ? presentation.ExecutionFocusPoint : executionTarget.transform,
                config.ExecutionCameraFocusWeight,
                config.ExecutionCameraZoomMultiplier);
            activeCamera?.PlayImpact(standDirection, 0.08f, 0.14f, 0.12f);

            Transform rightFireOrigin = rig.GetRightFireOrigin();
            Vector2 aimDirection = targetPosition - (Vector2)rightFireOrigin.position;
            aimController.AimExecutionPose(aimDirection);
            context.Visual?.BeginExecutionVisual(aimDirection);
            yield return new WaitForSeconds(config.ExecutionFlourishDelaySeconds);
            yield return RunExecutionFlourish(executionTarget);

            yield return new WaitForSeconds(config.ExecutionAimSeconds);
            if (executionTarget == null)
            {
                FinishExecution();
                yield break;
            }

            rightFireOrigin = rig.GetRightFireOrigin();
            aimDirection = (Vector2)executionTarget.transform.position - (Vector2)rightFireOrigin.position;
            aimController.AimExecutionPose(aimDirection);

            yield return new WaitForSeconds(config.ExecutionShotDelaySeconds);
            if (executionTarget == null)
            {
                FinishExecution();
                yield break;
            }

            rightFireOrigin = rig.GetRightFireOrigin();
            aimDirection = aimController.AimGunAndGetDirection(
                rightFireOrigin,
                (Vector2)executionTarget.transform.position - (Vector2)rightFireOrigin.position);
            presentation.UpdateExecutionFocusPoint(context.PlayerTransform.position, executionTarget.transform.position);
            if (context.Visual != null)
            {
                yield return context.Visual.PlayExecutionRightArmHolstering(holsteringSeconds, true);
            }
            else
            {
                yield return new WaitForSeconds(holsteringSeconds);
            }

            SoundManager.PlaySfx("PlayerPowerShot");
            presentation.PlayExecutionShotDim();
            executionTarget.GetComponentInParent<BossAI>()?.PlayExecutionBarDrain();

            PlayExecutionParryShotVfx(
                rightFireOrigin.position,
                executionTarget.transform.position,
                aimDirection,
                config);
            activeCamera?.PlayImpact(aimDirection, 0.12f, 0.14f, 0.08f);

            if (context.Visual != null)
            {
                yield return context.Visual.PlayExecutionRightArmHolstering(holsteringSeconds, false);
            }
            else
            {
                yield return new WaitForSeconds(holsteringSeconds);
            }

            yield return new WaitForSeconds(config.ExecutionKillDelaySeconds);
            if (executionTarget == null)
            {
                FinishExecution();
                yield break;
            }

            Vector3 impactPosition = executionTarget.transform.position;
            float playerHpRecoverySeconds = presentation.ShowPlayerHpForExecutionRecovery();
            executionTarget.RecoverExecutorBullets(context.Owner);
            ExecutionVfx.PlayImpact(
                impactPosition,
                aimDirection,
                config.ExecutionImpactColor,
                config.ExecutionImpactParticleCount,
                config.ExecutionImpactParticleSeconds);
            context.CameraFollow?.PlayImpact(aimDirection, 0.18f, 0.18f, 0.1f);
            lockOnController.SetLockOnTarget(null);
            SetHoveredExecutionTarget(null);

            yield return new WaitForSeconds(Mathf.Max(config.ExecutionFinishSeconds, playerHpRecoverySeconds));

            bool executionFinished = false;
            if (executionTarget != null)
            {
                BossAI boss = executionTarget.GetComponentInParent<BossAI>();
                if (boss != null)
                {
                    EnemyProjectile.DestroyAllActive();
                    if (boss.TryConsumeLife())
                    {
                        executionTarget.CompleteExecutionWithoutKill();
                    }
                    else
                    {
                        SetWaitingForVictoryPanel(true);
                        FinishExecution();
                        executionFinished = true;
                        try
                        {
                            yield return presentation.WaitBeforeFinalDeathFocus();
                            CameraFollow2D finalDeathCamera = presentation.BeginFinalDeathCameraFocus(boss);
                            yield return boss.PlayFinalDeathSequence();
                            if (context.VictoryPanelDelaySeconds > 0f)
                            {
                                yield return new WaitForSeconds(context.VictoryPanelDelaySeconds);
                            }

                            if (executionTarget != null)
                            {
                                executionTarget.CompleteExecution(context.Owner, false);
                                executionTarget.DestroyExecutedTarget();
                            }

                            finalDeathCamera?.EndCinematicFocus();
                        }
                        finally
                        {
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
            PlayExecutionParryShotVfx(fireOrigin.position, impactPosition, aimDirection, config);

            if (executionTarget != null)
            {
                executionTarget.PlayHitReaction(executionTarget.transform.position, aimDirection, config.ExecutionShotColor);
            }
        }

        private static void PlayExecutionParryShotVfx(
            Vector3 firePosition,
            Vector3 impactPosition,
            Vector2 direction,
            PlayerCombatConfig config)
        {
            ProjectileVfx.PlayShotLine(firePosition, impactPosition, config.ParryEffectColor, 0.08f, 0.06f);
            ProjectileVfx.PlayMuzzleFlash(firePosition, direction, config.ParryEffectColor, 1f);
        }

    }
}
