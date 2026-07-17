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

    public interface IBossProjectileEmissionAction
    {
    }

    internal interface IConductorCueOverlayEarlyStartSource
    {
        bool ShouldStartConductorCueOverlayEarly { get; }
        void ClearConductorCueOverlayEarlyStart();
    }

    internal interface IConductorCueOverlayExplicitStartSource
    {
        bool ShouldStartConductorCueOverlay { get; }
        bool IsConductorCueOverlaySourceFinished { get; }
        void ClearConductorCueOverlayStart();
    }

    internal interface IConductorCueDurationCompanion
    {
        void SetMinimumConductorCueDuration(float seconds);
        void ClearMinimumConductorCueDuration();
    }
}
