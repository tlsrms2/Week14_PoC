using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    [RequireComponent(typeof(Health), typeof(BulletGauge))]
    public sealed class ExecutionTarget : MonoBehaviour
    {
        [SerializeField] private PlayerCombatConfig config;

        private Health health;
        private BulletGauge bullets;
        private BossAI bossAI;
        private Drone drone;
        private bool executionInProgress;

        private void Awake()
        {
            health = GetComponent<Health>();
            bullets = GetComponent<BulletGauge>();
            bossAI = GetComponent<BossAI>() ?? GetComponentInParent<BossAI>();
            drone = GetComponent<Drone>() ?? GetComponentInParent<Drone>();
        }

        public void SetConfig(PlayerCombatConfig nextConfig)
        {
            config = nextConfig;
        }

        public bool CanExecute(Transform executor)
        {
            PlayerCombatConfig activeConfig = Config;
            if (executor == null || activeConfig == null || health.IsDead || executionInProgress)
            {
                return false;
            }

            if (bullets == null || !bullets.IsEmpty)
            {
                return false;
            }

            return Vector2.Distance(transform.position, executor.position) <= activeConfig.ExecutionRange;
        }

        public bool BeginExecution(PlayerCombatController player)
        {
            PlayerCombatConfig activeConfig = Config;
            if (player == null || activeConfig == null || !CanExecute(player.transform))
            {
                return false;
            }

            executionInProgress = true;
            bossAI?.SetExecutionLocked(true);
            drone?.SetExecutionLocked(true);
            return true;
        }

        public bool Execute(PlayerCombatController player)
        {
            return Execute(player, true, true);
        }

        public bool Execute(PlayerCombatController player, bool destroyAfterExecute)
        {
            return Execute(player, destroyAfterExecute, true);
        }

        public bool Execute(PlayerCombatController player, bool destroyAfterExecute, bool recoverBullets)
        {
            PlayerCombatConfig activeConfig = Config;
            if (player == null || activeConfig == null || !CanExecute(player.transform))
            {
                return false;
            }

            BeginExecution(player);
            health.Kill();
            if (recoverBullets)
            {
                player.Bullets.Restore(activeConfig.ExecutionBulletRecovery, BulletChangeSource.Execution);
            }

            if (destroyAfterExecute && activeConfig.DestroyTargetOnExecute)
            {
                Destroy(gameObject);
            }

            return true;
        }

        public void PlayHitReaction(Vector3 hitPosition, Vector2 hitDirection, Color hitColor)
        {
            bossAI?.PlayExecutionHitReaction(hitPosition, hitDirection, hitColor);
            drone?.PlayExecutionHitReaction(hitPosition, hitDirection, hitColor);
        }

        public bool RecoverExecutorBullets(PlayerCombatController player)
        {
            PlayerCombatConfig activeConfig = Config;
            if (player == null || activeConfig == null || health.IsDead)
            {
                return false;
            }

            player.Bullets.Restore(activeConfig.ExecutionBulletRecovery, BulletChangeSource.Execution);
            return true;
        }

        public bool CompleteExecution(PlayerCombatController player, bool recoverBullets)
        {
            PlayerCombatConfig activeConfig = Config;
            if (player == null || activeConfig == null || health.IsDead)
            {
                return false;
            }

            health.Kill();
            ReleaseExecutionLock();
            if (recoverBullets)
            {
                player.Bullets.Restore(activeConfig.ExecutionBulletRecovery, BulletChangeSource.Execution);
            }

            return true;
        }

        public void CompleteExecutionWithoutKill()
        {
            ReleaseExecutionLock();
        }

        public void DestroyExecutedTarget()
        {
            if (Config != null && Config.DestroyTargetOnExecute)
            {
                Destroy(gameObject);
            }
        }

        private void ReleaseExecutionLock()
        {
            executionInProgress = false;
            bossAI?.SetExecutionLocked(false);
            drone?.SetExecutionLocked(false);
        }

        private PlayerCombatConfig Config => config != null ? config : PlayerCombatController.Active?.Config;
    }
}
