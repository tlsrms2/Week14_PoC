using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Story
{
    public enum StoryEpisodeId
    {
        Act1,
        Act2,
        Act3,
        FinalBossAftermath,
        Epilogue
    }

    [Serializable]
    public sealed class InGameDialogueLine
    {
        [SerializeField] private string speaker;
        [SerializeField, TextArea] private string text;

        public InGameDialogueLine(string speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }

        public string Speaker => speaker;
        public string Text => text;
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
