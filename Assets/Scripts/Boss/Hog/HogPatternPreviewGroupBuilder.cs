using System.Collections.Generic;
using UnityEngine;

namespace Week14.Enemy
{
    internal static class HogPatternPreviewGroupBuilder
    {
        public static void Build(
            HogBossAI.PatternKind pattern,
            HogBossAI.Pattern1Settings pattern1,
            HogBossAI.Pattern2Settings pattern2,
            HogBossAI.Pattern4Settings pattern4,
            HogBossAI.Pattern5Settings pattern5,
            HogBossAI.Pattern4Settings pattern6,
            HogBossAI.Pattern7Settings pattern7,
            List<int> groups)
        {
            if (groups == null)
            {
                return;
            }

            groups.Clear();

            switch (pattern)
            {
                case HogBossAI.PatternKind.Pattern1:
                    AddRepeatedGroups(groups, 1, Mathf.Max(1, pattern1.RadialBulletCount));
                    break;
                case HogBossAI.PatternKind.Pattern2:
                    AddPattern2Groups(groups, pattern2);
                    break;
                case HogBossAI.PatternKind.Pattern3:
                    groups.Add(1);
                    break;
                case HogBossAI.PatternKind.Pattern4:
                    groups.Add(Mathf.Max(1, pattern4.BulletCount));
                    break;
                case HogBossAI.PatternKind.Pattern5:
                    AddRepeatedGroups(
                        groups,
                        pattern5.FireInterval <= 0f ? Mathf.Max(1, pattern5.BulletCount) : 1,
                        pattern5.FireInterval <= 0f ? 1 : Mathf.Max(1, pattern5.BulletCount));
                    break;
                case HogBossAI.PatternKind.Pattern6:
                    groups.Add(Mathf.Max(1, pattern6.BulletCount));
                    break;
                case HogBossAI.PatternKind.Pattern7:
                    AddPattern7Groups(groups, pattern7);
                    break;
            }
        }

        private static void AddPattern7Groups(List<int> groups, HogBossAI.Pattern7Settings pattern7)
        {
            int volleyCount = Mathf.Max(1, pattern7.NormalVolleyCount);
            int specialBulletCount = Mathf.Max(0, pattern7.SpecialBulletCount);
            if (pattern7.NormalVolleyInterval <= 0f)
            {
                groups.Add(volleyCount * 3 + specialBulletCount);
                return;
            }

            groups.Add(3 + specialBulletCount);
            AddRepeatedGroups(groups, 3, volleyCount - 1);
        }

        private static void AddPattern2Groups(List<int> groups, HogBossAI.Pattern2Settings pattern2)
        {
            IReadOnlyList<HogBossAI.Pattern2Settings.VolleySettings> volleys = pattern2.Volleys;
            if (volleys == null || volleys.Count == 0)
            {
                return;
            }

            for (int i = 0; i < volleys.Count; i++)
            {
                HogBossAI.Pattern2Settings.VolleySettings volley = volleys[i];
                if (volley == null)
                {
                    continue;
                }

                int bulletCount = Mathf.Max(1, volley.BulletCount);
                if (volley.FireInterval <= 0f)
                {
                    groups.Add(bulletCount);
                }
                else
                {
                    AddRepeatedGroups(groups, 1, bulletCount);
                }
            }
        }

        private static void AddRepeatedGroups(List<int> groups, int groupSize, int groupCount)
        {
            int count = Mathf.Max(0, groupCount);
            int size = Mathf.Max(1, groupSize);
            for (int i = 0; i < count; i++)
            {
                groups.Add(size);
            }
        }
    }
}
