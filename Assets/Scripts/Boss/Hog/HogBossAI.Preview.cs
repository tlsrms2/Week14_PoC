using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using Week14.UI;

namespace Week14.Enemy
{
    public sealed partial class HogBossAI
    {
        private void BeginPatternBulletPreview(PatternKind nextPattern, float duration)
        {
            BuildPatternBulletPreviewGroups(nextPattern, patternBulletPreviewGroups);
            ConfigurePatternPreviewPresenter();
            patternPreviewPresenter.BeginLoading(patternBulletPreviewGroups, duration);
        }

        private void UpdatePatternBulletPreviewLoading(float duration, float remaining)
        {
            ConfigurePatternPreviewPresenter();
            patternPreviewPresenter.UpdateLoading(duration, remaining);
        }

        private void BeginPatternBulletPreviewPlayback(PatternKind pattern)
        {
            BuildPatternBulletPreviewGroups(pattern, patternBulletPreviewGroups);
            ConfigurePatternPreviewPresenter();
            patternPreviewPresenter.BeginPlayback(patternBulletPreviewGroups);
        }

        private IEnumerator ReloadPatternWavePreview(PatternKind pattern, float duration)
        {
            float reloadDuration = Mathf.Max(0f, duration);
            if (reloadDuration <= 0f)
            {
                BeginPatternBulletPreviewPlayback(pattern);
                yield break;
            }

            BeginPatternBulletPreview(pattern, reloadDuration);
            yield return patternRecovery.RunRecovery(reloadDuration, UpdatePatternBulletPreviewLoading, IsBossExecutionPaused, Stop);
            BeginPatternBulletPreviewPlayback(pattern);
        }

        private void AdvancePatternBulletPreviewGroup()
        {
            ConfigurePatternPreviewPresenter();
            patternPreviewPresenter.AdvanceGroup();
        }

        private void HidePatternBulletPreview()
        {
            patternPreviewPresenter.Hide();
        }

        private void ConfigurePatternPreviewPresenter()
        {
            patternPreviewPresenter.Configure(
                this,
                BodyRoot != null ? BodyRoot : transform,
                showPatternBulletPreview,
                patternBulletPreviewFullHoldSeconds,
                patternBulletPreviewSingleGroupFullHoldRatio);
        }

        private void BuildPatternBulletPreviewGroups(PatternKind pattern, List<int> groups)
        {
            if (groups == null)
            {
                return;
            }

            groups.Clear();
            switch (pattern)
            {
                case PatternKind.Pattern1:
                    AddRepeatedPreviewGroups(groups, 1, Mathf.Max(1, pattern1.RadialBulletCount));
                    break;
                case PatternKind.Pattern2:
                    AddPattern2PreviewGroups(groups);
                    break;
                case PatternKind.Pattern3:
                    groups.Add(1);
                    break;
                case PatternKind.Pattern4:
                    groups.Add(Mathf.Max(1, pattern4.BulletCount));
                    break;
                case PatternKind.Pattern5:
                    AddRepeatedPreviewGroups(
                        groups,
                        pattern5.FireInterval <= 0f ? Mathf.Max(1, pattern5.BulletCount) : 1,
                        pattern5.FireInterval <= 0f ? 1 : Mathf.Max(1, pattern5.BulletCount));
                    break;
                case PatternKind.Pattern6:
                    groups.Add(Mathf.Max(1, pattern6.BulletCount));
                    break;
                case PatternKind.Pattern7:
                    AddPattern7PreviewGroups(groups);
                    break;
            }
        }

        private void AddPattern2PreviewGroups(List<int> groups)
        {
            IReadOnlyList<Pattern2Settings.VolleySettings> volleys = pattern2.Volleys;
            if (volleys == null || volleys.Count == 0)
            {
                return;
            }

            for (int i = 0; i < volleys.Count; i++)
            {
                Pattern2Settings.VolleySettings volley = volleys[i];
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
                    AddRepeatedPreviewGroups(groups, 1, bulletCount);
                }
            }
        }

        private void AddPattern7PreviewGroups(List<int> groups)
        {
            int volleyCount = Mathf.Max(1, pattern7.NormalVolleyCount);
            int secondaryBulletCount = Mathf.Max(0, pattern7.SecondaryBulletCount);
            if (pattern7.NormalVolleyInterval <= 0f)
            {
                groups.Add(volleyCount * 3 + secondaryBulletCount);
                return;
            }

            groups.Add(3 + secondaryBulletCount);
            AddRepeatedPreviewGroups(groups, 3, volleyCount - 1);
        }

