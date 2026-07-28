namespace Week14.Combat
{
    public partial class EnemyProjectile
    {
        private bool ignoresExecutionPause;

        public void ConfigureExecutionPauseIgnored(bool ignored)
        {
            ignoresExecutionPause = ignored;
            if (ignored)
            {
                ResumeFromExecutionPause();
            }
        }
    }
}
