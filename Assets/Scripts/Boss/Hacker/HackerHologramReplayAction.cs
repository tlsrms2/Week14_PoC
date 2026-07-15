using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerHologramReplayAction : BossAction
    {
        [SerializeField, Min(0.01f)] private float hologramStartDelaySeconds = 0.2f;

        internal float HologramStartDelaySeconds => Mathf.Max(0.01f, hologramStartDelaySeconds);

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context?.Boss is HackerBossAI hacker)
            {
                hacker.TryEnsureHologram();
            }

            yield break;
        }
    }
}
