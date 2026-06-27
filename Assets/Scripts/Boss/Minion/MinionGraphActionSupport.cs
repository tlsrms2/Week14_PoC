using System.Collections;

namespace Week14.Enemy
{
    internal static class MinionGraphActionHost
    {
        public static bool TryGet(BossActionContext context, out IMinionPatternHost host)
        {
            host = context?.Boss as IMinionPatternHost;
            if (host == null || !host.MinionPatternEnabled)
            {
                host = null;
            }

            return host != null;
        }

        public static bool TryResolveProjectile(
            BossActionContext context,
            string projectileName,
            out IMinionPatternHost host,
            out BossProjectileSettings projectile)
        {
            projectile = null;
            if (!TryGet(context, out host))
            {
                return false;
            }

            return TryResolveProjectile(host, projectileName, out projectile);
        }

        public static bool TryResolveProjectile(
            IMinionPatternHost host,
            string projectileName,
            out BossProjectileSettings projectile)
        {
            projectile = host?.ResolveMinionProjectileSettings(projectileName);
            return projectile != null;
        }
    }

    internal static class MinionGraphCommandRunner
    {
        public static IEnumerator WaitWindupIfNeeded(BossActionContext context, float windupSeconds)
        {
            if (context != null && windupSeconds > 0f)
            {
                yield return context.WaitSeconds(windupSeconds);
            }
        }

        public static IEnumerator WaitForDurationIfNeeded(
            BossActionContext context,
            float duration,
            bool waitForDuration)
        {
            if (waitForDuration && context != null && duration > 0f)
            {
                yield return context.WaitSeconds(duration);
            }
        }
    }
}
