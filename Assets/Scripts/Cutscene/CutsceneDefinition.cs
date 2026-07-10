using System.Collections.Generic;
using UnityEngine;

namespace Week14.Cutscene
{
    [CreateAssetMenu(fileName = "CutsceneDefinition", menuName = "Week14/Cutscene/Cutscene Definition")]
    public sealed class CutsceneDefinition : ScriptableObject
    {
        [SerializeField] private string cutsceneId;
        [SerializeField] private bool skippable = true;
        [SerializeField] private List<CutsceneStep> steps = new();

        public string CutsceneId => cutsceneId;
        public bool Skippable => skippable;
        public IReadOnlyList<CutsceneStep> Steps => steps;

        private void OnValidate()
        {
            if (steps == null)
            {
                return;
            }

            for (int i = 0; i < steps.Count; i++)
            {
                steps[i]?.MigrateLegacyDialogue();
            }
        }
    }
}
