using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.Video;

namespace Week14.Tutorial
{
    public enum TutorialStepId
    {
        Intro,
        Move,
        Attack,
        Parry,
        Skill,
        Duel,
        Complete,
        BulletTimeout,
        SkillGauge,
        SkillRetry,
        Shoot,
        AttackRefill,
        Hit,
        RoomTransition
    }

    [Serializable]
    public sealed class TutorialExplanationContent
    {
        [SerializeField] private bool enabled;
        [SerializeField] private Sprite image;
        [SerializeField] private VideoClip video;
        [SerializeField] private bool loopVideo = true;
        [SerializeField] private string title;
        [SerializeField, TextArea(8, 24)] private string text;
        [SerializeField] private LocalizedString localizedTitle;
        [SerializeField] private LocalizedString localizedText;

        public bool Enabled => enabled;
        public Sprite Image => image;
        public VideoClip Video => video;
        public bool LoopVideo => loopVideo;
        public string Title => title;
        public string Text => text;
        public LocalizedString LocalizedTitle => localizedTitle;
        public LocalizedString LocalizedText => localizedText;
        public bool HasLocalizedTitle => HasLocalizedString(localizedTitle);
        public bool HasLocalizedText => HasLocalizedString(localizedText);
        public bool HasContent => enabled
            && (image != null
                || video != null
                || !string.IsNullOrWhiteSpace(title)
                || !string.IsNullOrWhiteSpace(text));

        private static bool HasLocalizedString(LocalizedString value)
        {
            return value != null
                && value.TableReference.ReferenceType != TableReference.Type.Empty
                && value.TableEntryReference.ReferenceType != TableEntryReference.Type.Empty;
        }
    }

    [Serializable]
    public sealed class TutorialDialogueLine
    {
        [SerializeField] private string speaker;
        [SerializeField, TextArea(4, 14)] private string text;
        [SerializeField] private string sfxId;
        [SerializeField] private TutorialExplanationContent explanation;
        [SerializeField] private LocalizedString localizedSpeaker;
        [SerializeField] private LocalizedString localizedText;

        public TutorialDialogueLine(string speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }

        public string Speaker => speaker;
        public string Text => text;
        public string SfxId => sfxId;
        public TutorialExplanationContent Explanation => explanation;
        public LocalizedString LocalizedSpeaker => localizedSpeaker;
        public LocalizedString LocalizedText => localizedText;
        public bool HasLocalizedSpeaker => HasLocalizedString(localizedSpeaker);
        public bool HasLocalizedText => HasLocalizedString(localizedText);

        private static bool HasLocalizedString(LocalizedString value)
        {
            return value != null
                && value.TableReference.ReferenceType != TableReference.Type.Empty
                && value.TableEntryReference.ReferenceType != TableEntryReference.Type.Empty;
        }
    }

    [Serializable]
    public sealed class TutorialStepContent
    {
        [SerializeField] private TutorialStepId step;
        [SerializeField] private List<TutorialDialogueLine> dialogues = new();
        [SerializeField] private string objectiveFormat;
        [SerializeField] private LocalizedString localizedObjectiveFormat;
        [SerializeField] private string objectiveKeyText;
        [SerializeField] private LocalizedString localizedObjectiveKeyText;

        public TutorialStepContent(TutorialStepId step, string objectiveFormat, params TutorialDialogueLine[] dialogues)
        {
            this.step = step;
            this.objectiveFormat = objectiveFormat;
            this.dialogues = dialogues != null ? new List<TutorialDialogueLine>(dialogues) : new List<TutorialDialogueLine>();
        }

        public TutorialStepId Step => step;
        public IReadOnlyList<TutorialDialogueLine> Dialogues => dialogues;
        public string ObjectiveFormat => objectiveFormat;
        public LocalizedString LocalizedObjectiveFormat => localizedObjectiveFormat;
        public bool HasLocalizedObjectiveFormat => HasLocalizedString(localizedObjectiveFormat);
        public string ObjectiveKeyText => objectiveKeyText;
        public LocalizedString LocalizedObjectiveKeyText => localizedObjectiveKeyText;
        public bool HasLocalizedObjectiveKeyText => HasLocalizedString(localizedObjectiveKeyText);

        private static bool HasLocalizedString(LocalizedString value)
        {
            return value != null
                && value.TableReference.ReferenceType != TableReference.Type.Empty
                && value.TableEntryReference.ReferenceType != TableEntryReference.Type.Empty;
        }
    }

    [CreateAssetMenu(menuName = "Week14/Tutorial/Dialogue Set", fileName = "TutorialDialogueSet")]
    public sealed class TutorialDialogueSetSO : ScriptableObject
    {
        [SerializeField] private List<TutorialStepContent> steps = new();

        public IReadOnlyList<TutorialStepContent> Steps => steps;

        public TutorialStepContent GetStep(TutorialStepId step)
        {
            for (int i = 0; i < steps.Count; i++)
            {
                if (steps[i] != null && steps[i].Step == step)
                {
                    return steps[i];
                }
            }

            return null;
        }

    }
}
