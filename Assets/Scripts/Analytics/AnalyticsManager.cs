using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Week14.Combat;
using Week14.Enemy;
using Week14.Skills;
using Week14.Weapons;
#if UNITY_6000_2_OR_NEWER
using UnityEngine.UnityConsent;
#endif

namespace Week14.Analytics
{
    [DefaultExecutionOrder(-1000)]
    public sealed class AnalyticsManager : MonoBehaviour
    {
        private const string GameHeartbeatEventName = "game_heartbeat";
        private const string BossCombatStartedEventName = "boss_combat_started";
        private const string BossCombatEndedEventName = "boss_combat_ended";
        private const string BossPhaseResultEventName = "boss_phase_result";
        private const string BossChallengeResultEventName = "boss_challenge_result";
        private const string BossPatternResultEventName = "boss_pattern_result";
        private const string RuntimeTypeParameterName = "runtime_type";
        private const string SessionElapsedSecondsParameterName = "session_elapsed_sec";
        private const string EditorRuntimeType = "editor";
        private const string BuildRuntimeType = "build";
        private const string NoneValue = "none";
        private const string ClearResult = "clear";
        private const string DeathResult = "death";
        private const string QuitResult = "quit";
        private const float HeartbeatIntervalSeconds = 600f;

        private static AnalyticsManager instance;

        private Task<bool> initializationTask;
        private Task dataCollectionStartTask;
        private Coroutine heartbeatCoroutine;
        private double dataCollectionStartedAt;
        private BossAI activeBoss;
        private Health subscribedPlayerHealth;
        private string currentRunId;
        private string currentBossId;
        private string currentWeaponId;
        private string currentActiveSkillId;
        private string currentPassiveSkillId;
        private string currentPatternId;
        private int currentPatternPhaseNumber;
        private int currentPatternHitCount;
        private int currentPhaseNumber;
        private float currentPhaseStartedAt;
        private float lastKnownCombatElapsedSeconds;
        private bool combatActive;
        private bool phaseActive;
        private bool patternActive;
        private bool isReady;
        private bool missingConsentWarned;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            initializationTask = InitializeAsync();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            EnsureDataCollectionStarted();
#endif
        }

        private void OnEnable()
        {
            BossAI.CombatStarted += HandleBossCombatStarted;
            BossAI.Defeated += HandleBossDefeated;
            PlayerDamageReceiver.PlayerHitByEnemy += HandlePlayerHit;
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        }

        private void OnDisable()
        {
            BossAI.CombatStarted -= HandleBossCombatStarted;
            BossAI.Defeated -= HandleBossDefeated;
            PlayerDamageReceiver.PlayerHitByEnemy -= HandlePlayerHit;
            SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
            UnsubscribeCombatTargets();
        }

        private void Update()
        {
            if (!combatActive)
            {
                return;
            }

            if (activeBoss != null)
            {
                lastKnownCombatElapsedSeconds = activeBoss.CombatElapsedSeconds;
            }

            if (subscribedPlayerHealth == null)
            {
                SubscribeToPlayerDeath();
            }
        }

        private void OnDestroy()
        {
            if (instance == this)
            {
                instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            EndCombat(QuitResult);
            if (isReady)
            {
                RecordHeartbeat();
                AnalyticsService.Instance.Flush();
            }
        }

        public static void StartDataCollectionAfterConsent()
        {
            if (instance == null)
            {
                Debug.LogWarning($"[{nameof(AnalyticsManager)}] 매니저가 씬에 없어 데이터 수집을 시작하지 못했습니다.");
                return;
            }

            instance.EnsureDataCollectionStarted();
        }

        public static void BeginBossPattern(string patternId, int phaseNumber)
        {
            instance?.BeginPattern(patternId, phaseNumber);
        }

        public static void EndBossPattern(string patternId, int phaseNumber, bool completed)
        {
            instance?.FinishPattern(patternId, phaseNumber, completed);
        }

        public static void RecordBossChallengeResult(
            string bossId,
            string challengeId,
            string challengeType,
            bool completedThisRun,
            bool wasAlreadyCompleted,
            string combatResult)
        {
            instance?.RecordChallengeResult(
                bossId,
                challengeId,
                challengeType,
                completedThisRun,
                wasAlreadyCompleted,
                combatResult);
        }

        private async Task<bool> InitializeAsync()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }

