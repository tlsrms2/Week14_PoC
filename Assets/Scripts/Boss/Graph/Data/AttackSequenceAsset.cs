using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    public class BossGraphActionAsset : ScriptableObject
    {
        [SerializeReference] private List<BossAction> actions = new();

        public BossAction Action => actions != null && actions.Count > 0 ? actions[0] : null;
        public IReadOnlyList<BossAction> Actions => actions;

        public IEnumerator Execute(BossActionContext context)
        {
            BossAction action = Action;
            if (context == null || action == null)
            {
                yield break;
            }

            yield return action.Execute(context);
        }

        private void OnValidate()
        {
            TrimToSingleAction();
        }

        private void TrimToSingleAction()
        {
            if (actions == null)
            {
                return;
            }

            while (actions.Count > 1)
            {
                actions.RemoveAt(actions.Count - 1);
            }
        }
    }

    [CreateAssetMenu(menuName = "Week14/Boss/Boss Graph Action", fileName = "BossGraphAction")]
    [Obsolete("Use BossGraphActionAsset instead. This type remains only for existing serialized assets.")]
    public sealed class AttackSequenceAsset : BossGraphActionAsset
    {
    }
}
