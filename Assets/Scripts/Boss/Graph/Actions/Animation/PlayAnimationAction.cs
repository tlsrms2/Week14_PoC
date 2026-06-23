using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    public enum BossAnimationPlayMode
    {
        Trigger,
        State
    }

    [Serializable]
    public sealed class PlayAnimationAction : BossAction
    {
        [SerializeField] private BossAnimationPlayMode playMode;
        [SerializeField] private string triggerName;
        [SerializeField] private string stateName;
        [SerializeField] private int layer;
        [SerializeField, Range(0f, 1f)] private float normalizedTime;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            if (playMode == BossAnimationPlayMode.Trigger)
            {
                context.PlayAnimationTrigger(triggerName);
            }
            else
            {
                context.PlayAnimationState(stateName, layer, normalizedTime);
            }
        }
    }
}
