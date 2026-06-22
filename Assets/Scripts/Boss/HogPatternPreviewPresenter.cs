using System.Collections.Generic;
using UnityEngine;
using Week14.UI;

namespace Week14.Enemy
{
    internal sealed class HogPatternPreviewPresenter
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
