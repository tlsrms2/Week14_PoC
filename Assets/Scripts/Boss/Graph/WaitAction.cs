using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class WaitAction : BossAction
    {
        [SerializeField, Min(0f)] private float seconds = 1f;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            yield return context.WaitSeconds(seconds);
        }
    }
}
