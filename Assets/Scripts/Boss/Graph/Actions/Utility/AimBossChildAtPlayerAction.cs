using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

namespace Week14.Enemy
{
    public enum BossChildAimActionMode
    {
        Start = 0,
        End = 2
    }

    [Serializable]
    public sealed class AimBossChildAtPlayerAction : BossAction, ISerializationCallbackReceiver
    {
        [SerializeField] private BossChildAimActionMode mode = BossChildAimActionMode.Start;
        [SerializeField, BossGraphNodeId] private string startNodeId;
        [SerializeField, BossGraphBossChildPath] private string targetPath;
        [SerializeField] private bool activateOnStart = true;
        [SerializeField] private bool flipYByFacing = true;
        [SerializeField, FormerlySerializedAs("deactivateOnStop")] private bool deactivateOnEnd = true;
        [SerializeField] private bool deactivateOnPatternEnd = true;

        public BossChildAimActionMode Mode => mode;

        public override IEnumerator Execute(BossActionContext context)
        {
            if (context == null)
            {
                yield break;
            }

            if (mode == BossChildAimActionMode.End)
            {
                if (!context.StopBossChildAimAtPlayerStartedByNode(startNodeId, deactivateOnEnd)
                    && !string.IsNullOrWhiteSpace(targetPath))
                {
                    context.StopBossChildAimAtPlayer(targetPath, deactivateOnEnd);
                }

                yield break;
            }

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                yield break;
            }

            context.StartBossChildAimAtPlayer(targetPath, activateOnStart, flipYByFacing, deactivateOnPatternEnd);
        }

        public void OnBeforeSerialize()
        {
        }

        public void OnAfterDeserialize()
        {
            if (!Enum.IsDefined(typeof(BossChildAimActionMode), mode))
            {
                mode = BossChildAimActionMode.Start;
            }
        }
    }
}
