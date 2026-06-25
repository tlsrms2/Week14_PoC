using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MoveBodyRootLocalAction : BossAction
    {
        [SerializeField] private Vector3 targetLocalOffset;
        [SerializeField, Min(0f)] private float duration = 0.1f;
        [SerializeField] private bool releaseBaseAfterMove;
        [Tooltip("이동이 끝났을 때 재생할 사운드 ID입니다.")]
        [SerializeField, BossGraphSfxId] private string completeSfxId;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            yield return context.MoveBodyRootLocalOffset(targetLocalOffset, duration, releaseBaseAfterMove);
            context.PlaySfx(completeSfxId);
        }
    }

    [Serializable]
    public sealed class ResetBodyRootLocalAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            context?.ResetBodyRootLocalOffset();
            yield break;
        }
    }
}
