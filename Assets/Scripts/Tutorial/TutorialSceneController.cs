using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Video;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Combat;
using Week14.Environment;
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
        [SerializeField] private GameObject explanationPanelRoot;
        [SerializeField] private CanvasGroup explanationPanelCanvasGroup;
        [SerializeField] private Image explanationImage;
        [SerializeField] private RawImage explanationVideoImage;
        [SerializeField] private VideoPlayer explanationVideoPlayer;
        [SerializeField] private TMP_Text explanationText;
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

        [Header("Room Transition")]
        [SerializeField] private BossCombatSlidingDoor roomTransitionDoor;
        [SerializeField] private Collider2D nextRoomArea;
        [SerializeField] private Transform nextRoomTarget;
        [SerializeField, Min(0.1f)] private float nextRoomArrivalRadius = 1f;
        [SerializeField] private Transform firstRoomRespawnPoint;
        [SerializeField] private Transform secondRoomRespawnPoint;

        [Header("Goals")]
        [SerializeField, Min(0.1f)] private float moveDistanceGoal = 3f;
        [SerializeField, Min(1)] private int attackHitGoal = 3;
        [SerializeField, Min(1)] private int hitGoal = 1;
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
        private int hitCount;
        private int roomTransitionCount;
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
        private bool preLeftAttackSuppressionPushed;
        private bool preSkillSuppressionPushed;
        private bool explanationLeftAttackSuppressionPushed;
        private bool explanationParrySuppressionPushed;
        private bool explanationSkillSuppressionPushed;
        private bool skillAttemptRunning;
        private bool skillUsedThisAttempt;
        private bool skillHitThisAttempt;
        private bool initialMovementLockReleased;
        private bool completionInvulnerabilityPushed;
        private PlayerCombatController initialMovementLockedPlayer;
        private PlayerCombatController explanationMovementLockedPlayer;
        private Coroutine tutorialRoutine;
        private Coroutine deathRoutine;
        private Coroutine explanationVideoRoutine;

        private void OnEnable()
        {
            PlayerProjectile.NormalAttackDamageDealt += HandleNormalAttackDamageDealt;
            PlayerParryController.ProjectileParried += HandleProjectileParried;
            PlayerCombatController.AttackReceived += HandlePlayerAttackReceived;
            PlayerCombatController.PushExternalCombatPermission();
            combatPermissionPushed = true;
            PushPreLeftAttackSuppression();
            PushPreSkillSuppression();
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
            PopPreLeftAttackSuppression();
            PopPreSkillSuppression();
            HideExplanation();
            PopExplanationInputLock();
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

            StopExplanationVideo();
        }

        private void Start()
        {
            if (sceneTrainingEnemy != null && hideSceneEnemyUntilTraining)
            {
                sceneTrainingEnemy.gameObject.SetActive(false);
            }

            dialoguePanel?.Hide();
            HideExplanation();
            SetBossUiVisible(false);
            ApplyDoorStateForStep(TutorialStepId.Intro);
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
            yield return RunTutorialFrom(TutorialStepId.Intro, true);
        }

        private IEnumerator RunTutorialFrom(TutorialStepId startStep, bool waitFirstDialogue)
        {
            ConfigureInputSuppressionForStep(startStep);
            ApplyDoorStateForStep(startStep);

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

            if (ShouldRunStep(startIndex, TutorialStepId.RoomTransition))
            {
                yield return RunRoomTransitionStage();
            }

            if (ShouldRunStep(startIndex, TutorialStepId.Attack))
            {
                yield return RunObjectiveStage(TutorialStepId.Attack, TutorialTrainingEnemyMode.AttackTarget, attackHitGoal);
            }

            if (ShouldRunStep(startIndex, TutorialStepId.Hit))
            {
                yield return RunObjectiveStage(TutorialStepId.Hit, TutorialTrainingEnemyMode.ParryPractice, hitGoal);
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

            if (ShouldRunStep(startIndex, TutorialStepId.Duel))
            {
                yield return RunObjectiveStage(TutorialStepId.Duel, TutorialTrainingEnemyMode.Duel, 1);
            }

            if (ShouldRunStep(startIndex, TutorialStepId.Complete))
            {
                activeStep = TutorialStepId.Complete;
                yield return PlayDialogue(TutorialStepId.Complete);
            }

            if (dialoguePanel != null)
            {
                yield return dialoguePanel.HideAnimated();
            }

            CompleteTutorial();
            tutorialRoutine = null;
        }

        private IEnumerator RunRoomTransitionStage()
        {
            activeStep = TutorialStepId.RoomTransition;
            ResetStepProgress(TutorialStepId.RoomTransition);
            SetBossUiVisible(false);
            activeEnemy?.Deactivate();
            EnemyProjectile.DestroyAllActive();

            yield return PlayDialogue(TutorialStepId.RoomTransition);

            if (roomTransitionDoor != null)
            {
                yield return roomTransitionDoor.OpenAndWait();
            }

            if (dialoguePanel != null)
            {
                yield return dialoguePanel.HideAnimated();
            }

            while (roomTransitionCount < 1)
            {
                TickStepProgress(TutorialStepId.RoomTransition);
                yield return null;
            }

            if (roomTransitionDoor != null)
            {
                yield return roomTransitionDoor.CloseAndWait();
            }
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
            else if (step == TutorialStepId.Hit)
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

                yield return PlayDialogueLine(line.Speaker, line.Text, line.SfxId, line.Explanation);
            }

            PopDialogueAdvanceInput();
        }

        private IEnumerator PlayDialogueLine(
            string speaker,
            string text,
            string sfxId,
            TutorialExplanationContent explanation)
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

            if (explanation != null && explanation.HasContent)
            {
                yield return PlayExplanation(explanation);
            }
        }

        private IEnumerator PlayExplanation(TutorialExplanationContent explanation)
        {
            if (explanationPanelRoot == null && explanationPanelCanvasGroup == null)
            {
                Debug.LogWarning($"{nameof(TutorialSceneController)}: explanationPanel is missing.");
                yield break;
            }

            ShowExplanation(explanation);
            while (!ExplanationClosePressed())
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

            SetText(explanationText, explanation.Text);
            PushExplanationInputLock();
            SetExplanationVisible(true);

            bool hasVideo = explanation.Video != null;
            SetExplanationImage(hasVideo ? null : explanation.Image);
            SetExplanationVideo(explanation.Video, explanation.LoopVideo);
        }

        private void HideExplanation()
        {
            PopExplanationInputLock();
            StopExplanationVideo();
            SetExplanationImage(null);
            SetText(explanationText, string.Empty);
            SetExplanationVisible(false);
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
            else if (step == TutorialStepId.RoomTransition)
            {
                roomTransitionCount = HasReachedNextRoom() ? 1 : 0;
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
                TutorialStepId.RoomTransition => roomTransitionCount,
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
            else if (step == TutorialStepId.Hit)
            {
                hitCount = 0;
            }
            else if (step == TutorialStepId.RoomTransition)
            {
                roomTransitionCount = HasReachedNextRoom() ? 1 : 0;
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

        private bool HasReachedNextRoom()
        {
            Transform playerTransform = ResolvePlayer();
            if (playerTransform == null)
            {
                return false;
            }

            Vector2 playerPosition = playerTransform.position;
            if (nextRoomArea != null)
            {
                return nextRoomArea.OverlapPoint(playerPosition);
            }

            if (nextRoomTarget == null)
            {
                return false;
            }

            float radius = Mathf.Max(0.1f, nextRoomArrivalRadius);
            return Vector2.SqrMagnitude(playerPosition - (Vector2)nextRoomTarget.position) <= radius * radius;
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
            if (activeStep == TutorialStepId.Hit
                && player == PlayerCombatController.Active)
            {
                hitCount++;
                EnemyProjectile.DestroyAllActive();
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
            HideExplanation();
            dialoguePanel?.Hide();
            SetBossUiVisible(false);
            activeEnemy?.Deactivate();
            EnemyProjectile.DestroyAllActive();

            yield return PlayerDeathSequence.Play(PlayerCombatController.Active);

            yield return SceneTransition.PlayCoverReveal(() =>
            {
                RestorePlayerForRetry(restartStep);
                ConfigureInputSuppressionForStep(restartStep);
                ApplyDoorStateForStep(restartStep);
            });

            deathRoutine = null;
            tutorialRoutine = StartCoroutine(RunTutorialFrom(restartStep, false));
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
        }

        private void RestorePlayerForRetry(TutorialStepId step)
        {
            PlayerCombatController activePlayer = PlayerCombatController.Active;
            Transform playerTransform = activePlayer != null ? activePlayer.transform : ResolvePlayer();
            Transform checkpoint = ResolveRespawnPoint(step);

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
        }

        private Transform ResolveRespawnPoint(TutorialStepId step)
        {
            if (IsSecondRoomStep(step) && secondRoomRespawnPoint != null)
            {
                return secondRoomRespawnPoint;
            }

            if (firstRoomRespawnPoint != null)
            {
                return firstRoomRespawnPoint;
            }

            return ResolvePlayer();
        }

        private void ApplyDoorStateForStep(TutorialStepId step)
        {
            if (roomTransitionDoor == null)
            {
                return;
            }

            if (GetFlowIndex(GetCheckpointStep(step)) >= GetFlowIndex(TutorialStepId.RoomTransition))
            {
                roomTransitionDoor.CloseInstant();
                return;
            }

            roomTransitionDoor.CloseInstant();
        }

        private static bool IsSecondRoomStep(TutorialStepId step)
        {
            return GetFlowIndex(GetCheckpointStep(step)) > GetFlowIndex(TutorialStepId.RoomTransition);
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
                TutorialStepId.RoomTransition => 3,
                TutorialStepId.Attack => 4,
                TutorialStepId.Hit => 5,
                TutorialStepId.Parry => 6,
                TutorialStepId.BulletTimeout => 7,
                TutorialStepId.SkillGauge => 8,
                TutorialStepId.Skill => 9,
                TutorialStepId.Duel => 10,
                TutorialStepId.Complete => 11,
                TutorialStepId.AttackRefill => 4,
                TutorialStepId.SkillRetry => 9,
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
#if ENABLE_INPUT_SYSTEM
            Mouse mouse = Mouse.current;
            return GameInput.LeftAttackDown
                || (mouse != null && mouse.leftButton.wasPressedThisFrame);
#else
            return Input.GetMouseButtonDown(0);
#endif
        }

        private static bool ExplanationClosePressed()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.eKey.wasPressedThisFrame;
#else
            return Input.GetKeyDown(KeyCode.E);
#endif
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
