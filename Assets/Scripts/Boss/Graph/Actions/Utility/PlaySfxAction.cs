using System;
using System.Collections;
using UnityEngine;
using Week14.Audio;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class PlaySfxAction : BossAction
    {
        public override IEnumerator Execute(BossActionContext context)
        {
            context?.PlaySfx(SoundEvent.Boss_CustomCue);
            yield break;
        }
    }
}
