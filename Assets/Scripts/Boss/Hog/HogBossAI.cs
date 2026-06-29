using Week14.Audio;

namespace Week14.Enemy
{
    public sealed partial class HogBossAI : GraphBossAI
    {
        protected override bool RotatesBodyToPlayer => false;

        protected override void OnCombatStarted()
        {
            SoundManager.PlayBgm("HogBgm");
        }
    }
}
