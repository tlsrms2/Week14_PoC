using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class ArsonistSetSprinklerActiveAction : BossAction
    {
        [SerializeField, Min(0)] private int sprinklerIndex;
        [SerializeField] private bool active = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is ArsonistBossAI arsonist)
            {
                arsonist.SetSprinklerActive(sprinklerIndex, active);
            }

            yield break;
        }
    }
}
