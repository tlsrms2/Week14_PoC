using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Week14.Audio;
using Week14.Combat;
using Week14.Input;
using Week14.Save;
using Week14.Skills;
using Week14.GameFlow;
using Week14.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Tutorial
{
    public sealed class TutorialSceneController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private TutorialDialogueSetSO dialogueSet;

        [Header("UI")]
        [SerializeField] private TutorialDialoguePanelView dialoguePanel;
        [SerializeField, Min(0f)] private float firstDialogueDelaySeconds = 1f;
        [SerializeField] private GameObject bossCombatUiRoot;
        [SerializeField] private BossBulletBarView bossHpBarView;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private string trainingEnemyName = "훈련 몹";

        [Header("Scene")]
        [SerializeField] private Transform player;
        [SerializeField] private TutorialTrainingEnemy sceneTrainingEnemy;
        [SerializeField] private TutorialTrainingEnemy trainingEnemyPrefab;
        [SerializeField] private Transform enemySpawnPoint;
        [SerializeField] private bool hideSceneEnemyUntilTraining = true;
        [SerializeField] private bool markSaveOnComplete = true;
        [SerializeField] private bool returnToLobbyOnComplete = true;
        [SerializeField] private string lobbySceneName = "LobbyScene";
        [SerializeField] private UnityEvent completed;

        [Header("Goals")]
        [SerializeField, Min(0.1f)] private float moveDistanceGoal = 3f;
        [SerializeField, Min(1)] private int attackHitGoal = 3;
        [SerializeField, Min(1)] private int parryGoal = 3;
        [SerializeField, Min(1)] private int skillGoal = 1;
        [SerializeField, Min(0.1f)] private float skillAttemptResolveSeconds = 2.5f;

        private TutorialTrainingEnemy activeEnemy;
        private TutorialTrainingEnemy spawnedEnemy;
        private SkillLoadoutManager subscribedSkillManager;
        private BulletGauge subscribedPlayerBullets;
        private Health subscribedPlayerHealth;
        private TutorialStepId activeStep;
        private int moveCount;
        private int shootCount;
        private int attackHitCount;
        private int parryCount;
        private int skillCount;
        private int duelDefeatCount;
        private int previousPlayerBulletCount;
        private float moveDistance;
        private Vector2 previousMovePosition;
        private bool hasPreviousMovePosition;
        private bool attackRefillRequested;
        private bool combatPermissionPushed;
        private bool bulletTimeoutLockPushed;
        private bool dialogueAdvanceInputPushed;
        private bool skillAttemptRunning;
        private bool skillUsedThisAttempt;
        private bool skillHitThisAttempt;
        private bool initialMovementLockReleased;
        private bool completionInvulnerabilityPushed;
        private PlayerCombatController initialMovementLockedPlayer;
        private Coroutine tutorialRoutine;
        private Coroutine deathRoutine;

        private void OnEnable()
        {
            PlayerProjectile.NormalAttackDamageDealt += HandleNormalAttackDamageDealt;
            PlayerParryController.ProjectileParried += HandleProjectileParried;
            PlayerCombatController.AttackReceived += HandlePlayerAttackReceived;
            PlayerCombatController.PushExternalCombatPermission();
            combatPermissionPushed = true;
            PlayerHP.PushBulletTimeoutLock();
            bulletTimeoutLockPushed = true;
            TrySubscribeSkillManager();
            TrySubscribePlayerBullets();
            TrySubscribePlayerHealth();
        }

        private void OnDisable()
        {
            PlayerProjectile.NormalAttackDamageDealt -= HandleNormalAttackDamageDealt;
            PlayerParryController.ProjectileParried -= HandleProjectileParried;
            PlayerCombatController.AttackReceived -= HandlePlayerAttackReceived;
            UnsubscribeSkillManager();
            UnsubscribePlayerBullets();
            UnsubscribePlayerHealth();
            ClearEnemySubscription();
            SetBossUiVisible(false);
            PopDialogueAdvanceInput();
            ReleaseInitialMovementLock();
            PopCompletionInvulnerability();

            if (combatPermissionPushed)
            {
                PlayerCombatController.PopExternalCombatPermission();
                combatPermissionPushed = false;
            }

            ReleaseBulletTimeoutLock(false);

            if (tutorialRoutine != null)
            {
                StopCoroutine(tutorialRoutine);
                tutorialRoutine = null;
            }

            if (deathRoutine != null)
            {
                StopCoroutine(deathRoutine);
                deathRoutine = null;
                Time.timeScale = 1f;
            }

            if (spawnedEnemy != null)
            {
                Destroy(spawnedEnemy.gameObject);
                spawnedEnemy = null;
            }
        }

        private void Start()
        {
            if (sceneTrainingEnemy != null && hideSceneEnemyUntilTraining)
            {
                sceneTrainingEnemy.gameObject.SetActive(false);
            }

            dialoguePanel?.Hide();
            SetBossUiVisible(false);
            TryPushInitialMovementLock();
            tutorialRoutine = StartCoroutine(RunTutorial());
        }

        private void Update()
        {
            TryPushInitialMovementLock();
            TrySubscribeSkillManager();
            TrySubscribePlayerBullets();
            TrySubscribePlayerHealth();
        }

        private IEnumerator RunTutorial()
        {
            yield return WaitUnscaled(firstDialogueDelaySeconds);
            yield return PlayDialogue(TutorialStepId.Intro);
            yield return RunObjectiveStage(TutorialStepId.Move, TutorialTrainingEnemyMode.Passive, 1);
            yield return RunObjectiveStage(TutorialStepId.Shoot, TutorialTrainingEnemyMode.Passive, 1);
            yield return RunObjectiveStage(TutorialStepId.Attack, TutorialTrainingEnemyMode.AttackTarget, attackHitGoal);
            yield return RunObjectiveStage(TutorialStepId.Parry, TutorialTrainingEnemyMode.ParryPractice, parryGoal);
            activeEnemy?.Deactivate();
            EnemyProjectile.DestroyAllActive();
            yield return PlayDialogue(TutorialStepId.BulletTimeout);
            ReleaseBulletTimeoutLock(true);
            yield return PlayDialogue(TutorialStepId.SkillGauge);
            yield return RunObjectiveStage(TutorialStepId.Skill, TutorialTrainingEnemyMode.DodgePractice, skillGoal);
            yield return RunObjectiveStage(TutorialStepId.Duel, TutorialTrainingEnemyMode.Duel, 1);
            yield return PlayDialogue(TutorialStepId.Complete);
            if (dialoguePanel != null)
            {
                yield return dialoguePanel.HideAnimated();
            }

            CompleteTutorial();
            tutorialRoutine = null;
        }

        private IEnumerator RunObjectiveStage(TutorialStepId step, TutorialTrainingEnemyMode enemyMode, int goal)
        {
            activeStep = step;
            ResetStepProgress(step);
            ActivateTrainingEnemy(TutorialTrainingEnemyMode.Passive);
            SetBossUiVisible(false);
            if (step == TutorialStepId.Move)
            {
                ReleaseInitialMovementLock();
            }

            yield return PlayDialogue(step);

            int safeGoal = Mathf.Max(1, goal);
            if (step == TutorialStepId.Skill)
            {
                yield return RunSkillObjectiveStage(enemyMode, safeGoal);
                yield break;
            }

            if (step == TutorialStepId.Duel)
            {
                RestorePlayerResources(true);
            }

            ActivateTrainingEnemy(enemyMode);
            SetBossUiVisible(step == TutorialStepId.Duel);
            if (step == TutorialStepId.Shoot || step == TutorialStepId.Attack)
            {
                RestorePlayerBullets();
            }

            ShowObjective(step, safeGoal);
            while (GetStepProgress(step) < safeGoal)
            {
                if (step == TutorialStepId.Attack && attackRefillRequested)
                {
                    yield return RefillAttackAmmo();
                }

                TickStepProgress(step);
                ShowObjective(step, safeGoal);
                yield return null;
            }

            if (dialoguePanel != null)
            {
                yield return dialoguePanel.PlayObjectiveCompleted(FormatObjective(step, safeGoal, safeGoal));
            }
        }

        private IEnumerator RunSkillObjectiveStage(TutorialTrainingEnemyMode enemyMode, int goal)
        {
            SetBossUiVisible(false);
            RestorePlayerResources(true);
            ShowObjective(TutorialStepId.Skill, goal);

            while (skillCount < goal)
            {
                RestorePlayerResources(true);
                skillUsedThisAttempt = false;
                skillHitThisAttempt = false;
                skillAttemptRunning = true;

                ActivateTrainingEnemy(enemyMode);

                for (float elapsed = 0f; elapsed < skillAttemptResolveSeconds && !skillHitThisAttempt; elapsed += Time.deltaTime)
                {
                    yield return null;
                }

                skillAttemptRunning = false;

                if (skillUsedThisAttempt && !skillHitThisAttempt)
                {
                    skillCount = goal;
                    ShowObjective(TutorialStepId.Skill, goal);
                    EnemyProjectile.DestroyAllActive();
                    break;
                }

                skillCount = 0;
                ShowObjective(TutorialStepId.Skill, goal);
                EnemyProjectile.DestroyAllActive();
                RestorePlayerResources(true);
                yield return PlayDialogue(TutorialStepId.SkillRetry);
                ShowObjective(TutorialStepId.Skill, goal);
            }

            if (dialoguePanel != null)
            {
                yield return dialoguePanel.PlayObjectiveCompleted(FormatObjective(TutorialStepId.Skill, goal, goal));
            }
        }

        private IEnumerator PlayDialogue(TutorialStepId step)
        {
            TutorialStepContent content = dialogueSet != null ? dialogueSet.GetStep(step) : null;
            if (content == null || dialoguePanel == null)
            {
                Debug.LogWarning($"{nameof(TutorialSceneController)}: {step} dialogue is missing in TutorialDialogueSet.");
                yield break;
            }

            yield return PlayDialogueLines(content);
        }

        private IEnumerator PlayDialogueLines(TutorialStepContent content)
        {
            PushDialogueAdvanceInput();
            for (int i = 0; i < content.Dialogues.Count; i++)
            {
                TutorialDialogueLine line = content.Dialogues[i];
                if (line == null)
                {
                    continue;
                }

                yield return PlayDialogueLine(line.Speaker, line.Text, line.SfxId);
            }

            PopDialogueAdvanceInput();
        }

        private IEnumerator PlayDialogueLine(string speaker, string text, string sfxId)
        {
            bool revealRequested = false;
            bool canAcceptAdvance = false;
            PlayDialogueSfx(sfxId);
            dialoguePanel.ShowLine(speaker, text);
            IEnumerator typing = dialoguePanel.PlayTypewriter(text, () => revealRequested);
            while (typing.MoveNext())
            {
                if (canAcceptAdvance && AdvancePressed())
                {
                    revealRequested = true;
                }

                yield return typing.Current;
                canAcceptAdvance = true;
            }

            yield return null;
            while (!AdvancePressed())
            {
                yield return null;
            }
        }

        private static void PlayDialogueSfx(string sfxId)
        {
            if (!string.IsNullOrWhiteSpace(sfxId))
            {
                SoundManager.PlaySfx(sfxId);
            }
        }

        private IEnumerator RefillAttackAmmo()
        {
            attackRefillRequested = false;
            RestorePlayerBullets();
            yield return PlayDialogue(TutorialStepId.AttackRefill);
        }

        private void ActivateTrainingEnemy(TutorialTrainingEnemyMode mode)
        {
            TutorialTrainingEnemy enemy = EnsureTrainingEnemy();
            Transform playerTarget = ResolvePlayer();
            if (enemy == null || playerTarget == null)
            {
                return;
            }

            enemy.Activate(playerTarget, mode);
            if (mode == TutorialTrainingEnemyMode.Duel)
            {
                enemy.Defeated += HandleTrainingEnemyDefeated;
            }
        }

        private TutorialTrainingEnemy EnsureTrainingEnemy()
        {
            if (activeEnemy != null)
            {
                return activeEnemy;
            }

            if (trainingEnemyPrefab != null)
            {
                Vector3 position = enemySpawnPoint != null ? enemySpawnPoint.position : transform.position;
                Quaternion rotation = enemySpawnPoint != null ? enemySpawnPoint.rotation : Quaternion.identity;
                spawnedEnemy = Instantiate(trainingEnemyPrefab, position, rotation);
                activeEnemy = spawnedEnemy;
            }
            else
            {
                activeEnemy = sceneTrainingEnemy;
                if (activeEnemy != null && enemySpawnPoint != null)
                {
                    activeEnemy.transform.SetPositionAndRotation(enemySpawnPoint.position, enemySpawnPoint.rotation);
                }
            }

            return activeEnemy;
        }

        private Transform ResolvePlayer()
        {
            if (player != null)
            {
                return player;
            }

            PlayerCombatController activePlayer = PlayerCombatController.Active;
            player = activePlayer != null ? activePlayer.transform : null;
            return player;
        }

        private void TickStepProgress(TutorialStepId step)
        {
            if (step == TutorialStepId.Move)
            {
                TickMoveDistance();
            }
        }

        private int GetStepProgress(TutorialStepId step)
        {
            return step switch
            {
                TutorialStepId.Move => moveCount,
                TutorialStepId.Shoot => shootCount,
                TutorialStepId.Attack => attackHitCount,
                TutorialStepId.Parry => parryCount,
                TutorialStepId.Skill => skillCount,
                TutorialStepId.Duel => duelDefeatCount,
                _ => 0
            };
        }

        private void ResetStepProgress(TutorialStepId step)
        {
            if (step == TutorialStepId.Move)
            {
                moveCount = 0;
                moveDistance = 0f;
                hasPreviousMovePosition = TryGetPlayerPosition(out previousMovePosition);
            }
            else if (step == TutorialStepId.Attack)
            {
                attackHitCount = 0;
                attackRefillRequested = false;
            }
            else if (step == TutorialStepId.Shoot)
            {
                shootCount = 0;
            }
            else if (step == TutorialStepId.Parry)
            {
                parryCount = 0;
            }
            else if (step == TutorialStepId.Skill)
            {
                skillCount = 0;
                skillAttemptRunning = false;
                skillUsedThisAttempt = false;
                skillHitThisAttempt = false;
            }
            else if (step == TutorialStepId.Duel)
            {
                ClearEnemySubscription();
                duelDefeatCount = 0;
            }
        }

        private string FormatObjective(TutorialStepId step, int current, int goal)
        {
            TutorialStepContent content = dialogueSet != null ? dialogueSet.GetStep(step) : null;
            string format = content != null ? content.ObjectiveFormat : string.Empty;
            if (string.IsNullOrWhiteSpace(format))
            {
                format = "{0}/{1}";
            }

            return format
                .Replace("{0}", Mathf.Clamp(current, 0, goal).ToString())
                .Replace("{1}", goal.ToString());
        }

        private void ShowObjective(TutorialStepId step, int goal)
        {
            dialoguePanel?.ShowObjective(FormatObjective(step, GetStepProgress(step), goal));
        }

        private void TickMoveDistance()
        {
            if (!TryGetPlayerPosition(out Vector2 currentPosition))
            {
                return;
            }

            if (!hasPreviousMovePosition)
            {
                previousMovePosition = currentPosition;
                hasPreviousMovePosition = true;
                return;
            }

            float delta = Vector2.Distance(previousMovePosition, currentPosition);
            previousMovePosition = currentPosition;
            if (delta <= 0.001f)
            {
                return;
            }

            float goal = Mathf.Max(0.1f, moveDistanceGoal);
            moveDistance = Mathf.Min(goal, moveDistance + delta);
            moveCount = moveDistance >= goal ? 1 : 0;
        }

        private bool TryGetPlayerPosition(out Vector2 position)
        {
            Transform playerTransform = ResolvePlayer();
            if (playerTransform == null)
            {
                position = Vector2.zero;
                return false;
            }

            position = playerTransform.position;
            return true;
        }

        private void SetBossUiVisible(bool visible)
        {
            if (bossCombatUiRoot != null)
            {
                bossCombatUiRoot.SetActive(visible);
            }

            if (!visible)
            {
                bossHpBarView?.SetTarget(null);
                return;
            }

            bossHpBarView?.SetTarget(activeEnemy != null ? activeEnemy.Bullets : null);
            if (bossNameText != null)
            {
                bossNameText.text = trainingEnemyName;
            }
        }

        private void CompleteTutorial()
        {
            activeStep = TutorialStepId.Complete;
            SetBossUiVisible(false);
            activeEnemy?.Deactivate();
            EnemyProjectile.DestroyAllActive();

            if (markSaveOnComplete)
            {
                GameSaveManager.MarkTutorialCompleted();
            }

            if (returnToLobbyOnComplete)
            {
                GameFlowController.ReturnToLobby(lobbySceneName);
                return;
            }

            completed?.Invoke();
        }

        private void TrySubscribeSkillManager()
        {
            SkillLoadoutManager nextManager = SkillLoadoutManager.Instance;
            if (subscribedSkillManager == nextManager)
            {
                return;
            }

            UnsubscribeSkillManager();
            subscribedSkillManager = nextManager;
            if (subscribedSkillManager != null)
            {
                subscribedSkillManager.SkillUsed += HandleSkillUsed;
            }
        }

        private void UnsubscribeSkillManager()
        {
            if (subscribedSkillManager == null)
            {
                return;
            }

            subscribedSkillManager.SkillUsed -= HandleSkillUsed;
            subscribedSkillManager = null;
        }

        private void TrySubscribePlayerBullets()
        {
            PlayerCombatController activePlayer = PlayerCombatController.Active;
            BulletGauge nextBullets = activePlayer != null ? activePlayer.Bullets : null;
            if (subscribedPlayerBullets == nextBullets)
            {
                return;
            }

            UnsubscribePlayerBullets();
            subscribedPlayerBullets = nextBullets;
            if (subscribedPlayerBullets != null)
            {
                previousPlayerBulletCount = subscribedPlayerBullets.CurrentBullets;
                subscribedPlayerBullets.Changed += HandlePlayerBulletsChanged;
                subscribedPlayerBullets.Emptied += HandlePlayerBulletsEmptied;
            }
        }

        private void UnsubscribePlayerBullets()
        {
            if (subscribedPlayerBullets == null)
            {
                return;
            }

            subscribedPlayerBullets.Emptied -= HandlePlayerBulletsEmptied;
            subscribedPlayerBullets.Changed -= HandlePlayerBulletsChanged;
            subscribedPlayerBullets = null;
        }

        private void TrySubscribePlayerHealth()
        {
            PlayerCombatController activePlayer = PlayerCombatController.Active;
            Health nextHealth = activePlayer != null ? activePlayer.Health : null;
            if (subscribedPlayerHealth == nextHealth)
            {
                return;
            }

            UnsubscribePlayerHealth();
            subscribedPlayerHealth = nextHealth;
            if (subscribedPlayerHealth != null)
            {
                subscribedPlayerHealth.Died += HandlePlayerDied;
            }
        }

        private void UnsubscribePlayerHealth()
        {
            if (subscribedPlayerHealth == null)
            {
                return;
            }

            subscribedPlayerHealth.Died -= HandlePlayerDied;
            subscribedPlayerHealth = null;
        }

        private void RestorePlayerBullets()
        {
            TrySubscribePlayerBullets();
            if (subscribedPlayerBullets == null)
            {
                return;
            }

            subscribedPlayerBullets.Restore(subscribedPlayerBullets.MaxBullets, BulletChangeSource.CombatStart);
            PlayerHP.ResetCurrentBulletTimeouts();
        }

        private void RestorePlayerResources(bool restoreSkill)
        {
            PlayerCombatController activePlayer = PlayerCombatController.Active;
            activePlayer?.Health?.Revive();
            RestorePlayerBullets();

            if (restoreSkill)
            {
                SkillLoadoutManager.Instance?.ResetActiveCooldown();
            }
        }

        private void ReleaseBulletTimeoutLock(bool startCurrentBulletTimers)
        {
            if (!bulletTimeoutLockPushed)
            {
                return;
            }

            PlayerHP.PopBulletTimeoutLock(startCurrentBulletTimers);
            bulletTimeoutLockPushed = false;
        }

        private void ClearEnemySubscription()
        {
            if (activeEnemy == null)
            {
                return;
            }

            activeEnemy.Defeated -= HandleTrainingEnemyDefeated;
        }

        private void HandleNormalAttackDamageDealt(int _)
        {
            if (activeStep == TutorialStepId.Attack)
            {
                attackHitCount++;
            }
        }

        private void HandlePlayerBulletsEmptied(BulletGauge _)
        {
            if (activeStep == TutorialStepId.Attack && attackHitCount < attackHitGoal)
            {
                attackRefillRequested = true;
            }
        }

        private void HandlePlayerBulletsChanged(int current, int _)
        {
            BulletChangeSource source = subscribedPlayerBullets != null
                ? subscribedPlayerBullets.LastChangeSource
                : BulletChangeSource.None;
            if (activeStep == TutorialStepId.Shoot
                && source == BulletChangeSource.Attack
                && current < previousPlayerBulletCount)
            {
                shootCount = 1;
            }

            previousPlayerBulletCount = current;
        }

        private void HandleProjectileParried()
        {
            if (activeStep == TutorialStepId.Parry)
            {
                parryCount++;
            }
        }

        private void HandleSkillUsed(SkillSlot _, BaseSkillSO __)
        {
            if (activeStep == TutorialStepId.Skill && skillAttemptRunning)
            {
                skillUsedThisAttempt = true;
            }
        }

        private void HandleTrainingEnemyDefeated(TutorialTrainingEnemy _)
        {
            if (activeStep == TutorialStepId.Duel)
            {
                PushCompletionInvulnerability();
                SetBossUiVisible(false);
                activeEnemy?.Deactivate();
                EnemyProjectile.DestroyAllActive();
                duelDefeatCount = 1;
            }
        }

        private void HandlePlayerAttackReceived(PlayerCombatController player)
        {
            if (activeStep == TutorialStepId.Skill
                && skillAttemptRunning
                && player == PlayerCombatController.Active)
            {
                skillHitThisAttempt = true;
                EnemyProjectile.DestroyAllActive();
            }
        }

        private void HandlePlayerDied(Health _)
        {
            if (deathRoutine != null)
            {
                return;
            }

            deathRoutine = StartCoroutine(RestartTutorialAfterDeath());
        }

        private IEnumerator RestartTutorialAfterDeath()
        {
            if (tutorialRoutine != null)
            {
                StopCoroutine(tutorialRoutine);
                tutorialRoutine = null;
            }

            PopDialogueAdvanceInput();
            ReleaseInitialMovementLock();
            dialoguePanel?.Hide();

            yield return PlayerDeathSequence.Play(PlayerCombatController.Active);

            SetBossUiVisible(false);
            activeEnemy?.Deactivate();
            EnemyProjectile.DestroyAllActive();
            GameFlowController.RestartCurrentScene();
        }

        private void PushCompletionInvulnerability()
        {
            if (completionInvulnerabilityPushed)
            {
                return;
            }

            PlayerCombatController.PushExternalInvulnerability();
            completionInvulnerabilityPushed = true;
        }

        private void PopCompletionInvulnerability()
        {
            if (!completionInvulnerabilityPushed)
            {
                return;
            }

            PlayerCombatController.PopExternalInvulnerability();
            completionInvulnerabilityPushed = false;
        }

        private void TryPushInitialMovementLock()
        {
            if (initialMovementLockReleased || initialMovementLockedPlayer != null)
            {
                return;
            }

            PlayerCombatController activePlayer = PlayerCombatController.Active;
            if (activePlayer == null)
            {
                return;
            }

            activePlayer.PushExternalMovementLock();
            initialMovementLockedPlayer = activePlayer;
        }

        private void ReleaseInitialMovementLock()
        {
            initialMovementLockReleased = true;
            if (initialMovementLockedPlayer == null)
            {
                return;
            }

            initialMovementLockedPlayer.PopExternalMovementLock();
            initialMovementLockedPlayer = null;
        }

        private void PushDialogueAdvanceInput()
        {
            if (dialogueAdvanceInputPushed)
            {
                return;
            }

            PlayerCombatController.PushLeftAttackSuppression();
            dialogueAdvanceInputPushed = true;
        }

        private void PopDialogueAdvanceInput()
        {
            if (!dialogueAdvanceInputPushed)
            {
                return;
            }

            PlayerCombatController.PopLeftAttackSuppression();
            dialogueAdvanceInputPushed = false;
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            for (float elapsed = 0f; elapsed < seconds; elapsed += Time.unscaledDeltaTime)
            {
                yield return null;
            }
        }

        private static bool AdvancePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return GameInput.LeftAttackDown
                || (mouse != null && mouse.leftButton.wasPressedThisFrame);
#else
            return Input.GetMouseButtonDown(0);
#endif
        }
    }
}
