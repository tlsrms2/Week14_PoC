using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.Video;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.Enemy;
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
        private const string TextPanelRootName = "TextPanelRoot";
        private const string ObjectivePanelRootName = "ObjectivePanelRoot";
        private const string TutorialMapRootName = "TutorialMap";
        private const string MoveDestinationName = "Move Destination";
        private const string TrapdoorLeftName = "Trapdoor-L";
        private const string TrapdoorRightName = "Trapdoor-R";
        private const string UnderfloorName = "underfloor";
        private const string BossNameTable = "Boss";
        private const string TrainingEnemyNameKey = "trainingbot.name";

        [Header("Data")]
        [SerializeField] private TutorialDialogueSetSO dialogueSet;

        [Header("UI")]
        [SerializeField, FormerlySerializedAs("dialoguePanel")] private TutorialDialoguePanelView textDialoguePanel;
        [SerializeField] private TutorialDialoguePanelView objectiveDialoguePanel;
        [SerializeField] private GameObject explanationPanelRoot;
        [SerializeField] private CanvasGroup explanationPanelCanvasGroup;
        [SerializeField] private Image explanationImage;
        [SerializeField] private RawImage explanationVideoImage;
        [SerializeField] private VideoPlayer explanationVideoPlayer;
        [SerializeField] private TMP_Text explanationTitle;
        [SerializeField] private TMP_Text explanationText;
        [SerializeField] private Button explanationCloseButton;
        [SerializeField, Min(0f)] private float firstDialogueDelaySeconds = 1f;
        [SerializeField, Min(0f)] private float objectiveCompleteResumeDelaySeconds = 0.25f;
        [SerializeField] private GameObject bossCombatUiRoot;
        [SerializeField] private BossBulletBarView bossHpBarView;
        [SerializeField] private TMP_Text bossNameText;
        [SerializeField] private string trainingEnemyName = "훈련 몹";
        [SerializeField] private LocalizedString localizedTrainingEnemyName = new(BossNameTable, TrainingEnemyNameKey);

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

        [Header("Training Enemy Summon")]
        [SerializeField] private Transform trapdoorLeft;
        [SerializeField] private Transform trapdoorRight;
        [SerializeField] private Transform underfloor;
        [SerializeField] private Vector3 trapdoorLowerLocalOffset = new(0f, -1f, 0f);
        [SerializeField] private Vector3 underfloorRaiseLocalOffset = new(0f, 2f, 0f);
        [SerializeField, Min(0f)] private float trapdoorLowerSeconds = 0.45f;
        [SerializeField, Min(0f)] private float underfloorRaiseSeconds = 0.8f;
        [SerializeField, Min(0f)] private float summonSettleSeconds = 0.15f;

        [Header("Move Tutorial")]
        [SerializeField] private Transform moveDestination;
        [SerializeField, Min(0.01f)] private float moveDestinationBlinkSeconds = 0.8f;
        [SerializeField, Range(0f, 1f)] private float moveDestinationMinAlpha = 0f;
        [SerializeField, Range(0f, 1f)] private float moveDestinationMaxAlpha = 1f;
        [SerializeField, Min(0f)] private float moveDestinationFallbackRadius = 0.5f;

        [Header("Respawn")]
        [SerializeField] private Transform firstRoomRespawnPoint;

        [Header("Audio")]
        [Tooltip("튜토리얼 진입 및 사망 후 재시작 시 재생할 BGM입니다.")]
        [SerializeField, BossGraphBgmId] private string tutorialBgmId = "CutSceneBGM";
        [SerializeField, Min(0f)] private float tutorialBgmFadeSeconds = 0.5f;

        [Header("Goals")]
        [SerializeField, Min(1)] private int attackHitGoal = 3;
        [SerializeField, Min(1)] private int hitGoal = 1;
        [SerializeField, Min(1)] private int parryGoal = 3;
        [SerializeField, Min(1)] private int skillGoal = 1;
        [SerializeField, Min(1)] private int suppressGoal = 2;
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
        private int hitCount;
        private int parryCount;
        private int skillCount;
        private int suppressCount;
        private int duelDefeatCount;
        private int previousPlayerBulletCount;
        private bool attackRefillRequested;
        private bool combatPermissionPushed;
        private bool bulletTimeoutLockPushed;
        private bool dialogueAdvanceInputPushed;
        private bool dialogueSkillSuppressionPushed;
        private bool preLeftAttackSuppressionPushed;
        private bool preSkillSuppressionPushed;
        private bool preParrySuppressionPushed;
        private bool hitParrySuppressionPushed;
        private bool explanationLeftAttackSuppressionPushed;
        private bool explanationParrySuppressionPushed;
        private bool explanationSkillSuppressionPushed;
        private bool skillAttemptRunning;
        private bool skillUsedThisAttempt;
        private bool skillHitThisAttempt;
        private bool bossUiVisible;
        private TutorialDialogueLine currentTextDialogueLine;
        private bool currentTextDialogueRevealRequestedByLocale;
        private TutorialExplanationContent currentExplanationContent;
        private bool initialMovementLockReleased;
        private bool completionInvulnerabilityPushed;
        private bool trainingEnemySummoned;
        private bool summonStartPoseCached;
        private Vector3 trapdoorLeftClosedLocalPosition;
        private Vector3 trapdoorRightClosedLocalPosition;
        private Vector3 underfloorHiddenLocalPosition;
        private SpriteRenderer[] moveDestinationRenderers;
        private Color[] moveDestinationBaseColors;
        private Collider2D[] moveDestinationColliders;
        private BoxCollider2D trapdoorLeftBarrier;
        private BoxCollider2D trapdoorRightBarrier;
        private PlayerCombatController initialMovementLockedPlayer;
        private PlayerCombatController summonMovementLockedPlayer;
        private PlayerCombatController dialogueMovementLockedPlayer;
        private PlayerCombatController explanationMovementLockedPlayer;
        private Coroutine tutorialRoutine;
        private Coroutine deathRoutine;
        private Coroutine explanationVideoRoutine;
        private Coroutine moveDestinationBlinkRoutine;
        private bool explanationCloseRequested;
        private bool explanationCursorPushed;

        private void Awake()
        {
            ResolveDialoguePanels();
        }

        private void OnEnable()
        {
            if (explanationCloseButton != null)
            {
                explanationCloseButton.onClick.AddListener(HandleExplanationCloseButtonClicked);
            }

            PlayerProjectile.NormalAttackDamageDealt += HandleNormalAttackDamageDealt;
            PlayerParryController.ProjectileParried += HandleProjectileParried;
            PlayerCombatController.AttackReceived += HandlePlayerAttackReceived;
            PlayerCombatController.PushExternalCombatPermission();
            combatPermissionPushed = true;
            PushPreLeftAttackSuppression();
            PushPreSkillSuppression();
            PushPreParrySuppression();
            PlayerHP.PushBulletTimeoutLock();
            bulletTimeoutLockPushed = true;
            TrySubscribeSkillManager();
            TrySubscribePlayerBullets();
            TrySubscribePlayerHealth();
            BindTrainingEnemyName();
            LocalizationSettings.SelectedLocaleChanged += HandleSelectedLocaleChanged;
        }

        private void OnDisable()
        {
            if (explanationCloseButton != null)
            {
                explanationCloseButton.onClick.RemoveListener(HandleExplanationCloseButtonClicked);
            }

            PlayerProjectile.NormalAttackDamageDealt -= HandleNormalAttackDamageDealt;
            PlayerParryController.ProjectileParried -= HandleProjectileParried;
            PlayerCombatController.AttackReceived -= HandlePlayerAttackReceived;
            UnsubscribeSkillManager();
            UnsubscribePlayerBullets();
            UnsubscribePlayerHealth();
            ClearEnemySubscription();
            SetBossUiVisible(false);
            UnbindTrainingEnemyName();
            LocalizationSettings.SelectedLocaleChanged -= HandleSelectedLocaleChanged;
            SetCurrentTextDialogueLine(null);
            PopDialogueAdvanceInput();
            PopDialogueMovementLock();
            PopPreLeftAttackSuppression();
            PopPreSkillSuppression();
            PopPreParrySuppression();
            PopHitParrySuppression();
            HideExplanation();
            PopExplanationInputLock();
            EndMoveDestinationObjective(false);
            SetTrapdoorBarrierActive(false);
            ReleaseInitialMovementLock();
            ReleaseSummonMovementLock();
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

            StopExplanationVideo();
        }

        private void Start()
        {
            ResolveDialoguePanels();
            ResolveSummonSceneReferences();
            ResolveMoveDestinationReference();
            CacheSummonStartPose();
            CacheMoveDestinationReferences();
            SetupTrapdoorPlayerBarriers();

            if (sceneTrainingEnemy != null && hideSceneEnemyUntilTraining)
            {
                sceneTrainingEnemy.gameObject.SetActive(false);
            }

            PrepareTrainingEnemyForSummon();
            SetMoveDestinationActive(false);

            HideDialoguePanels();
            HideExplanation();
            SetBossUiVisible(false);
            TryPushInitialMovementLock();
            PlayTutorialBgm();
            tutorialRoutine = StartCoroutine(RunTutorial());
        }

        private void Update()
        {
            TryPushInitialMovementLock();
            TrySubscribeSkillManager();
            TrySubscribePlayerBullets();
            TrySubscribePlayerHealth();
        }

        private void ResolveDialoguePanels()
        {
            TutorialDialoguePanelView[] panels = FindObjectsByType<TutorialDialoguePanelView>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            TutorialDialoguePanelView namedTextPanel = FindDialoguePanelByName(panels, TextPanelRootName);
            TutorialDialoguePanelView namedObjectivePanel = FindDialoguePanelByName(panels, ObjectivePanelRootName);

            if (textDialoguePanel == null || IsDialoguePanelNamed(textDialoguePanel, ObjectivePanelRootName))
            {
                textDialoguePanel = namedTextPanel != null ? namedTextPanel : textDialoguePanel;
            }

            if (objectiveDialoguePanel == null || objectiveDialoguePanel == textDialoguePanel)
            {
                objectiveDialoguePanel = namedObjectivePanel;
            }

            if (textDialoguePanel == null)
            {
                textDialoguePanel = FindFirstDialoguePanelExcept(panels, objectiveDialoguePanel);
            }

            if (objectiveDialoguePanel == null)
            {
                objectiveDialoguePanel = FindFirstDialoguePanelExcept(panels, textDialoguePanel);
            }

            objectiveDialoguePanel ??= textDialoguePanel;
        }

        private TutorialDialoguePanelView FindDialoguePanelByName(
            TutorialDialoguePanelView[] panels,
            string nameToken)
        {
            if (panels == null)
            {
                return null;
            }

            for (int i = 0; i < panels.Length; i++)
            {
                TutorialDialoguePanelView panel = panels[i];
                if (IsDialoguePanelInScene(panel) && IsDialoguePanelNamed(panel, nameToken))
                {
                    return panel;
                }
            }

            return null;
        }

        private TutorialDialoguePanelView FindFirstDialoguePanelExcept(
            TutorialDialoguePanelView[] panels,
            TutorialDialoguePanelView excluded)
        {
            if (panels == null)
            {
                return null;
            }

            for (int i = 0; i < panels.Length; i++)
            {
                TutorialDialoguePanelView panel = panels[i];
                if (panel != excluded && IsDialoguePanelInScene(panel))
                {
                    return panel;
                }
            }

            return null;
        }

        private bool IsDialoguePanelInScene(TutorialDialoguePanelView panel)
        {
            return panel != null && panel.gameObject.scene == gameObject.scene;
        }

        private static bool IsDialoguePanelNamed(TutorialDialoguePanelView panel, string nameToken)
        {
            return panel != null
                && !string.IsNullOrWhiteSpace(nameToken)
                && panel.name.IndexOf(nameToken, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void HideDialoguePanels()
        {
            textDialoguePanel?.Hide();

            if (objectiveDialoguePanel != null && objectiveDialoguePanel != textDialoguePanel)
            {
                objectiveDialoguePanel.Hide();
            }
        }

        private IEnumerator HideAllDialoguePanelsAnimated()
        {
            if (textDialoguePanel != null)
            {
                yield return textDialoguePanel.HideAnimated();
            }

            if (objectiveDialoguePanel != null && objectiveDialoguePanel != textDialoguePanel)
            {
                yield return objectiveDialoguePanel.HideAnimated();
            }
        }

        private IEnumerator RunTutorial()
        {
            yield return RunTutorialFrom(TutorialStepId.Intro, true);
        }

        private IEnumerator RunTutorialFrom(TutorialStepId startStep, bool waitFirstDialogue)
        {
            ConfigureInputSuppressionForStep(startStep);

            if (waitFirstDialogue)
            {
                yield return WaitUnscaled(firstDialogueDelaySeconds);
            }

            int startIndex = GetFlowIndex(startStep);
            if (ShouldRunStep(startIndex, TutorialStepId.Intro))
            {
                activeStep = TutorialStepId.Intro;
                yield return PlayDialogue(TutorialStepId.Intro);
            }

            if (ShouldRunStep(startIndex, TutorialStepId.Move))
            {
                yield return RunObjectiveStage(TutorialStepId.Move, TutorialTrainingEnemyMode.Passive, 1);
            }

            if (ShouldRunStep(startIndex, TutorialStepId.Shoot))
            {
                yield return RunObjectiveStage(TutorialStepId.Shoot, TutorialTrainingEnemyMode.Passive, 1);
            }

            if (ShouldRunStep(startIndex, TutorialStepId.Attack))
            {
                yield return EnsureTrainingEnemySummoned(startIndex < GetFlowIndex(TutorialStepId.Attack));
                yield return RunObjectiveStage(TutorialStepId.Attack, TutorialTrainingEnemyMode.AttackTarget, attackHitGoal);
            }

            if (ShouldRunStep(startIndex, TutorialStepId.Hit))
            {
                yield return RunObjectiveStage(TutorialStepId.Hit, TutorialTrainingEnemyMode.ForcedHitPractice, hitGoal);
            }

            if (ShouldRunStep(startIndex, TutorialStepId.Parry))
            {
                yield return RunObjectiveStage(TutorialStepId.Parry, TutorialTrainingEnemyMode.ParryPractice, parryGoal);
            }

            activeEnemy?.Deactivate();
            EnemyProjectile.DestroyAllActive();
            if (ShouldRunStep(startIndex, TutorialStepId.BulletTimeout))
            {
                activeStep = TutorialStepId.BulletTimeout;
                yield return PlayDialogue(TutorialStepId.BulletTimeout);
                ReleaseBulletTimeoutLock(true);
            }

            if (ShouldRunStep(startIndex, TutorialStepId.SkillGauge))
            {
                activeStep = TutorialStepId.SkillGauge;
                yield return PlayDialogue(TutorialStepId.SkillGauge);
            }

            if (ShouldRunStep(startIndex, TutorialStepId.Skill))
            {
                yield return RunObjectiveStage(TutorialStepId.Skill, TutorialTrainingEnemyMode.DodgePractice, skillGoal);
            }

            if (ShouldRunStep(startIndex, TutorialStepId.Suppress))
            {
                yield return RunObjectiveStage(TutorialStepId.Suppress, TutorialTrainingEnemyMode.SuppressionPractice, suppressGoal);
            }

            if (ShouldRunStep(startIndex, TutorialStepId.Duel))
            {
                yield return RunObjectiveStage(TutorialStepId.Duel, TutorialTrainingEnemyMode.Duel, 1);
            }

            if (ShouldRunStep(startIndex, TutorialStepId.Complete))
            {
                activeStep = TutorialStepId.Complete;
                yield return PlayDialogue(TutorialStepId.Complete);
            }

            yield return HideAllDialoguePanelsAnimated();

            CompleteTutorial();
            tutorialRoutine = null;
        }

        private IEnumerator RunObjectiveStage(TutorialStepId step, TutorialTrainingEnemyMode enemyMode, int goal)
        {
            activeStep = step;
            ResetStepProgress(step);
            bool usesTrainingEnemy = UsesTrainingEnemy(step);
            if (usesTrainingEnemy)
            {
                ActivateTrainingEnemy(TutorialTrainingEnemyMode.Passive);
            }
            else
            {
                PrepareTrainingEnemyForSummon();
            }

            SetBossUiVisible(false);
            if (step == TutorialStepId.Hit)
            {
                RestorePlayerResources(true);
            }

            yield return PlayDialogue(step);
            if (step == TutorialStepId.Shoot)
            {
                PopPreLeftAttackSuppression();
            }
            else if (step == TutorialStepId.Skill)
            {
                PopPreSkillSuppression();
            }
            else if (step == TutorialStepId.Parry)
            {
                PopPreParrySuppression();
            }

            if (step == TutorialStepId.Hit)
            {
                PushHitParrySuppression();
            }

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

            if (usesTrainingEnemy)
            {
                ActivateTrainingEnemy(enemyMode);
            }

            SetBossUiVisible(step == TutorialStepId.Duel);
            if (step == TutorialStepId.Shoot || step == TutorialStepId.Attack)
            {
                RestorePlayerBullets();
            }

            if (step == TutorialStepId.Move)
            {
                BeginMoveDestinationObjective();
            }

            yield return ShowObjectivePanel(step, safeGoal);
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

            yield return CompleteObjectivePanel(step, safeGoal);

            if (step == TutorialStepId.Hit)
            {
                PopHitParrySuppression();
            }
        }

        private IEnumerator RunSkillObjectiveStage(TutorialTrainingEnemyMode enemyMode, int goal)
        {
            SetBossUiVisible(false);
            RestorePlayerResources(true);
            yield return ShowObjectivePanel(TutorialStepId.Skill, goal);

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
                yield return PlaySupplementalDialogue(TutorialStepId.SkillRetry);
                ShowObjective(TutorialStepId.Skill, goal);
            }

            yield return CompleteObjectivePanel(TutorialStepId.Skill, goal);
        }

        private IEnumerator PlayDialogue(TutorialStepId step)
        {
            TutorialStepContent content = dialogueSet != null ? dialogueSet.GetStep(step) : null;
            if (content == null || textDialoguePanel == null)
            {
                Debug.LogWarning($"{nameof(TutorialSceneController)}: {step} dialogue is missing in TutorialDialogueSet.");
                yield break;
            }

            yield return PlayDialogueLines(content);
        }

        private IEnumerator PlaySupplementalDialogue(TutorialStepId step)
        {
            yield return PlayDialogue(step);

            if (textDialoguePanel != null && textDialoguePanel != objectiveDialoguePanel)
            {
                yield return textDialoguePanel.HideAnimated();
            }
        }

        private IEnumerator ShowObjectivePanel(TutorialStepId step, int goal)
        {
            if (textDialoguePanel != null && textDialoguePanel != objectiveDialoguePanel)
            {
                yield return textDialoguePanel.HideAnimated();
            }

            ShowObjective(step, goal);
        }

        private IEnumerator CompleteObjectivePanel(TutorialStepId step, int goal)
        {
            if (objectiveDialoguePanel != null)
            {
                yield return objectiveDialoguePanel.PlayObjectiveCompleted(
                    FormatObjective(step, goal, goal),
                    FormatObjectiveKeyText(step),
                    fadeOut: false,
                    clearText: false);
                yield return objectiveDialoguePanel.HideAnimated();
            }

            if (objectiveCompleteResumeDelaySeconds > 0f)
            {
                yield return WaitUnscaled(objectiveCompleteResumeDelaySeconds);
            }
        }

        private IEnumerator PlayDialogueLines(TutorialStepContent content)
        {
            bool shouldLockMovement = ShouldLockMovementDuringDialogue();
            if (shouldLockMovement)
            {
                PushDialogueMovementLock();
            }

            PushDialogueAdvanceInput();
            try
            {
                for (int i = 0; i < content.Dialogues.Count; i++)
                {
                    TutorialDialogueLine line = content.Dialogues[i];
                    if (line == null)
                    {
                        continue;
                    }

                    yield return PlayDialogueLine(line);
                }
            }
            finally
            {
                SetCurrentTextDialogueLine(null);
                PopDialogueAdvanceInput();
                if (shouldLockMovement)
                {
                    ReleaseInitialMovementLock();
                    PopDialogueMovementLock();
                }
            }
        }

        private IEnumerator PlayDialogueLine(TutorialDialogueLine line)
        {
            SetCurrentTextDialogueLine(line);
            currentTextDialogueRevealRequestedByLocale = false;
            string speaker = ResolveDialogueSpeaker(line);
            string text = ResolveDialogueText(line);
            bool revealRequested = false;
            bool canAcceptAdvance = false;
            PlayDialogueSfx(line.SfxId);
            textDialoguePanel.ShowLine(speaker, text, line.Speaker);
            IEnumerator typing = textDialoguePanel.PlayTypewriter(
                text,
                () => revealRequested || currentTextDialogueRevealRequestedByLocale);
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

            TutorialExplanationContent explanation = line.Explanation;
            if (explanation != null && explanation.HasContent)
            {
                yield return PlayExplanation(explanation);
            }

            currentTextDialogueRevealRequestedByLocale = false;
        }

        private void HandleSelectedLocaleChanged(Locale _)
        {
            RefreshCurrentTextDialogueLine();
            RefreshCurrentExplanation();
            RefreshExplanationPanelLocalizedEvents();
        }

        private void HandleCurrentTextDialogueLocalizedStringChanged(string _)
        {
            RefreshCurrentTextDialogueLine();
        }

        private void SetCurrentTextDialogueLine(TutorialDialogueLine line)
        {
            if (currentTextDialogueLine == line)
            {
                return;
            }

            UnbindCurrentTextDialogueLine();
            currentTextDialogueLine = line;
            BindCurrentTextDialogueLine();
        }

        private void BindCurrentTextDialogueLine()
        {
            if (currentTextDialogueLine == null)
            {
                return;
            }

            if (currentTextDialogueLine.HasLocalizedSpeaker)
            {
                currentTextDialogueLine.LocalizedSpeaker.StringChanged += HandleCurrentTextDialogueLocalizedStringChanged;
            }

            if (currentTextDialogueLine.HasLocalizedText)
            {
                currentTextDialogueLine.LocalizedText.StringChanged += HandleCurrentTextDialogueLocalizedStringChanged;
            }
        }

        private void UnbindCurrentTextDialogueLine()
        {
            if (currentTextDialogueLine == null)
            {
                return;
            }

            if (currentTextDialogueLine.HasLocalizedSpeaker)
            {
                currentTextDialogueLine.LocalizedSpeaker.StringChanged -= HandleCurrentTextDialogueLocalizedStringChanged;
            }

            if (currentTextDialogueLine.HasLocalizedText)
            {
                currentTextDialogueLine.LocalizedText.StringChanged -= HandleCurrentTextDialogueLocalizedStringChanged;
            }
        }

        private void RefreshCurrentTextDialogueLine()
        {
            if (currentTextDialogueLine == null || textDialoguePanel == null)
            {
                return;
            }

            textDialoguePanel.ReplaceLineText(
                ResolveDialogueSpeaker(currentTextDialogueLine),
                ResolveDialogueText(currentTextDialogueLine),
                currentTextDialogueLine.Speaker);
            currentTextDialogueRevealRequestedByLocale = true;
        }

        private static string ResolveDialogueSpeaker(TutorialDialogueLine line)
        {
            if (line == null)
            {
                return string.Empty;
            }

            return line.HasLocalizedSpeaker ? line.LocalizedSpeaker.GetLocalizedString() : line.Speaker;
        }

        private static string ResolveDialogueText(TutorialDialogueLine line)
        {
            if (line == null)
            {
                return string.Empty;
            }

            return line.HasLocalizedText ? line.LocalizedText.GetLocalizedString() : line.Text;
        }

        private IEnumerator PlayExplanation(TutorialExplanationContent explanation)
        {
            if (explanationPanelRoot == null && explanationPanelCanvasGroup == null)
            {
                Debug.LogWarning($"{nameof(TutorialSceneController)}: explanationPanel is missing.");
                yield break;
            }

            ShowExplanation(explanation);
            while (!explanationCloseRequested)
            {
                yield return null;
            }

            HideExplanation();
        }

        private void ShowExplanation(TutorialExplanationContent explanation)
        {
            if (explanation == null || !explanation.HasContent)
            {
                HideExplanation();
                return;
            }

            SetCurrentExplanationContent(explanation);
            RefreshCurrentExplanation();
            RefreshExplanationPanelLocalizedEvents();
            explanationCloseRequested = false;
            PushExplanationCursor();
            PushExplanationInputLock();
            SetExplanationVisible(true);

            bool hasVideo = explanation.Video != null;
            SetExplanationImage(hasVideo ? null : explanation.Image);
            SetExplanationVideo(explanation.Video, explanation.LoopVideo);
        }

        private void HideExplanation()
        {
            SetCurrentExplanationContent(null);
            PopExplanationInputLock();
            StopExplanationVideo();
            SetExplanationImage(null);
            SetText(explanationTitle, string.Empty);
            SetText(explanationText, string.Empty);
            explanationCloseRequested = false;
            PopExplanationCursor();
            SetExplanationVisible(false);
        }

        private void HandleCurrentExplanationLocalizedStringChanged(string _)
        {
            RefreshCurrentExplanation();
        }

        private void SetCurrentExplanationContent(TutorialExplanationContent explanation)
        {
            if (currentExplanationContent == explanation)
            {
                return;
            }

            UnbindCurrentExplanationContent();
            currentExplanationContent = explanation;
            BindCurrentExplanationContent();
        }

        private void BindCurrentExplanationContent()
        {
            if (currentExplanationContent == null)
            {
                return;
            }

            if (currentExplanationContent.HasLocalizedTitle)
            {
                currentExplanationContent.LocalizedTitle.StringChanged += HandleCurrentExplanationLocalizedStringChanged;
            }

            if (currentExplanationContent.HasLocalizedText)
            {
                currentExplanationContent.LocalizedText.StringChanged += HandleCurrentExplanationLocalizedStringChanged;
            }
        }

        private void UnbindCurrentExplanationContent()
        {
            if (currentExplanationContent == null)
            {
                return;
            }

            if (currentExplanationContent.HasLocalizedTitle)
            {
                currentExplanationContent.LocalizedTitle.StringChanged -= HandleCurrentExplanationLocalizedStringChanged;
            }

            if (currentExplanationContent.HasLocalizedText)
            {
                currentExplanationContent.LocalizedText.StringChanged -= HandleCurrentExplanationLocalizedStringChanged;
            }
        }

        private void RefreshCurrentExplanation()
        {
            if (currentExplanationContent == null)
            {
                return;
            }

            SetText(
                explanationTitle,
                currentExplanationContent.HasLocalizedTitle
                    ? currentExplanationContent.LocalizedTitle.GetLocalizedString()
                    : currentExplanationContent.Title);
            SetText(
                explanationText,
                currentExplanationContent.HasLocalizedText
                    ? currentExplanationContent.LocalizedText.GetLocalizedString()
                    : currentExplanationContent.Text);
        }

        private void RefreshExplanationPanelLocalizedEvents()
        {
            GameObject root = explanationPanelRoot != null
                ? explanationPanelRoot
                : explanationPanelCanvasGroup != null ? explanationPanelCanvasGroup.gameObject : null;
            if (root == null)
            {
                return;
            }

            LocalizeStringEvent[] localizedTexts = root.GetComponentsInChildren<LocalizeStringEvent>(true);
            for (int i = 0; i < localizedTexts.Length; i++)
            {
                LocalizeStringEvent localizedText = localizedTexts[i];
                if (localizedText != null)
                {
                    localizedText.RefreshString();
                }
            }
        }

        private void PushExplanationCursor()
        {
            if (explanationCursorPushed)
            {
                return;
            }

            CursorController.PushExplanationCursorVisible();
            PlayerCombatController.PushMouseParryReticleSuppression();
            explanationCursorPushed = true;
        }

        private void PopExplanationCursor()
        {
            if (!explanationCursorPushed)
            {
                return;
            }

            CursorController.PopExplanationCursorVisible();
            PlayerCombatController.PopMouseParryReticleSuppression();
            explanationCursorPushed = false;
        }

        private void HandleExplanationCloseButtonClicked()
        {
            if (TutorialInputBlocked())
            {
                return;
            }

            explanationCloseRequested = true;
        }

        private void SetExplanationImage(Sprite sprite)
        {
            if (explanationImage == null)
            {
                return;
            }

            explanationImage.sprite = sprite;
            explanationImage.enabled = sprite != null;
        }

        private void SetExplanationVideo(VideoClip video, bool loop)
        {
            if (video == null)
            {
                StopExplanationVideo();
                return;
            }

            EnsureExplanationVideoObjects();
            if (explanationVideoImage == null || explanationVideoPlayer == null)
            {
                return;
            }

            StopExplanationVideo();
            explanationVideoImage.gameObject.SetActive(true);
            explanationVideoImage.enabled = true;
            explanationVideoImage.texture = null;

            explanationVideoPlayer.playOnAwake = false;
            explanationVideoPlayer.renderMode = VideoRenderMode.APIOnly;
            explanationVideoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            explanationVideoPlayer.isLooping = loop;
            explanationVideoPlayer.clip = video;
            explanationVideoPlayer.Play();
            explanationVideoRoutine = StartCoroutine(BindExplanationVideoTexture(video));
        }

        private IEnumerator BindExplanationVideoTexture(VideoClip video)
        {
            while (explanationVideoPlayer != null
                && explanationVideoImage != null
                && explanationVideoPlayer.clip == video)
            {
                if (explanationVideoPlayer.texture != null)
                {
                    explanationVideoImage.texture = explanationVideoPlayer.texture;
                }

                yield return null;
            }
        }

        private void StopExplanationVideo()
        {
            if (explanationVideoRoutine != null)
            {
                StopCoroutine(explanationVideoRoutine);
                explanationVideoRoutine = null;
            }

            if (explanationVideoPlayer != null)
            {
                explanationVideoPlayer.Stop();
                explanationVideoPlayer.clip = null;
            }

            if (explanationVideoImage != null)
            {
                explanationVideoImage.texture = null;
                explanationVideoImage.enabled = false;
                explanationVideoImage.gameObject.SetActive(false);
            }
        }

        private void EnsureExplanationVideoObjects()
        {
            if (explanationVideoImage == null && explanationImage != null)
            {
                GameObject videoObject = new("TutorialExplanationVideo", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                videoObject.transform.SetParent(explanationImage.transform.parent, false);

                RectTransform videoRect = videoObject.GetComponent<RectTransform>();
                RectTransform imageRect = explanationImage.rectTransform;
                videoRect.anchorMin = imageRect.anchorMin;
                videoRect.anchorMax = imageRect.anchorMax;
                videoRect.pivot = imageRect.pivot;
                videoRect.anchoredPosition = imageRect.anchoredPosition;
                videoRect.sizeDelta = imageRect.sizeDelta;
                videoRect.localRotation = Quaternion.identity;
                videoRect.localScale = Vector3.one;

                explanationVideoImage = videoObject.GetComponent<RawImage>();
                explanationVideoImage.raycastTarget = false;
            }

            if (explanationVideoPlayer == null)
            {
                GameObject host = explanationPanelRoot != null ? explanationPanelRoot : gameObject;
                explanationVideoPlayer = host.GetComponent<VideoPlayer>();
                if (explanationVideoPlayer == null)
                {
                    explanationVideoPlayer = host.AddComponent<VideoPlayer>();
                }
            }
        }

        private void SetExplanationVisible(bool visible)
        {
            if (explanationPanelRoot != null)
            {
                explanationPanelRoot.SetActive(visible);
            }

            if (explanationPanelCanvasGroup != null)
            {
                explanationPanelCanvasGroup.alpha = visible ? 1f : 0f;
                explanationPanelCanvasGroup.interactable = visible;
                explanationPanelCanvasGroup.blocksRaycasts = visible;
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
            yield return PlaySupplementalDialogue(TutorialStepId.AttackRefill);
        }

        private IEnumerator EnsureTrainingEnemySummoned(bool playSequence)
        {
            if (trainingEnemySummoned)
            {
                yield break;
            }

            ResolveSummonSceneReferences();
            CacheSummonStartPose();
            PrepareTrainingEnemyForSummon();

            if (playSequence)
            {
                PushSummonMovementLock();
                yield return AnimateTrapdoorsLowering();
                trainingEnemySummoned = true;
                ActivateTrainingEnemy(TutorialTrainingEnemyMode.Passive);
                activeEnemy?.SetPlayerInteractionEnabled(false);
                yield return AnimateUnderfloorRaising();
                ReleaseSummonMovementLock();
                activeEnemy?.SetPlayerInteractionEnabled(true);
                yield return WaitUnscaled(summonSettleSeconds);
                yield break;
            }
            else
            {
                ApplyTrapdoorPose(1f);
                ApplyUnderfloorPose(1f);
                SetTrapdoorBarrierActive(false);
            }

            trainingEnemySummoned = true;
            ActivateTrainingEnemy(TutorialTrainingEnemyMode.Passive);
            ReleaseSummonMovementLock();
        }

        private IEnumerator AnimateTrapdoorsLowering()
        {
            float duration = Mathf.Max(0f, trapdoorLowerSeconds);
            if (duration <= 0f)
            {
                ApplyTrapdoorPose(1f);
                SetTrapdoorBarrierActive(false);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                ApplyTrapdoorPose(Ease01(elapsed / duration));
                yield return null;
            }

            ApplyTrapdoorPose(1f);
            SetTrapdoorBarrierActive(false);
        }

        private IEnumerator AnimateUnderfloorRaising()
        {
            float duration = Mathf.Max(0f, underfloorRaiseSeconds);
            if (duration <= 0f)
            {
                ApplyUnderfloorPose(1f);
                yield break;
            }

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                ApplyUnderfloorPose(Ease01(elapsed / duration));
                yield return null;
            }

            ApplyUnderfloorPose(1f);
        }

        private void ApplyTrapdoorPose(float t)
        {
            float eased = Mathf.Clamp01(t);
            if (trapdoorLeft != null)
            {
                trapdoorLeft.localPosition = trapdoorLeftClosedLocalPosition + trapdoorLowerLocalOffset * eased;
            }

            if (trapdoorRight != null)
            {
                trapdoorRight.localPosition = trapdoorRightClosedLocalPosition + trapdoorLowerLocalOffset * eased;
            }
        }

        private void ApplyUnderfloorPose(float t)
        {
            if (underfloor != null)
            {
                underfloor.localPosition = underfloorHiddenLocalPosition + underfloorRaiseLocalOffset * Mathf.Clamp01(t);
            }
        }

        private void SetupTrapdoorPlayerBarriers()
        {
            ResolveSummonSceneReferences();
            trapdoorLeftBarrier = EnsureTrapdoorPlayerBarrier(trapdoorLeft, trapdoorLeftBarrier);
            trapdoorRightBarrier = EnsureTrapdoorPlayerBarrier(trapdoorRight, trapdoorRightBarrier);
        }

        private BoxCollider2D EnsureTrapdoorPlayerBarrier(Transform trapdoor, BoxCollider2D cachedBarrier)
        {
            if (trapdoor == null)
            {
                return null;
            }

            BoxCollider2D barrier = cachedBarrier != null && cachedBarrier.transform == trapdoor
                ? cachedBarrier
                : trapdoor.GetComponent<BoxCollider2D>();
            if (barrier == null)
            {
                barrier = trapdoor.gameObject.AddComponent<BoxCollider2D>();
            }

            if (!trapdoor.TryGetComponent(out PlayerOnlyMovementBarrier _))
            {
                trapdoor.gameObject.AddComponent<PlayerOnlyMovementBarrier>();
            }

            barrier.isTrigger = true;
            ConfigureTrapdoorBarrierSize(trapdoor, barrier);
            return barrier;
        }

        private static void ConfigureTrapdoorBarrierSize(Transform trapdoor, BoxCollider2D barrier)
        {
            if (barrier == null)
            {
                return;
            }

            SpriteRenderer renderer = trapdoor != null ? trapdoor.GetComponent<SpriteRenderer>() : null;
            Sprite sprite = renderer != null ? renderer.sprite : null;
            if (sprite == null)
            {
                barrier.offset = Vector2.zero;
                barrier.size = Vector2.one;
                return;
            }

            Bounds localBounds = sprite.bounds;
            barrier.offset = localBounds.center;
            barrier.size = localBounds.size;
        }

        private void SetTrapdoorBarrierActive(bool active)
        {
            if (trapdoorLeftBarrier != null)
            {
                trapdoorLeftBarrier.enabled = active;
            }

            if (trapdoorRightBarrier != null)
            {
                trapdoorRightBarrier.enabled = active;
            }
        }

        private void PrepareTrainingEnemyForSummon()
        {
            trainingEnemySummoned = false;
            SetupTrapdoorPlayerBarriers();
            SetTrapdoorBarrierActive(true);
            TutorialTrainingEnemy enemy = EnsureTrainingEnemy();
            if (enemy == null)
            {
                return;
            }

            enemy.Deactivate();
            enemy.SetPlayerInteractionEnabled(false);
            if (hideSceneEnemyUntilTraining)
            {
                enemy.gameObject.SetActive(false);
            }
        }

        private void ResolveSummonSceneReferences()
        {
            trapdoorLeft ??= FindSceneTransformByName(TrapdoorLeftName);
            trapdoorRight ??= FindSceneTransformByName(TrapdoorRightName);
            underfloor ??= FindSceneTransformByName(UnderfloorName);
        }

        private void ResolveMoveDestinationReference()
        {
            moveDestination ??= FindSceneTransformByName(MoveDestinationName);
        }

        private void CacheMoveDestinationReferences()
        {
            ResolveMoveDestinationReference();
            if (moveDestination == null)
            {
                moveDestinationRenderers = null;
                moveDestinationBaseColors = null;
                moveDestinationColliders = null;
                return;
            }

            moveDestinationRenderers = moveDestination.GetComponentsInChildren<SpriteRenderer>(true);
            moveDestinationBaseColors = new Color[moveDestinationRenderers.Length];
            for (int i = 0; i < moveDestinationRenderers.Length; i++)
            {
                SpriteRenderer renderer = moveDestinationRenderers[i];
                moveDestinationBaseColors[i] = renderer != null ? renderer.color : Color.white;
            }

            moveDestinationColliders = moveDestination.GetComponentsInChildren<Collider2D>(true);
        }

        private void CacheSummonStartPose()
        {
            if (summonStartPoseCached)
            {
                return;
            }

            ResolveSummonSceneReferences();
            trapdoorLeftClosedLocalPosition = trapdoorLeft != null ? trapdoorLeft.localPosition : Vector3.zero;
            trapdoorRightClosedLocalPosition = trapdoorRight != null ? trapdoorRight.localPosition : Vector3.zero;
            underfloorHiddenLocalPosition = underfloor != null ? underfloor.localPosition : Vector3.zero;
            summonStartPoseCached = true;
        }

        private Transform FindSceneTransformByName(string targetName)
        {
            if (string.IsNullOrWhiteSpace(targetName))
            {
                return null;
            }

            Transform fallback = null;
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate == null
                    || candidate.gameObject.scene != gameObject.scene
                    || candidate.name != targetName)
                {
                    continue;
                }

                if (HasParentNamed(candidate, TutorialMapRootName))
                {
                    return candidate;
                }

                fallback ??= candidate;
            }

            return fallback;
        }

        private static bool HasParentNamed(Transform candidate, string parentName)
        {
            for (Transform current = candidate; current != null; current = current.parent)
            {
                if (current.name == parentName)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool UsesTrainingEnemy(TutorialStepId step)
        {
            TutorialStepId checkpoint = GetCheckpointStep(step);
            return checkpoint == TutorialStepId.Attack
                || checkpoint == TutorialStepId.Hit
                || checkpoint == TutorialStepId.Parry
                || checkpoint == TutorialStepId.Skill
                || checkpoint == TutorialStepId.Suppress
                || checkpoint == TutorialStepId.Duel;
        }

        private static float Ease01(float t)
        {
            return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
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
            else if (mode == TutorialTrainingEnemyMode.SuppressionPractice)
            {
                enemy.BaitSuppressed += HandleSuppressionBaitSuppressed;
            }
        }

        private TutorialTrainingEnemy EnsureTrainingEnemy()
        {
            if (activeEnemy != null)
            {
                return activeEnemy;
            }

            TutorialTrainingEnemy prefab = IsSceneObjectReference(trainingEnemyPrefab) ? null : trainingEnemyPrefab;
            if (sceneTrainingEnemy != null)
            {
                activeEnemy = sceneTrainingEnemy;
                if (activeEnemy != null && enemySpawnPoint != null)
                {
                    activeEnemy.transform.SetPositionAndRotation(enemySpawnPoint.position, enemySpawnPoint.rotation);
                }
            }
            else if (prefab != null)
            {
                Vector3 position = enemySpawnPoint != null ? enemySpawnPoint.position : transform.position;
                Quaternion rotation = enemySpawnPoint != null ? enemySpawnPoint.rotation : Quaternion.identity;
                spawnedEnemy = Instantiate(prefab, position, rotation);
                activeEnemy = spawnedEnemy;
            }

            return activeEnemy;
        }

        private static bool IsSceneObjectReference(Component component)
        {
            return component != null && component.gameObject.scene.IsValid();
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
                TickMoveDestination();
            }
        }

        private int GetStepProgress(TutorialStepId step)
        {
            return step switch
            {
                TutorialStepId.Move => moveCount,
                TutorialStepId.Shoot => shootCount,
                TutorialStepId.Attack => attackHitCount,
                TutorialStepId.Hit => hitCount,
                TutorialStepId.Parry => parryCount,
                TutorialStepId.Skill => skillCount,
                TutorialStepId.Suppress => suppressCount,
                TutorialStepId.Duel => duelDefeatCount,
                _ => 0
            };
        }

        private void ResetStepProgress(TutorialStepId step)
        {
            if (step == TutorialStepId.Move)
            {
                moveCount = 0;
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
            else if (step == TutorialStepId.Hit)
            {
                hitCount = 0;
            }
            else if (step == TutorialStepId.Skill)
            {
                skillCount = 0;
                skillAttemptRunning = false;
                skillUsedThisAttempt = false;
                skillHitThisAttempt = false;
            }
            else if (step == TutorialStepId.Suppress)
            {
                ClearEnemySubscription();
                suppressCount = 0;
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
            string format = content != null && content.HasLocalizedObjectiveFormat
                ? content.LocalizedObjectiveFormat.GetLocalizedString()
                : content != null ? content.ObjectiveFormat : string.Empty;
            if (string.IsNullOrWhiteSpace(format))
            {
                format = "{0}/{1}";
            }

            return format
                .Replace("{0}", Mathf.Clamp(current, 0, goal).ToString())
                .Replace("{1}", goal.ToString());
        }

        private string FormatObjectiveKeyText(TutorialStepId step)
        {
            TutorialStepContent content = dialogueSet != null ? dialogueSet.GetStep(step) : null;
            if (content == null)
            {
                return string.Empty;
            }

            return content.HasLocalizedObjectiveKeyText
                ? content.LocalizedObjectiveKeyText.GetLocalizedString()
                : content.ObjectiveKeyText;
        }

        private void ShowObjective(TutorialStepId step, int goal)
        {
            objectiveDialoguePanel?.ShowObjective(
                FormatObjective(step, GetStepProgress(step), goal),
                FormatObjectiveKeyText(step));
        }

        private void BeginMoveDestinationObjective()
        {
            moveCount = 0;
            ReleaseSummonMovementLock();
            ResolveMoveDestinationReference();
            CacheMoveDestinationReferences();
            SetMoveDestinationActive(true);
            SetMoveDestinationAlpha(moveDestinationMinAlpha);
            StartMoveDestinationBlink();
            ReleaseInitialMovementLock();
        }

        private void TickMoveDestination()
        {
            if (moveCount > 0 || !HasReachedMoveDestination())
            {
                return;
            }

            moveCount = 1;
            EndMoveDestinationObjective(true);
        }

        private void EndMoveDestinationObjective(bool reached)
        {
            StopMoveDestinationBlink();
            SetMoveDestinationActive(false);
        }

        private void StartMoveDestinationBlink()
        {
            StopMoveDestinationBlink();
            if (moveDestination == null)
            {
                return;
            }

            moveDestinationBlinkRoutine = StartCoroutine(BlinkMoveDestination());
        }

        private void StopMoveDestinationBlink()
        {
            if (moveDestinationBlinkRoutine == null)
            {
                return;
            }

            StopCoroutine(moveDestinationBlinkRoutine);
            moveDestinationBlinkRoutine = null;
        }

        private IEnumerator BlinkMoveDestination()
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, moveDestinationBlinkSeconds);
            while (true)
            {
                float t = Mathf.PingPong(elapsed / duration, 1f);
                float alpha = Mathf.Lerp(moveDestinationMinAlpha, moveDestinationMaxAlpha, t);
                SetMoveDestinationAlpha(alpha);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void SetMoveDestinationActive(bool active)
        {
            ResolveMoveDestinationReference();
            if (moveDestination == null)
            {
                return;
            }

            if (moveDestination.gameObject.activeSelf != active)
            {
                moveDestination.gameObject.SetActive(active);
            }

            if (!active)
            {
                SetMoveDestinationAlpha(moveDestinationMinAlpha);
            }
        }

        private void SetMoveDestinationAlpha(float alpha)
        {
            if (moveDestinationRenderers == null)
            {
                CacheMoveDestinationReferences();
            }

            if (moveDestinationRenderers == null || moveDestinationBaseColors == null)
            {
                return;
            }

            float clampedAlpha = Mathf.Clamp01(alpha);
            for (int i = 0; i < moveDestinationRenderers.Length; i++)
            {
                SpriteRenderer renderer = moveDestinationRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                Color color = i < moveDestinationBaseColors.Length ? moveDestinationBaseColors[i] : renderer.color;
                color.a = clampedAlpha;
                renderer.color = color;
            }
        }

        private bool HasReachedMoveDestination()
        {
            Transform playerTransform = ResolvePlayer();
            if (playerTransform == null || moveDestination == null || !moveDestination.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (moveDestinationColliders == null)
            {
                CacheMoveDestinationReferences();
            }

            Vector2 playerPosition = playerTransform.position;
            Collider2D[] playerColliders = playerTransform.GetComponentsInChildren<Collider2D>();
            bool hasDestinationCollider = false;

            if (moveDestinationColliders != null)
            {
                for (int destinationIndex = 0; destinationIndex < moveDestinationColliders.Length; destinationIndex++)
                {
                    Collider2D destinationCollider = moveDestinationColliders[destinationIndex];
                    if (!IsUsableCollider(destinationCollider))
                    {
                        continue;
                    }

                    hasDestinationCollider = true;
                    if (destinationCollider.OverlapPoint(playerPosition))
                    {
                        return true;
                    }

                    for (int playerIndex = 0; playerIndex < playerColliders.Length; playerIndex++)
                    {
                        Collider2D playerCollider = playerColliders[playerIndex];
                        if (!IsUsableCollider(playerCollider))
                        {
                            continue;
                        }

                        ColliderDistance2D distance = destinationCollider.Distance(playerCollider);
                        if (destinationCollider.IsTouching(playerCollider)
                            || distance.isOverlapped
                            || distance.distance <= 0f)
                        {
                            return true;
                        }
                    }
                }
            }

            if (hasDestinationCollider)
            {
                return false;
            }

            return Vector2.Distance(playerPosition, moveDestination.position) <= moveDestinationFallbackRadius;
        }

        private static bool IsUsableCollider(Collider2D collider)
        {
            return collider != null && collider.enabled && collider.gameObject.activeInHierarchy;
        }

        private void SetBossUiVisible(bool visible)
        {
            bossUiVisible = visible;
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
            RefreshBossNameText();
        }

        private void BindTrainingEnemyName()
        {
            if (!HasLocalizedString(localizedTrainingEnemyName))
            {
                return;
            }

            localizedTrainingEnemyName.StringChanged += HandleTrainingEnemyNameChanged;
            localizedTrainingEnemyName.RefreshString();
        }

        private void UnbindTrainingEnemyName()
        {
            if (!HasLocalizedString(localizedTrainingEnemyName))
            {
                return;
            }

            localizedTrainingEnemyName.StringChanged -= HandleTrainingEnemyNameChanged;
        }

        private void HandleTrainingEnemyNameChanged(string _)
        {
            if (bossUiVisible)
            {
                RefreshBossNameText();
            }
        }

        private void RefreshBossNameText()
        {
            if (bossNameText != null)
            {
                bossNameText.text = ResolveTrainingEnemyName();
            }
        }

        private string ResolveTrainingEnemyName()
        {
            if (HasLocalizedString(localizedTrainingEnemyName))
            {
                string localizedName = localizedTrainingEnemyName.GetLocalizedString();
                if (!string.IsNullOrWhiteSpace(localizedName))
                {
                    return localizedName;
                }
            }

            return trainingEnemyName;
        }

        private static bool HasLocalizedString(LocalizedString value)
        {
            return value != null
                && value.TableReference.ReferenceType != TableReference.Type.Empty
                && value.TableEntryReference.ReferenceType != TableEntryReference.Type.Empty;
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
                GameFlowController.ContinueAfterTutorial(lobbySceneName);
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
            activeEnemy.BaitSuppressed -= HandleSuppressionBaitSuppressed;
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

        private void HandleProjectileParried(EnemyProjectile _)
        {
            if (activeStep == TutorialStepId.Parry)
            {
                parryCount++;
            }
        }

        private void HandleSuppressionBaitSuppressed()
        {
            if (activeStep == TutorialStepId.Suppress)
            {
                suppressCount++;
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
            if (activeStep == TutorialStepId.Hit
                && player == PlayerCombatController.Active)
            {
                hitCount = Mathf.Max(hitCount + 1, Mathf.Max(1, hitGoal));
                EnemyProjectile.DestroyAllActive();
                activeEnemy?.Deactivate();
                return;
            }

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
            TutorialStepId restartStep = GetCheckpointStep(activeStep);
            if (tutorialRoutine != null)
            {
                StopCoroutine(tutorialRoutine);
                tutorialRoutine = null;
            }

            PopDialogueAdvanceInput();
            PopDialogueMovementLock();
            PopHitParrySuppression();
            HideExplanation();
            HideDialoguePanels();
            SetBossUiVisible(false);
            activeEnemy?.Deactivate();
            EnemyProjectile.DestroyAllActive();

            yield return PlayerDeathSequence.Play(PlayerCombatController.Active);

            yield return SceneTransition.PlayCoverReveal(() =>
            {
                RestorePlayerForRetry();
                ConfigureInputSuppressionForStep(restartStep);
                PlayTutorialBgm();
            });

            deathRoutine = null;
            tutorialRoutine = StartCoroutine(RunTutorialFrom(restartStep, false));
        }

        private void PlayTutorialBgm()
        {
            if (!string.IsNullOrWhiteSpace(tutorialBgmId))
            {
                SoundManager.PlayBgm(tutorialBgmId, Mathf.Max(0f, tutorialBgmFadeSeconds));
            }
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

        private void PushSummonMovementLock()
        {
            if (summonMovementLockedPlayer != null)
            {
                return;
            }

            PlayerCombatController activePlayer = PlayerCombatController.Active;
            if (activePlayer == null)
            {
                return;
            }

            activePlayer.PushExternalMovementLock();
            summonMovementLockedPlayer = activePlayer;
        }

        private void ReleaseSummonMovementLock()
        {
            if (summonMovementLockedPlayer == null)
            {
                return;
            }

            summonMovementLockedPlayer.PopExternalMovementLock();
            summonMovementLockedPlayer = null;
        }

        private bool ShouldLockMovementDuringDialogue()
        {
            return GetFlowIndex(activeStep) > GetFlowIndex(TutorialStepId.Move);
        }

        private void PushDialogueMovementLock()
        {
            if (dialogueMovementLockedPlayer != null)
            {
                return;
            }

            PlayerCombatController activePlayer = PlayerCombatController.Active;
            if (activePlayer == null)
            {
                return;
            }

            activePlayer.PushExternalMovementLock();
            dialogueMovementLockedPlayer = activePlayer;
        }

        private void PopDialogueMovementLock()
        {
            if (dialogueMovementLockedPlayer == null)
            {
                return;
            }

            dialogueMovementLockedPlayer.PopExternalMovementLock();
            dialogueMovementLockedPlayer = null;
        }

        private void PushDialogueAdvanceInput()
        {
            if (dialogueAdvanceInputPushed)
            {
                return;
            }

            PlayerCombatController.PushLeftAttackSuppression();
            dialogueAdvanceInputPushed = true;

            SkillLoadoutManager.PushSkillUseSuppression();
            dialogueSkillSuppressionPushed = true;
        }

        private void PopDialogueAdvanceInput()
        {
            if (!dialogueAdvanceInputPushed)
            {
                return;
            }

            PlayerCombatController.PopLeftAttackSuppression();
            dialogueAdvanceInputPushed = false;

            if (dialogueSkillSuppressionPushed)
            {
                SkillLoadoutManager.PopSkillUseSuppression();
                dialogueSkillSuppressionPushed = false;
            }
        }

        private void PushPreLeftAttackSuppression()
        {
            if (preLeftAttackSuppressionPushed)
            {
                return;
            }

            PlayerCombatController.PushLeftAttackSuppression();
            preLeftAttackSuppressionPushed = true;
        }

        private void PopPreLeftAttackSuppression()
        {
            if (!preLeftAttackSuppressionPushed)
            {
                return;
            }

            PlayerCombatController.PopLeftAttackSuppression();
            preLeftAttackSuppressionPushed = false;
        }

        private void PushPreSkillSuppression()
        {
            if (preSkillSuppressionPushed)
            {
                return;
            }

            SkillLoadoutManager.PushSkillUseSuppression();
            preSkillSuppressionPushed = true;
        }

        private void PopPreSkillSuppression()
        {
            if (!preSkillSuppressionPushed)
            {
                return;
            }

            SkillLoadoutManager.PopSkillUseSuppression();
            preSkillSuppressionPushed = false;
        }

        private void PushPreParrySuppression()
        {
            if (preParrySuppressionPushed)
            {
                return;
            }

            PlayerCombatController.PushParrySuppression();
            preParrySuppressionPushed = true;
        }

        private void PopPreParrySuppression()
        {
            if (!preParrySuppressionPushed)
            {
                return;
            }

            PlayerCombatController.PopParrySuppression();
            preParrySuppressionPushed = false;
        }

        private void PushHitParrySuppression()
        {
            if (hitParrySuppressionPushed)
            {
                return;
            }

            PlayerCombatController.PushParrySuppression();
            hitParrySuppressionPushed = true;
        }

        private void PopHitParrySuppression()
        {
            if (!hitParrySuppressionPushed)
            {
                return;
            }

            PlayerCombatController.PopParrySuppression();
            hitParrySuppressionPushed = false;
        }

        private void PushExplanationInputLock()
        {
            if (explanationMovementLockedPlayer == null)
            {
                PlayerCombatController activePlayer = PlayerCombatController.Active;
                if (activePlayer != null)
                {
                    activePlayer.PushExternalMovementLock();
                    explanationMovementLockedPlayer = activePlayer;
                }
            }

            if (!explanationLeftAttackSuppressionPushed)
            {
                PlayerCombatController.PushLeftAttackSuppression();
                explanationLeftAttackSuppressionPushed = true;
            }

            if (!explanationParrySuppressionPushed)
            {
                PlayerCombatController.PushParrySuppression();
                explanationParrySuppressionPushed = true;
            }

            if (!explanationSkillSuppressionPushed)
            {
                SkillLoadoutManager.PushSkillUseSuppression();
                explanationSkillSuppressionPushed = true;
            }
        }

        private void PopExplanationInputLock()
        {
            if (explanationMovementLockedPlayer != null)
            {
                explanationMovementLockedPlayer.PopExternalMovementLock();
                explanationMovementLockedPlayer = null;
            }

            if (explanationLeftAttackSuppressionPushed)
            {
                PlayerCombatController.PopLeftAttackSuppression();
                explanationLeftAttackSuppressionPushed = false;
            }

            if (explanationParrySuppressionPushed)
            {
                PlayerCombatController.PopParrySuppression();
                explanationParrySuppressionPushed = false;
            }

            if (explanationSkillSuppressionPushed)
            {
                SkillLoadoutManager.PopSkillUseSuppression();
                explanationSkillSuppressionPushed = false;
            }
        }

        private void ConfigureInputSuppressionForStep(TutorialStepId step)
        {
            TutorialStepId checkpoint = GetCheckpointStep(step);
            if (GetFlowIndex(checkpoint) <= GetFlowIndex(TutorialStepId.Shoot))
            {
                PushPreLeftAttackSuppression();
            }
            else
            {
                PopPreLeftAttackSuppression();
            }

            if (GetFlowIndex(checkpoint) <= GetFlowIndex(TutorialStepId.Skill))
            {
                PushPreSkillSuppression();
            }
            else
            {
                PopPreSkillSuppression();
            }

            if (GetFlowIndex(checkpoint) < GetFlowIndex(TutorialStepId.Parry))
            {
                PushPreParrySuppression();
            }
            else
            {
                PopPreParrySuppression();
            }
        }

        private void RestorePlayerForRetry()
        {
            PlayerCombatController activePlayer = PlayerCombatController.Active;
            Transform playerTransform = activePlayer != null ? activePlayer.transform : ResolvePlayer();
            Transform checkpoint = ResolveRespawnPoint();

            if (playerTransform != null && checkpoint != null)
            {
                Rigidbody2D body = playerTransform.GetComponent<Rigidbody2D>();
                if (body != null)
                {
                    body.linearVelocity = Vector2.zero;
                    body.angularVelocity = 0f;
                    body.position = checkpoint.position;
                    body.rotation = checkpoint.eulerAngles.z;
                }

                playerTransform.SetPositionAndRotation(checkpoint.position, checkpoint.rotation);
            }

            RestorePlayerResources(true);
            activePlayer?.Visual?.RestoreAfterDeath();
            RestorePlayerHpView(activePlayer);
        }

        private static void RestorePlayerHpView(PlayerCombatController activePlayer)
        {
            PlayerHP hpView = activePlayer != null ? activePlayer.PlayerHpView : null;
            hpView ??= UnityEngine.Object.FindFirstObjectByType<PlayerHP>(FindObjectsInactive.Include);
            hpView?.SetExecutionVisible(true);
        }

        private Transform ResolveRespawnPoint()
        {
            return firstRoomRespawnPoint != null ? firstRoomRespawnPoint : ResolvePlayer();
        }

        private static TutorialStepId GetCheckpointStep(TutorialStepId step)
        {
            return step switch
            {
                TutorialStepId.AttackRefill => TutorialStepId.Attack,
                TutorialStepId.SkillRetry => TutorialStepId.Skill,
                _ => step
            };
        }

        private static bool ShouldRunStep(int startIndex, TutorialStepId step)
        {
            return GetFlowIndex(step) >= startIndex;
        }

        private static int GetFlowIndex(TutorialStepId step)
        {
            return step switch
            {
                TutorialStepId.Intro => 0,
                TutorialStepId.Move => 1,
                TutorialStepId.Shoot => 2,
                // Legacy serialized value resumes at the first post-shoot combat stage.
                TutorialStepId.RoomTransition => 3,
                TutorialStepId.Attack => 3,
                TutorialStepId.Hit => 4,
                TutorialStepId.Parry => 5,
                TutorialStepId.BulletTimeout => 6,
                TutorialStepId.SkillGauge => 7,
                TutorialStepId.Skill => 8,
                TutorialStepId.Suppress => 9,
                TutorialStepId.Duel => 10,
                TutorialStepId.Complete => 11,
                TutorialStepId.AttackRefill => 3,
                TutorialStepId.SkillRetry => 8,
                _ => 0
            };
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
            if (TutorialInputBlocked())
            {
                return false;
            }

#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            return GameInput.LeftAttackDown
                || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && keyboard.spaceKey.wasPressedThisFrame);
#else
            return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private static bool TutorialInputBlocked()
        {
            return GameModalState.BlocksGameplayInput || Mathf.Approximately(Time.timeScale, 0f);
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }
    }
}
