using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class HackerHologramReplayAction : BossAction
    {
        [SerializeField, Min(0.01f)] private float hologramStartDelaySeconds = 0.2f;
        [SerializeField, BossGraphProjectileName, Tooltip("재현 패턴 동안 홀로그램이 발사할 투사체입니다. 비워 두면 각 액션의 설정을 사용합니다.")]
        private string hologramProjectileName;

        internal float HologramStartDelaySeconds => Mathf.Max(0.01f, hologramStartDelaySeconds);
        internal string HologramProjectileName => hologramProjectileName?.Trim() ?? string.Empty;

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
