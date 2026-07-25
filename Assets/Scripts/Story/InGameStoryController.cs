using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Week14.Audio;
using Week14.Bootstrap;
using Week14.Input;
using Week14.Save;
using Week14.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Week14.Story
{
    public sealed class InGameStoryController : MonoBehaviour
    {
        private const string MapObjectsRootName = "MapObjects";
        private const string UiBlockerName = "UIBlocker";

        [Serializable]
        private sealed class StoryPlaybackRule
        {
            [SerializeField] private InGameStorySequenceSO sequence;
            [SerializeField] private string[] requiredClearedBossIds = Array.Empty<string>();
            [SerializeField, Min(0)] private int requiredClearedBossCount;
            [SerializeField] private StoryEpisodeId[] requiredSeenStoryEpisodes = Array.Empty<StoryEpisodeId>();

            public bool HasSequence => sequence != null;
            public StoryEpisodeId EpisodeId => sequence.EpisodeId;
            public bool Skippable => sequence.Skippable;
            public InGameStorySequenceSO Sequence => sequence;
            public IReadOnlyList<string> RequiredClearedBossIds => requiredClearedBossIds ?? Array.Empty<string>();
            public int RequiredClearedBossCount => Mathf.Max(0, requiredClearedBossCount);
            public IReadOnlyList<StoryEpisodeId> RequiredSeenStoryEpisodes => requiredSeenStoryEpisodes ?? Array.Empty<StoryEpisodeId>();
        }

        [Serializable]
        private sealed class LobbyTutorialCue
        {
            [SerializeField] private StoryEpisodeId episodeId = StoryEpisodeId.LobbyTutorialBoss;
            [Tooltip("1-based dialogue line number in the sequence.")]
            [SerializeField, Min(1)] private int lineNumber = 1;
            [Tooltip("Objects to show while this cue is active, such as spotlights, outlines, or panels.")]
            [SerializeField] private GameObject[] activeObjects = Array.Empty<GameObject>();
            [Tooltip("If enabled, this cue is hidden when the current dialogue line finishes. If disabled, it stays until another cue starts or the story ends.")]
            [SerializeField] private bool hideWhenLineEnds = true;
            [SerializeField] private UnityEvent onEnter;
            [SerializeField] private UnityEvent onExit;

            public bool HideWhenLineEnds => hideWhenLineEnds;

            public bool Matches(StoryEpisodeId targetEpisodeId, int targetLineNumber)
            {
                return episodeId == targetEpisodeId && lineNumber == targetLineNumber;
            }

            public void Enter()
            {
                SetActive(activeObjects, true);
                onEnter?.Invoke();
            }

            public void Exit()
            {
                onExit?.Invoke();
                SetActive(activeObjects, false);
            }

            private static void SetActive(GameObject[] targets, bool active)
            {
                if (targets == null)
                {
                    return;
                }

                for (int i = 0; i < targets.Length; i++)
                {
                    if (targets[i] != null)
                    {
                        targets[i].SetActive(active);
                    }
                }
            }
        }

        private readonly struct GraphicRaycastState
        {
            private readonly Graphic graphic;
            private readonly bool raycastTarget;

            public GraphicRaycastState(Graphic graphic)
            {
                this.graphic = graphic;
                raycastTarget = graphic != null && graphic.raycastTarget;
            }

            public void Restore()
            {
                if (graphic != null)
                {
                    graphic.raycastTarget = raycastTarget;
                }
            }
        }

        private readonly struct ColliderState
        {
            private readonly Collider2D collider;
            private readonly bool enabled;

            public ColliderState(Collider2D collider)
            {
                this.collider = collider;
                enabled = collider != null && collider.enabled;
            }

            public void Restore()
            {
                if (collider != null)
                {
                    collider.enabled = enabled;
                }
            }
        }

        private readonly struct SelectableState
        {
            private readonly Selectable selectable;
            private readonly bool interactable;

            public SelectableState(Selectable selectable)
            {
                this.selectable = selectable;
                interactable = selectable != null && selectable.interactable;
            }

            public void Restore()
            {
                if (selectable != null)
                {
                    selectable.interactable = interactable;
                }
            }
        }

        [Header("View")]
        [SerializeField] private InGameDialoguePanelView dialoguePanel;
        [SerializeField] private GameObject uiBlocker;

        [Header("Lobby Interaction")]
        [SerializeField] private Transform mapObjectsRoot;
        [SerializeField] private LobbyMenuController lobbyMenuController;
        [SerializeField] private bool blockMapObjectsDuringStory = true;

        [Header("Lobby Tutorial Cues")]
        [SerializeField] private List<LobbyTutorialCue> lobbyTutorialCues = new();

        [Header("Playback")]
        [SerializeField, Min(0f)] private float firstStoryDelaySeconds = 0.5f;
        [SerializeField] private List<StoryPlaybackRule> playbackRules = new();

        [Header("Skip")]
        [SerializeField, Min(0.1f)] private float skipHoldSeconds = 0.8f;

        private Coroutine playRoutine;
        private bool previousInputBlock;
        private bool skipRequested;
        private bool isPlaying;
        private bool pendingStoryBlockActive;
        private bool mapObjectsInteractionBlocked;
        private bool activeLobbyTutorialCueEndsWithLine;
        private float skipHoldElapsed;
        private LobbyTutorialCue activeLobbyTutorialCue;
        private readonly List<GraphicRaycastState> blockedGraphics = new();
        private readonly List<ColliderState> blockedColliders = new();
        private readonly List<SelectableState> blockedSelectables = new();

        private void Awake()
        {
            dialoguePanel ??= GetComponentInChildren<InGameDialoguePanelView>(true);
            ResolveUiBlocker();
            ResolveLobbyReferences();
            RefreshPendingStoryBlock();
            HideDialoguePanel();
        }

        private void OnEnable()
        {
            RefreshPendingStoryBlock();
            playRoutine = StartCoroutine(PlayFirstAvailableStoryRoutine());
        }

        private void OnDisable()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            FinishPlaybackState();
        }

        private IEnumerator PlayFirstAvailableStoryRoutine()
        {
            bool waitedForTransition = false;
            while (SceneTransition.IsTransitioning)
            {
                waitedForTransition = true;
                yield return null;
            }

            if (!waitedForTransition)
            {
                yield return WaitUnscaled(firstStoryDelaySeconds);
            }

            RefreshPendingStoryBlock();

            bool hasStartedPlayback = false;
            while (TryGetNextStory(out StoryEpisodeId episodeId, out bool skippable, out IReadOnlyList<InGameDialogueLine> dialogues))
            {
                if (!hasStartedPlayback)
                {
                    BeginPlaybackState();
                    hasStartedPlayback = true;
                }

                yield return PlayStory(episodeId, skippable, dialogues);
            }

            if (hasStartedPlayback)
            {
                yield return FinishPlaybackStateAnimated();
            }
            else
            {
                RefreshPendingStoryBlock();
            }

            playRoutine = null;
        }

        private bool HasPendingStory()
        {
            if (dialoguePanel == null || playbackRules == null)
            {
                return false;
            }

            for (int i = 0; i < playbackRules.Count; i++)
            {
                StoryPlaybackRule rule = playbackRules[i];
                if (rule == null || !rule.HasSequence || !CanPlay(rule))
                {
                    continue;
                }

                IReadOnlyList<InGameDialogueLine> dialogues = rule.Sequence.Dialogues;
                if (dialogues.Count > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetNextStory(
            out StoryEpisodeId episodeId,
            out bool skippable,
            out IReadOnlyList<InGameDialogueLine> dialogues)
        {
            for (int i = 0; i < playbackRules.Count; i++)
            {
                StoryPlaybackRule rule = playbackRules[i];
                if (rule == null || !rule.HasSequence || !CanPlay(rule))
                {
                    continue;
                }

                IReadOnlyList<InGameDialogueLine> resolvedDialogues = rule.Sequence.Dialogues;
                if (resolvedDialogues.Count == 0)
                {
                    Debug.LogWarning($"{nameof(InGameStoryController)}: {rule.Sequence.name} has no dialogue lines.");
                    continue;
                }

                episodeId = rule.EpisodeId;
                skippable = rule.Skippable;
                dialogues = resolvedDialogues;
                return true;
            }

            episodeId = default;
            skippable = false;
            dialogues = Array.Empty<InGameDialogueLine>();
            return false;
        }

        private static bool CanPlay(StoryPlaybackRule rule)
        {
            StoryEpisodeId episodeId = rule.EpisodeId;
            if (GameSaveManager.HasSeenStoryEpisode(GetEpisodeSaveId(episodeId)))
            {
                return false;
            }

            IReadOnlyList<StoryEpisodeId> requiredSeenEpisodes = rule.RequiredSeenStoryEpisodes;
            for (int i = 0; i < requiredSeenEpisodes.Count; i++)
            {
                if (!GameSaveManager.HasSeenStoryEpisode(GetEpisodeSaveId(requiredSeenEpisodes[i])))
                {
                    return false;
                }
            }

            IReadOnlyList<string> bossIds = rule.RequiredClearedBossIds;
            for (int i = 0; i < bossIds.Count; i++)
            {
                string bossId = bossIds[i];
                if (!string.IsNullOrWhiteSpace(bossId) && !GameSaveManager.IsCleared(bossId))
                {
                    return false;
                }
            }

            if (GameSaveManager.ClearedBossCount < rule.RequiredClearedBossCount)
            {
                return false;
            }

            return true;
        }

        private IEnumerator PlayStory(
            StoryEpisodeId episodeId,
            bool skippable,
            IReadOnlyList<InGameDialogueLine> dialogues)
        {
            if (dialoguePanel == null || dialogues == null || dialogues.Count == 0)
            {
                yield break;
            }

            bool blocksLobbyPanelBackClose = IsLobbyTutorialEpisode(episodeId);
            if (blocksLobbyPanelBackClose)
            {
                lobbyMenuController?.SetBackCloseBlocked(true);
            }

            try
            {
                BeginStorySegment();

                for (int i = 0; i < dialogues.Count && !skipRequested; i++)
                {
                    InGameDialogueLine line = dialogues[i];
                    if (line == null)
                    {
                        continue;
                    }

                    BeginLobbyTutorialCue(episodeId, i + 1);
                    yield return PlayLine(line, skippable);
                    EndLobbyTutorialCueLine();
                }

                GameSaveManager.MarkStoryEpisodeSeen(GetEpisodeSaveId(episodeId));
            }
            finally
            {
                if (blocksLobbyPanelBackClose)
                {
                    lobbyMenuController?.SetBackCloseBlocked(false);
                }
            }
        }

        private IEnumerator PlayLine(InGameDialogueLine line, bool skippable)
        {
            bool revealRequested = false;
            bool canAcceptAdvance = false;
            PlayDialogueSfx(line.SfxId);

            string speaker = line.HasLocalizedSpeaker ? line.LocalizedSpeaker.GetLocalizedString() : line.Speaker;
            string text = line.HasLocalizedText ? line.LocalizedText.GetLocalizedString() : line.Text;
            dialoguePanel.ShowLine(
                speaker,
                text,
                line.Speaker,
                line.ExpressionId,
                line.PortraitSlot,
                line.ClearPortraitsBeforeLine);

            IEnumerator typing = dialoguePanel.PlayTypewriter(
                text,
                () => revealRequested || skipRequested,
                () => skipRequested);

            while (typing.MoveNext())
            {
                TickSkip(skippable);
                if (skipRequested)
                {
                    yield break;
                }

                if (canAcceptAdvance && AdvancePressed())
                {
                    revealRequested = true;
                }

                yield return typing.Current;
                canAcceptAdvance = true;
            }

            yield return null;
            while (!skipRequested && !AdvancePressed())
            {
                TickSkip(skippable);
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

        private void BeginPlaybackState()
        {
            if (!pendingStoryBlockActive)
            {
                previousInputBlock = GameModalState.BlocksGameplayInput;
            }

            pendingStoryBlockActive = false;
            GameModalState.BlocksGameplayInput = true;
            skipRequested = false;
            isPlaying = true;
            skipHoldElapsed = 0f;
            SetSkipProgress(false, 0f);
            SetUiBlockerVisible(true);
            BlockMapObjectsInteraction();
        }

        private void BeginStorySegment()
        {
            skipRequested = false;
            skipHoldElapsed = 0f;
            SetSkipProgress(false, 0f);
        }

        private void FinishPlaybackState()
        {
            CompletePlaybackState();
            HideDialoguePanel();
        }

        private IEnumerator FinishPlaybackStateAnimated()
        {
            ClearActiveLobbyTutorialCue();
            skipRequested = false;
            skipHoldElapsed = 0f;
            SetSkipProgress(false, 0f);

            if (dialoguePanel != null)
            {
                yield return dialoguePanel.HideAnimated();
            }

            CompletePlaybackState();
        }

        private void CompletePlaybackState()
        {
            bool shouldRestoreInputBlock = isPlaying || pendingStoryBlockActive;

            ClearActiveLobbyTutorialCue();
            RestoreMapObjectsInteraction();
            SetUiBlockerVisible(false);

            if (shouldRestoreInputBlock)
            {
                GameModalState.BlocksGameplayInput = previousInputBlock;
            }

            isPlaying = false;
            pendingStoryBlockActive = false;
            skipRequested = false;
            skipHoldElapsed = 0f;
            SetSkipProgress(false, 0f);
        }

        private void RefreshPendingStoryBlock()
        {
            if (isPlaying)
            {
                return;
            }

            if (HasPendingStory())
            {
                BeginPendingStoryBlock();
                return;
            }

            if (pendingStoryBlockActive)
            {
                FinishPlaybackState();
                return;
            }

            SetUiBlockerVisible(false);
        }

        private void BeginPendingStoryBlock()
        {
            if (!pendingStoryBlockActive)
            {
                previousInputBlock = GameModalState.BlocksGameplayInput;
                pendingStoryBlockActive = true;
            }

            GameModalState.BlocksGameplayInput = true;
            SetUiBlockerVisible(true);
        }

        private void BlockMapObjectsInteraction()
        {
            if (!blockMapObjectsDuringStory || mapObjectsInteractionBlocked)
            {
                return;
            }

            ResolveLobbyReferences();
            if (mapObjectsRoot == null)
            {
                return;
            }

            blockedGraphics.Clear();
            blockedColliders.Clear();
            blockedSelectables.Clear();

            Graphic[] graphics = mapObjectsRoot.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                blockedGraphics.Add(new GraphicRaycastState(graphic));
                graphic.raycastTarget = false;
            }

            Collider2D[] colliders = mapObjectsRoot.GetComponentsInChildren<Collider2D>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D targetCollider = colliders[i];
                if (targetCollider == null)
                {
                    continue;
                }

                blockedColliders.Add(new ColliderState(targetCollider));
                targetCollider.enabled = false;
            }

            Selectable[] selectables = mapObjectsRoot.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];
                if (selectable == null)
                {
                    continue;
                }

                blockedSelectables.Add(new SelectableState(selectable));
                selectable.interactable = false;
            }

            BossHoverHighlight.ClearHovered();
            LoadoutHoverHighlight.ClearHovered();
            lobbyMenuController?.ClearHoverHighlights();
            mapObjectsInteractionBlocked = true;
        }

        private void RestoreMapObjectsInteraction()
        {
            if (!mapObjectsInteractionBlocked)
            {
                return;
            }

            for (int i = 0; i < blockedGraphics.Count; i++)
            {
                blockedGraphics[i].Restore();
            }

            for (int i = 0; i < blockedColliders.Count; i++)
            {
                blockedColliders[i].Restore();
            }

            for (int i = 0; i < blockedSelectables.Count; i++)
            {
                blockedSelectables[i].Restore();
            }

            blockedGraphics.Clear();
            blockedColliders.Clear();
            blockedSelectables.Clear();
            mapObjectsInteractionBlocked = false;
        }

        private void BeginLobbyTutorialCue(StoryEpisodeId episodeId, int lineNumber)
        {
            if (!IsLobbyTutorialEpisode(episodeId))
            {
                ClearActiveLobbyTutorialCue();
                return;
            }

            LobbyTutorialCue cue = FindLobbyTutorialCue(episodeId, lineNumber);
            if (cue == null)
            {
                return;
            }

            ClearActiveLobbyTutorialCue();
            activeLobbyTutorialCue = cue;
            activeLobbyTutorialCueEndsWithLine = cue.HideWhenLineEnds;
            activeLobbyTutorialCue.Enter();
        }

        private void EndLobbyTutorialCueLine()
        {
            if (activeLobbyTutorialCue != null && activeLobbyTutorialCueEndsWithLine)
            {
                ClearActiveLobbyTutorialCue();
            }
        }

        private void ClearActiveLobbyTutorialCue()
        {
            if (activeLobbyTutorialCue == null)
            {
                return;
            }

            LobbyTutorialCue cue = activeLobbyTutorialCue;
            activeLobbyTutorialCue = null;
            activeLobbyTutorialCueEndsWithLine = false;
            cue.Exit();
        }

        private LobbyTutorialCue FindLobbyTutorialCue(StoryEpisodeId episodeId, int lineNumber)
        {
            for (int i = 0; i < lobbyTutorialCues.Count; i++)
            {
                LobbyTutorialCue cue = lobbyTutorialCues[i];
                if (cue != null && cue.Matches(episodeId, lineNumber))
                {
                    return cue;
                }
            }

            return null;
        }

        private void TickSkip(bool skippable)
        {
            if (!skippable)
            {
                skipHoldElapsed = 0f;
                SetSkipProgress(false, 0f);
                return;
            }

            if (SkipHeld())
            {
                skipHoldElapsed += Time.unscaledDeltaTime;
                if (skipHoldElapsed >= skipHoldSeconds)
                {
                    skipRequested = true;
                }
            }
            else
            {
                skipHoldElapsed = 0f;
            }

            SetSkipProgress(true, skipHoldElapsed / skipHoldSeconds);
        }

        private void HideDialoguePanel()
        {
            if (dialoguePanel != null)
            {
                dialoguePanel.Hide();
            }
        }

        private void SetUiBlockerVisible(bool visible)
        {
            ResolveUiBlocker();
            if (uiBlocker != null)
            {
                uiBlocker.SetActive(visible);
            }
        }

        private void SetSkipProgress(bool visible, float progress)
        {
            if (dialoguePanel != null)
            {
                dialoguePanel.SetSkipProgress(visible, progress);
            }
        }

        private static string GetEpisodeSaveId(StoryEpisodeId episodeId)
        {
            return episodeId.ToString();
        }

        private void ResolveLobbyReferences()
        {
            if (mapObjectsRoot == null)
            {
                GameObject mapObjects = GameObject.Find(MapObjectsRootName);
                if (mapObjects != null)
                {
                    mapObjectsRoot = mapObjects.transform;
                }
            }

            lobbyMenuController ??= FindFirstObjectByType<LobbyMenuController>(FindObjectsInactive.Include);
        }

        private void ResolveUiBlocker()
        {
            if (uiBlocker != null)
            {
                return;
            }

            Transform canvasRoot = dialoguePanel != null
                ? dialoguePanel.GetComponentInParent<Canvas>(true)?.transform
                : null;

            Transform found = FindChildRecursive(canvasRoot, UiBlockerName);
            if (found != null)
            {
                uiBlocker = found.gameObject;
                return;
            }

            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == UiBlockerName)
                {
                    uiBlocker = candidate.gameObject;
                    return;
                }
            }
        }

        private static Transform FindChildRecursive(Transform root, string childName)
        {
            if (root == null || string.IsNullOrEmpty(childName))
            {
                return null;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name == childName)
                {
                    return child;
                }

                Transform found = FindChildRecursive(child, childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static bool IsLobbyTutorialEpisode(StoryEpisodeId episodeId)
        {
            return episodeId.ToString().StartsWith("LobbyTutorial", StringComparison.Ordinal);
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
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            return GameInput.LeftAttackDown
                || (mouse != null && mouse.leftButton.wasPressedThisFrame)
                || (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame));
#else
            return Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space);
#endif
        }

        private static bool SkipHeld()
        {
#if ENABLE_INPUT_SYSTEM
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.isPressed;
#else
            return Input.GetKey(KeyCode.Escape);
#endif
        }
    }
}
