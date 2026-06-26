using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MinionWaitCommandsAction : BossAction
    {
        [SerializeField, Min(0f)] private float timeoutSeconds;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (!MinionGraphActionHost.TryGet(context, out IMinionPatternHost host))
            {
                yield break;
            }

            yield return host.WaitForMinionCommands(timeoutSeconds);
        }
    }
}
