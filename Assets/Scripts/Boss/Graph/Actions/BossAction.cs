using System;
using System.Collections;

namespace Week14.Enemy
{
    [Serializable]
    public abstract class BossAction
    {
        public abstract IEnumerator Execute(BossActionContext context);
    }

    internal interface IBossGraphValidatedAction
    {
        void OnGraphValidated(BossGraphAsset graph, BossStateNode node);
    }

    internal interface IBossActionDurationProvider
    {
        bool TryGetDurationSeconds(out float seconds);
    }

    internal interface IBossActionContextDurationProvider
    {
        bool TryGetDurationSeconds(BossActionContext context, out float seconds);
    }

    internal interface IConductorCueOverlayEarlyStartSource
    {
        bool ShouldStartConductorCueOverlayEarly { get; }
        void ClearConductorCueOverlayEarlyStart();
    }
}
