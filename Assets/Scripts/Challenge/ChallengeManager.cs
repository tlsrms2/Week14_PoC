using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Analytics;
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

        // 챌린지가 하나 완료 확정될 때마다 발생합니다(이미 완료된 챌린지 재완료는 제외). Steam 업적 연동 등 외부 구독자를 위한 이벤트입니다.
        public static event System.Action<string> ChallengeCompleted;

        private readonly List<(ChallengeDefinitionSO Definition, ChallengeRunState Run)> activeRuns = new();
        private readonly HashSet<string> alreadyCompletedBeforeRun = new();
        private readonly Dictionary<string, int> progressBeforeRun = new();
        private BossAI currentBoss;
        private Health subscribedPlayerHealth;
        private string currentBossId;
        private int hitCountThisRun;
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
            EnemyProjectile.AnyDestroyed += HandleProjectileDestroyed;
            ParryBaitRewardProjectile.AnyParryFailed += HandleParryFailed;
            TrySubscribePlayer();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            BossAI.CombatStarted -= HandleCombatStarted;
            BossAI.Defeated -= HandleVictory;
            PlayerDamageReceiver.PlayerHitByEnemy -= HandlePlayerHit;
            PlayerParryController.ProjectileParried -= HandleParried;
            EnemyProjectile.AnyDestroyed -= HandleProjectileDestroyed;
            ParryBaitRewardProjectile.AnyParryFailed -= HandleParryFailed;
            UnsubscribeBoss();
            UnsubscribePlayer();
        }

        private void Update()
        {
            if (!combatActive || currentBoss == null)
            {
                return;
            }

            // 화면에 보이는 전투 타이머(BossAI.CombatElapsedSeconds)를 그대로 판정 기준으로 쓴다.
            // 마지막 목숨 처형 연출이 시작되면 이 값이 그 즉시 고정(FreezeCombatTimer)되므로,
            // 챌린지 판정이 끝나는 시점과 화면 타이머가 멈추는 시점이 항상 일치한다.
            float elapsedSeconds = currentBoss.CombatElapsedSeconds;
            for (int i = 0; i < activeRuns.Count; i++)
            {
                activeRuns[i].Run.OnTick(elapsedSeconds);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (combatActive)
            {
                LogQuitWithoutSaving("quit");
            }

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

            hitCountThisRun = 0;
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

        private void HandleParried(EnemyProjectile projectile)
        {
            if (!combatActive)
            {
                return;
            }

            for (int i = 0; i < activeRuns.Count; i++)
            {
                activeRuns[i].Run.OnParried(projectile);
            }
        }

        // 사유가 Intercepted(플레이어의 공격으로 파괴됨)인 경우만 전달합니다. Expired(수명 만료)나
        // OwnerDestroyed(보스가 강제 정리)는 플레이어가 파괴한 게 아니므로 챌린지에 넘기지 않습니다.
        private void HandleProjectileDestroyed(EnemyProjectile projectile, EnemyProjectileDestroyReason reason)
        {
            if (!combatActive || reason != EnemyProjectileDestroyReason.Intercepted)
            {
                return;
            }

            for (int i = 0; i < activeRuns.Count; i++)
            {
                activeRuns[i].Run.OnObjectDestroyed(projectile);
            }
        }

        private void HandleParryFailed(ParryBaitRewardProjectile bait)
        {
            if (!combatActive)
            {
                return;
            }

            for (int i = 0; i < activeRuns.Count; i++)
            {
                activeRuns[i].Run.OnParryFailed(bait);
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

            EvaluateAndSave(victory: true, combatResult: "clear");
        }

        private void HandleDefeat(Health _)
        {
            EvaluateAndSave(victory: false, combatResult: "death");
        }

        // 전투 중 로비 복귀/재시작 등으로 씬이 바뀌어 중단된 경우입니다. 보스 처치·플레이어 사망이
        // 아니므로 진행도를 세이브(GameSaveManager)에 반영하지 않고, 분석 로그만 남깁니다.
        private void LogQuitWithoutSaving(string combatResult)
        {
            for (int i = 0; i < activeRuns.Count; i++)
            {
                ChallengeDefinitionSO definition = activeRuns[i].Definition;
                AnalyticsManager.RecordBossChallengeResult(
                    currentBossId,
                    definition.ChallengeId,
                    ToAnalyticsChallengeType(definition.Kind),
                    false,
                    false,
                    combatResult);
            }

            foreach (ChallengeDefinitionSO definition in database.ForBoss(currentBossId))
            {
                string saveKey = GameSaveManager.BuildChallengeSaveKey(currentBossId, definition.ChallengeId);
                if (!alreadyCompletedBeforeRun.Contains(saveKey))
                {
                    continue;
                }

                AnalyticsManager.RecordBossChallengeResult(
                    currentBossId,
                    definition.ChallengeId,
                    ToAnalyticsChallengeType(definition.Kind),
                    false,
                    true,
                    combatResult);
            }
        }

        private void EvaluateAndSave(bool victory, string combatResult)
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
                bool achieved = run.TryFinalize(victory, saveKey);
                AnalyticsManager.RecordBossChallengeResult(
                    currentBossId,
                    definition.ChallengeId,
                    ToAnalyticsChallengeType(definition.Kind),
                    achieved,
                    false,
                    combatResult);

                if (achieved)
                {
                    GameSaveManager.CompleteChallenge(saveKey, definition.RewardPoint);
                    LastRunEarnedPoints += definition.RewardPoint;
                    ChallengeCompleted?.Invoke(currentBossId);
                }
            }

            foreach (ChallengeDefinitionSO definition in database.ForBoss(currentBossId))
            {
                string saveKey = GameSaveManager.BuildChallengeSaveKey(currentBossId, definition.ChallengeId);
                if (!alreadyCompletedBeforeRun.Contains(saveKey))
                {
                    continue;
                }

                AnalyticsManager.RecordBossChallengeResult(
                    currentBossId,
                    definition.ChallengeId,
                    ToAnalyticsChallengeType(definition.Kind),
                    false,
                    true,
                    combatResult);
            }

            activeRuns.Clear();
            combatActive = false;
        }

        private static string ToAnalyticsChallengeType(ChallengeType type)
        {
            return type switch
            {
                ChallengeType.PhaseReach => "phase_reach",
                ChallengeType.TimeAttack => "time_attack",
                ChallengeType.HitLimit => "hit_limit",
                ChallengeType.BossClear => "boss_clear",
                ChallengeType.ParryCount => "parry_count",
                ChallengeType.ParryClear => "parry_clear",
                ChallengeType.DestroyObjectCount => "destroy_object_count",
                ChallengeType.PerfectParry => "perfect_parry",
                _ => type.ToString().ToLowerInvariant()
            };
        }
    }
}
