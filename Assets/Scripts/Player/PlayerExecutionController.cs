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
            context.ExecutionImage?.Play(flourishSeconds + config.ExecutionAimSeconds + config.ExecutionShotDelaySeconds + config.ExecutionKillDelaySeconds);

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

            Transform leftFireOrigin = rig.GetLeftFireOrigin();
            Transform leftGunOrigin = GetLeftGunOrigin();
            Vector2 aimDirection = targetPosition - (Vector2)leftGunOrigin.position;
            aimController.AimExecutionPose(aimDirection);
            yield return new WaitForSeconds(config.ExecutionFlourishDelaySeconds);
            yield return RunExecutionFlourish(executionTarget, aimDirection);

            yield return new WaitForSeconds(config.ExecutionAimSeconds);
            if (executionTarget == null)
            {
                FinishExecution();
                yield break;
            }

            leftFireOrigin = rig.GetLeftFireOrigin();
            leftGunOrigin = GetLeftGunOrigin();
            aimDirection = (Vector2)executionTarget.transform.position - (Vector2)leftGunOrigin.position;
            aimController.AimExecutionPose(aimDirection);

            yield return new WaitForSeconds(config.ExecutionShotDelaySeconds);
            if (executionTarget == null)
            {
                FinishExecution();
                yield break;
            }

            leftFireOrigin = rig.GetLeftFireOrigin();
            leftGunOrigin = GetLeftGunOrigin();
            aimDirection = aimController.AimGunAndGetDirection(
                leftGunOrigin,
                (Vector2)executionTarget.transform.position - (Vector2)leftGunOrigin.position);
            aimController.LockLeftGunAim(aimDirection);
            presentation.UpdateExecutionFocusPoint(context.PlayerTransform.position, executionTarget.transform.position);
            context.Visual?.PlayShot();
            SoundManager.PlaySfx("PlayerPowerShot");
            presentation.PlayExecutionShotDim();
            executionTarget.GetComponentInParent<BossAI>()?.PlayExecutionBarDrain();

            PlayerProjectile executionShot = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                leftFireOrigin.position,
                aimDirection,
                context.Owner,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                0,
                config.ExecutionShotColor,
                false);
            if (executionShot != null)
            {
                Color muzzleFlashColor = Color.Lerp(config.ExecutionShotColor, Color.white, 0.65f);
                muzzleFlashColor.a = 1f;
                ProjectileVfx.PlayMuzzleFlash(leftFireOrigin.position, aimDirection, muzzleFlashColor, 1.55f);
                activeCamera?.PlayImpact(aimDirection, 0.12f, 0.14f, 0.08f);
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

        private IEnumerator RunExecutionFlourish(ExecutionTarget executionTarget, Vector2 aimDirection)
        {
            PlayerCombatConfig config = context.Config;
            int shotCount = Mathf.Max(0, config.ExecutionFlourishShotCount);
            float interval = Mathf.Max(0.01f, config.ExecutionFlourishShotInterval);
            for (int i = 0; i < shotCount; i++)
            {
                context.Visual?.PlayShot();
                SoundManager.PlaySfx("PlayerShot");
                FireExecutionFlourishShot(executionTarget, aimDirection);
                yield return new WaitForSeconds(interval);
            }
        }

        private void FireExecutionFlourishShot(ExecutionTarget executionTarget, Vector2 aimDirection)
        {
            PlayerCombatConfig config = context.Config;
            Transform fireOrigin = rig.GetLeftFireOrigin();
            if (fireOrigin == null || config == null)
            {
                return;
            }

            PlayerProjectile flourishShot = PlayerProjectile.Spawn(
                config.ProjectilePrefab,
                fireOrigin.position,
                aimDirection,
                context.Owner,
                config.ProjectileSpeed,
                config.ProjectileLifetime,
                config.ProjectileRadius,
                0,
                config.ExecutionShotColor,
                false);
            if (flourishShot != null)
            {
                Color muzzleFlashColor = Color.Lerp(config.ExecutionShotColor, Color.white, 0.65f);
                muzzleFlashColor.a = 1f;
                ProjectileVfx.PlayMuzzleFlash(fireOrigin.position, aimDirection, muzzleFlashColor, 1.55f);
            }

            if (executionTarget != null)
            {
                executionTarget.PlayHitReaction(executionTarget.transform.position, aimDirection, config.ExecutionShotColor);
            }
        }

        private Transform GetLeftGunOrigin()
        {
            return context.LeftGunOrigin != null ? context.LeftGunOrigin : context.PlayerTransform;
        }
    }
}
