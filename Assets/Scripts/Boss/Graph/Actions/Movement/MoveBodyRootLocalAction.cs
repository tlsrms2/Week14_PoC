using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using Week14.Audio;

namespace Week14.Enemy
{
    [Serializable]
    public sealed class MoveBodyRootLocalAction : BossAction, IBossActionDurationProvider
    {
        [FormerlySerializedAs("targetLocalOffset")]
        [FormerlySerializedAs("targetOffset")]
        [SerializeField] private Vector3 targetPosition;
        [SerializeField, Min(0f)] private float duration = 0.1f;
        [FormerlySerializedAs("releaseBaseAfterMove")]
        [SerializeField] private bool stopWhenFinished = true;
        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            yield return context.MoveBodyRootToPosition(targetPosition, duration, stopWhenFinished);
            context.PlaySfx(SoundEvent.Boss_MoveComplete);
        }

        public bool TryGetDurationSeconds(out float seconds)
        {
            seconds = Mathf.Max(0f, duration);
            return seconds > 0f;
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
