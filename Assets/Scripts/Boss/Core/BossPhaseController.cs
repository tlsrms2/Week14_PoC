using System.Collections;
using UnityEngine;
using Week14.Combat;

namespace Week14.Enemy
{
    internal sealed class BossPhaseController
    {
        private readonly BossAI boss;
        private int currentLives;
        private bool isPhaseTransitionWaiting;
        private float phaseTransitionWaitEndsAt;
        private bool isCombatStarted;
        private float currentPhaseStartTime;
        private int currentEnragePhase;
        private int pendingEnragePhase;

        public BossPhaseController(BossAI boss)
        {
            this.boss = boss;
        }

        public int CurrentLives => Mathf.Clamp(currentLives, 0, boss.MaxLives);
        public int CurrentPhaseIndex => Mathf.Clamp(boss.MaxLives - CurrentLives, 0, boss.MaxLives - 1);
        public int CurrentPhaseNumber => CurrentPhaseIndex + 1;
        public int CurrentEnragePhase => currentEnragePhase;
        public bool IsPhaseTransitionWaiting => isPhaseTransitionWaiting;
        public bool IsCombatStarted => isCombatStarted;
        public float CurrentEnrageProgress => GetCurrentEnrageProgress();
        public float CurrentEnrageRemainingSeconds => GetCurrentEnrageRemainingSeconds();

        public void Initialize()
        {
            currentLives = boss.MaxLives;
            boss.NotifyLivesChanged();
            boss.OnBossPhaseChangedForController(CurrentPhaseIndex, CurrentPhaseNumber);
        }

        public void TryStartCombat(bool playerDetected)
        {
            if (isCombatStarted || !playerDetected)
            {
                return;
            }

            isCombatStarted = true;
            ResetEnrageTimer();
            boss.OnCombatStartedForController();
        }

        public void TickEnrage()
        {
            if (!isCombatStarted)
            {
                return;
            }

            if (pendingEnragePhase != 0)
            {
                return;
            }

            float elapsed = Time.time - currentPhaseStartTime;
            if (currentEnragePhase == 0 && elapsed >= boss.EnragePhase1Seconds)
            {
                pendingEnragePhase = 1;
            }
            else if (currentEnragePhase == 1 && elapsed >= boss.EnragePhase2Seconds)
            {
                pendingEnragePhase = 2;
            }

            boss.NotifyEnrageChanged();
        }

        public IEnumerator ApplyPendingEnrageIfAny()
        {
            if (pendingEnragePhase == 0)
            {
                yield break;
            }

            boss.Stop();
            yield return boss.PlayEnrageWindupTrembleForController();

            if (pendingEnragePhase == 1)
            {
                currentEnragePhase = 1;
                currentPhaseStartTime = Time.time;
                ApplyPlayerMaxBullets(boss.EnragePhase1MaxBullets);
            }
            else if (pendingEnragePhase == 2)
            {
                currentEnragePhase = 2;
                ApplyPlayerMaxBullets(boss.EnragePhase2MaxBullets);
            }

            pendingEnragePhase = 0;
            boss.NotifyEnrageChanged();
            boss.PlayEnrageTransitionEffectForController();
        }

        public void ResetEnrageTimer()
        {
            currentPhaseStartTime = Time.time;
            currentEnragePhase = 0;
            pendingEnragePhase = 0;
            boss.NotifyEnrageChanged();

            PlayerCombatController player = PlayerCombatController.Active;
            if (player != null && player.Config != null && player.Bullets != null)
            {
                player.Bullets.Configure(player.Config.MaxBullets, true);
            }
        }

        public bool TryConsumeLife()
        {
            if (currentLives > 1)
            {
                currentLives--;
                boss.NotifyLivesChanged();
                boss.OnBossPhaseChangedForController(CurrentPhaseIndex, CurrentPhaseNumber);
                ResetEnrageTimer();

                boss.SetExecutionLocked(false);
                boss.enabled = true;
                if (boss.Body != null)
                {
                    boss.Body.simulated = true;
                }

                boss.CancelBossActionForController();
                boss.Stop();
                BeginPhaseTransitionWait();
                boss.RefillHpForPhaseController();
                return true;
            }

            currentLives = 0;
            isPhaseTransitionWaiting = false;
            phaseTransitionWaitEndsAt = 0f;
            boss.NotifyLivesChanged();
            return false;
        }

        public void TickPhaseTransitionWait()
        {
            boss.Stop();
            if (Time.time < phaseTransitionWaitEndsAt)
            {
                return;
            }

            isPhaseTransitionWaiting = false;
            phaseTransitionWaitEndsAt = 0f;
        }

        private void BeginPhaseTransitionWait()
        {
            float waitSeconds = Mathf.Max(0f, boss.PhaseTransitionWaitSeconds);
            if (waitSeconds <= 0f)
            {
                isPhaseTransitionWaiting = false;
                phaseTransitionWaitEndsAt = 0f;
                return;
            }

            isPhaseTransitionWaiting = true;
            phaseTransitionWaitEndsAt = Time.time + waitSeconds;
        }

        private float GetCurrentEnrageProgress()
        {
            if (!isCombatStarted)
            {
                return 0f;
            }

            if (currentEnragePhase <= 0)
            {
                return Mathf.Clamp01((Time.time - currentPhaseStartTime) / Mathf.Max(0.0001f, boss.EnragePhase1Seconds));
            }

            if (currentEnragePhase == 1)
            {
                return Mathf.Clamp01((Time.time - currentPhaseStartTime) / Mathf.Max(0.0001f, boss.EnragePhase2Seconds));
            }

            return 1f;
        }

        private float GetCurrentEnrageRemainingSeconds()
        {
            if (!isCombatStarted)
            {
                return boss.EnragePhase1Seconds;
            }

            if (currentEnragePhase <= 0)
            {
                return Mathf.Max(0f, boss.EnragePhase1Seconds - (Time.time - currentPhaseStartTime));
            }

            if (currentEnragePhase == 1)
            {
                return Mathf.Max(0f, boss.EnragePhase2Seconds - (Time.time - currentPhaseStartTime));
            }

            return 0f;
        }

        private static void ApplyPlayerMaxBullets(int newMax)
        {
            PlayerCombatController player = PlayerCombatController.Active;
            if (player != null && player.Bullets != null)
            {
                player.Bullets.Configure(newMax, false);
            }
        }
    }
}
