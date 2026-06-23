using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    [CreateAssetMenu(menuName = "Week14/Boss/Attack Sequence", fileName = "AttackSequence")]
    public sealed class AttackSequenceAsset : ScriptableObject
    {
        [SerializeReference] private List<BossAction> actions = new();

        public IReadOnlyList<BossAction> Actions => actions;

        public IEnumerator Execute(BossActionContext context)
        {
            if (context == null || actions == null)
            {
                yield break;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                BossAction action = actions[i];
                if (action == null)
                {
                    continue;
                }

                yield return action.Execute(context);
            }
        }
    }
}
