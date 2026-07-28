using UnityEngine;

namespace Week14.Save
{
    public static class FullScreenModeResolver
    {
        public const int DefaultContentWidth = 1920;
        public const int DefaultContentHeight = 1080;

        public static FullScreenMode Resolve(int width, int height, FullScreenMode requestedMode)
        {
            return requestedMode;
        }

        public static (int width, int height) GetEffectiveScreenResolution(
            int contentWidth,
            int contentHeight,
            FullScreenMode requestedMode)
        {
            return (contentWidth, contentHeight);
        }
    }
}
