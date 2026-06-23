namespace Week14.Enemy
{
    public sealed partial class HogBossAI
    {
        protected override void OnBossPhaseChanged(int phaseIndex, int phaseNumber)
        {
            graphRunner.Reset();
        }
    }
}
