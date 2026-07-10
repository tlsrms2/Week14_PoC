using System;
using System.Collections.Generic;
using UnityEngine;
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
        [SerializeField, TextArea(8, 24)] private string text;

        public bool Enabled => enabled;
        public Sprite Image => image;
        public VideoClip Video => video;
        public bool LoopVideo => loopVideo;
        public string Text => text;
        public bool HasContent => enabled && (image != null || video != null || !string.IsNullOrWhiteSpace(text));
    }

    [Serializable]
    public sealed class TutorialDialogueLine
    {
        [SerializeField] private string speaker;
        [SerializeField, TextArea(4, 14)] private string text;
        [SerializeField] private string sfxId;
        [SerializeField] private TutorialExplanationContent explanation;

        public TutorialDialogueLine(string speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }

        public string Speaker => speaker;
        public string Text => text;
        public string SfxId => sfxId;
        public TutorialExplanationContent Explanation => explanation;
    }

    [Serializable]
    public sealed class TutorialStepContent
    {
        [SerializeField] private TutorialStepId step;
        [SerializeField] private List<TutorialDialogueLine> dialogues = new();
        [SerializeField] private string objectiveFormat;

        public TutorialStepContent(TutorialStepId step, string objectiveFormat, params TutorialDialogueLine[] dialogues)
        {
            this.step = step;
            this.objectiveFormat = objectiveFormat;
            this.dialogues = dialogues != null ? new List<TutorialDialogueLine>(dialogues) : new List<TutorialDialogueLine>();
        }

        public TutorialStepId Step => step;
        public IReadOnlyList<TutorialDialogueLine> Dialogues => dialogues;
        public string ObjectiveFormat => objectiveFormat;
    }

    [CreateAssetMenu(menuName = "Week14/Tutorial/Dialogue Set", fileName = "TutorialDialogueSet")]
    public sealed class TutorialDialogueSetSO : ScriptableObject
    {
        [SerializeField] private List<TutorialStepContent> steps = new();

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
