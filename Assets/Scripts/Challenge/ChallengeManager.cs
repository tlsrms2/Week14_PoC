using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Combat;
using Week14.Enemy;
using Week14.Save;

namespace Week14.Challenge
{
    public sealed class ChallengeManager : MonoBehaviour
    {
        [Tooltip("게임에 존재하는 모든 챌린지 정의를 담은 데이터베이스입니다.")]
        [SerializeField] private ChallengeDatabaseSO database;

        private static ChallengeManager instance;

        public static ChallengeManager Instance => instance;

        private readonly List<(ChallengeDefinitionSO Definition, ChallengeRunState Run)> activeRuns = new();
        private BossAI currentBoss;
        private Health subscribedPlayerHealth;
        private string currentBossId;
        private float combatStartTime;
        private int hitCountThisRun;
        private int parryCountThisRun;
        private bool combatActive;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            BossAI.CombatStarted += HandleCombatStarted;
            BossAI.Defeated += HandleVictory;
            PlayerDamageReceiver.PlayerHitByEnemy += HandlePlayerHit;
            PlayerParryController.ProjectileParried += HandleParried;
            TrySubscribePlayer();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            BossAI.CombatStarted -= HandleCombatStarted;
            BossAI.Defeated -= HandleVictory;
            PlayerDamageReceiver.PlayerHitByEnemy -= HandlePlayerHit;
            PlayerParryController.ProjectileParried -= HandleParried;
            UnsubscribeBoss();
            UnsubscribePlayer();
        }

        private void Update()
        {
            if (!combatActive)
            {
                return;
            }

            float elapsedSeconds = Time.time - combatStartTime;
            for (int i = 0; i < activeRuns.Count; i++)
            {
                activeRuns[i].Run.OnTick(elapsedSeconds);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            UnsubscribeBoss();
            currentBoss = FindFirstObjectByType<BossAI>();
            if (currentBoss != null)
            {
                currentBoss.LivesChanged += HandlePhaseChanged;
            }

            TrySubscribePlayer();
        }

        private void UnsubscribeBoss()
        {
            if (currentBoss != null)
            {
                currentBoss.LivesChanged -= HandlePhaseChanged;
            }

            currentBoss = null;
            combatActive = false;
            activeRuns.Clear();
        }

        private void TrySubscribePlayer()
        {
            PlayerCombatController player = PlayerCombatController.Active;
            Health nextHealth = player != null ? player.Health : null;
            if (subscribedPlayerHealth == nextHealth)
            {
                return;
            }

            UnsubscribePlayer();
            subscribedPlayerHealth = nextHealth;
            if (subscribedPlayerHealth != null)
            {
                subscribedPlayerHealth.Died += HandleDefeat;
            }
        }

        private void UnsubscribePlayer()
        {
            if (subscribedPlayerHealth == null)
            {
                return;
            }

            subscribedPlayerHealth.Died -= HandleDefeat;
            subscribedPlayerHealth = null;
        }

        private void HandleCombatStarted(BossAI boss)
        {
            if (boss != currentBoss || database == null)
            {
                return;
            }

            combatStartTime = Time.time;
            hitCountThisRun = 0;
            parryCountThisRun = 0;
            combatActive = true;

            activeRuns.Clear();
            currentBossId = boss.BossData != null ? boss.BossData.Id : null;
            foreach (ChallengeDefinitionSO definition in database.ForBoss(currentBossId))
            {
                if (GameSaveManager.IsChallengeCompleted(GameSaveManager.BuildChallengeSaveKey(currentBossId, definition.ChallengeId)))
                {
                    continue;
                }

                activeRuns.Add((definition, definition.CreateRunState()));
            }
        }

        private void HandlePlayerHit(int _)
        {
            if (!combatActive)
            {
                return;
            }

            hitCountThisRun++;
            for (int i = 0; i < activeRuns.Count; i++)
            {
                activeRuns[i].Run.OnPlayerHit(hitCountThisRun);
            }
        }

        private void HandleParried()
        {
            if (!combatActive)
            {
                return;
            }

            parryCountThisRun++;
            for (int i = 0; i < activeRuns.Count; i++)
            {
                activeRuns[i].Run.OnParried(parryCountThisRun);
            }
        }

        private void HandlePhaseChanged(int currentLives, int maxLives)
        {
            if (!combatActive || currentBoss == null)
            {
                return;
            }

            int phaseNumber = currentBoss.CurrentPhaseNumber;
            for (int i = 0; i < activeRuns.Count; i++)
            {
                activeRuns[i].Run.OnPhaseReached(phaseNumber);
            }
        }

        private void HandleVictory(BossAI boss)
        {
            if (boss != currentBoss)
            {
                return;
            }

            EvaluateAndSave(victory: true);
        }

        private void HandleDefeat(Health _)
        {
            EvaluateAndSave(victory: false);
        }

        private void EvaluateAndSave(bool victory)
        {
            if (!combatActive)
            {
                return;
            }

            for (int i = 0; i < activeRuns.Count; i++)
            {
                ChallengeDefinitionSO definition = activeRuns[i].Definition;
                ChallengeRunState run = activeRuns[i].Run;
                string saveKey = GameSaveManager.BuildChallengeSaveKey(currentBossId, definition.ChallengeId);
                if (run.TryFinalize(victory, saveKey))
                {
                    GameSaveManager.CompleteChallenge(saveKey, definition.RewardPoint);
                }
            }

            activeRuns.Clear();
            combatActive = false;
        }
    }
}
