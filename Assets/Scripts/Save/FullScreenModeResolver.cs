using UnityEngine;

namespace Week14.Save
{
    // FullScreenWindow(테두리없는 창) 모드는 창 크기가 모니터 네이티브 해상도와 다르면
    // Windows DWM이 창을 감마 보정 없이 확대(stretch)하면서 화면이 전체적으로
    // 어두워지는 문제가 있다. 네이티브가 아닌 해상도를 borderless로 요청하면
    // 대신 ExclusiveFullScreen(실제 디스플레이 출력 모드 전환)을 사용해 이를 피한다.
    public static class FullScreenModeResolver
    {
        // UICanvas의 m_ReferenceResolution과 동일한, 이 게임의 실제 디자인 기준 해상도(16:9).
        // 유저가 해상도를 한 번도 고른 적이 없어 저장된 콘텐츠 해상도가 없을 때 이 값을 대신 쓴다.
        public const int DefaultContentWidth = 1920;
        public const int DefaultContentHeight = 1080;

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

        private const float AspectMatchTolerance = 0.02f;

        // FullScreenWindow(테두리없는 창) 모드에서 실제 Screen.SetResolution에 넘길 값을 계산한다.
        // 종횡비가 모니터 네이티브와 같으면(예: 모니터 1920x1080에서 1280x720 선택, 둘 다 16:9) 그 해상도로
        // 실제 렌더링해도 화면 전체에 비율 그대로 균일하게 확대될 뿐 찌그러지지 않으므로, 고른 해상도를 그대로 쓴다
        // (해상도를 낮춰 성능/화질을 조절하려는 사용자 의도를 그대로 존중 — Resolve()가 이 경우 ExclusiveFullScreen으로
        // 전환해 실제 디스플레이 출력 자체를 바꾸므로 DWM 늘림도 없다).
        // 종횡비가 다르면(예: 4:3, 21:9 등) 그대로 요청 시 DWM이 몰래 늘리면서 찌그러지고, Unity의
        // Screen.width/height는 "요청한 값 그대로"만 보고해 그 사실 자체를 스크립트가 감지할 수 없다.
        // 이 경우에만 항상 모니터 네이티브 해상도로 실제 창을 맞춰 DWM이 늘릴 필요를 없애고,
        // 사용자가 고른 해상도는 카메라 rect 레터박스가 유지할 "목표 비율"로만 사용한다.
        public static (int width, int height) GetEffectiveScreenResolution(
            int contentWidth,
            int contentHeight,
            FullScreenMode requestedMode)
        {
            if (requestedMode == FullScreenMode.Windowed || contentWidth <= 0 || contentHeight <= 0)
            {
                return (contentWidth, contentHeight);
            }

            Resolution native = Screen.currentResolution;
            if (native.width <= 0 || native.height <= 0)
            {
                return (contentWidth, contentHeight);
            }

            float contentAspect = contentWidth / (float)contentHeight;
            float nativeAspect = native.width / (float)native.height;
            if (Mathf.Abs(contentAspect - nativeAspect) <= AspectMatchTolerance)
            {
                return (contentWidth, contentHeight);
            }

            return (native.width, native.height);
        }
    }
}
