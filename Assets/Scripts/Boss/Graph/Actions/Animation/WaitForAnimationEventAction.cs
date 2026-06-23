using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class WaitForAnimationEventAction : BossAction
    {
        [SerializeField] private string eventId;
        [SerializeField, Min(0f), Tooltip("0이면 이벤트가 올 때까지 계속 기다립니다.")]
        private float timeoutSeconds;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            yield return context.WaitForAnimationEvent(eventId, timeoutSeconds);
        }
    }
}
