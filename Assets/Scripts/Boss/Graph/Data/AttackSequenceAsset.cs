using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    public class BossGraphActionAsset : ScriptableObject
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

    [CreateAssetMenu(menuName = "Week14/Boss/Boss Graph Action", fileName = "BossGraphAction")]
    [Obsolete("Use BossGraphActionAsset instead. This type remains only for existing serialized assets.")]
    public sealed class AttackSequenceAsset : BossGraphActionAsset
    {
    }
}
