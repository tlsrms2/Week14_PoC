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

        public BossPhaseController(BossAI boss)
        {
            this.boss = boss;
        }

        public int CurrentLives => Mathf.Clamp(currentLives, 0, boss.MaxLives);
        public int CurrentPhaseIndex => Mathf.Clamp(boss.MaxLives - CurrentLives, 0, boss.MaxLives - 1);
        public int CurrentPhaseNumber => CurrentPhaseIndex + 1;
        public bool IsPhaseTransitionWaiting => isPhaseTransitionWaiting;
        public bool IsCombatStarted => isCombatStarted;

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
            RefillPlayerBulletsForCombatStart();
            boss.OnCombatStartedForController();
        }

        private static void RefillPlayerBulletsForCombatStart()
        {
            PlayerCombatController player = PlayerCombatController.Active;
            if (player == null || player.Bullets == null)
            {
                return;
            }

            player.Bullets.Configure(player.Bullets.MaxBullets, true, BulletChangeSource.CombatStart);
        }

        public bool TryConsumeLife()
        {
            if (currentLives > 1)
            {
                currentLives--;
                boss.NotifyLivesChanged();
                boss.OnBossPhaseChangedForController(CurrentPhaseIndex, CurrentPhaseNumber);

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
    }
}
