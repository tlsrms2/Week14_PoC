using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.Serialization;
using Week14.Enemy;

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
        [SerializeField, TextArea(4, 14)] private string text;
        [SerializeField] private string sfxId;
        [SerializeField] private LocalizedString localizedName;
        [SerializeField] private LocalizedString localizedText;

        public string Name => name;
        public string Text => text;
        public string SfxId => sfxId;
        public LocalizedString LocalizedName => localizedName;
        public LocalizedString LocalizedText => localizedText;
        public bool HasLocalizedName => HasLocalizedString(localizedName);
        public bool HasLocalizedText => HasLocalizedString(localizedText);

        public CutsceneDialogue() { }

        internal CutsceneDialogue(string name, string text)
        {
            this.name = name;
            this.text = text;
        }

        private static bool HasLocalizedString(LocalizedString value)
        {
            return value != null
                && value.TableReference.ReferenceType != TableReference.Type.Empty
                && value.TableEntryReference.ReferenceType != TableEntryReference.Type.Empty;
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
        [BossGraphBgmId]
        [SerializeField] private string bgmId;
        [SerializeField, Min(0f)] private float bgmFadeSeconds = 0.5f;

        [Header("Transition")]
        [SerializeField] private CutsceneStepTransitionMode transitionMode = CutsceneStepTransitionMode.BlackFade;
        [FormerlySerializedAs("fadeSeconds")]
        [SerializeField, Min(0f)] private float fadeInSeconds = 0.5f;
        [SerializeField, Min(0f)] private float fadeOutSeconds = 0.25f;
        [SerializeField, Min(0f)] private float darkHoldSeconds;

        [Header("Background Motion")]
        [SerializeField, Min(0f)] private float motionSeconds;
        [SerializeField] private Vector2 startOffset;
        [SerializeField] private Vector2 endOffset;
        [SerializeField, Min(0.01f)] private float startZoom = 1f;
        [SerializeField, Min(0.01f)] private float endZoom = 1f;

        [Header("Background Glitch")]
        [Tooltip("Frames shown briefly between periods of the base background image.")]
        [SerializeField] private List<Sprite> glitchFrames = new();
        [SerializeField, Min(0f)] private float glitchHoldMinSeconds = 0.2f;
        [SerializeField, Min(0f)] private float glitchHoldMaxSeconds = 0.6f;
        [SerializeField, Min(1)] private int glitchBurstMinFrames = 2;
        [SerializeField, Min(1)] private int glitchBurstMaxFrames = 5;
        [SerializeField, Min(0.01f)] private float glitchFrameMinSeconds = 0.025f;
        [SerializeField, Min(0.01f)] private float glitchFrameMaxSeconds = 0.075f;

        public Sprite BackgroundImage => backgroundImage;
        public IReadOnlyList<CutsceneDialogue> Dialogues => dialogues;
        public bool Skippable => skippable;
        public string BgmId => bgmId;
        public float BgmFadeSeconds => bgmFadeSeconds;
        public CutsceneStepTransitionMode TransitionMode => transitionMode;
        public float FadeInSeconds => Mathf.Max(0f, fadeInSeconds);
        public float FadeOutSeconds => Mathf.Max(0f, fadeOutSeconds);
        public float DarkHoldSeconds => Mathf.Max(0f, darkHoldSeconds);
        public float MotionSeconds => motionSeconds;
        public Vector2 StartOffset => startOffset;
        public Vector2 EndOffset => endOffset;
        public float StartZoom => Mathf.Max(0.01f, startZoom);
        public float EndZoom => Mathf.Max(0.01f, endZoom);
        public bool HasBackgroundMotion => motionSeconds > 0f;
        public IReadOnlyList<Sprite> GlitchFrames => glitchFrames;
        public bool HasBackgroundGlitch => glitchFrames != null && glitchFrames.Exists(frame => frame != null);
        public float GlitchHoldMinSeconds => Mathf.Max(0f, glitchHoldMinSeconds);
        public float GlitchHoldMaxSeconds => Mathf.Max(GlitchHoldMinSeconds, glitchHoldMaxSeconds);
        public int GlitchBurstMinFrames => Mathf.Max(1, glitchBurstMinFrames);
        public int GlitchBurstMaxFrames => Mathf.Max(GlitchBurstMinFrames, glitchBurstMaxFrames);
        public float GlitchFrameMinSeconds => Mathf.Max(0.01f, glitchFrameMinSeconds);
        public float GlitchFrameMaxSeconds => Mathf.Max(GlitchFrameMinSeconds, glitchFrameMaxSeconds);

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
