using System;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Story
{
    public enum StoryEpisodeId
    {
        Act1 = 0,
        Act2 = 1,
        Act3 = 2,
        FinalBossAftermath = 3,
        Epilogue = 4,
        LobbyTutorialBoss = 5,
        LobbyTutorialSkill = 6
    }

    [Serializable]
    public sealed class InGameDialogueLine
    {
        [SerializeField] private string speaker;
        [SerializeField, TextArea(4, 14)] private string text;
        [SerializeField] private string sfxId;

        public InGameDialogueLine(string speaker, string text)
        {
            this.speaker = speaker;
            this.text = text;
        }

        public string Speaker => speaker;
        public string Text => text;
        public string SfxId => sfxId;
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
