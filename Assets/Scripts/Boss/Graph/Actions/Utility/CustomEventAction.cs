using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class CustomEventAction : BossAction
    {
        [SerializeField, Tooltip("보스 오브젝트 또는 자식 오브젝트에 SendMessage/BroadcastMessage로 호출할 메서드명입니다.")]
        private string methodName;
        [SerializeField] private bool broadcastToChildren;

        public override IEnumerator Execute(BossActionContext context)
        {
            context?.SendCustomEvent(methodName, broadcastToChildren);
            yield break;
        }
    }
}
