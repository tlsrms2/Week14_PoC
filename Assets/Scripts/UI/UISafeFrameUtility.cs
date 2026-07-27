using UnityEngine;
using UnityEngine.UI;

namespace Week14.UI
{
    public static class UISafeFrameUtility
    {
        public const float ReferenceWidth = 1920f;
        public const float ReferenceHeight = 1080f;

        private const string SafeFrameName = "__UIReferenceSafeFrame";

        public static RectTransform ClipToReferenceFrame(RectTransform target)
        {
            if (target == null || target.parent == null)
            {
                return null;
            }

            if (IsInsideSafeFrame(target))
            {
                return FindSafeFrameInParents(target);
            }

            if (target.parent is not RectTransform parent)
            {
                return null;
            }

            RectTransform safeFrame = FindOrCreateSafeFrame(parent);
            if (safeFrame == null || target.parent == safeFrame)
            {
                return safeFrame;
            }

            int originalSiblingIndex = target.GetSiblingIndex();
            target.SetParent(safeFrame, true);
            safeFrame.SetSiblingIndex(Mathf.Min(originalSiblingIndex, safeFrame.parent.childCount - 1));
            return safeFrame;
        }

        private static RectTransform FindOrCreateSafeFrame(RectTransform parent)
        {
            RectTransform existing = FindDirectSafeFrame(parent);
            if (existing != null)
            {
                ConfigureSafeFrame(existing);
                return existing;
            }

            var frameObject = new GameObject(SafeFrameName, typeof(RectTransform), typeof(RectMask2D));
            frameObject.layer = parent.gameObject.layer;

            RectTransform frame = frameObject.GetComponent<RectTransform>();
            frame.SetParent(parent, false);
            ConfigureSafeFrame(frame);
            return frame;
        }

        private static RectTransform FindDirectSafeFrame(RectTransform parent)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i) is RectTransform child && IsSafeFrame(child))
                {
                    return child;
                }
            }

            return null;
        }

        private static RectTransform FindSafeFrameInParents(RectTransform target)
        {
            Transform current = target.parent;
            while (current != null)
            {
                if (current is RectTransform rectTransform && IsSafeFrame(rectTransform))
                {
                    return rectTransform;
                }

                current = current.parent;
            }

            return null;
        }

        private static bool IsInsideSafeFrame(RectTransform target)
        {
            return FindSafeFrameInParents(target) != null;
        }

        private static bool IsSafeFrame(RectTransform target)
        {
            return target != null
                && target.name == SafeFrameName
                && target.GetComponent<RectMask2D>() != null;
        }

        private static void ConfigureSafeFrame(RectTransform frame)
        {
            if (frame == null)
            {
                return;
            }

            frame.anchorMin = new Vector2(0.5f, 0.5f);
            frame.anchorMax = new Vector2(0.5f, 0.5f);
            frame.pivot = new Vector2(0.5f, 0.5f);
            frame.anchoredPosition = Vector2.zero;
            frame.sizeDelta = new Vector2(ReferenceWidth, ReferenceHeight);
            frame.localRotation = Quaternion.identity;
            frame.localScale = Vector3.one;

            RectMask2D mask = frame.GetComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            mask.softness = Vector2Int.zero;
        }
    }
}
