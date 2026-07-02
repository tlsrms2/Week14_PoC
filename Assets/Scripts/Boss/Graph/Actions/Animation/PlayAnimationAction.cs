using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.Enemy
{
    public enum BossAnimationParameterType
    {
        Float,
        Int,
        Bool,
        Trigger
    }

    [Serializable]
    public sealed class PlayAnimationAction : BossAction
    {
        [SerializeField] private BossAnimationParameterType parameterType = BossAnimationParameterType.Trigger;
        [SerializeField, FormerlySerializedAs("triggerName")] private string parameterName;
        [SerializeField] private float floatValue;
        [SerializeField] private int intValue;
        [SerializeField] private bool boolValue;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            switch (parameterType)
            {
                case BossAnimationParameterType.Float:
                    context.SetAnimationFloat(parameterName, floatValue);
                    break;
                case BossAnimationParameterType.Int:
                    context.SetAnimationInt(parameterName, intValue);
                    break;
                case BossAnimationParameterType.Bool:
                    context.SetAnimationBool(parameterName, boolValue);
                    break;
                case BossAnimationParameterType.Trigger:
                    context.PlayAnimationTrigger(parameterName);
                    break;
            }
        }
    }
}
