using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;

namespace Week14.Story
{
    public enum StoryEpisodeId
    {
        Act1 = 0,
        Act2 = 1,
        Act3 = 2,
        FinalBossAftermath = 3,
        LobbyTutorialBoss = 5,
        LobbyTutorialSkill = 6
    }

    public enum InGameDialoguePortraitSlot
    {
        Auto = 0,
        Left = 1,
        Right = 2,
        Hidden = 3
    }

    [Serializable]
    public sealed class InGameDialogueLine
    {
        [SerializeField] private string speaker;
        [SerializeField, TextArea(4, 14)] private string text;
        [SerializeField] private string expressionId;
        [SerializeField] private InGameDialoguePortraitSlot portraitSlot = InGameDialoguePortraitSlot.Auto;
        [SerializeField] private bool clearPortraitsBeforeLine;
        [SerializeField] private LocalizedString localizedSpeaker;
        [SerializeField] private LocalizedString localizedText;

        public InGameDialogueLine(string speaker, string text, string expressionId = null)
        {
            this.speaker = speaker;
            this.text = text;
            this.expressionId = expressionId;
        }

        public string Speaker => speaker;
        public string Text => text;
        public string ExpressionId => expressionId;
        public InGameDialoguePortraitSlot PortraitSlot => portraitSlot;
        public bool ClearPortraitsBeforeLine => clearPortraitsBeforeLine;
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

    [CreateAssetMenu(menuName = "Week14/Story/In-Game Story Sequence", fileName = "InGameStorySequence")]
    public sealed class InGameStorySequenceSO : ScriptableObject
    {
        [SerializeField] private StoryEpisodeId episodeId = StoryEpisodeId.Act1;
        [SerializeField] private bool skippable = true;
        [SerializeField] private List<InGameDialogueLine> dialogues = new();

        public StoryEpisodeId EpisodeId => episodeId;
        public bool Skippable => skippable;
        public IReadOnlyList<InGameDialogueLine> Dialogues => dialogues;
    }
}
