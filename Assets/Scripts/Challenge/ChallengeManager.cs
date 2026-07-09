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

        // 가장 최근 전투 종료(EvaluateAndSave) 시 새로 지급된 포인트 총합입니다. 이미 완료했던 챌린지는 포함하지 않습니다.
        public int LastRunEarnedPoints { get; private set; }

        private readonly List<(ChallengeDefinitionSO Definition, ChallengeRunState Run)> activeRuns = new();
        private readonly HashSet<string> alreadyCompletedBeforeRun = new();
        private readonly Dictionary<string, int> progressBeforeRun = new();
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
            alreadyCompletedBeforeRun.Clear();
            progressBeforeRun.Clear();
            currentBossId = boss.BossData != null ? boss.BossData.Id : null;
            foreach (ChallengeDefinitionSO definition in database.ForBoss(currentBossId))
            {
                string saveKey = GameSaveManager.BuildChallengeSaveKey(currentBossId, definition.ChallengeId);
                if (GameSaveManager.IsChallengeCompleted(saveKey))
                {
                    alreadyCompletedBeforeRun.Add(saveKey);
                    continue;
                }

                progressBeforeRun[saveKey] = definition.GetCurrentProgress(currentBossId);
                activeRuns.Add((definition, definition.CreateRunState()));
            }
        }

        // 이번 전투가 시작되기 전에 이미 클리어되어 있던 챌린지인지 여부입니다. 결과 화면 공개 연출에서 스윕 애니메이션을 건너뛸지 판단하는 데 씁니다.
        public bool WasAlreadyCompletedBeforeRun(string saveKey)
        {
            return alreadyCompletedBeforeRun.Contains(saveKey);
        }

        // 이번 전투가 시작되기 전의 진행도 값입니다. 결과 화면 공개 연출에서 스윕 전에는 이 값을, 스윕 후에는 최신 값을 보여주는 데 씁니다.
        public int GetProgressBeforeRun(string saveKey, int fallback)
        {
            return progressBeforeRun.TryGetValue(saveKey, out int value) ? value : fallback;
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

            LastRunEarnedPoints = 0;
            for (int i = 0; i < activeRuns.Count; i++)
            {
                ChallengeDefinitionSO definition = activeRuns[i].Definition;
                ChallengeRunState run = activeRuns[i].Run;
                string saveKey = GameSaveManager.BuildChallengeSaveKey(currentBossId, definition.ChallengeId);
                if (run.TryFinalize(victory, saveKey))
                {
                    GameSaveManager.CompleteChallenge(saveKey, definition.RewardPoint);
                    LastRunEarnedPoints += definition.RewardPoint;
                }
            }

            activeRuns.Clear();
            combatActive = false;
        }
    }
}
