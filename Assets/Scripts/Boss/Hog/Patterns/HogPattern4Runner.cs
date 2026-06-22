using System;
using System.Collections;
using UnityEngine;

namespace Week14.Enemy
{
    internal static class HogPattern4Runner
    {
        internal static IEnumerator Run(HogBossAI.Pattern4Settings settings, HogBossAI.PatternKind patternKind, HogPatternContext context)
        {
            context.Stop();

            int waveCount = Mathf.Max(1, settings.WaveCount);
            for (int wave = 0; wave < waveCount; wave++)
            {
                yield return context.WaitWhileExecutionPaused();
                yield return context.SlamBodyRoot(settings);

                float offset = settings.StartAngleOffset + wave * (360f / Mathf.Max(1, settings.BulletCount) * 0.5f);
                context.FirePattern4Wave(settings, offset);
                context.AdvancePreviewGroup();
                yield return context.RecoverBodyRoot(settings);

                if (wave < waveCount - 1)
                {
                    yield return context.ReloadWavePreview(patternKind, settings.WaveInterval);
                }
            }

            context.ResetBodyRoot();
        }
    }
}
