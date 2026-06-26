using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionPatternCleanupAction : BossAction
    {
        [SerializeField] private bool waitForCommands = true;
        [SerializeField, Min(0f)] private float waitTimeoutSeconds;
        [SerializeField] private bool clearSynchronizedFire = true;
        [SerializeField] private bool stopAllMinions;
        [SerializeField] private bool resumeIdle = true;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            if (clearSynchronizedFire)
            {
                host.ClearSynchronizedMinionFire();
            }

            if (stopAllMinions)
            {
                host.StopAllMinions();
            }

            if (resumeIdle)
            {
                host.ResumeAllMinions();
            }

            if (waitForCommands)
            {
                yield return host.WaitForMinionCommands(waitTimeoutSeconds);
            }
        }
    }
}
