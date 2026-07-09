using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.Cutscene
{
    public enum CutsceneStepTransitionMode
    {
        BlackFade,
        ImageFade
    }

    [Serializable]
    public sealed class CutsceneDialogue
    {
        [SerializeField] private string name;
        [SerializeField, TextArea(2, 6)] private string text;

        public string Name => name;
        public string Text => text;

        public CutsceneDialogue() { }

        internal CutsceneDialogue(string name, string text)
        {
            this.name = name;
            this.text = text;
        }
    }

    [Serializable]
    public sealed class CutsceneStep
    {
        [FormerlySerializedAs("backgroundSprite")]
        [SerializeField] private Sprite backgroundImage;
        [SerializeField] private List<CutsceneDialogue> dialogues = new();
        [SerializeField] private bool skippable;

        [Header("Audio")]
        [SerializeField] private string bgmId;
        [SerializeField, Min(0f)] private float bgmFadeSeconds = 0.5f;

        [Header("Transition")]
        [SerializeField] private CutsceneStepTransitionMode transitionMode = CutsceneStepTransitionMode.BlackFade;
        [SerializeField, Min(0f)] private float fadeSeconds = 0.5f;

        [Header("Background Motion")]
        [SerializeField, Min(0f)] private float motionSeconds;
        [SerializeField] private Vector2 startOffset;
        [SerializeField] private Vector2 endOffset;
        [SerializeField, Min(0.01f)] private float startZoom = 1f;
        [SerializeField, Min(0.01f)] private float endZoom = 1f;

        public Sprite BackgroundImage => backgroundImage;
        public IReadOnlyList<CutsceneDialogue> Dialogues => dialogues;
        public bool Skippable => skippable;
        public string BgmId => bgmId;
        public float BgmFadeSeconds => bgmFadeSeconds;
        public CutsceneStepTransitionMode TransitionMode => transitionMode;
        public float FadeSeconds => fadeSeconds;
        public float MotionSeconds => motionSeconds;
        public Vector2 StartOffset => startOffset;
        public Vector2 EndOffset => endOffset;
        public float StartZoom => Mathf.Max(0.01f, startZoom);
        public float EndZoom => Mathf.Max(0.01f, endZoom);
        public bool HasBackgroundMotion => motionSeconds > 0f;

        [SerializeField, HideInInspector, FormerlySerializedAs("speakerName")]
        private string legacySpeakerName;
        [SerializeField, HideInInspector, FormerlySerializedAs("dialogueText")]
        private string legacyDialogueText;
        [SerializeField, HideInInspector, FormerlySerializedAs("waitForInput")]
        private bool legacyWaitForInput = true;

        internal void MigrateLegacyDialogue()
        {
            if (dialogues == null)
            {
                dialogues = new List<CutsceneDialogue>();
            }

            if (dialogues.Count > 0
                || string.IsNullOrWhiteSpace(legacySpeakerName)
                && string.IsNullOrWhiteSpace(legacyDialogueText))
            {
                return;
            }

            if (!legacyWaitForInput)
            {
                legacyWaitForInput = true;
            }

            dialogues.Add(new CutsceneDialogue(legacySpeakerName, legacyDialogueText));
            legacySpeakerName = null;
            legacyDialogueText = null;
        }
    }
}
