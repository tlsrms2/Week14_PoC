using UnityEngine;
using Week14.Enemy;

namespace Week14.Combat
{
    [RequireComponent(typeof(Health), typeof(HeatGauge))]
    public sealed class ExecutionTarget : MonoBehaviour
    {
        [SerializeField] private PlayerCombatConfig config;

        private Health health;
        private HeatGauge heat;
        private EnemyAI enemyAI;
        private bool executionInProgress;

        private void Awake()
        {
            health = GetComponent<Health>();
            heat = GetComponent<HeatGauge>();
            enemyAI = GetComponent<EnemyAI>();
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

            if (!heat.IsOverheated && !health.IsDurabilityDepleted)
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
            enemyAI?.SetExecutionLocked(true);
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

        public bool Execute(PlayerCombatController player, bool destroyAfterExecute, bool recoverHeat)
        {
            PlayerCombatConfig activeConfig = Config;
            if (player == null || activeConfig == null || !CanExecute(player.transform))
            {
                return false;
            }

            BeginExecution(player);
            health.Kill();
            if (recoverHeat)
            {
                player.Heat.ReduceHeat(activeConfig.ExecutionHeatRecovery);
            }

            if (destroyAfterExecute && activeConfig.DestroyTargetOnExecute)
            {
                Destroy(gameObject);
            }

            return true;
        }

        public bool RecoverExecutorHeat(PlayerCombatController player)
        {
            PlayerCombatConfig activeConfig = Config;
            if (player == null || activeConfig == null || health.IsDead)
            {
                return false;
            }

            player.Heat.ReduceHeat(activeConfig.ExecutionHeatRecovery);
            return true;
        }

        public bool CompleteExecution(PlayerCombatController player, bool recoverHeat)
        {
            PlayerCombatConfig activeConfig = Config;
            if (player == null || activeConfig == null || health.IsDead)
            {
                return false;
            }

            health.Kill();
            if (recoverHeat)
            {
                player.Heat.ReduceHeat(activeConfig.ExecutionHeatRecovery);
            }

            return true;
        }

        public void DestroyExecutedTarget()
        {
            if (Config != null && Config.DestroyTargetOnExecute)
            {
                Destroy(gameObject);
            }
        }

        private PlayerCombatConfig Config => config != null ? config : PlayerCombatController.Active?.Config;
    }
}
