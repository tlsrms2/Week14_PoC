using UnityEngine;

namespace Week14.Save
{
    // FullScreenWindow(테두리없는 창) 모드는 창 크기가 모니터 네이티브 해상도와 다르면
    // Windows DWM이 창을 감마 보정 없이 확대(stretch)하면서 화면이 전체적으로
    // 어두워지는 문제가 있다. 네이티브가 아닌 해상도를 borderless로 요청하면
    // 대신 ExclusiveFullScreen(실제 디스플레이 출력 모드 전환)을 사용해 이를 피한다.
    public static class FullScreenModeResolver
    {
        public static FullScreenMode Resolve(int width, int height, FullScreenMode requestedMode)
        {
            if (requestedMode != FullScreenMode.FullScreenWindow)
            {
                return requestedMode;
            }

            Resolution native = Screen.currentResolution;
            bool isNativeResolution = width <= 0 || height <= 0
                || (width == native.width && height == native.height);

            return isNativeResolution ? FullScreenMode.FullScreenWindow : FullScreenMode.ExclusiveFullScreen;
        }
    }
}
