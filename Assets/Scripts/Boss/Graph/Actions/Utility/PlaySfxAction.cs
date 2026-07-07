using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class PlaySfxAction : BossAction
    {
        [SerializeField, BossGraphSfxId] private string sfxId;

        public override IEnumerator Execute(BossActionContext context)
        {
            context?.PlaySfx(sfxId);
            yield break;
        }
    }
}