        private static void AddRepeatedPreviewGroups(List<int> groups, int groupSize, int groupCount)
        {
            int count = Mathf.Max(0, groupCount);
            int size = Mathf.Max(1, groupSize);
            for (int i = 0; i < count; i++)
            {
                groups.Add(size);
            }
        }

        private sealed class PatternPreviewPresenter
        {
            private readonly List<int> groups = new();

            private Component owner;
            private Transform target;
            private BossPatternBulletLineView view;
            private bool isEnabled;
            private float fullHoldSeconds;
            private float singleGroupFullHoldRatio;
            private int groupIndex;
            private float fillDuration;

            public void Configure(
                Component nextOwner,
                Transform nextTarget,
                bool nextEnabled,
                float nextFullHoldSeconds,
                float nextSingleGroupFullHoldRatio)
            {
                owner = nextOwner;
                target = nextTarget != null ? nextTarget : owner != null ? owner.transform : null;
                isEnabled = nextEnabled;
                fullHoldSeconds = Mathf.Max(0f, nextFullHoldSeconds);
                singleGroupFullHoldRatio = Mathf.Clamp01(nextSingleGroupFullHoldRatio);
            }

            public void BeginLoading(IReadOnlyList<int> nextGroups, float duration)
            {
                if (!isEnabled || duration <= 0f || !CopyGroups(nextGroups))
                {
                    Hide();
                    return;
                }

                BossPatternBulletLineView lineView = EnsureView();
                if (lineView == null)
                {
                    return;
                }

                float holdSeconds = GetFullHoldSeconds(duration);
                fillDuration = duration - holdSeconds;
                lineView.ShowLoading(groups, 0);
            }

            public void UpdateLoading(float duration, float remaining)
            {
                if (!isEnabled || view == null)
                {
                    return;
                }

                float elapsed = Mathf.Max(0f, duration - remaining);
                float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fillDuration));
                int fullGroupCount = progress >= 1f
                    ? groups.Count
                    : Mathf.Clamp(Mathf.FloorToInt(progress * groups.Count + 0.0001f), 0, groups.Count);
                view.ShowLoading(groups, fullGroupCount);
            }

            public void BeginPlayback(IReadOnlyList<int> nextGroups)
            {
                if (!isEnabled || !CopyGroups(nextGroups))
                {
                    Hide();
                    return;
                }

                BossPatternBulletLineView lineView = EnsureView();
                if (lineView == null)
                {
                    return;
                }

                groupIndex = 0;
                lineView.ShowNextGroup(groups, groupIndex);
            }

            public void AdvanceGroup()
            {
                if (!isEnabled || view == null)
                {
                    return;
                }

                groupIndex++;
                view.ShowNextGroup(groups, groupIndex);
            }

            public void Hide()
            {
                groupIndex = 0;
                fillDuration = 0f;
                view?.Hide();
            }

            private bool CopyGroups(IReadOnlyList<int> nextGroups)
            {
                groups.Clear();
                if (nextGroups == null)
                {
                    return false;
                }

                for (int i = 0; i < nextGroups.Count; i++)
                {
                    groups.Add(nextGroups[i]);
                }

                return groups.Count > 0;
            }

            private float GetFullHoldSeconds(float duration)
            {
                float maxHoldSeconds = Mathf.Max(0f, duration - 0.01f);
                float holdSeconds = Mathf.Clamp(fullHoldSeconds, 0f, maxHoldSeconds);
                if (groups.Count == 1)
                {
                    float singleGroupHoldSeconds = duration * singleGroupFullHoldRatio;
                    holdSeconds = Mathf.Max(holdSeconds, singleGroupHoldSeconds);
                }

                return Mathf.Clamp(holdSeconds, 0f, maxHoldSeconds);
            }

            private BossPatternBulletLineView EnsureView()
            {
                if (owner == null)
                {
                    return null;
                }

                Transform targetRoot = target != null ? target : owner.transform;
                if (view != null)
                {
                    view.SetTarget(targetRoot);
                    return view;
                }

                view = owner.GetComponent<BossPatternBulletLineView>();
                if (view == null)
                {
                    view = owner.GetComponentInChildren<BossPatternBulletLineView>(true);
                }

                if (view == null)
                {
                    view = owner.gameObject.AddComponent<BossPatternBulletLineView>();
                }

                view.SetTarget(targetRoot);
                return view;
            }
        }
    }
}