                Debug.Log($"[{nameof(AnalyticsManager)}] Unity Services 초기화 완료");
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(AnalyticsManager)}] Unity Services 초기화 실패: {exception.Message}");
                return false;
            }
        }

        private void EnsureDataCollectionStarted()
        {
            dataCollectionStartTask ??= StartDataCollectionAsync();
        }

        private async Task StartDataCollectionAsync()
        {
            if (!await initializationTask)
            {
                return;
            }

            try
            {
#if UNITY_6000_2_OR_NEWER
                ConsentState consentState = EndUserConsent.GetConsentState();
                consentState.AnalyticsIntent = ConsentStatus.Granted;
                EndUserConsent.SetConsentState(consentState);
#else
                AnalyticsService.Instance.StartDataCollection();
#endif

                isReady = true;
                dataCollectionStartedAt = Time.realtimeSinceStartupAsDouble;
                heartbeatCoroutine ??= StartCoroutine(HeartbeatRoutine());
                Debug.Log($"[{nameof(AnalyticsManager)}] Unity Analytics 데이터 수집 시작");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(AnalyticsManager)}] Unity Analytics 데이터 수집 시작 실패: {exception.Message}");
            }
        }

        private void HandleBossCombatStarted(BossAI boss)
        {
            if (boss == null || boss.BossData == null)
            {
                return;
            }

            if (combatActive)
            {
                EndCombat(QuitResult);
            }

            activeBoss = boss;
            currentRunId = Guid.NewGuid().ToString("N");
            currentBossId = NormalizeId(boss.BossData.Id);
            currentWeaponId = NormalizeId(WeaponLoadoutManager.Instance?.CurrentWeapon?.WeaponId);
            currentActiveSkillId = NormalizeId(
                SkillLoadoutManager.Instance?.GetEquippedSkill(SkillSlot.Skill1)?.SkillId);
            currentPassiveSkillId = NormalizeId(
                PassiveSkillLoadoutManager.Instance?.GetEquippedSkill(PassiveSkillSlot.Passive1)?.SkillId);
            lastKnownCombatElapsedSeconds = boss.CombatElapsedSeconds;
            combatActive = true;

            activeBoss.LivesChanged += HandleBossLivesChanged;
            SubscribeToPlayerDeath();

            CustomEvent combatStartedEvent = CreateCombatEvent(BossCombatStartedEventName);
            combatStartedEvent.Add("weapon_id", currentWeaponId);
            combatStartedEvent.Add("active_skill_id", currentActiveSkillId);
            combatStartedEvent.Add("passive_skill_id", currentPassiveSkillId);

            RecordWhenReady(combatStartedEvent, BossCombatStartedEventName);
            BeginPhase(boss.CurrentPhaseNumber);
        }

        private void HandleBossDefeated(BossAI boss)
        {
            if (boss == activeBoss)
            {
                EndCombat(ClearResult);
            }
        }

        private void HandleBossLivesChanged(int currentLives, int _)
        {
            if (!combatActive || activeBoss == null)
            {
                return;
            }

            if (currentLives <= 0)
            {
                FinishPhase(ClearResult);
                return;
            }

            int nextPhaseNumber = activeBoss.CurrentPhaseNumber;
            if (nextPhaseNumber == currentPhaseNumber)
            {
                return;
            }

            FinishPhase(ClearResult);
            BeginPhase(nextPhaseNumber);
        }

        private void HandlePlayerDied(Health _)
        {
            EndCombat(DeathResult);
        }

        private void HandlePlayerHit(int _)
        {
            if (combatActive && patternActive)
            {
                currentPatternHitCount++;
            }
        }

        private void HandleActiveSceneChanged(Scene _, Scene __)
        {
            EndCombat(QuitResult);
        }

        private void SubscribeToPlayerDeath()
        {
            Health playerHealth = PlayerCombatController.Active != null
                ? PlayerCombatController.Active.Health
                : null;

            if (subscribedPlayerHealth == playerHealth)
            {
                return;
            }

            if (subscribedPlayerHealth != null)
            {
                subscribedPlayerHealth.Died -= HandlePlayerDied;
            }

            subscribedPlayerHealth = playerHealth;
            if (subscribedPlayerHealth != null)
            {
                subscribedPlayerHealth.Died += HandlePlayerDied;
            }
        }

        private void UnsubscribeCombatTargets()
        {
            if (activeBoss != null)
            {
                activeBoss.LivesChanged -= HandleBossLivesChanged;
            }

            if (subscribedPlayerHealth != null)
            {
                subscribedPlayerHealth.Died -= HandlePlayerDied;
            }

            activeBoss = null;
            subscribedPlayerHealth = null;
        }

        private void BeginPhase(int phaseNumber)
        {
            if (!combatActive || activeBoss == null)
            {
                return;
            }

            currentPhaseNumber = Mathf.Max(1, phaseNumber);
            currentPhaseStartedAt = ResolveCombatElapsedSeconds();
            phaseActive = true;
        }

        private void FinishPhase(string result)
        {
            if (!combatActive || !phaseActive)
            {
                return;
            }

            float duration = Mathf.Max(0f, ResolveCombatElapsedSeconds() - currentPhaseStartedAt);
            CustomEvent phaseResultEvent = CreateCombatEvent(BossPhaseResultEventName);
            phaseResultEvent.Add("phase_number", currentPhaseNumber);
            phaseResultEvent.Add("result", result);
            phaseResultEvent.Add("phase_duration_sec", duration);

            phaseActive = false;
            RecordWhenReady(phaseResultEvent, BossPhaseResultEventName);
        }

        private void EndCombat(string result)
        {
            if (!combatActive)
            {
                return;
            }

            FinishActivePattern(false);
            FinishPhase(result);

            float duration = ResolveCombatElapsedSeconds();
            CustomEvent combatEndedEvent = CreateCombatEvent(BossCombatEndedEventName);
            combatEndedEvent.Add("result", result);
            combatEndedEvent.Add("combat_duration_sec", Mathf.Max(0f, duration));
            combatEndedEvent.Add("reached_phase_number", Mathf.Max(1, currentPhaseNumber));
            combatEndedEvent.Add("weapon_id", currentWeaponId);
            combatEndedEvent.Add("active_skill_id", currentActiveSkillId);
            combatEndedEvent.Add("passive_skill_id", currentPassiveSkillId);

            combatActive = false;
            RecordWhenReady(combatEndedEvent, BossCombatEndedEventName, true);
            UnsubscribeCombatTargets();
        }

        private float ResolveCombatElapsedSeconds()
        {
            if (activeBoss != null)
            {
                lastKnownCombatElapsedSeconds = activeBoss.CombatElapsedSeconds;
            }

            return Mathf.Max(0f, lastKnownCombatElapsedSeconds);
        }

        private void BeginPattern(string patternId, int phaseNumber)
        {
            if (!combatActive || string.IsNullOrWhiteSpace(patternId))
            {
                return;
            }

            FinishActivePattern(false);
            currentPatternId = patternId;
            currentPatternPhaseNumber = Mathf.Max(1, phaseNumber);
            currentPatternHitCount = 0;
            patternActive = true;
        }

        private void FinishPattern(string patternId, int phaseNumber, bool completed)
        {
            if (!patternActive
                || currentPatternId != patternId
                || currentPatternPhaseNumber != Mathf.Max(1, phaseNumber))
            {
                return;
            }

            FinishActivePattern(completed);
        }

        private void FinishActivePattern(bool completed)
        {
            if (!combatActive || !patternActive)
            {
                return;
            }

            CustomEvent patternResultEvent = CreateCombatEvent(BossPatternResultEventName);
            patternResultEvent.Add("phase_number", currentPatternPhaseNumber);
            patternResultEvent.Add("pattern_id", currentPatternId);
            patternResultEvent.Add("pattern_completed", completed);
            patternResultEvent.Add("was_hit", currentPatternHitCount > 0);
            patternResultEvent.Add("hit_count", currentPatternHitCount);

            patternActive = false;
            RecordWhenReady(patternResultEvent, BossPatternResultEventName);
        }

        private void RecordChallengeResult(
            string bossId,
            string challengeId,
            string challengeType,
            bool completedThisRun,
            bool wasAlreadyCompleted,
            string combatResult)
        {
            if (string.IsNullOrEmpty(currentRunId) || NormalizeId(bossId) != currentBossId)
            {
                return;
            }

            CustomEvent challengeResultEvent = CreateCombatEvent(BossChallengeResultEventName);
            challengeResultEvent.Add("challenge_id", NormalizeId(challengeId));
            challengeResultEvent.Add("challenge_type", NormalizeId(challengeType));
            challengeResultEvent.Add("completed_this_run", completedThisRun);
            challengeResultEvent.Add("was_already_completed", wasAlreadyCompleted);
            challengeResultEvent.Add("is_completed", completedThisRun || wasAlreadyCompleted);
            challengeResultEvent.Add("combat_result", NormalizeId(combatResult));

            RecordWhenReady(challengeResultEvent, BossChallengeResultEventName);
        }

        private CustomEvent CreateRuntimeEvent(string eventName)
        {
            return new CustomEvent(eventName)
            {
                { RuntimeTypeParameterName, GetRuntimeType() }
            };
        }

        private CustomEvent CreateCombatEvent(string eventName)
        {
            CustomEvent analyticsEvent = CreateRuntimeEvent(eventName);
            analyticsEvent.Add("run_id", NormalizeId(currentRunId));
            analyticsEvent.Add("boss_id", NormalizeId(currentBossId));
            return analyticsEvent;
        }

        private async void RecordWhenReady(
            CustomEvent analyticsEvent,
            string eventName,
            bool forceFlush = false)
        {
            try
            {
                if (dataCollectionStartTask == null)
                {
                    if (!missingConsentWarned)
                    {
                        missingConsentWarned = true;
                        Debug.LogWarning($"[{nameof(AnalyticsManager)}] 데이터 수집 동의가 적용되지 않아 이벤트를 기록하지 않았습니다.");
                    }

                    return;
                }

                await dataCollectionStartTask;
                if (!isReady)
                {
                    return;
                }

                AnalyticsService.Instance.RecordEvent(analyticsEvent);
                if (forceFlush)
                {
                    AnalyticsService.Instance.Flush();
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[{nameof(AnalyticsManager)}] 이벤트 기록: {eventName}");
#endif
            }
            catch (Exception exception)
            {
                Debug.LogError($"[{nameof(AnalyticsManager)}] 이벤트 기록 실패 ({eventName}): {exception.Message}");
            }
        }

        private IEnumerator HeartbeatRoutine()
        {
            WaitForSecondsRealtime interval = new WaitForSecondsRealtime(HeartbeatIntervalSeconds);

            while (isReady)
            {
                yield return interval;

                if (isReady)
                {
                    RecordHeartbeat();
                }
            }
        }

        private void RecordHeartbeat()
        {
            int sessionElapsedSeconds = (int)(Time.realtimeSinceStartupAsDouble - dataCollectionStartedAt);
            CustomEvent heartbeatEvent = CreateRuntimeEvent(GameHeartbeatEventName);
            heartbeatEvent.Add(SessionElapsedSecondsParameterName, sessionElapsedSeconds);

            RecordWhenReady(heartbeatEvent, GameHeartbeatEventName, true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[{nameof(AnalyticsManager)}] 하트비트 기록: {sessionElapsedSeconds}초");
#endif
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? NoneValue : value;
        }

        private static string GetRuntimeType()
        {
            return Application.isEditor ? EditorRuntimeType : BuildRuntimeType;
        }
    }
}
