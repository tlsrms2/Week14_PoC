using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
        [Serializable]
        private sealed class StoryPlaybackRule
        {
            [SerializeField] private InGameStorySequenceSO sequence;
            [SerializeField] private string[] requiredClearedBossIds = Array.Empty<string>();

            public bool HasSequence => sequence != null;
            public StoryEpisodeId EpisodeId => sequence.EpisodeId;
            public bool Skippable => sequence.Skippable;
            public InGameStorySequenceSO Sequence => sequence;
            public IReadOnlyList<string> RequiredClearedBossIds => requiredClearedBossIds ?? Array.Empty<string>();
        }

        [Header("View")]
        [SerializeField] private InGameDialoguePanelView dialoguePanel;

        [Header("Playback")]
        [SerializeField, Min(0f)] private float firstStoryDelaySeconds = 0.5f;
        [SerializeField] private List<StoryPlaybackRule> playbackRules = new();

        [Header("Skip")]
        [SerializeField, Min(0.1f)] private float skipHoldSeconds = 0.8f;

        private Coroutine playRoutine;
        private bool previousInputBlock;
        private bool skipRequested;
        private bool isPlaying;
        private float skipHoldElapsed;

        private void Awake()
        {
            dialoguePanel ??= GetComponentInChildren<InGameDialoguePanelView>(true);
            dialoguePanel?.Hide();
        }

        private void OnEnable()
        {
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
            yield return WaitUnscaled(firstStoryDelaySeconds);

            if (TryGetNextStory(out StoryEpisodeId episodeId, out bool skippable, out IReadOnlyList<InGameDialogueLine> dialogues))
            {
                yield return PlayStory(episodeId, skippable, dialogues);
            }

            playRoutine = null;
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

            IReadOnlyList<string> bossIds = rule.RequiredClearedBossIds;
            for (int i = 0; i < bossIds.Count; i++)
            {
                string bossId = bossIds[i];
                if (!string.IsNullOrWhiteSpace(bossId) && !GameSaveManager.IsCleared(bossId))
                {
                    return false;
                }
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

            BeginPlaybackState();

            for (int i = 0; i < dialogues.Count && !skipRequested; i++)
            {
                InGameDialogueLine line = dialogues[i];
                if (line == null)
                {
                    continue;
                }

                yield return PlayLine(line, skippable);
            }

            GameSaveManager.MarkStoryEpisodeSeen(GetEpisodeSaveId(episodeId));
            FinishPlaybackState();
        }

        private IEnumerator PlayLine(InGameDialogueLine line, bool skippable)
        {
            bool revealRequested = false;
            bool canAcceptAdvance = false;
            dialoguePanel.ShowLine(line.Speaker, line.Text);

            IEnumerator typing = dialoguePanel.PlayTypewriter(
                line.Text,
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

        private void BeginPlaybackState()
        {
            previousInputBlock = GameModalState.BlocksGameplayInput;
            GameModalState.BlocksGameplayInput = true;
            skipRequested = false;
            isPlaying = true;
            skipHoldElapsed = 0f;
            dialoguePanel?.SetSkipProgress(false, 0f);
        }

        private void FinishPlaybackState()
        {
            if (isPlaying)
            {
                GameModalState.BlocksGameplayInput = previousInputBlock;
            }

            isPlaying = false;
            skipRequested = false;
            skipHoldElapsed = 0f;
            dialoguePanel?.SetSkipProgress(false, 0f);
            dialoguePanel?.Hide();
        }

        private void TickSkip(bool skippable)
        {
            if (!skippable)
            {
                skipHoldElapsed = 0f;
                dialoguePanel?.SetSkipProgress(false, 0f);
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

            dialoguePanel?.SetSkipProgress(true, skipHoldElapsed / skipHoldSeconds);
        }

        private static string GetEpisodeSaveId(StoryEpisodeId episodeId)
        {
            return episodeId.ToString();
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
